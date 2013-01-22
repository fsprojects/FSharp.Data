// --------------------------------------------------------------------------------------
// Helpers for writing type providers
// ----------------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.IO
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes

module Seq = 
  /// Merge two sequences by pairing elements for which
  /// the specified predicate returns the same key
  ///
  /// (If the inputs contain the same keys, then the order
  /// of the elements is preserved.)
  let pairBy f first second = 
    let vals1 = [ for o in first -> f o, o ]
    let vals2 = [ for o in second -> f o, o ]
    let d1, d2 = dict vals1, dict vals2
    let k1, k2 = set d1.Keys, set d2.Keys
    let keys = List.map fst vals1 @ (List.ofSeq (k2 - k1))
    let asOption = function true, v -> Some v | _ -> None
    [ for k in keys -> 
        k, asOption (d1.TryGetValue(k)), asOption (d2.TryGetValue(k)) ]

  /// Take at most the specified number of arguments from the sequence
  let takeMax count input =
    input 
    |> Seq.mapi (fun i v -> i, v)
    |> Seq.takeWhile (fun (i, v) -> i < count)
    |> Seq.map snd

// ----------------------------------------------------------------------------------------------

[<AutoOpen>]
module ActivePatterns =

  /// Helper active pattern that can be used when constructing InvokeCode
  /// (to avoid writing pattern matching or incomplete matches):
  ///
  ///    p.InvokeCode <- fun (Singleton self) -> <@ 1 + 2 @>
  ///
  let (|Singleton|) = function [l] -> l | _ -> failwith "Parameter mismatch"

  /// Takes dictionary or a map and succeeds if it contains exactly one value
  let (|SingletonMap|_|) map = 
    if Seq.length map <> 1 then None else
      let (KeyValue(k, v)) = Seq.head map 
      Some(k, v)

// ----------------------------------------------------------------------------------------------
// Dynamic operator (?) that can be used for constructing quoted F# code without 
// quotations (to simplify constructing F# quotations in portable libraries - where
// we need to pass the System.Type of various types as arguments)
// ----------------------------------------------------------------------------------------------

