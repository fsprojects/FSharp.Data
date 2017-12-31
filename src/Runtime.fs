namespace global

open System.Runtime.CompilerServices
#if !NETSTANDARD2_0
open Microsoft.FSharp.Core.CompilerServices

[<assembly:TypeProviderAssembly("FSharp.Data.DesignTime")>]
#endif
[<assembly:InternalsVisibleToAttribute("FSharp.Data.Tests")>]
do()
