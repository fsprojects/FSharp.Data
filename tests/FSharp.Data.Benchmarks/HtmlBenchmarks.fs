namespace FSharp.Data.Benchmarks

open System
open System.IO
open BenchmarkDotNet.Attributes
open FSharp.Data

[<MemoryDiagnoser>]
[<SimpleJob>]
type HtmlBenchmarks() =
    
    let mutable simpleHtmlText = ""
    let mutable zooplaHtmlText = ""
    let mutable usPresidentsHtmlText = ""
    let mutable doctorWhoHtmlText = ""
    let mutable wimbledonHtmlText = ""
    
    [<GlobalSetup>]
    member this.Setup() =
        let dataPath = Path.Combine(__SOURCE_DIRECTORY__, "../FSharp.Data.Tests/Data")
        
        // Load various HTML files of different sizes and complexity
        simpleHtmlText <- File.ReadAllText(Path.Combine(dataPath, "SimpleHtmlTablesWithTr.html"))
        zooplaHtmlText <- File.ReadAllText(Path.Combine(dataPath, "zoopla.html"))  // ~773KB
        usPresidentsHtmlText <- File.ReadAllText(Path.Combine(dataPath, "us_presidents_wikipedia.html"))  // ~698KB
        doctorWhoHtmlText <- File.ReadAllText(Path.Combine(dataPath, "doctor_who2.html"))  // ~518KB
        wimbledonHtmlText <- File.ReadAllText(Path.Combine(dataPath, "wimbledon_wikipedia.html"))  // ~411KB
    
    [<Benchmark>]
    member this.ParseSimpleHtml() =
        HtmlDocument.Parse(simpleHtmlText)
    
    [<Benchmark>]
    member this.ParseZooplaHtml() =
        HtmlDocument.Parse(zooplaHtmlText)
    
    [<Benchmark>]
    member this.ParseUsPresidentsHtml() =
        HtmlDocument.Parse(usPresidentsHtmlText)
    
    [<Benchmark>]
    member this.ParseDoctorWhoHtml() =
        HtmlDocument.Parse(doctorWhoHtmlText)
    
    [<Benchmark>]
    member this.ParseWimbledonHtml() =
        HtmlDocument.Parse(wimbledonHtmlText)