namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.Data.DesignTime")>]
[<assembly: AssemblyProductAttribute("FSharp.Data")>]
[<assembly: AssemblyDescriptionAttribute("Library of F# type providers and data access tools")>]
[<assembly: AssemblyVersionAttribute("2.2.5.0")>]
[<assembly: AssemblyFileVersionAttribute("2.2.5.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "2.2.5.0"
