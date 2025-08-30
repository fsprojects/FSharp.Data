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
    | [| "csv" |] -> BenchmarkRunner.Run<CsvParsingBenchmarks>() |> ignore
    | [| "csv-streaming" |] -> BenchmarkRunner.Run<CsvStreamingBenchmarks>() |> ignore
    | _ -> 
        printfn "Running all benchmarks..."
        BenchmarkRunner.Run<JsonBenchmarks>() |> ignore
        BenchmarkRunner.Run<JsonConversionBenchmarks>() |> ignore
        BenchmarkRunner.Run<CsvParsingBenchmarks>() |> ignore
        BenchmarkRunner.Run<CsvStreamingBenchmarks>() |> ignore
    
    0