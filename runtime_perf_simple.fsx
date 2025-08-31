open System
open System.IO
open System.Text
open System.Diagnostics

// Simulating the old vs new approach for logging
let oldLogString (str: string) (indentation: int) =
    "[" + DateTime.Now.TimeOfDay.ToString() + "] " + String(' ', indentation * 2) + str

let newLogString (str: string) (indentation: int) =
    let sb = StringBuilder()
    sb.Append("[") |> ignore
    sb.Append(DateTime.Now.TimeOfDay.ToString()) |> ignore
    sb.Append("] ") |> ignore
    sb.Append(String(' ', indentation * 2)) |> ignore
    sb.Append(str) |> ignore
    sb.ToString()

// Simulating cache file path creation
let oldCacheFile (encoded: string) =
    encoded + ".txt"

let newCacheFile (encoded: string) =
    let sb = StringBuilder(encoded.Length + 4)
    sb.Append(encoded) |> ignore
    sb.Append(".txt") |> ignore
    sb.ToString()

// Simulating JsonDocument path creation
let oldJsonPath (i: int) =
    "[" + (string i) + "]"

let newJsonPath (i: int) =
    let indexStr = string i
    let sb = StringBuilder(1 + indexStr.Length + 1)
    sb.Append("[") |> ignore
    sb.Append(indexStr) |> ignore  
    sb.Append("]") |> ignore
    sb.ToString()

// Performance tests
let runTests () =
    let iterations = 10000
    let testData = "Test log message here"
    let encodedData = "a1b2c3d4e5f6g7h8i9j0"
    
    printfn "Runtime Utilities String Processing Performance Test"
    printfn "================================================="
    printfn ""
    
    // Test 1: Log string building
    let sw = Stopwatch.StartNew()
    for i in 1..iterations do
        oldLogString testData 3 |> ignore
    sw.Stop()
    let oldLogTime = sw.Elapsed.TotalMilliseconds
    
    sw.Restart()
    for i in 1..iterations do
        newLogString testData 3 |> ignore
    sw.Stop()
    let newLogTime = sw.Elapsed.TotalMilliseconds
    
    printfn "Log String Building (%d iterations):" iterations
    printfn "  Old approach (string concatenation): %.2fms (%.4fms avg)" oldLogTime (oldLogTime / float iterations)
    printfn "  New approach (StringBuilder): %.2fms (%.4fms avg)" newLogTime (newLogTime / float iterations)
    printfn "  Improvement: %.1fx faster" (oldLogTime / newLogTime)
    printfn ""
    
    // Test 2: Cache file path building
    sw.Restart()
    for i in 1..iterations do
        oldCacheFile encodedData |> ignore
    sw.Stop()
    let oldCacheTime = sw.Elapsed.TotalMilliseconds
    
    sw.Restart()
    for i in 1..iterations do
        newCacheFile encodedData |> ignore
    sw.Stop()
    let newCacheTime = sw.Elapsed.TotalMilliseconds
    
    printfn "Cache File Path Building (%d iterations):" iterations
    printfn "  Old approach (string concatenation): %.2fms (%.4fms avg)" oldCacheTime (oldCacheTime / float iterations)
    printfn "  New approach (StringBuilder): %.2fms (%.4fms avg)" newCacheTime (newCacheTime / float iterations)
    printfn "  Improvement: %.1fx faster" (oldCacheTime / newCacheTime)
    printfn ""
    
    // Test 3: JSON path building
    sw.Restart()
    for i in 1..iterations do
        oldJsonPath i |> ignore
    sw.Stop()
    let oldJsonTime = sw.Elapsed.TotalMilliseconds
    
    sw.Restart()
    for i in 1..iterations do
        newJsonPath i |> ignore
    sw.Stop()
    let newJsonTime = sw.Elapsed.TotalMilliseconds
    
    printfn "JSON Path Building (%d iterations):" iterations
    printfn "  Old approach (string concatenation): %.2fms (%.4fms avg)" oldJsonTime (oldJsonTime / float iterations)
    printfn "  New approach (StringBuilder): %.2fms (%.4fms avg)" newJsonTime (newJsonTime / float iterations)
    printfn "  Improvement: %.1fx faster" (oldJsonTime / newJsonTime)
    printfn ""
    
    // Validate output correctness
    let oldResult = oldLogString testData 3
    let newResult = newLogString testData 3
    let resultsMatch = oldResult = newResult
    
    printfn "Correctness Validation:"
    printfn "  Results match: %b" resultsMatch
    if not resultsMatch then
        printfn "  Old: %s" oldResult
        printfn "  New: %s" newResult

runTests()