module QuotationBuilder = 

  open System.Reflection
  open Microsoft.FSharp.Quotations
  open Microsoft.FSharp.Quotations.Patterns
  open Microsoft.FSharp.Reflection

  let (?) (typ:System.Type) (operation:string) (args1:'T) : 'R = 
    // Arguments are either Expr or other type - in the second case,
    // we treat them as Expr.Value (which will only work for primitives)
    let convertValue (arg:obj) = 
      match arg with
      | :? Expr as e -> e
      | value -> Expr.Value(value, value.GetType())

    let invokeOperation (tyargs:obj, tyargsT) (args:obj, argsT) =
      // To support (e1, e2, ..) syntax, we use tuples - extract tuple arguments
      // First, extract type arguments - a list of System.Type values
      let tyargs = 
        if tyargsT = typeof<unit> then []
        elif FSharpType.IsTuple(tyargsT) then
          [ for f in FSharpValue.GetTupleFields(tyargs) -> f :?> System.Type ]
        else [ tyargs :?> System.Type ]
      // Second, extract arguments (which are either Expr values or primitive constants)
      let args = 
        if argsT = typeof<unit> then []
        elif FSharpType.IsTuple(argsT) then
          [ for f in FSharpValue.GetTupleFields(args) -> convertValue f ]
        else [ convertValue args ]

      // Find a method that we want to call
      let flags = BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.Instance
      match typ.GetMember(operation, MemberTypes.All, flags) with 
      | [| :? MethodInfo as mi |] -> 
          let mi = 
            if tyargs = [] then mi
            else mi.MakeGenericMethod(tyargs |> Array.ofList)
          if mi.IsStatic then Expr.Call(mi, args)
          else Expr.Call(List.head args, mi, List.tail args)
      | [| :? ConstructorInfo as ci |] ->
          if tyargs <> [] then failwith "Constructor cannot be generic!"
          Expr.NewObject(ci, args)
      | options -> failwithf "Constructing call of the '%s' operation failed. Got %A" operation options

    // If the result is a function, we are called with two tuples as arguments
    // and the first tuple represents type arguments for a generic function...
    if FSharpType.IsFunction(typeof<'R>) then
      let domTyp, res = FSharpType.GetFunctionElements(typeof<'R>)
      if res <> typeof<Expr> then failwith "QuotationBuilder: The resulting type must be Expr!"
      FSharpValue.MakeFunction(typeof<'R>, fun args2 ->
        invokeOperation (args1, typeof<'T>) (args2, domTyp) |> box) |> unbox<'R>
    else invokeOperation ((), typeof<unit>) (args1, typeof<'T>) |> unbox<'R>

// ----------------------------------------------------------------------------------------------

module internal ReflectionHelpers = 

  open Microsoft.FSharp.Quotations

  let makeFunc (exprfunc:Expr -> Expr) argType = 
    let var = Var.Global("t", argType)
    let convBody = exprfunc (Expr.Var var)
    convBody.Type, Expr.Lambda(var, convBody)
        
  let makeMethodCall (typ:Type) name tyargs args =
    let convMeth = typ.GetMethod(name)
    let convMeth = 
      if tyargs = [] then convMeth else
      convMeth.MakeGenericMethod (Array.ofSeq tyargs)
    Expr.Call(convMeth, args)

// ----------------------------------------------------------------------------------------------

module ProviderHelpers =

  open System.IO
  open FSharp.Data.RuntimeImplementation.DataLoading

  /// Resolve a location of a file (or a web location) and open it for shared
  /// read, and trigger the specified function whenever the file changes
  let readTextAtDesignTime (cfg:TypeProviderConfig) invalidate resolutionFolder fileName = 
    let stream = 
      asyncOpenStreamInProvider true (false, cfg.ResolutionFolder) (Some invalidate) resolutionFolder fileName 
      |> Async.RunSynchronously
    new StreamReader(stream)

// ----------------------------------------------------------------------------------------------

open Microsoft.FSharp.Quotations

type AssemblyReplacer =
    abstract member ToRuntime : Type -> Type
    abstract member ToRuntime : Expr -> Expr
    abstract member ToDesignTime: Expr -> Expr

// ----------------------------------------------------------------------------------------------
// Conversions from string to various primitive types
// ----------------------------------------------------------------------------------------------

module Conversions = 

  open Microsoft.FSharp.Quotations
  open FSharp.Data.RuntimeImplementation
  open QuotationBuilder

  /// Creates a function that takes Expr<string option> and converts it to 
  /// an expression of other type - the type is specified by `typ` and 
  let convertValue (culture:string) (fieldName:string) optional typ (replacer:AssemblyReplacer) = 
    let returnTyp = if optional then typedefof<option<_>>.MakeGenericType [| typ |] else typ
    returnTyp, fun e ->
      let converted = 
        let culture = <@@ Operations.GetCulture(culture) @@>
        if typ = typeof<int> then <@@ Operations.ConvertInteger(%%culture, %%e) @@>
        elif typ = typeof<int64> then <@@ Operations.ConvertInteger64(%%culture, %%e) @@>
        elif typ = typeof<decimal> then <@@ Operations.ConvertDecimal(%%culture, %%e) @@>
        elif typ = typeof<float> then <@@ Operations.ConvertFloat(%%culture, %%e) @@>
        elif typ = typeof<string> then <@@ Operations.ConvertString(%%e) @@>
        elif typ = typeof<bool> then <@@ Operations.ConvertBoolean(%%e) @@>
        elif typ = typeof<DateTime> then <@@ Operations.ConvertDateTime(%%culture, %%e) @@>
        else failwith "convertValue: Unsupported primitive type"
        |> replacer.ToRuntime
      if not optional then 
        let operationsTyp = replacer.ToRuntime typeof<Operations>
        operationsTyp?GetNonOptionalValue (typ) (fieldName, converted)
      else converted

// ----------------------------------------------------------------------------------------------
        
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private AssemblyReplacer =

    open System.Collections.Generic
    open System.Reflection
    open Microsoft.FSharp.Quotations.ExprShape
    open Microsoft.FSharp.Quotations.Patterns
    open Microsoft.FSharp.Reflection

    let private replace (asmMappings : (Assembly * Assembly) list) (original, originalAsms : Assembly list) f =
        let toAsm = 
            asmMappings
            |> Seq.tryPick (fun (fromAsm, toAsm) -> 
                if originalAsms |> List.exists (fun originalAsm -> originalAsm = fromAsm) then                
                    if fromAsm = originalAsms.Head then 
                        Some toAsm 
                    else 
                        Some originalAsms.Head
                else 
                    None)
        match toAsm with
        | Some toAsm -> f toAsm
        | None -> original

    let private replaceLazy (asmMappings : (Assembly * Assembly) list) (lazyOriginal : 'a Lazy, originalAsm) f =
        let toAsm = 
            asmMappings
            |> Seq.tryPick (fun (fromAsm, toAsm) -> if originalAsm = fromAsm then Some toAsm else None)
        match toAsm with
        | Some toAsm -> f toAsm
        | None -> lazyOriginal.Value

    let private getType (asm:Assembly) (t:Type) rt =
        let getFullName (t:Type) =
            let fullName = t.FullName        
            if fullName.StartsWith("FSI_")
            then fullName.Substring(fullName.IndexOf('.') + 1)
            else fullName
        let newT =
            if t.IsGenericType && not t.IsGenericTypeDefinition then 
                let genericType = t.GetGenericTypeDefinition()
                let newT = asm.GetType (getFullName genericType)
                if newT = null then 
                    null
                else
                    let typeArguments = 
                        t.GetGenericArguments()
                        |> Seq.map rt
                        |> Seq.toArray
                    newT.MakeGenericType(typeArguments)
            else 
                asm.GetType (getFullName t)
        if newT = null then
            failwithf "Type '%O' not found in '%s'" t asm.Location
        newT

    let rec getAssemblies (t:Type) = [
        yield t.Assembly
        if t.IsGenericType && not t.IsGenericTypeDefinition then
            for t in t.GetGenericArguments() do
                yield! getAssemblies t
    ]

    let rec private replaceType asmMappings (t : Type) =        
        if t.GetType().Name = "ProvidedSymbolType" then t
        elif t.GetType() = typeof<ProvidedTypeDefinition> then t
        else replace asmMappings (t, getAssemblies t) (fun toAsm -> getType toAsm t (replaceType asmMappings))

    let private replaceProperty asmMappings (p : PropertyInfo) =
        if p.GetType() = typeof<ProvidedProperty> then p
        else replace asmMappings (p, getAssemblies p.DeclaringType) (fun toAsm ->
            let t = getType toAsm p.DeclaringType (replaceType asmMappings)
            let isStatic = 
                p.CanRead && p.GetGetMethod().IsStatic || 
                p.CanWrite && p.GetSetMethod().IsStatic
            let bindingFlags = BindingFlags.Public ||| BindingFlags.NonPublic ||| 
                               (if isStatic then BindingFlags.Static else BindingFlags.Instance)
            let newP = t.GetProperty(p.Name, bindingFlags)
            if newP = null then
                failwithf "Property '%O' of type '%O' not found in '%s'" p t toAsm.Location
            newP)
    
    let private replaceMethod asmMappings (m : MethodInfo) =
        if m.GetType() = typeof<ProvidedMethod> then m
        elif m.DeclaringType.FullName = "Microsoft.FSharp.Core.LanguagePrimitives+IntrinsicFunctions" then m // these methods don't really exist, so there's no need to replace them
        else replace asmMappings (m, getAssemblies m.DeclaringType) (fun toAsm ->
                let t = getType toAsm m.DeclaringType (replaceType asmMappings)
                let parameterTypes = 
                    m.GetParameters() 
                    |> Seq.map (fun p -> replaceType asmMappings p.ParameterType) 
                    |> Seq.toArray
                let newM =
                    if m.IsGenericMethod then 
                        let genericMethod = t.GetMethod(m.Name)
                        if genericMethod = null then 
                            null
                        else
                            let typeArguments = 
                                m.GetGenericArguments()
                                |> Seq.map (fun t -> replaceType asmMappings t) 
                                |> Seq.toArray                        
                            genericMethod.MakeGenericMethod(typeArguments)
                    else 
                        t.GetMethod(m.Name, parameterTypes)
                if newM = null then
                    failwithf "Method '%O' of type '%O' not found in '%s'" m t toAsm.Location
                else
                    newM)

    let private replaceConstructor asmMappings (c : ConstructorInfo) =
        if c.GetType() = typeof<ProvidedConstructor> then c
        else replace asmMappings (c, getAssemblies c.DeclaringType) (fun toAsm ->
            let t = getType toAsm c.DeclaringType (replaceType asmMappings)
            let parameterTypes = 
                c.GetParameters() 
                |> Seq.map (fun p -> replaceType asmMappings p.ParameterType) 
                |> Seq.toArray
            let newC = t.GetConstructor(parameterTypes)
            if newC = null then
                failwithf "Constructor '%O' of type '%O' not found in '%s'" c t toAsm.Location
            else
                newC)

    let private replaceUnionCase asmMappings (uci : UnionCaseInfo) exprs =
        replaceLazy asmMappings (lazy (Expr.NewUnionCase (uci, exprs)), uci.DeclaringType.Assembly) (fun toAsm ->
            let t = getType toAsm uci.DeclaringType (replaceType asmMappings)
            let constructorMethod = t.GetMethod(uci.Name)
            if constructorMethod = null then
                failwithf "Method '%s' of type '%O' not found in '%s'" uci.Name t toAsm.Location
            Expr.Call (constructorMethod, exprs))

    let private replaceVar asmMappings (varTable: IDictionary<_,_>) reversePass (v: Var) =
        if v.Type.GetType() = typeof<ProvidedTypeDefinition> then v
        else replace asmMappings (v, getAssemblies v.Type) (fun toAsm ->
            if reversePass then
                let newVar = Var (v.Name, getType toAsm v.Type (replaceType asmMappings), v.IsMutable)
                // store the asmMappings as we'll have to revert them later
                varTable.Add(newVar, v)
                newVar
            else
                varTable.[v])
    
    let rec private replaceExpr asmMappings varTable reversePass quotation =
        let rt = replaceType asmMappings
        let rp = replaceProperty asmMappings
        let rm = replaceMethod asmMappings
        let rc = replaceConstructor asmMappings
        let ru = replaceUnionCase asmMappings
        let rv = replaceVar asmMappings varTable reversePass
        let re = replaceExpr asmMappings varTable reversePass
        
        match quotation with
        | Call (expr, m, exprs) -> 
            match expr with
            | Some expr -> Expr.Call (re expr, rm m, List.map re exprs)
            | None -> Expr.Call (rm m, List.map re exprs)
        | PropertyGet (expr, p, exprs) -> 
            match expr with
            | Some expr -> Expr.PropertyGet (re expr, rp p, List.map re exprs)
            | None -> Expr.PropertyGet (rp p, List.map re exprs)
        | NewObject (c, exprs) ->
            Expr.NewObject (rc c, (List.map re exprs))
        | NewUnionCase (uci, exprs) ->
            ru uci (List.map re exprs)
        | ShapeVar v -> 
            Expr.Var (rv v)
        | ShapeLambda (v, expr) -> 
            Expr.Lambda (rv v, re expr)
        | ShapeCombination (o, exprs) -> 
            RebuildShapeCombination (o, List.map re exprs)

    let create asmMappings =

        let asmMappingsReversed = asmMappings |> List.map (fun (a, b) -> b, a)
        let variablesTable = new Dictionary<_, _>()
            
        { new AssemblyReplacer with
            member __.ToRuntime (t:Type) = t |> replaceType asmMappings
            member __.ToRuntime (e:Expr) = e |> replaceExpr asmMappings variablesTable false
            member __.ToDesignTime (e:Expr) = e |> replaceExpr asmMappingsReversed variablesTable true }

// ----------------------------------------------------------------------------------------------

module AssemblyResolver =

    open System.Reflection
    open System.Runtime.Versioning

    let private (++) a b = Path.Combine(a,b)
    let private portableFSharpCorePath = 
        Environment.GetFolderPath Environment.SpecialFolder.ProgramFilesX86 
        ++ "Reference Assemblies" 
        ++ "Microsoft" 
        ++ "FSharp" 
        ++ "3.0" 
        ++ "Runtime" 
        ++ ".NETPortable" 
        ++ "FSharp.Core.dll"

    let private assemblyResolveHandler = ResolveEventHandler(fun _ args ->
        try 
            let assemName = AssemblyName(args.Name)
            if assemName.Name = "FSharp.Core" && assemName.Version.ToString() = "2.3.5.0" then 
                Assembly.LoadFrom portableFSharpCorePath
            else 
                null
        with e ->  
            null)

    let mutable initialized = false    

    let init (cfg : TypeProviderConfig) = 

        if not initialized then
            initialized <- true
            AppDomain.CurrentDomain.add_AssemblyResolve(assemblyResolveHandler)

        let runtimeAssembly = Assembly.LoadFrom cfg.RuntimeAssembly
        
        let targetFrameworkAttr = runtimeAssembly.GetCustomAttribute<TargetFrameworkAttribute>()
        let isPortable = targetFrameworkAttr.FrameworkName = ".NETPortable,Version=v4.0,Profile=Profile47"

        let asmMappings = [Assembly.GetExecutingAssembly(), runtimeAssembly]
        let asmMappings = 
            if isPortable then
                let fullFSharpCore = typedefof<int list>.Assembly
                let portableFSharpCore = Assembly.LoadFrom portableFSharpCorePath
                (fullFSharpCore, portableFSharpCore)::asmMappings
            else
                asmMappings

        runtimeAssembly, isPortable, AssemblyReplacer.create asmMappings
