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
    | _ -> 
        printfn "Running all benchmarks..."
        BenchmarkRunner.Run<JsonBenchmarks>() |> ignore
        BenchmarkRunner.Run<JsonConversionBenchmarks>() |> ignore
    
    0