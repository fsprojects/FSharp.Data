module ProviderImplementation.AssemblyResolver

open System.Net
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices

[<RequireQualifiedAccess>]
type FSharpDataRuntimeVersion =
    | Net40
    | Portable47 //net45+sl5+win8
    | Portable259 //net45+win8+wpa81+wp8
    member x.SupportsLocalFileSystem = 
        match x with
        | Net40 -> true
        | _ -> false

let init (cfg : TypeProviderConfig) = 

    WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials
    
    let runtimeAssembly = Assembly.LoadFrom cfg.RuntimeAssembly

    let runtimeAssemblyName = runtimeAssembly.GetName()

    let runtimeVersion =
        match runtimeAssemblyName.Version.Revision with
        | 0 -> FSharpDataRuntimeVersion.Net40
        | 47 -> FSharpDataRuntimeVersion.Portable47
        | 259 -> FSharpDataRuntimeVersion.Portable259
        | _ -> failwithf "Unexpected version of %s.dll: %O [Looking for revision 0, 7 or 47]" runtimeAssemblyName.Name runtimeAssemblyName.Version

    runtimeAssembly, runtimeVersion
