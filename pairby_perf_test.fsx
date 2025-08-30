// Performance test for pairBy optimization
#r "src/FSharp.Data.Runtime.Utilities/bin/Release/netstandard2.0/FSharp.Data.Runtime.Utilities.dll"

open System
open System.Diagnostics
open FSharp.Data.Runtime.StructuralInference

// Test data generation
let generateTestData size =
    let firstSeq = [1..size] |> List.map (fun i -> sprintf "key%d" (i % (size/2)), sprintf "value1_%d" i)
    let secondSeq = [1..size] |> List.map (fun i -> sprintf "key%d" (i % (size/3)), sprintf "value2_%d" i)
    firstSeq, secondSeq

let measureTime description iterations f =
    // Warmup
    for _ in 1..min 100 iterations do f() |> ignore
    
    let sw = Stopwatch.StartNew()
    for _ in 1..iterations do 
        f() |> ignore
    sw.Stop()
    
    let avgMs = float sw.ElapsedMilliseconds / float iterations
    printfn "%s: %.3f ms per iteration (total: %d ms for %d iterations)" 
        description avgMs sw.ElapsedMilliseconds iterations

let testPairByPerformance() =
    printfn "PairBy Performance Optimization Test"
    printfn "===================================="
    printfn ""
    
    // Test different sizes
    let testSizes = [10; 50; 100; 500; 1000]
    
    for size in testSizes do
        let first, second = generateTestData size
        
        printfn "Testing with %d elements..." size
        
        measureTime (sprintf "PairBy with %d elements" size) 1000 (fun () ->
            List.pairBy fst first second
        )
        
        printfn ""

// Test correctness
let testCorrectness() =
    printfn "Testing correctness..."
    
    let first = [(1, "a"); (2, "b"); (3, "c")]
    let second = [(1, "A"); (3, "C"); (4, "D")]
    
    let result = List.pairBy fst first second
    let expected = [(1, Some (1, "a"), Some (1, "A")); (2, Some (2, "b"), None); (3, Some (3, "c"), Some (3, "C")); (4, None, Some (4, "D"))]
    
    if Set.ofList result = Set.ofList expected then
        printfn "✅ Correctness test passed"
    else
        printfn "❌ Correctness test failed!"
        printfn "Expected: %A" expected
        printfn "Got:      %A" result

testCorrectness()
testPairByPerformance()