namespace ProviderImplementation

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.ExprShape
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Reflection
open ProviderImplementation.ProvidedTypes

type AssemblyReplacer =
    abstract member ToRuntime : Type -> Type
    abstract member ToRuntime : Expr -> Expr
    abstract member ToDesignTime: Expr -> Expr

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private AssemblyReplacer =

    let private replace asmMappings (original, originalAsms) f =
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

    let private replaceLazy asmMappings (lazyOriginal : 'a Lazy, originalAsms) f =
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
        replaceLazy asmMappings (lazy (Expr.NewUnionCase (uci, exprs)), getAssemblies uci.DeclaringType) (fun toAsm ->
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
        
        // this is not exhaustive, it's missing fields, setters, etc...
        // add more patterns as needed
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
            Expr.NewObject (rc c, List.map re exprs)
        | Coerce (expr, t) ->
            Expr.Coerce (re expr, rt t)
        | NewUnionCase (uci, exprs) ->
            ru uci (List.map re exprs)
        | NewDelegate (t, vars, expr) ->
            Expr.NewDelegate (rt t, List.map rv vars, re expr)
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
