module FSharp.Data.Tests.JsonRuntime

open System
open System.Globalization
open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes

#nowarn "10001"

// Helper function to create IJsonDocument for testing
let createJsonDoc jsonValue path = JsonDocument.Create(jsonValue, path)

[<Test>]
let ``ConvertString with valid JSON string`` () =
    let json = Some (JsonValue.String "test")
    let result = JsonRuntime.ConvertString("", json)
    result |> should equal (Some "test")

[<Test>] 
let ``ConvertString with null JSON`` () =
    let json = Some JsonValue.Null
    let result = JsonRuntime.ConvertString("", json)
    result |> should equal None

[<Test>]
let ``ConvertString with None`` () =
    let result = JsonRuntime.ConvertString("", None)
    result |> should equal None

[<Test>]
let ``ConvertInteger with valid JSON number`` () =
    let json = Some (JsonValue.Number 42M)
    let result = JsonRuntime.ConvertInteger("", json)
    result |> should equal (Some 42)

[<Test>]
let ``ConvertInteger with invalid JSON`` () =
    let json = Some (JsonValue.String "invalid")
    let result = JsonRuntime.ConvertInteger("", json)
    result |> should equal None

[<Test>]
let ``ConvertInteger64 with valid JSON number`` () =
    let json = Some (JsonValue.Number 9223372036854775807M)
    let result = JsonRuntime.ConvertInteger64("", json)
    result |> should equal (Some 9223372036854775807L)

[<Test>]
let ``ConvertDecimal with valid JSON number`` () =
    let json = Some (JsonValue.Number 123.45M)
    let result = JsonRuntime.ConvertDecimal("", json)
    result |> should equal (Some 123.45M)

[<Test>]
let ``ConvertFloat with valid JSON number`` () =
    let json = Some (JsonValue.Float 123.45)
    let result = JsonRuntime.ConvertFloat("", "", json)
    result |> should equal (Some 123.45)

[<Test>]
let ``ConvertBoolean with valid JSON boolean`` () =
    let json = Some (JsonValue.Boolean true)
    let result = JsonRuntime.ConvertBoolean(json)
    result |> should equal (Some true)

[<Test>]
let ``ConvertDateTimeOffset with valid JSON date`` () =
    let dateStr = "2023-12-25T10:30:00+02:00"
    let json = Some (JsonValue.String dateStr)
    let result = JsonRuntime.ConvertDateTimeOffset("", json)
    result.IsSome |> should be True

[<Test>]
let ``ConvertDateTime with valid JSON date`` () =
    let dateStr = "2023-12-25T10:30:00"
    let json = Some (JsonValue.String dateStr)
    let result = JsonRuntime.ConvertDateTime("", json)
    result.IsSome |> should be True

[<Test>]
let ``ConvertTimeSpan with valid JSON timespan`` () =
    let timeStr = "1.02:03:04"
    let json = Some (JsonValue.String timeStr)
    let result = JsonRuntime.ConvertTimeSpan("", json)
    result.IsSome |> should be True

[<Test>]
let ``ConvertGuid with valid JSON guid`` () =
    let guidStr = "12345678-1234-1234-1234-123456789012"
    let json = Some (JsonValue.String guidStr)
    let result = JsonRuntime.ConvertGuid(json)
    result |> should equal (Some (Guid.Parse(guidStr)))

[<Test>]
let ``GetNonOptionalValue returns value when present`` () =
    let result = JsonRuntime.GetNonOptionalValue<string>("/test", Some "value", None)
    result |> should equal "value"

[<Test>]
let ``GetNonOptionalValue returns empty string for missing string`` () =
    let result = JsonRuntime.GetNonOptionalValue<string>("/test", None, None)
    result |> should equal ""

[<Test>]
let ``GetNonOptionalValue returns NaN for missing float`` () =
    let result = JsonRuntime.GetNonOptionalValue<float>("/test", None, None)
    Double.IsNaN(result) |> should be True

[<Test>]
let ``GetNonOptionalValue throws for missing required type`` () =
    (fun () -> JsonRuntime.GetNonOptionalValue<int>("/test", None, None) |> ignore)
    |> should throw typeof<System.Exception>

