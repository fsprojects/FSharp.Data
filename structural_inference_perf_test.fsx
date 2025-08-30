// Performance test script for structural inference optimizations
#r "src/FSharp.Data.Runtime.Utilities/bin/Release/netstandard2.0/FSharp.Data.Runtime.Utilities.dll"

open System
open System.Diagnostics
open FSharp.Data.Runtime.StructuralInference
open FSharp.Data.Runtime.StructuralTypes

// Test data setup
let createSampleRecord name properties =
    InferedType.Record(
        Some name,
        properties |> List.map (fun (propName, propType, optional) ->
            { InferedProperty.Name = propName
              Type = InferedType.Primitive(propType, None, optional, false) }),
        false)

let recordType1 = createSampleRecord "UserType1" [
    ("name", typeof<string>, false)
    ("age", typeof<int>, false)
    ("email", typeof<string>, true)
    ("active", typeof<bool>, false)
]

let recordType2 = createSampleRecord "UserType2" [
    ("name", typeof<string>, false)
    ("age", typeof<string>, false)  // Different type
    ("email", typeof<string>, true)
    ("phone", typeof<string>, true) // Additional property
    ("active", typeof<bool>, false)
]

let recordType3 = createSampleRecord "UserType3" [
    ("name", typeof<string>, false)
    ("age", typeof<int>, true)       // Optional this time
    ("city", typeof<string>, true)   // Different additional property
    ("active", typeof<bool>, false)
]

// Create many record types for stress testing
let createManyRecordTypes count =
    [1..count] |> List.map (fun i ->
        createSampleRecord (sprintf "RecordType%d" i) [
            (sprintf "field1_%d" i, typeof<string>, i % 3 = 0)
            (sprintf "field2_%d" i, typeof<int>, i % 5 = 0)
            (sprintf "field3_%d" i, typeof<bool>, i % 7 = 0)
            (sprintf "common_field", typeof<string>, false)
        ])

// Performance measurement function
let measureTime desc f =
    let sw = Stopwatch.StartNew()
    let result = f()
    sw.Stop()
    printfn "%s: %dms" desc sw.ElapsedMilliseconds
    result

// Test basic structural inference performance
let testBasicInference() =
    printfn "=== Basic Structural Inference Performance ==="
    
    // Test 1: Simple record union
    measureTime "Simple record union" (fun () ->
        for _ in 1..1000 do
            subtypeInfered true recordType1 recordType2 |> ignore
    ) |> ignore
    
    // Test 2: Three-way record union
    measureTime "Three-way record union" (fun () ->
        for _ in 1..500 do
            let temp = subtypeInfered true recordType1 recordType2
            subtypeInfered true temp recordType3 |> ignore
    ) |> ignore
    
    // Test 3: Collection type inference
    let collectionTypes = [recordType1; recordType2; recordType3; recordType1; recordType2]
    measureTime "Collection type inference" (fun () ->
        for _ in 1..200 do
            inferCollectionType true collectionTypes |> ignore
    ) |> ignore

// Test with many similar records (stresses pairBy function)
let testManyRecordsInference() =
    printfn "\n=== Many Records Performance (stresses pairBy) ==="
    
    let manyRecords = createManyRecordTypes 20
    
    measureTime "Union of 20 similar records" (fun () ->
        manyRecords |> List.reduce (subtypeInfered true) |> ignore
    ) |> ignore
    
    measureTime "Collection with many record types" (fun () ->
        for _ in 1..10 do
            inferCollectionType true manyRecords |> ignore
    ) |> ignore

// Test heterogeneous type unions
let testHeterogeneousTypes() =
    printfn "\n=== Heterogeneous Type Performance ==="
    
    let primitiveTypes = [
        InferedType.Primitive(typeof<string>, None, false, false)
        InferedType.Primitive(typeof<int>, None, false, false)  
        InferedType.Primitive(typeof<float>, None, false, false)
        InferedType.Primitive(typeof<bool>, None, false, false)
        InferedType.Primitive(typeof<DateTime>, None, false, false)
    ]
    
    measureTime "Heterogeneous type union" (fun () ->
        for _ in 1..500 do
            primitiveTypes |> List.reduce (subtypeInfered true) |> ignore
    ) |> ignore
    
    // Mix primitives with records
    let mixedTypes = primitiveTypes @ [recordType1; recordType2]
    measureTime "Mixed primitives and records" (fun () ->
        for _ in 1..200 do
            mixedTypes |> List.reduce (subtypeInfered true) |> ignore
    ) |> ignore

// Main execution
let main() =
    printfn "Structural Inference Performance Test"
    printfn "====================================="
    
    testBasicInference()
    testManyRecordsInference() 
    testHeterogeneousTypes()
    
    printfn "\nPerformance test completed."

main()