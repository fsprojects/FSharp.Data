namespace global

open System.Runtime.CompilerServices
open Microsoft.FSharp.Core.CompilerServices

#if EXPERIMENTAL
[<assembly:TypeProviderAssembly("FSharp.Data.Experimental.DesignTime")>]
#else
[<assembly:TypeProviderAssembly("FSharp.Data.DesignTime")>]
#endif
[<assembly:InternalsVisibleToAttribute("FSharp.Data.Tests")>]
do()
