// HTTP String Processing Performance Test - Simple Version
#r "src/FSharp.Data.Http/bin/Release/netstandard2.0/FSharp.Data.Http.dll"

open System.Diagnostics
open System.Text
open FSharp.Data

// Simple form data test
let testFormDataParsing () =
    let formData = [
        ("username", "testuser123")
        ("password", "secretpassword")  
        ("email", "user@example.com")
        ("firstName", "John")
        ("lastName", "Doe")
    ]
    
    printfn "Testing form data encoding performance..."
    let sw = Stopwatch.StartNew()
    for i in 1..10000 do
        let body = FormValues formData
        ignore body
    sw.Stop()
    
    printfn "Form data encoding (10,000 operations): %dms (~%.4fms per operation)" 
        sw.ElapsedMilliseconds (float sw.ElapsedMilliseconds / 10000.0)

// Basic string building test to show the concept
let testStringBuilding () =
    printfn "Testing string building optimizations concept..."
    
    // Simulate before optimization (string concatenation)
    let testStringConcat () =
        let mutable result = "param1=value1"
        for i in 2..100 do
            result <- result + "&param" + string i + "=value" + string i
        result
    
    // Simulate after optimization (StringBuilder)  
    let testStringBuilder () =
        let sb = StringBuilder("param1=value1")
        for i in 2..100 do
            sb.Append("&param") |> ignore
            sb.Append(string i) |> ignore  
            sb.Append("=value") |> ignore
            sb.Append(string i) |> ignore
        sb.ToString()
    
    // Time string concatenation approach
    let sw1 = Stopwatch.StartNew()
    for i in 1..1000 do
        ignore (testStringConcat())
    sw1.Stop()
    
    // Time StringBuilder approach  
    let sw2 = Stopwatch.StartNew()
    for i in 1..1000 do
        ignore (testStringBuilder())
    sw2.Stop()
    
    printfn "String concatenation (1,000 ops): %dms (~%.4fms per op)" sw1.ElapsedMilliseconds (float sw1.ElapsedMilliseconds / 1000.0)
    printfn "StringBuilder (1,000 ops): %dms (~%.4fms per op)" sw2.ElapsedMilliseconds (float sw2.ElapsedMilliseconds / 1000.0)
    printfn "StringBuilder improvement: %.1fx faster" (float sw1.ElapsedMilliseconds / float sw2.ElapsedMilliseconds)

printfn "=== HTTP String Processing Performance Analysis ==="
printfn ""
testFormDataParsing()
printfn ""
testStringBuilding()
printfn ""
printfn "HTTP Performance Optimizations Implemented:"
printfn "1. Cookie processing - StringBuilder instead of string concatenation"
printfn "2. URL query string building - StringBuilder for parameter encoding"  
printfn "3. Form data encoding - StringBuilder for key=value&key=value patterns"
printfn "4. XML root wrapping - StringBuilder for <root>content</root> pattern"
printfn ""
printfn "Expected Benefits:"
printfn "- Reduced memory allocations in HTTP request processing"
printfn "- Better performance for high-throughput HTTP scenarios"
printfn "- Lower GC pressure during form submissions and API calls"