module internal ProviderImplementation.AssemblyResolver

open System
open System.IO
open System.Net
open System.Reflection
open System.Xml.Linq
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation
open ProviderImplementation.TypeProviderBindingContext

let private designTimeAssemblies = 
    [| yield Assembly.GetExecutingAssembly() 
       for asm in Assembly.GetExecutingAssembly().GetReferencedAssemblies() do
         let asm = try Assembly.Load(asm) with _ -> null
         if asm <> null then 
            yield asm |]

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

    let bindingContext = cfg.GetTypeProviderBindingContext()

    let version = 
        let fsCore = bindingContext.TryGetFSharpCoreAssembly()

        match fsCore with 
        | Choice2Of2 _err -> FSharpDataRuntimeVersion.Net40
        | Choice1Of2 asm -> 
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

    if not initialized then
        initialized <- true
        // the following parameter is just here to force System.Xml.Linq to load
        WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials
        ProvidedTypes.ProvidedTypeDefinition.Logger := Some FSharp.Data.Runtime.IO.log

    let runtimeFSharpDataAssembly = 
        let asmSimpleName = Path.GetFileNameWithoutExtension cfg.RuntimeAssembly
        match bindingContext.TryBindAssembly(AssemblyName(asmSimpleName)) with
        | Choice2Of2 err -> raise err
        | Choice1Of2 loader -> (loader :> Assembly)
    
    let referencedAssemblies = bindingContext.ReferencedAssemblies

    bindingContext, runtimeFSharpDataAssembly, version, AssemblyReplacer (designTimeAssemblies, referencedAssemblies)

