﻿namespace global

open System.Runtime.CompilerServices
open Microsoft.FSharp.Core.CompilerServices

[<assembly:TypeProviderAssembly("FSharp.Data.DesignTime")>]
[<assembly:InternalsVisibleToAttribute("FSharp.Data.Tests")>]
do()