[<Test>]
let ``ConvertArray with valid JSON array`` () =
    let json = JsonValue.Array [| JsonValue.String "a"; JsonValue.String "b" |]
    let doc = createJsonDoc json "/array"
    let mapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with 
        | JsonValue.String s -> s 
        | _ -> "")
    let result = JsonRuntime.ConvertArray(doc, mapping)
    result |> should equal [| "a"; "b" |]

[<Test>]
let ``ConvertArray filters out null values`` () =
    let json = JsonValue.Array [| JsonValue.String "a"; JsonValue.Null; JsonValue.String "b" |]
    let doc = createJsonDoc json "/array"
    let mapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with 
        | JsonValue.String s -> s 
        | _ -> "")
    let result = JsonRuntime.ConvertArray(doc, mapping)
    result |> should equal [| "a"; "b" |]

[<Test>]
let ``ConvertArray with null JSON returns empty array`` () =
    let doc = createJsonDoc JsonValue.Null "/array"
    let mapping = Func<IJsonDocument, string>(fun _ -> "")
    let result = JsonRuntime.ConvertArray(doc, mapping)
    result |> should equal [||]

[<Test>]
let ``ConvertArray throws on non-array JSON`` () =
    let doc = createJsonDoc (JsonValue.String "not-array") "/test"
    let mapping = Func<IJsonDocument, string>(fun _ -> "")
    (fun () -> JsonRuntime.ConvertArray(doc, mapping) |> ignore)
    |> should throw typeof<System.Exception>

[<Test>]
let ``GetRecordProperties with valid JSON record`` () =
    let json = JsonValue.Record [| ("key1", JsonValue.String "value1"); ("key2", JsonValue.Number 42M) |]
    let doc = createJsonDoc json "/record"
    let result = JsonRuntime.GetRecordProperties(doc)
    result.Length |> should equal 2
    result.[0] |> should equal ("key1", JsonValue.String "value1")
    result.[1] |> should equal ("key2", JsonValue.Number 42M)

[<Test>]
let ``GetRecordProperties with null JSON returns empty array`` () =
    let doc = createJsonDoc JsonValue.Null "/record"
    let result = JsonRuntime.GetRecordProperties(doc)
    result |> should equal [||]

[<Test>]
let ``GetRecordProperties throws on non-record JSON`` () =
    let doc = createJsonDoc (JsonValue.String "not-record") "/test"
    (fun () -> JsonRuntime.GetRecordProperties(doc) |> ignore)
    |> should throw typeof<System.Exception>

[<Test>]
let ``ConvertRecordToDictionary converts record to key-value pairs`` () =
    let json = JsonValue.Record [| ("key1", JsonValue.String "value1"); ("key2", JsonValue.Number 42M) |]
    let doc = createJsonDoc json "/record"
    let keyMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let valueMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with 
        | JsonValue.String s -> s 
        | JsonValue.Number n -> n.ToString() 
        | _ -> "")
    let result = JsonRuntime.ConvertRecordToDictionary(doc, keyMapping, valueMapping) |> Seq.toArray
    result.Length |> should equal 2
    result |> should contain ("key1", "value1")
    result |> should contain ("key2", "42")

