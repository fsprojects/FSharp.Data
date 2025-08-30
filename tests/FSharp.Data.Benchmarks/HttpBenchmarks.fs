namespace FSharp.Data.Benchmarks

open System
open System.Net
open BenchmarkDotNet.Attributes
open FSharp.Data

[<MemoryDiagnoser>]
[<SimpleJob>]
type HttpBenchmarks() =
    
    // Use public HTTP test endpoints for realistic benchmarks
    let httpBinUrl = "https://httpbin.org"
    let smallEndpoint = $"{httpBinUrl}/get"
    let jsonEndpoint = $"{httpBinUrl}/json"
    
    [<Benchmark>]
    member _.SingleHttpBinRequest() =
        try
            Http.RequestString(smallEndpoint, timeout = 5000)
        with
        | :? WebException -> "Network error" // Handle network issues in CI

    [<Benchmark>]
    member _.HttpBinJsonRequest() =
        try  
            Http.RequestString(jsonEndpoint, timeout = 5000)
        with
        | :? WebException -> "Network error" // Handle network issues in CI

    [<Benchmark>]
    member _.MultipleSequentialRequests() =
        for _ in 1..3 do
            try
                Http.RequestString(smallEndpoint, timeout = 5000) |> ignore
            with
            | :? WebException -> () // Handle network issues in CI

    [<Benchmark>]
    member _.PostRequest() =
        let body = TextRequest "test data"
        try
            Http.RequestString($"{httpBinUrl}/post", httpMethod = "POST", body = body, timeout = 5000)
        with
        | :? WebException -> "Network error" // Handle network issues in CI

    [<Benchmark>]
    member _.RequestWithHeaders() =
        let headers = [("User-Agent", "FSharp.Data.Benchmarks"); ("Accept", "application/json")]
        try
            Http.RequestString(smallEndpoint, headers = headers, timeout = 5000)
        with
        | :? WebException -> "Network error" // Handle network issues in CI

    [<Benchmark>]  
    member _.TypeProviderWorkloadSimulation() =
        // Simulate a type provider fetching sample data and caching
        for _ in 1..2 do
            try
                Http.RequestString(jsonEndpoint, timeout = 5000) |> ignore
            with
            | :? WebException -> () // Handle network issues in CI

    [<Benchmark>]
    member _.MultipleConnectionsToSameHost() =
        // Test connection reuse with keep-alive optimization
        let endpoints = [
            $"{httpBinUrl}/get"
            $"{httpBinUrl}/json" 
            $"{httpBinUrl}/user-agent"
        ]
        for endpoint in endpoints do
            try
                Http.RequestString(endpoint, timeout = 5000) |> ignore
            with
            | :? WebException -> () // Handle network issues in CI