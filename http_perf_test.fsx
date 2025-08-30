// Simple HTTP performance test to measure keep-alive optimization impact
#I __SOURCE_DIRECTORY__
#r "src/FSharp.Data.Runtime.Utilities/bin/Release/netstandard2.0/FSharp.Data.Runtime.Utilities.dll"
#r "src/FSharp.Data.Http/bin/Release/netstandard2.0/FSharp.Data.Http.dll"

open System
open System.Diagnostics
open System.Net
open FSharp.Data

let measureTime name iterations f =
    // Warm up
    try f() |> ignore with _ -> ()
    
    let sw = Stopwatch.StartNew()
    let mutable successful = 0
    for i in 1..iterations do
        try 
            f() |> ignore
            successful <- successful + 1
        with 
        | :? WebException -> () // Skip network errors in CI
        | ex -> printfn "Error in %s: %s" name ex.Message
    sw.Stop()
    
    let avgTime = if successful > 0 then sw.ElapsedMilliseconds / (int64 successful) else 0L
    printfn "%s: %d/%d successful, avg %d ms per request" name successful iterations avgTime

// Test HTTP performance with realistic scenarios
printfn "=== HTTP Performance Test ==="
printfn "Testing connection keep-alive and pooling optimizations"
printfn ""

// Test 1: Single request performance  
measureTime "Single HTTP request" 3 (fun () ->
    Http.RequestString("https://httpbin.org/get", timeout = 10000)
)

printfn ""

// Test 2: Multiple requests to same host (should benefit from keep-alive)
measureTime "Multiple requests (keep-alive test)" 1 (fun () ->
    let endpoints = [
        "https://httpbin.org/get"
        "https://httpbin.org/json"  
        "https://httpbin.org/user-agent"
    ]
    for endpoint in endpoints do
        try
            Http.RequestString(endpoint, timeout = 10000) |> ignore
        with _ -> ()
)

printfn ""

// Test 3: POST request performance
measureTime "POST request" 2 (fun () ->
    let body = TextRequest "test data for performance measurement"
    Http.RequestString("https://httpbin.org/post", httpMethod = "POST", body = body, timeout = 10000)
)

printfn ""

// Test 4: Type provider simulation - multiple JSON requests
measureTime "Type provider simulation" 1 (fun () ->
    for _ in 1..3 do
        try
            Http.RequestString("https://httpbin.org/json", timeout = 10000) |> ignore
        with _ -> ()
)

printfn ""
printfn "Performance test completed!"
printfn "Keep-alive optimization should improve 'Multiple requests' and 'Type provider simulation' scenarios"
printfn "where multiple requests are made to the same host."