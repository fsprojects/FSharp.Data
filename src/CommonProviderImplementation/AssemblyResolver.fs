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
type FSharpDataRuntimeInfo =
    | Net40
    | Portable_47_7_259
    member x.SupportsLocalFileSystem = 
        match x with
        | Net40 -> true
        | Portable_47_7_259 -> false


let init (cfg : TypeProviderConfig) = 

    let bindingContext = cfg.GetTypeProviderBindingContext()

    let version = 
        let fsCore = bindingContext.TryGetFSharpCoreAssembly()

        match fsCore with 
        | Choice2Of2 _err -> FSharpDataRuntimeInfo.Net40
        | Choice1Of2 asm -> 
           let ver = asm.GetName().Version
           match ver.Major, ver.Minor with 
            | 4, _ -> FSharpDataRuntimeInfo.Net40 // 4.3.0.0, 4.3.1.0, 4.4.0.0
            | _ -> FSharpDataRuntimeInfo.Portable_47_7_259

    if not initialized then
        initialized <- true
        WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials
        ProvidedTypes.ProvidedTypeDefinition.Logger := Some FSharp.Data.Runtime.IO.log

    let runtimeFSharpDataAssembly = 
        let asmSimpleName = Path.GetFileNameWithoutExtension cfg.RuntimeAssembly
        match bindingContext.TryBindAssembly(AssemblyName(asmSimpleName)) with
        | Choice2Of2 err -> raise err
        | Choice1Of2 loader -> (loader :> Assembly)
    
    let referencedAssemblies = bindingContext.ReferencedAssemblies

    bindingContext, runtimeFSharpDataAssembly, version, AssemblyReplacer (designTimeAssemblies, referencedAssemblies)

