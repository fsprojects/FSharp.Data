/// Benchmark: System.Text.Json-backed parser vs current FSharp.Data hand-written parser
/// Prototypes "Option 2" from https://github.com/fsprojects/FSharp.Data/issues/1671 —
/// keep the JsonValue public API while using Utf8JsonReader / JsonDocument as the parsing kernel.
///
/// Run with: dotnet run -c Release -- stj
namespace FSharp.Data.Benchmarks

open System.IO
open System.Text.Json
open BenchmarkDotNet.Attributes
open FSharp.Data

/// Converts a System.Text.Json JsonElement into a FSharp.Data JsonValue.
/// This is the prototype STJ backend that could replace the hand-written parser.
module private StjConverter =

    let rec ofJsonElement (el: JsonElement) : JsonValue =
        match el.ValueKind with
        | JsonValueKind.Null -> JsonValue.Null
        | JsonValueKind.True -> JsonValue.Boolean true
        | JsonValueKind.False -> JsonValue.Boolean false
        | JsonValueKind.String -> JsonValue.String(el.GetString())
        | JsonValueKind.Number ->
            let mutable d = 0m

            if el.TryGetDecimal(&d) then
                JsonValue.Number(d)
            else
                JsonValue.Float(el.GetDouble())
        | JsonValueKind.Array ->
            el.EnumerateArray()
            |> Seq.map ofJsonElement
            |> Seq.toArray
            |> JsonValue.Array
        | JsonValueKind.Object ->
            el.EnumerateObject()
            |> Seq.map (fun p -> p.Name, ofJsonElement p.Value)
            |> Seq.toArray
            |> JsonValue.Record
        | _ -> JsonValue.Null

    /// Parse a JSON string to JsonValue using System.Text.Json as the parsing backend.
    let parse (text: string) : JsonValue =
        use doc = JsonDocument.Parse(text)
        ofJsonElement doc.RootElement

/// Compares the current FSharp.Data hand-written parser with a System.Text.Json-backed
/// prototype on three representative real-world JSON files. See issue #1671 for context.
[<MemoryDiagnoser>]
[<SimpleJob>]
type JsonStjBenchmarks() =

    let mutable githubJsonText = ""
    let mutable twitterJsonText = ""
    let mutable worldBankJsonText = ""

    [<GlobalSetup>]
    member _.Setup() =
        let dataPath = Path.Combine(__SOURCE_DIRECTORY__, "../FSharp.Data.Tests/Data")
        githubJsonText <- File.ReadAllText(Path.Combine(dataPath, "GitHub.json"))
        twitterJsonText <- File.ReadAllText(Path.Combine(dataPath, "TwitterSample.json"))
        worldBankJsonText <- File.ReadAllText(Path.Combine(dataPath, "WorldBank.json"))

    // ── Current hand-written parser (baseline) ──────────────────────────────────────

    [<Benchmark(Baseline = true)>]
    member _.ParseGitHub_Current() = JsonValue.Parse(githubJsonText)

    [<Benchmark>]
    member _.ParseTwitter_Current() = JsonValue.Parse(twitterJsonText)

    [<Benchmark>]
    member _.ParseWorldBank_Current() = JsonValue.Parse(worldBankJsonText)

    // ── STJ-backed prototype (Option 2 from #1671) ──────────────────────────────────

    [<Benchmark>]
    member _.ParseGitHub_Stj() = StjConverter.parse githubJsonText

    [<Benchmark>]
    member _.ParseTwitter_Stj() = StjConverter.parse twitterJsonText

    [<Benchmark>]
    member _.ParseWorldBank_Stj() = StjConverter.parse worldBankJsonText
