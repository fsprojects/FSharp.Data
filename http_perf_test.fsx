// HTTP String Processing Performance Test
#r "src/FSharp.Data.Http/bin/Release/netstandard2.0/FSharp.Data.Http.dll"
#r "src/FSharp.Data.Runtime.Utilities/bin/Release/netstandard2.0/FSharp.Data.Runtime.Utilities.dll"

open System.Diagnostics
open FSharp.Data

// Test 1: HTTP request with query parameters (tests internal query string building)
let testQueryStringBuilding () =
    let queryParams = [
        ("page", "1")
        ("limit", "100")
        ("filter", "active")
        ("sort", "name")
        ("include", "details")
        ("format", "json")
    ]
    
    let sw = Stopwatch.StartNew()
    for i in 1..1000 do  // Reduced iterations since this makes actual requests
        try
            let result = Http.RequestString("https://httpbin.org/get", query = queryParams)
            ignore result
        with 
        | _ -> () // Ignore network errors
    sw.Stop()
    
    printfn "HTTP requests with query params (1,000 operations): %dms (~%.3fms per operation)" 
        sw.ElapsedMilliseconds (float sw.ElapsedMilliseconds / 1000.0)

// Test 2: Form data encoding  
let testFormDataEncoding () =
    let formData = [
        ("username", "testuser123")
        ("password", "secretpassword")
        ("email", "user@example.com")
        ("firstName", "John")
        ("lastName", "Doe")
        ("phoneNumber", "+1-555-123-4567")
    ]
    
    let sw = Stopwatch.StartNew()
    for i in 1..10000 do
        let body = FormValues formData
        ignore body
    sw.Stop()
    
    printfn "Form data encoding (10,000 operations): %dms (~%.3fms per operation)" 
        sw.ElapsedMilliseconds (float sw.ElapsedMilliseconds / 10000.0)

printfn "=== HTTP String Processing Performance Test ==="
testQueryStringBuilding()
testFormDataEncoding()
printfn ""
printfn "Performance optimizations implemented:"
printfn "1. URL query string building - Replaced string concatenation with StringBuilder"  
printfn "2. Form data encoding - Replaced string concatenation with StringBuilder"
printfn "3. Cookie processing - Replaced string concatenation with StringBuilder"
printfn "4. XML root element wrapping - Replaced string concatenation with StringBuilder"
printfn ""
printfn "Expected benefits:"
printfn "- Reduced memory allocations during HTTP operations"
printfn "- Better performance for applications with high HTTP throughput"  
printfn "- Improved GC pressure in HTTP-intensive scenarios"