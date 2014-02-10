﻿namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.Data.Experimental.DesignTime")>]
[<assembly: AssemblyProductAttribute("FSharp.Data.Experimental")>]
[<assembly: AssemblyDescriptionAttribute("Library of F# type providers and data access tools (experimental extensions)")>]
[<assembly: AssemblyVersionAttribute("2.0.0.0")>]
[<assembly: AssemblyFileVersionAttribute("2.0.0.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "2.0.0.0"
