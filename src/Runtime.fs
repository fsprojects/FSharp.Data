namespace global

open System.Runtime.CompilerServices
open FSharp.Core.CompilerServices

[<assembly: TypeProviderAssembly("FSharp.Data.DesignTime")>]
[<assembly: InternalsVisibleTo("FSharp.Data.Tests")>]
do ()
