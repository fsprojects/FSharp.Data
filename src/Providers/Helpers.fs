// --------------------------------------------------------------------------------------
// Helpers for writing type providers
// ----------------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.IO
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes

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
  open FSharp.Data.Importing

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
  open FSharp.Data.Conversions
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

    let private replace (asmMappings : (Assembly * Assembly) list) (original, originalAsm) f =
        let toAsm = 
            asmMappings
            |> Seq.tryPick (fun (fromAsm, toAsm) -> if originalAsm = fromAsm then Some toAsm else None)
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

    let rec private replaceType asmMappings (t : Type) =        
        if t.GetType().Name = "ProvidedSymbolType" then t
        elif t.GetType() = typeof<ProvidedTypeDefinition> then t
        else replace asmMappings (t, t.Assembly) (fun toAsm -> getType toAsm t (replaceType asmMappings))

    let private replaceProperty asmMappings (p : PropertyInfo) =
        if p.GetType() = typeof<ProvidedProperty> then p
        else replace asmMappings (p, p.DeclaringType.Assembly) (fun toAsm ->
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
        else replace asmMappings (m, m.DeclaringType.Assembly) (fun toAsm ->
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
        else replace asmMappings (c, c.DeclaringType.Assembly) (fun toAsm ->
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
        else replace asmMappings (v, v.Type.Assembly) (fun toAsm ->
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

// ----------------------------------------------------------------------------------------------

module Debug = 

    open System.Collections.Generic
    open System.Reflection
    open System.Text
    open Microsoft.FSharp.Core.CompilerServices
    open Microsoft.FSharp.Reflection

    /// Converts a sequence of strings to a single string separated with the delimiters
    let inline private separatedBy delimiter (items: string seq) = String.Join(delimiter, Array.ofSeq items)

    let generate (resolutionFolder: string) (runtimeAssembly: string) typeProviderConstructor args =
        let cfg = new TypeProviderConfig(fun _ -> false)
        cfg.GetType().GetProperty("ResolutionFolder").GetSetMethod(nonPublic = true).Invoke(cfg, [| box resolutionFolder |]) |> ignore
        cfg.GetType().GetProperty("RuntimeAssembly").GetSetMethod(nonPublic = true).Invoke(cfg, [| box runtimeAssembly |]) |> ignore
        cfg.GetType().GetProperty("ReferencedAssemblies").GetSetMethod(nonPublic = true).Invoke(cfg, [| box ([||]: string[]) |]) |> ignore        

        let typeProviderForNamespaces = typeProviderConstructor cfg :> TypeProviderForNamespaces

        let providedTypeDefinition = typeProviderForNamespaces.Namespaces |> Seq.last |> snd |> Seq.last
            
        match args with
        | [||] -> providedTypeDefinition
        | args ->
            let typeName = providedTypeDefinition.Name + (args |> Seq.map (fun s -> ",\"" + (if s = null then "" else s.ToString()) + "\"") |> Seq.reduce (+))
            providedTypeDefinition.MakeParametricType(typeName, args)

    let private innerPrettyPrint signatureOnly (maxDepth: int option) exclude (t: ProvidedTypeDefinition) =        

        let ns = 
            [ t.Namespace
              "Microsoft.FSharp.Core"
              "Microsoft.FSharp.Core.Operators"
              "Microsoft.FSharp.Collections"
              "Microsoft.FSharp.Control"
              "Microsoft.FSharp.Text" ]
            |> Set.ofSeq

        let pending = new Queue<_>()
        let visited = new HashSet<_>()

        let add t =
            if not (exclude t) && visited.Add t then
                pending.Enqueue t

        let fullName (t: Type) =
            let fullName = t.FullName
            if fullName.StartsWith "FSI_" then
                fullName.Substring(fullName.IndexOf('.') + 1)
            else
                fullName

        let rec toString (t: Type) =

            if t = null then
                "<NULL>" // happens in the CSV and Freebase providers
            else

                let hasUnitOfMeasure = t.Name.Contains("[")

                let innerToString (t: Type) =
                    match t with
                    | t when t = typeof<bool> -> "bool"
                    | t when t = typeof<obj> -> "obj"
                    | t when t = typeof<int> -> "int"
                    | t when t = typeof<int64> -> "int64"
                    | t when t = typeof<float> -> "float"
                    | t when t = typeof<float32> -> "float32"
                    | t when t = typeof<decimal> -> "decimal"
                    | t when t = typeof<string> -> "string"
                    | t when t = typeof<Void> -> "()"
                    | t when t = typeof<unit> -> "()"
                    | t when t.IsArray -> (t.GetElementType() |> toString) + "[]"
                    | :? ProvidedTypeDefinition as t ->
                        add t
                        t.Name.Split([| ',' |]).[0]
                    | t when t.IsGenericType ->            
                        let args =                 
                            t.GetGenericArguments() 
                            |> Seq.map (if hasUnitOfMeasure then (fun t -> t.Name) else toString)
                            |> separatedBy ", "
                        let name, reverse = 
                            match t with
                            | t when hasUnitOfMeasure -> toString t.UnderlyingSystemType, false
                            | t when t.GetGenericTypeDefinition() = typeof<int seq>.GetGenericTypeDefinition() -> "seq", true
                            | t when t.GetGenericTypeDefinition() = typeof<int list>.GetGenericTypeDefinition() -> "list", true
                            | t when t.GetGenericTypeDefinition() = typeof<int option>.GetGenericTypeDefinition() -> "option", true
                            | t when t.GetGenericTypeDefinition() = typeof<int ref>.GetGenericTypeDefinition() -> "ref", true
                            | t when ns.Contains t.Namespace -> t.Name, false
                            | t -> fullName t, false
                        let name = name.Split([| '`' |]).[0]
                        if reverse then
                            args + " " + name 
                        else
                            name + "<" + args + ">"
                    | t when ns.Contains t.Namespace -> t.Name
                    | t when t.IsGenericParameter -> t.Name
                    | t -> fullName t

                let rec warnIfWrongAssembly (t:Type) =
                    match t with
                    | :? ProvidedTypeDefinition as t -> ""
                    | t when t.IsGenericType -> defaultArg (t.GetGenericArguments() |> Seq.map warnIfWrongAssembly |> Seq.tryFind (fun s -> s <> "")) ""
                    | t when t.IsArray -> warnIfWrongAssembly <| t.GetElementType()
                    | t -> if not t.IsGenericParameter && t.Assembly = Assembly.GetExecutingAssembly() then " [DESIGNTIME]" else ""

                if hasUnitOfMeasure || t.IsGenericParameter || t.DeclaringType = null then
                    innerToString t + (warnIfWrongAssembly t)
                else
                    (toString t.DeclaringType) + "+" + (innerToString t) + (warnIfWrongAssembly t)

        let toSignature (parameters: ParameterInfo[]) =
            if parameters.Length = 0 then
                "()"
            else
                parameters 
                |> Seq.map (fun p -> p.Name + ":" + (toString p.ParameterType))
                |> separatedBy " -> "

        let sb = StringBuilder ()
        let print (str: string) =
            sb.Append(str) |> ignore
        let println() =
            sb.AppendLine() |> ignore
                
        let printMember (memberInfo: MemberInfo) =        

            let print str =
                print "    "                
                print str
                println()

            let getMethodBody (m: ProvidedMethod) = 
                seq { if not m.IsStatic then yield m.DeclaringType.BaseType
                      for param in m.GetParameters() do yield param.ParameterType }
                |> Seq.map (fun typ -> Expr.Value(null, typ))
                |> Array.ofSeq
                |> m.GetInvokeCodeInternal false

            let getConstructorBody (c: ProvidedConstructor) = 
                seq { if not c.IsStatic then yield c.DeclaringType.BaseType
                      for param in c.GetParameters() do yield param.ParameterType }
                |> Seq.map (fun typ -> Expr.Value(null, typ))
                |> Array.ofSeq
                |> c.GetInvokeCodeInternal false

            match memberInfo with

            | :? ProvidedConstructor as cons -> 
                let body = 
                    if signatureOnly then ""
                    else cons |> getConstructorBody |> sprintf "\n%A\n"
                print <| "new : " + 
                         (toSignature <| cons.GetParameters()) + " -> " + 
                         (toString memberInfo.DeclaringType) + body

            | :? ProvidedLiteralField as field -> 
                let value = 
                    if signatureOnly then ""
                    else field.GetRawConstantValue() |> sprintf "\n%O\n" 
                print <| "val " + field.Name + ": " + 
                         (toString field.FieldType) + 
                         value
                         
            | :? ProvidedProperty as prop -> 
                let body = 
                    if signatureOnly then ""
                    else
                        let getter = 
                            if not prop.CanRead then ""
                            else getMethodBody (prop.GetMethod :?> ProvidedMethod) |> sprintf "\n%A\n"
                        let setter = 
                            if not prop.CanWrite then ""
                            else getMethodBody (prop.SetMethod :?> ProvidedMethod) |> sprintf "\n%A\n"
                        getter + setter
                print <| (if prop.IsStatic then "static " else "") + "member " + 
                         prop.Name + ": " + (toString prop.PropertyType) + 
                         " with " + (if prop.CanRead && prop.CanWrite then "get, set" else if prop.CanRead then "get" else "set")            

            | :? ProvidedMethod as m ->
                let body = 
                    if signatureOnly then ""
                    else m |> getMethodBody |> sprintf "\n%A\n"
                if m.Attributes &&& MethodAttributes.SpecialName <> MethodAttributes.SpecialName then
                    print <| (if m.IsStatic then "static " else "") + "member " + 
                    m.Name + ": " + (toSignature <| m.GetParameters()) + 
                    " -> " + (toString m.ReturnType) + body

            | :? ProvidedTypeDefinition as t -> add t

            | _ -> ()

        add t

        let currentDepth = ref 0

        let stop() =
            match maxDepth with
            | Some maxDepth -> !currentDepth > maxDepth
            | None -> false

        while pending.Count <> 0 && not (stop()) do
            let pendingForThisDepth = new Queue<_>(pending)
            pending.Clear()
            while pendingForThisDepth.Count <> 0 do
                let t = pendingForThisDepth.Dequeue()
                match t with
                | t when FSharpType.IsRecord t-> "record "
                | t when FSharpType.IsModule t -> "module "
                | t when t.IsValueType -> "struct "
                | t when t.IsClass && t.IsSealed && t.IsAbstract -> "static class "
                | t when t.IsClass && t.IsAbstract -> "abstract class "
                | t when t.IsClass -> "class "
                | t -> ""
                |> print
                print (toString t)
                if t.BaseType <> typeof<obj> then
                    print " : "
                    print (toString t.BaseType)
                println()
                t.GetMembers() |> Seq.iter printMember
                println()
            currentDepth := !currentDepth + 1
    
        sb.ToString()

    let prettyPrint signatureOnly t = innerPrettyPrint signatureOnly None (fun _ -> false) t
    let prettyPrintWithMaxDepth signatureOnly maxDepth t = innerPrettyPrint signatureOnly (Some maxDepth) (fun _ -> false) t
    let prettyPrintWithMaxDepthAndExclusions signatureOnly maxDepth exclusions t = 
        let exclusions = Set.ofSeq exclusions
        innerPrettyPrint signatureOnly (Some maxDepth) (fun t -> exclusions.Contains t.Name) t
