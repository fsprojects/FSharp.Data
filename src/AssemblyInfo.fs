namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.Data")>]
[<assembly: AssemblyProductAttribute("FSharp.Data")>]
[<assembly: AssemblyDescriptionAttribute("Library of F# type providers and data access tools")>]
[<assembly: AssemblyVersionAttribute("2.3.1.0")>]
[<assembly: AssemblyFileVersionAttribute("2.3.1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "2.3.1.0"
    let [<Literal>] InformationalVersion = "2.3.1.0"
