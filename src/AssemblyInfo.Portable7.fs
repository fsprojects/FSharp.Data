namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.Data")>]
[<assembly: AssemblyProductAttribute("FSharp.Data")>]
[<assembly: AssemblyDescriptionAttribute("Library of F# type providers and data access tools")>]
[<assembly: AssemblyVersionAttribute("2.2.5.7")>]
[<assembly: AssemblyFileVersionAttribute("2.2.5.7")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "2.2.5.7"
