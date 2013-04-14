// --------------------------------------------------------------------------------------
// Helpers for writing type providers
// ----------------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.Collections.Generic
open System.Reflection
open System.Text
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection
open ProviderImplementation.ProvidedTypes

module Debug = 

    /// Converts a sequence of strings to a single string separated with the delimiters
    let inline private separatedBy delimiter (items: string seq) = String.Join(delimiter, Array.ofSeq items)

    /// Simulates a real instance of TypeProviderConfig and then creates an instance of the last
    /// type provider added to a namespace by the type provider constructor
    let generate (resolutionFolder: string) (runtimeAssembly: string) typeProviderForNamespacesConstructor args =
        let cfg = new TypeProviderConfig(fun _ -> false)
        cfg.GetType().GetProperty("ResolutionFolder").GetSetMethod(nonPublic = true).Invoke(cfg, [| box resolutionFolder |]) |> ignore
        cfg.GetType().GetProperty("RuntimeAssembly").GetSetMethod(nonPublic = true).Invoke(cfg, [| box runtimeAssembly |]) |> ignore
        cfg.GetType().GetProperty("ReferencedAssemblies").GetSetMethod(nonPublic = true).Invoke(cfg, [| box ([||]: string[]) |]) |> ignore        

        let typeProviderForNamespaces = typeProviderForNamespacesConstructor cfg :> TypeProviderForNamespaces

        let providedTypeDefinition = typeProviderForNamespaces.Namespaces |> Seq.last |> snd |> Seq.last
            
        match args with
        | [||] -> providedTypeDefinition
        | args ->
            let typeName = providedTypeDefinition.Name + (args |> Seq.map (fun s -> ",\"" + (if s = null then "" else s.ToString()) + "\"") |> Seq.reduce (+))
            providedTypeDefinition.MakeParametricType(typeName, args)

    // if ignoreOutput is true, this will still visit the full graph, but it will output an empty string to be faster
    let private innerPrettyPrint signatureOnly ignoreOutput (maxDepth: int option) exclude (t: ProvidedTypeDefinition) =        

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
            let fullName = t.Namespace + "." + t.Name
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
                        t.Name.Split(',').[0]
                    | t when t.IsGenericType ->            
                        let args =                 
                            t.GetGenericArguments() 
                            |> Seq.map (if hasUnitOfMeasure then (fun t -> t.Name) else toString)
                        if FSharpType.IsTuple t then
                            separatedBy " * " args
                        elif t.Name.StartsWith "FSharpFunc`" then
                            "(" + separatedBy " -> " args + ")"
                        else 
                          let args = separatedBy ", " args
                          let name, reverse = 
                              match t with
                              | t when hasUnitOfMeasure -> toString t.UnderlyingSystemType, false
                              | t when t.GetGenericTypeDefinition() = typeof<int seq>.GetGenericTypeDefinition() -> "seq", true
                              | t when t.GetGenericTypeDefinition() = typeof<int list>.GetGenericTypeDefinition() -> "list", true
                              | t when t.GetGenericTypeDefinition() = typeof<int option>.GetGenericTypeDefinition() -> "option", true
                              | t when t.GetGenericTypeDefinition() = typeof<int ref>.GetGenericTypeDefinition() -> "ref", true
                              | t when t.Name = "FSharpAsync`1" -> "async", true
                              | t when ns.Contains t.Namespace -> t.Name, false
                              | t -> fullName t, false
                          let name = name.Split('`').[0]
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

                if ignoreOutput then
                    ""
                elif hasUnitOfMeasure || t.IsGenericParameter || t.DeclaringType = null then
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
            if not ignoreOutput then
                sb.Append(str) |> ignore
        
        let println() =
            if not ignoreOutput then
                sb.AppendLine() |> ignore
              
        let printMember (memberInfo: MemberInfo) =        

            let print str =
                print "    "                
                print str
                println()

            let rec getTypeErasedTo (t:Type) =
                if t :? ProvidedTypeDefinition then
                    t.BaseType
                elif t.GetGenericArguments() |> Seq.exists (fun t -> t :? ProvidedTypeDefinition) then
                     let genericTypeDefinition = t.GetGenericTypeDefinition()
                     let genericArguments = 
                        t.GetGenericArguments()
                        |> Seq.map getTypeErasedTo
                        |> Seq.toArray
                     genericTypeDefinition.MakeGenericType(genericArguments)
                else
                    t

            let getMethodBody (m: ProvidedMethod) = 
                seq { if not m.IsStatic then yield (getTypeErasedTo m.DeclaringType.BaseType)
                      for param in m.GetParameters() do yield (getTypeErasedTo param.ParameterType) }
                |> Seq.map (fun typ -> Expr.Value(null, typ))
                |> Array.ofSeq
                |> m.GetInvokeCodeInternal false

            let getConstructorBody (c: ProvidedConstructor) = 
                seq { for param in c.GetParameters() do yield (getTypeErasedTo param.ParameterType) }
                |> Seq.map (fun typ -> Expr.Value(null, typ))
                |> Array.ofSeq
                |> c.GetInvokeCodeInternal false

            let printExpr x = 
                if ignoreOutput then 
                    ""
                else 
                    sprintf "\n%A\n" x

            let printObj x = 
                if ignoreOutput then 
                    ""
                else 
                    sprintf "\n%O\n" x

            match memberInfo with

            | :? ProvidedConstructor as cons -> 
                let body = 
                    if signatureOnly then ""
                    else cons |> getConstructorBody |> printExpr
                if not ignoreOutput then
                    print <| "new : " + 
                             (toSignature <| cons.GetParameters()) + " -> " + 
                             (toString memberInfo.DeclaringType) + body

            | :? ProvidedLiteralField as field -> 
                let value = 
                    if signatureOnly then ""
                    else field.GetRawConstantValue() |> printObj
                if not ignoreOutput then
                    print <| "val " + field.Name + ": " + 
                             (toString field.FieldType) + 
                             value
                         
            | :? ProvidedProperty as prop -> 
                let body = 
                    if signatureOnly then ""
                    else
                        let getter = 
                            if not prop.CanRead then ""
                            else getMethodBody (prop.GetGetMethod() :?> ProvidedMethod) |> printExpr
                        let setter = 
                            if not prop.CanWrite then ""
                            else getMethodBody (prop.GetSetMethod() :?> ProvidedMethod) |> printExpr
                        getter + setter
                if not ignoreOutput then
                    print <| (if prop.IsStatic then "static " else "") + "member " + 
                             prop.Name + ": " + (toString prop.PropertyType) + 
                             " with " + (if prop.CanRead && prop.CanWrite then "get, set" else if prop.CanRead then "get" else "set")            

            | :? ProvidedMethod as m ->
                let body = 
                    if signatureOnly then ""
                    else m |> getMethodBody |> printExpr
                if not ignoreOutput then
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

    /// Returns a string representation of the signature (and optionally also the body) of all the
    /// types generated by the type provider
    let prettyPrint signatureOnly ignoreOutput t = innerPrettyPrint signatureOnly ignoreOutput None (fun _ -> false) t

    /// Returns a string representation of the signature (and optionally also the body) of all the
    /// types generated by the type provider up to a certain depth
    let prettyPrintWithMaxDepth signatureOnly ignoreOutput maxDepth t = innerPrettyPrint signatureOnly ignoreOutput (Some maxDepth) (fun _ -> false) t

    /// Returns a string representation of the signature (and optionally also the body) of all the
    /// types generated by the type provider up to a certain depth and excluding some types
    let prettyPrintWithMaxDepthAndExclusions signatureOnly ignoreOutput maxDepth exclusions t = 
        let exclusions = Set.ofSeq exclusions
        innerPrettyPrint signatureOnly ignoreOutput (Some maxDepth) (fun t -> exclusions.Contains t.Name) t
