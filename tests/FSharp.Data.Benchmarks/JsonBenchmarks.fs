namespace FSharp.Data.Benchmarks

open System
open System.IO
open BenchmarkDotNet.Attributes
open FSharp.Data

[<MemoryDiagnoser>]
[<SimpleJob>]
type JsonBenchmarks() =
    
    let mutable githubJsonText = ""
    let mutable twitterJsonText = ""
    let mutable worldBankJsonText = ""
    let mutable simpleJsonText = ""
    let mutable nestedJsonText = ""
    
    [<GlobalSetup>]
    member this.Setup() =
        let dataPath = Path.Combine(__SOURCE_DIRECTORY__, "../FSharp.Data.Tests/Data")
        
        githubJsonText <- File.ReadAllText(Path.Combine(dataPath, "GitHub.json"))
        twitterJsonText <- File.ReadAllText(Path.Combine(dataPath, "TwitterSample.json"))
        worldBankJsonText <- File.ReadAllText(Path.Combine(dataPath, "WorldBank.json"))
        simpleJsonText <- File.ReadAllText(Path.Combine(dataPath, "Simple.json"))
        nestedJsonText <- File.ReadAllText(Path.Combine(dataPath, "Nested.json"))
    
    [<Benchmark>]
    member this.ParseSimpleJson() =
        JsonValue.Parse(simpleJsonText)
    
    [<Benchmark>]
    member this.ParseNestedJson() =
        JsonValue.Parse(nestedJsonText)
    
    [<Benchmark>]
    member this.ParseGitHubJson() =
        JsonValue.Parse(githubJsonText)
    
    [<Benchmark>]
    member this.ParseTwitterJson() =
        JsonValue.Parse(twitterJsonText)
    
    [<Benchmark>]
    member this.ParseWorldBankJson() =
        JsonValue.Parse(worldBankJsonText)
    
    [<Benchmark>]
    member this.ToStringGitHubJson() =
        let json = JsonValue.Parse(githubJsonText)
        json.ToString()
    
    [<Benchmark>]
    member this.ToStringTwitterJson() =
        let json = JsonValue.Parse(twitterJsonText)
        json.ToString()

[<MemoryDiagnoser>]
[<SimpleJob>]
type JsonConversionBenchmarks() =
    
    let mutable sampleJson = JsonValue.Null
    
    [<GlobalSetup>]
    member this.Setup() =
        let dataPath = Path.Combine(__SOURCE_DIRECTORY__, "../FSharp.Data.Tests/Data")
        let githubJsonText = File.ReadAllText(Path.Combine(dataPath, "GitHub.json"))
        sampleJson <- JsonValue.Parse(githubJsonText)
    
    [<Benchmark>]
    member this.AccessProperties() =
        match sampleJson with
        | JsonValue.Array elements when elements.Length > 0 ->
            // Get properties of the first issue in the GitHub issues array
            elements.[0].Properties()
            |> Array.head
            |> fun (_, value) -> value
        | JsonValue.Record _ ->
            // Fallback for record-type JSON
            sampleJson.Properties()
            |> Array.head
            |> fun (_, value) -> value
        | _ -> JsonValue.Null
    
    [<Benchmark>]
    member this.GetArrayElements() =
        match sampleJson with
        | JsonValue.Array elements -> 
            // GitHub.json is an array of issues, return the array elements
            elements
        | JsonValue.Record props -> 
            // Fallback: look for an "items" property
            match Array.tryFind (fun (k, _) -> k = "items") props with
            | Some (_, JsonValue.Array elements) -> elements
            | _ -> [||]
        | _ -> [||]