[<Test>]
let ``InferedDictionaryContainsKey returns true for existing key`` () =
    let json = JsonValue.Record [| ("key1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/record"
    let keyMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let result = JsonRuntime.InferedDictionaryContainsKey(doc, keyMapping, "key1")
    result |> should be True

[<Test>]
let ``InferedDictionaryContainsKey returns false for missing key`` () =
    let json = JsonValue.Record [| ("key1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/record"
    let keyMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let result = JsonRuntime.InferedDictionaryContainsKey(doc, keyMapping, "key2")
    result |> should be False

[<Test>]
let ``TryGetValueByKeyFromInferedDictionary returns Some for existing key`` () =
    let json = JsonValue.Record [| ("key1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/record"
    let keyMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let valueMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let result = JsonRuntime.TryGetValueByKeyFromInferedDictionary(doc, keyMapping, valueMapping, "key1")
    result |> should equal (Some "value1")

[<Test>]
let ``TryGetValueByKeyFromInferedDictionary returns None for missing key`` () =
    let json = JsonValue.Record [| ("key1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/record"
    let keyMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let valueMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let result = JsonRuntime.TryGetValueByKeyFromInferedDictionary(doc, keyMapping, valueMapping, "key2")
    result |> should equal None

[<Test>]
let ``GetValueByKeyFromInferedDictionary returns value for existing key`` () =
    let json = JsonValue.Record [| ("key1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/record"
    let keyMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let valueMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let result = JsonRuntime.GetValueByKeyFromInferedDictionary(doc, keyMapping, valueMapping, "key1")
    result |> should equal "value1"

[<Test>]
let ``GetValueByKeyFromInferedDictionary throws for missing key`` () =
    let json = JsonValue.Record [| ("key1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/record"
    let keyMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let valueMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    (fun () -> JsonRuntime.GetValueByKeyFromInferedDictionary(doc, keyMapping, valueMapping, "key2") |> ignore)
    |> should throw typeof<System.Collections.Generic.KeyNotFoundException>

[<Test>]
let ``GetKeysFromInferedDictionary returns all keys`` () =
    let json = JsonValue.Record [| ("key1", JsonValue.String "value1"); ("key2", JsonValue.Number 42M) |]
    let doc = createJsonDoc json "/record"
    let keyMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let result = JsonRuntime.GetKeysFromInferedDictionary(doc, keyMapping)
    result |> should equal [| "key1"; "key2" |]

[<Test>]
let ``GetValuesFromInferedDictionary returns all values`` () =
    let json = JsonValue.Record [| ("key1", JsonValue.String "value1"); ("key2", JsonValue.Number 42M) |]
    let doc = createJsonDoc json "/record"
    let valueMapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with 
        | JsonValue.String s -> s 
        | JsonValue.Number n -> n.ToString() 
        | _ -> "")
    let result = JsonRuntime.GetValuesFromInferedDictionary(doc, valueMapping)
    result |> should equal [| "value1"; "42" |]

[<Test>]
let ``TryGetPropertyUnpacked returns Some for existing property`` () =
    let json = JsonValue.Record [| ("prop1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/test"
    let result = JsonRuntime.TryGetPropertyUnpacked(doc, "prop1")
    result |> should equal (Some (JsonValue.String "value1"))

[<Test>]
let ``TryGetPropertyUnpacked returns None for missing property`` () =
    let json = JsonValue.Record [| ("prop1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/test"
    let result = JsonRuntime.TryGetPropertyUnpacked(doc, "prop2")
    result |> should equal None

[<Test>]
let ``TryGetPropertyUnpacked returns None for null property`` () =
    let json = JsonValue.Record [| ("prop1", JsonValue.Null) |]
    let doc = createJsonDoc json "/test"
    let result = JsonRuntime.TryGetPropertyUnpacked(doc, "prop1")
    result |> should equal None

[<Test>]
let ``TryGetPropertyUnpacked returns None for empty string property`` () =
    let json = JsonValue.Record [| ("prop1", JsonValue.String "") |]
    let doc = createJsonDoc json "/test"
    let result = JsonRuntime.TryGetPropertyUnpacked(doc, "prop1")
    result |> should equal None

[<Test>]
let ``TryGetPropertyUnpackedWithPath returns correct path`` () =
    let json = JsonValue.Record [| ("prop1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/test"
    let result = JsonRuntime.TryGetPropertyUnpackedWithPath(doc, "prop1")
    result.JsonOpt |> should equal (Some (JsonValue.String "value1"))
    result.Path |> should equal "/test/prop1"

[<Test>]
let ``TryGetPropertyPacked returns Some document for existing property`` () =
    let json = JsonValue.Record [| ("prop1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/test"
    let result = JsonRuntime.TryGetPropertyPacked(doc, "prop1")
    result.IsSome |> should be True
    result.Value.JsonValue |> should equal (JsonValue.String "value1")

[<Test>]
let ``GetPropertyPacked returns document for existing property`` () =
    let json = JsonValue.Record [| ("prop1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/test"
    let result = JsonRuntime.GetPropertyPacked(doc, "prop1")
    result.JsonValue |> should equal (JsonValue.String "value1")

[<Test>]
let ``GetPropertyPacked throws for missing property`` () =
    let json = JsonValue.Record [| ("prop1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/test"
    (fun () -> JsonRuntime.GetPropertyPacked(doc, "prop2") |> ignore)
    |> should throw typeof<System.Exception>

[<Test>]
let ``GetPropertyPackedOrNull returns document for existing property`` () =
    let json = JsonValue.Record [| ("prop1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/test"
    let result = JsonRuntime.GetPropertyPackedOrNull(doc, "prop1")
    result.JsonValue |> should equal (JsonValue.String "value1")

[<Test>]
let ``GetPropertyPackedOrNull returns null document for missing property`` () =
    let json = JsonValue.Record [| ("prop1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/test"
    let result = JsonRuntime.GetPropertyPackedOrNull(doc, "prop2")
    result.JsonValue |> should equal JsonValue.Null

[<Test>]
let ``ConvertOptionalProperty returns Some for existing property`` () =
    let json = JsonValue.Record [| ("prop1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/test"
    let mapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let result = JsonRuntime.ConvertOptionalProperty(doc, "prop1", mapping)
    result |> should equal (Some "value1")

[<Test>]
let ``ConvertOptionalProperty returns None for missing property`` () =
    let json = JsonValue.Record [| ("prop1", JsonValue.String "value1") |]
    let doc = createJsonDoc json "/test"
    let mapping = Func<IJsonDocument, string>(fun d -> 
        match d.JsonValue with JsonValue.String s -> s | _ -> "")
    let result = JsonRuntime.ConvertOptionalProperty(doc, "prop2", mapping)
    result |> should equal None

[<Test>]
let ``CreateValue creates document with scalar value`` () =
    let result = JsonRuntime.CreateValue("test string", "")
    result.JsonValue |> should equal (JsonValue.String "test string")

[<Test>]
let ``CreateValue creates document with number value`` () =
    let result = JsonRuntime.CreateValue(42, "")
    result.JsonValue |> should equal (JsonValue.Number 42M)

[<Test>]
let ``CreateValue creates document with boolean value`` () =
    let result = JsonRuntime.CreateValue(true, "")
    result.JsonValue |> should equal (JsonValue.Boolean true)

[<Test>]
let ``CreateRecord creates document with record value`` () =
    let properties = [| ("key1", "value1" :> obj); ("key2", 42 :> obj) |]
    let result = JsonRuntime.CreateRecord(properties, "")
    match result.JsonValue with
    | JsonValue.Record items ->
        items.Length |> should equal 2
        items |> should contain ("key1", JsonValue.String "value1")
        items |> should contain ("key2", JsonValue.Number 42M)
    | _ -> failwith "Expected Record"

[<Test>]
let ``CreateRecordFromDictionary creates document from key-value pairs`` () =
    let keyValuePairs = [("key1", "value1"); ("key2", "value2")]
    let mappingKeyBack = Func<string, string>(id)
    let result = JsonRuntime.CreateRecordFromDictionary(keyValuePairs, "", mappingKeyBack)
    match result.JsonValue with
    | JsonValue.Record items ->
        items.Length |> should equal 2
        items |> should contain ("key1", JsonValue.String "value1")
        items |> should contain ("key2", JsonValue.String "value2")
    | _ -> failwith "Expected Record"

[<Test>]
let ``CreateArray creates document with array value`` () =
    let elements = [| "item1" :> obj; "item2" :> obj |]
    let result = JsonRuntime.CreateArray(elements, "")
    match result.JsonValue with
    | JsonValue.Array items ->
        items.Length |> should equal 2
        items.[0] |> should equal (JsonValue.String "item1")
        items.[1] |> should equal (JsonValue.String "item2")
    | _ -> failwith "Expected Array"

[<Test>]
let ``CreateArray flattens nested arrays`` () =
    let nestedArray = JsonValue.Array [| JsonValue.String "nested1"; JsonValue.String "nested2" |]
    let elements = [| nestedArray :> obj; "item3" :> obj |]
    let result = JsonRuntime.CreateArray(elements, "")
    match result.JsonValue with
    | JsonValue.Array items ->
        items.Length |> should equal 3
        items.[0] |> should equal (JsonValue.String "nested1")
        items.[1] |> should equal (JsonValue.String "nested2")
        items.[2] |> should equal (JsonValue.String "item3")
    | _ -> failwith "Expected Array"

[<Test>]
let ``CreateArray filters out null values`` () =
    let elements = [| JsonValue.Null :> obj; "item1" :> obj |]
    let result = JsonRuntime.CreateArray(elements, "")
    match result.JsonValue with
    | JsonValue.Array items ->
        items.Length |> should equal 1
        items.[0] |> should equal (JsonValue.String "item1")
    | _ -> failwith "Expected Array"