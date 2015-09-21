module internal ProviderImplementation.AssemblyResolver

open System
open System.IO
open System.Net
open System.Reflection
open System.Xml.Linq
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation
open ProviderImplementation.ProvidedBindingContext

let private designTimeAssemblies = 
    [ for asm in Assembly.GetExecutingAssembly().GetReferencedAssemblies() do
         let asm = try Assembly.Load(asm) with _ -> null
         if asm <> null then 
            yield asm.GetName().Name, asm ]

let mutable private initialized = false    

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
    
    let bindingContext = cfg.GetBindingContext()

    let version = 
        let fsCore = bindingContext.TryGetFSharpCoreAssembly()

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


    let getRuntimeAssembly (asmSimpleName:string) = 
        match bindingContext.TryBindAssembly(AssemblyName(asmSimpleName)) with
        | None -> null
        | Some loader -> (loader :> Assembly)

    if not initialized then
        initialized <- true
        // the following parameter is just here to force System.Xml.Linq to load
        WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials
        ProvidedTypes.ProvidedTypeDefinition.Logger := Some FSharp.Data.Runtime.IO.log

    let runtimeFSharpDataAssembly = getRuntimeAssembly (Path.GetFileNameWithoutExtension cfg.RuntimeAssembly)
    
    let fsharpDataAssemblyPair = Assembly.GetExecutingAssembly(), runtimeFSharpDataAssembly

    let referencedAssembliesPairs = 
        [ yield fsharpDataAssemblyPair
          for (designTimeAsmSimpleName, designTimeAsm) in designTimeAssemblies do
             match getRuntimeAssembly designTimeAsmSimpleName with 
             | null -> ()
             | runtimeAsm -> yield (designTimeAsm, runtimeAsm) ]

    bindingContext, runtimeFSharpDataAssembly, version, AssemblyReplacer referencedAssembliesPairs