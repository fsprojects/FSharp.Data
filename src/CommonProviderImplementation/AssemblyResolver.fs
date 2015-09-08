module ProviderImplementation.AssemblyResolver

open System
open System.IO
open System.Net
open System.Reflection
open System.Xml.Linq
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation

let private designTimeAssemblies = 
    [ for asm in Assembly.GetExecutingAssembly().GetReferencedAssemblies() do
         let asm = try Assembly.Load(asm) with _ -> null
         if asm <> null then 
            yield asm.GetName().Name, asm ]

let mutable private initialized = false    

type System.Object with 
   member internal x.GetProperty(nm) = 
       let ty = x.GetType()
       let prop = ty.GetProperty(nm, BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic)
       let v = prop.GetValue(x)
       v
   member internal x.GetField(nm) = 
       let ty = x.GetType()
       let fld = ty.GetField(nm, BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic)
       let v = fld.GetValue(x)
       v
   member internal x.HasField(nm) = 
       let ty = x.GetType()
       let fld = ty.GetField(nm, BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic)
       fld <> null
   member internal x.GetElements() = [ for v in (x :?> System.Collections.IEnumerable) do yield v ]

[<RequireQualifiedAccess>]
type FSharpDataRuntimeVersion =
    | Net40
    | Portable7 //net45+win8
    | Portable47 //net45+wp8+win8
    member x.SupportsLocalFileSystem = 
        match x with
        | Net40 -> true
        | _ -> false


let init (cfg : TypeProviderConfig) = 

    // This is a hack to get the set of referenced assemblies for this instance of the type provider.
    // This information _should_ be provided in cfg.ReferencedAssemblies however that is missing a couple of crucial references (the mscorlib and FSharp.Core references).
    // So we use reflection to access into the captured F# compiler closures to find the correct set of DLLs.  
    // This is obviously not "right" but is reasonable considering that it's crucial to get this information.
    // The aim is to have code that works correctly for all Visual F# Tools and F# open edition uses 
    // of type providers for F# 3.0, 3.1 and 4.0.
    
    let referencedAssemblies = 
        try 
           let systemRuntimeContainsTypeObj = cfg.GetField("systemRuntimeContainsType") 
           // Account for https://github.com/Microsoft/visualfsharp/pull/591
           let systemRuntimeContainsTypeObj2 = 
               if systemRuntimeContainsTypeObj.HasField("systemRuntimeContainsTypeRef") then 
                   systemRuntimeContainsTypeObj.GetField("systemRuntimeContainsTypeRef").GetProperty("Value")
               else
                   systemRuntimeContainsTypeObj
           let tcImportsObj = systemRuntimeContainsTypeObj2.GetField("tcImports")
           [ for dllInfo in tcImportsObj.GetField("dllInfos").GetElements() -> (dllInfo.GetProperty("FileName") :?> string)
             for dllInfo in tcImportsObj.GetProperty("Base").GetProperty("Value").GetField("dllInfos").GetElements() -> (dllInfo.GetProperty("FileName") :?> string) ]
        with _ ->
           []

    let version = 
        let fsCore = 
             referencedAssemblies |> Seq.tryPick (fun asmPath -> 
                 let simpleName = Path.GetFileNameWithoutExtension(asmPath)
                 if simpleName = "FSharp.Core" then Some (Assembly.ReflectionOnlyLoadFrom(asmPath))
                 else None)

        match fsCore with 
        | None -> FSharpDataRuntimeVersion.Net40
        | Some asm -> 
           let ver = asm.GetName().Version
           match ver.Major, ver.Minor with 
            | 2, 3 -> FSharpDataRuntimeVersion.Portable47 // 2.3.5.0, 2.3.5.1
            | 4, _ -> FSharpDataRuntimeVersion.Net40 // 4.3.0.0, 4.3.1.0, 4.4.0.0
            | 3, 47 -> FSharpDataRuntimeVersion.Net40 // 3.47.4.0
            | 3, 7 -> FSharpDataRuntimeVersion.Portable7 // 3.7.4.0
            | 3, 78 -> FSharpDataRuntimeVersion.Portable7 // 3.7.4.0
            | 3, 259 -> FSharpDataRuntimeVersion.Portable7 // 3.7.4.0
            | 3, 3 -> FSharpDataRuntimeVersion.Portable7 // 3.3.1.0
            | _ -> FSharpDataRuntimeVersion.Net40

    let runtimeAssemblies = 
        [ for asmPath in referencedAssemblies do
             let simpleName = Path.GetFileNameWithoutExtension(asmPath)
             // Using ReflectionOnlyLoadFrom on mscorlib doesn't seem to work for mscorlib 4.0
             if not (version = FSharpDataRuntimeVersion.Net40 && simpleName = "mscorlib") then  
                yield simpleName, lazy (try Assembly.ReflectionOnlyLoadFrom(asmPath) with _ -> null) ]
        |> Map.ofList

    let getRuntimeAssembly (asmSimpleName:string) = 
        match runtimeAssemblies.TryFind asmSimpleName with
        | None -> null
        | Some loader -> loader.Force()

    let resolveRuntimeAssembly (asmName: AssemblyName) = 
        let simpleName = asmName.Name
        getRuntimeAssembly simpleName 

    let _remover = 
        let resolver = ResolveEventHandler(fun _ args ->  resolveRuntimeAssembly (AssemblyName args.Name))
        AppDomain.CurrentDomain.add_ReflectionOnlyAssemblyResolve resolver
        { new IDisposable with member x.Dispose() = AppDomain.CurrentDomain.remove_ReflectionOnlyAssemblyResolve resolver }

    if not initialized then
        initialized <- true
        // the following parameter is just here to force System.Xml.Linq to load
        WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials
        ProvidedTypes.ProvidedTypeDefinition.Logger := Some FSharp.Data.Runtime.IO.log

    let runtimeFSharpDataAssembly = Assembly.ReflectionOnlyLoadFrom cfg.RuntimeAssembly
    
    let runtimeFSharpDataAssemblyPair = Assembly.GetExecutingAssembly(), runtimeFSharpDataAssembly

    let referencedAssembliesPairs = 
        [ yield runtimeFSharpDataAssemblyPair
          for (designTimeAsmSimpleName, designTimeAsm) in designTimeAssemblies do
             match getRuntimeAssembly designTimeAsmSimpleName with 
             | null -> ()
             | runtimeAsm -> yield (designTimeAsm, runtimeAsm) ]

    // Preload all the dependencies
    for (_,runtimeAsm) in referencedAssembliesPairs do 
        for refAsm in runtimeAsm.GetReferencedAssemblies() do
            resolveRuntimeAssembly refAsm |> ignore

    runtimeFSharpDataAssembly, version, AssemblyReplacer.create referencedAssembliesPairs