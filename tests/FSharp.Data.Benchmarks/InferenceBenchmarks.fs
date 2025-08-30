namespace FSharp.Data.Benchmarks

open BenchmarkDotNet.Attributes
open FSharp.Data.Runtime.StructuralInference
open FSharp.Data.Runtime.StructuralTypes

[<MemoryDiagnoser>]
[<SimpleJob>]
type InferenceBenchmarks() =

    let sampleJson1 = """{"name":"John","age":30,"active":true}"""
    let sampleJson2 = """{"name":"Jane","age":25,"active":false,"email":"jane@example.com"}"""
    let sampleJson3 = """{"name":"Bob","age":"unknown","active":true,"city":"NY"}"""
    
    let complexJson = """
    {
        "users": [
            {"id": 1, "name": "Alice", "email": "alice@test.com", "age": 30, "active": true},
            {"id": 2, "name": "Bob", "email": "bob@test.com", "age": 25, "active": false},
            {"id": 3, "name": "Charlie", "age": 35, "active": true, "phone": "555-1234"}
        ],
        "metadata": {
            "total": 3,
            "page": 1,
            "per_page": 10
        }
    }"""

    let largeJsonArray = 
        let items = [1..100] |> List.map (fun i -> 
            sprintf """{"id":%d,"name":"User%d","score":%.2f,"active":%b,"tags":["%s","%s"]}"""
                i i (float i * 1.23) (i % 2 = 0) (sprintf "tag%d" i) (sprintf "category%d" (i % 5)))
        "[" + String.concat "," items + "]"

    // Sample record types for testing structural inference
    let recordType1 = InferedType.Record(
        None,
        [ { InferedProperty.Name = "name"; Type = InferedType.Primitive(typeof<string>, None, false, false) }
          { InferedProperty.Name = "age"; Type = InferedType.Primitive(typeof<int>, None, false, false) }
          { InferedProperty.Name = "active"; Type = InferedType.Primitive(typeof<bool>, None, false, false) } ],
        false)

    let recordType2 = InferedType.Record(
        None,
        [ { InferedProperty.Name = "name"; Type = InferedType.Primitive(typeof<string>, None, false, false) }
          { InferedProperty.Name = "age"; Type = InferedType.Primitive(typeof<string>, None, false, false) }
          { InferedProperty.Name = "active"; Type = InferedType.Primitive(typeof<bool>, None, false, false) }
          { InferedProperty.Name = "email"; Type = InferedType.Primitive(typeof<string>, None, true, false) } ],
        false)

    let recordType3 = InferedType.Record(
        None,
        [ { InferedProperty.Name = "name"; Type = InferedType.Primitive(typeof<string>, None, false, false) }
          { InferedProperty.Name = "age"; Type = InferedType.Primitive(typeof<int>, None, true, false) }
          { InferedProperty.Name = "city"; Type = InferedType.Primitive(typeof<string>, None, true, false) } ],
        false)

    [<Benchmark>]
    member this.SubtypeInferenceTwoRecords() =
        subtypeInfered true recordType1 recordType2

    [<Benchmark>]
    member this.SubtypeInferenceThreeRecords() =
        let temp = subtypeInfered true recordType1 recordType2
        subtypeInfered true temp recordType3

    [<Benchmark>]
    member this.InferCollectionTypeMixed() =
        let types = [ recordType1; recordType2; recordType3; recordType1; recordType2 ]
        inferCollectionType true types

    [<Benchmark>]
    member this.HeterogeneousTypeUnion() =
        let stringType = InferedType.Primitive(typeof<string>, None, false, false)
        let intType = InferedType.Primitive(typeof<int>, None, false, false)
        let boolType = InferedType.Primitive(typeof<bool>, None, false, false)
        subtypeInfered true (subtypeInfered true stringType intType) boolType

    [<Benchmark>]
    member this.LargeRecordUnion() =
        // Simulate merging records with many properties
        let makeLargeRecord suffix = 
            InferedType.Record(
                None,
                [ for i in 1..20 ->
                    { InferedProperty.Name = sprintf "field%d%s" i suffix
                      Type = if i % 3 = 0 then InferedType.Primitive(typeof<string>, None, i % 7 = 0, false)
                             elif i % 3 = 1 then InferedType.Primitive(typeof<int>, None, i % 5 = 0, false)
                             else InferedType.Primitive(typeof<bool>, None, i % 11 = 0, false) } ],
                false)
        
        let record1 = makeLargeRecord "A"
        let record2 = makeLargeRecord "B"
        subtypeInfered true record1 record2

    [<Benchmark>]
    member this.DeepNestedStructure() =
        // Simulate deeply nested record structures
        let rec makeNestedRecord depth =
            if depth <= 0 then
                InferedType.Primitive(typeof<string>, None, false, false)
            else
                InferedType.Record(
                    None,
                    [ { InferedProperty.Name = sprintf "level%d" depth
                        Type = makeNestedRecord (depth - 1) }
                      { InferedProperty.Name = sprintf "data%d" depth
                        Type = InferedType.Primitive(typeof<int>, None, false, false) } ],
                    false)
        
        let nested1 = makeNestedRecord 5
        let nested2 = makeNestedRecord 5
        subtypeInfered true nested1 nested2

    [<Benchmark>]
    member this.ManyHeterogeneousTypes() =
        // Test performance with many heterogeneous type combinations
        let types = [
            InferedType.Primitive(typeof<string>, None, false, false)
            InferedType.Primitive(typeof<int>, None, false, false)
            InferedType.Primitive(typeof<float>, None, false, false)
            InferedType.Primitive(typeof<bool>, None, false, false)
            InferedType.Primitive(typeof<System.DateTime>, None, false, false)
        ]
        
        types |> List.reduce (subtypeInfered true)

    [<Benchmark>]
    member this.CollectionWithManyTypes() =
        // Test collection inference with diverse types
        let collectionTypes = [
            recordType1; recordType2; recordType3
            InferedType.Primitive(typeof<string>, None, false, false)
            InferedType.Primitive(typeof<int>, None, false, false)
            recordType1; recordType2
        ]
        
        inferCollectionType true collectionTypes
