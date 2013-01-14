module ProviderImplementation.Providers
open Microsoft.FSharp.Core.CompilerServices
open System.Runtime.CompilerServices

[<assembly:TypeProviderAssembly>]
[<assembly:InternalsVisibleToAttribute("FSharp.Data.Experimental")>]
do()
