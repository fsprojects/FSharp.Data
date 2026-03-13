open BenchmarkDotNet.Running
open FSharp.Data.Benchmarks

[<EntryPoint>]
let main args =
    printfn "FSharp.Data Benchmarks"
    printfn "====================="
    printfn ""
    
    match args with
    | [| "json" |] -> BenchmarkRunner.Run<JsonBenchmarks>() |> ignore
    | [| "conversions" |] -> BenchmarkRunner.Run<JsonConversionBenchmarks>() |> ignore
    | [| "html" |] -> BenchmarkRunner.Run<HtmlBenchmarks>() |> ignore
    | [| "csv" |] -> BenchmarkRunner.Run<CsvBenchmarks>() |> ignore
    | [| "stj" |] -> BenchmarkRunner.Run<JsonStjBenchmarks>() |> ignore
    | _ ->
        printfn "Running all benchmarks..."
        BenchmarkRunner.Run<JsonBenchmarks>() |> ignore
        BenchmarkRunner.Run<JsonConversionBenchmarks>() |> ignore
        BenchmarkRunner.Run<HtmlBenchmarks>() |> ignore
        BenchmarkRunner.Run<CsvBenchmarks>() |> ignore
        BenchmarkRunner.Run<JsonStjBenchmarks>() |> ignore
    
    0