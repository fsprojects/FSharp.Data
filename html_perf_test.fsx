#r "src/FSharp.Data.Html.Core/bin/Release/netstandard2.0/FSharp.Data.Html.Core.dll"
#r "src/FSharp.Data.Runtime.Utilities/bin/Release/netstandard2.0/FSharp.Data.Runtime.Utilities.dll"

open System
open System.IO
open System.Diagnostics
open FSharp.Data

// Load test HTML files
let simpleHtml = File.ReadAllText("tests/FSharp.Data.Tests/Data/SimpleHtmlTablesWithTr.html")
let zooplaHtml = File.ReadAllText("tests/FSharp.Data.Tests/Data/zoopla.html")

// Simple performance test function
let timeHtmlParsing name html iterations =
    let sw = Stopwatch.StartNew()
    let mutable result = Unchecked.defaultof<HtmlDocument>
    for i = 1 to iterations do
        result <- HtmlDocument.Parse(html)
    sw.Stop()
    let totalMs = sw.ElapsedMilliseconds
    let avgMs = float totalMs / float iterations
    printfn "%s: %d iterations in %d ms (%.2f ms per parse, %d chars)" name iterations totalMs avgMs html.Length
    result

// Run tests
printfn "HTML Parsing Performance Tests"
printfn "=============================="

let simpleResult = timeHtmlParsing "Simple HTML" simpleHtml 1000
let zooplaResult = timeHtmlParsing "Zoopla HTML" zooplaHtml 10

printfn "\nTest completed successfully"