module FSharp.Data.Tests.JsonRuntime

open NUnit.Framework
open FsUnit
open System
open System.Globalization
open System.IO
open System.Reflection
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes

[<Test>]
let ``ConvertString with Some JsonValue String should return the string value`` () =
    let jsonValue = Some (JsonValue.String "hello")
    let result = JsonRuntime.ConvertString("", jsonValue)
    result |> should equal (Some "hello")

[<Test>] 
let ``ConvertString with None should return None`` () =
    let result = JsonRuntime.ConvertString("", None)
    result |> should equal None

[<Test>]
let ``ConvertString with JsonValue Number should return string representation`` () =
    let jsonValue = Some (JsonValue.Number 42m)
    let result = JsonRuntime.ConvertString("", jsonValue)
    result |> should equal (Some "42")

[<Test>]
let ``ConvertInteger with valid integer JsonValue should return the integer`` () =
    let jsonValue = Some (JsonValue.Number 42m)
    let result = JsonRuntime.ConvertInteger("", jsonValue)
    result |> should equal (Some 42)

[<Test>]
let ``ConvertInteger with None should return None`` () =
    let result = JsonRuntime.ConvertInteger("", None)
    result |> should equal None

[<Test>]
let ``ConvertInteger with string number should return the integer`` () =
    let jsonValue = Some (JsonValue.String "123")
    let result = JsonRuntime.ConvertInteger("", jsonValue)
    result |> should equal (Some 123)

[<Test>]
let ``ConvertInteger64 with valid integer JsonValue should return the long`` () =
    let jsonValue = Some (JsonValue.Number 9223372036854775807m)
    let result = JsonRuntime.ConvertInteger64("", jsonValue)
    result |> should equal (Some 9223372036854775807L)

[<Test>]
let ``ConvertInteger64 with None should return None`` () =
    let result = JsonRuntime.ConvertInteger64("", None)
    result |> should equal None

[<Test>]
let ``ConvertDecimal with valid decimal JsonValue should return the decimal`` () =
    let jsonValue = Some (JsonValue.Number 123.456m)
    let result = JsonRuntime.ConvertDecimal("", jsonValue)
    result |> should equal (Some 123.456m)

[<Test>]
let ``ConvertDecimal with None should return None`` () =
    let result = JsonRuntime.ConvertDecimal("", None)
    result |> should equal None

[<Test>]
let ``ConvertFloat with valid float JsonValue should return the float`` () =
    let jsonValue = Some (JsonValue.Number 123.456m)
    let result = JsonRuntime.ConvertFloat("", "", jsonValue)
    result |> should equal (Some 123.456)

[<Test>]
let ``ConvertFloat with None should return None`` () =
    let result = JsonRuntime.ConvertFloat("", "", None)
    result |> should equal None

[<Test>]
let ``ConvertBoolean with JsonValue Boolean true should return Some true`` () =
    let jsonValue = Some (JsonValue.Boolean true)
    let result = JsonRuntime.ConvertBoolean jsonValue
    result |> should equal (Some true)

[<Test>]
let ``ConvertBoolean with JsonValue Boolean false should return Some false`` () =
    let jsonValue = Some (JsonValue.Boolean false)
    let result = JsonRuntime.ConvertBoolean jsonValue
    result |> should equal (Some false)

[<Test>]
let ``ConvertBoolean with None should return None`` () =
    let result = JsonRuntime.ConvertBoolean None
    result |> should equal None

[<Test>]
let ``ConvertDateTime with valid ISO date should return the DateTime`` () =
    let jsonValue = Some (JsonValue.String "2023-12-25T10:30:00")
    let result = JsonRuntime.ConvertDateTime("", jsonValue)
    result.IsSome |> should equal true

[<Test>]
let ``ConvertDateTime with None should return None`` () =
    let result = JsonRuntime.ConvertDateTime("", None)
    result |> should equal None

[<Test>]
let ``ConvertDateTimeOffset with valid ISO date should return the DateTimeOffset`` () =
    let jsonValue = Some (JsonValue.String "2023-12-25T10:30:00+02:00")
    let result = JsonRuntime.ConvertDateTimeOffset("", jsonValue)
    result.IsSome |> should equal true

[<Test>]
let ``ConvertDateTimeOffset with None should return None`` () =
    let result = JsonRuntime.ConvertDateTimeOffset("", None)
    result |> should equal None

[<Test>]
let ``ConvertTimeSpan with valid timespan string should return the TimeSpan`` () =
    let jsonValue = Some (JsonValue.String "02:30:45")
    let result = JsonRuntime.ConvertTimeSpan("", jsonValue)
    result |> should equal (Some (TimeSpan(2, 30, 45)))

[<Test>]
let ``ConvertTimeSpan with None should return None`` () =
    let result = JsonRuntime.ConvertTimeSpan("", None)
    result |> should equal None

[<Test>]
let ``ConvertGuid with valid guid string should return the Guid`` () =
    let guidStr = "550e8400-e29b-41d4-a716-446655440000"
    let jsonValue = Some (JsonValue.String guidStr)
    let result = JsonRuntime.ConvertGuid jsonValue
    result |> should equal (Some (Guid.Parse guidStr))

[<Test>]
let ``ConvertGuid with None should return None`` () =
    let result = JsonRuntime.ConvertGuid None
    result |> should equal None

[<Test>]
let ``GetNonOptionalValue with Some value should return the value`` () =
    let value = Some "test"
    let result = JsonRuntime.GetNonOptionalValue<string>("/path", value, None)
    result |> should equal "test"

[<Test>]
let ``GetNonOptionalValue with None for string should return empty string`` () =
    let result = JsonRuntime.GetNonOptionalValue<string>("/path", None, None)
    result |> should equal ""

[<Test>]
let ``GetNonOptionalValue with None for float should return NaN`` () =
    let result = JsonRuntime.GetNonOptionalValue<float>("/path", None, None)
    result |> should equal Double.NaN

[<Test>]
let ``GetNonOptionalValue with None for other types should throw exception`` () =
    (fun () -> JsonRuntime.GetNonOptionalValue<int>("/path", None, None) |> ignore)
    |> should throw typeof<System.Exception>

[<Test>]
let ``GetNonOptionalValue with None and JsonValue Array should throw meaningful exception`` () =
    let arrayValue = Some (JsonValue.Array [| JsonValue.String "test" |])
    (fun () -> JsonRuntime.GetNonOptionalValue<int>("/path", None, arrayValue) |> ignore)
    |> should throw typeof<System.Exception>

[<Test>]
let ``CreateValue should create JsonDocument from simple string value`` () =
    let result = JsonRuntime.CreateValue("hello", "")
    result.JsonValue |> should equal (JsonValue.String "hello")

[<Test>]
let ``CreateValue should create JsonDocument from integer value`` () =
    let result = JsonRuntime.CreateValue(42, "")
    result.JsonValue |> should equal (JsonValue.Number 42m)

[<Test>]
let ``CreateValue should create JsonDocument from boolean value`` () =
    let result = JsonRuntime.CreateValue(true, "")
    result.JsonValue |> should equal (JsonValue.Boolean true)

[<Test>]
let ``CreateValue should create JsonDocument from DateTime value`` () =
    let dateTime = DateTime(2023, 12, 25, 10, 30, 0)
    let result = JsonRuntime.CreateValue(dateTime, "")
    match result.JsonValue with
    | JsonValue.String s -> s |> should startWith "2023-12-25T10:30:00"
    | _ -> failwith "Expected JsonValue.String"

[<Test>]
let ``CreateValue should create JsonDocument from null value`` () =
    let result = JsonRuntime.CreateValue(null, "")
    result.JsonValue |> should equal JsonValue.Null

[<Test>]
let ``CreateValue should create JsonDocument from Guid value`` () =
    let guid = Guid.NewGuid()
    let result = JsonRuntime.CreateValue(guid, "")
    match result.JsonValue with
    | JsonValue.String s -> Guid.Parse(s) |> should equal guid
    | _ -> failwith "Expected JsonValue.String"

[<Test>]
let ``CreateValue should create JsonDocument from TimeSpan value`` () =
    let timeSpan = TimeSpan(2, 30, 45)
    let result = JsonRuntime.CreateValue(timeSpan, "")
    match result.JsonValue with
    | JsonValue.String s -> s |> should equal "2:30:45"
    | _ -> failwith "Expected JsonValue.String"

[<Test>]
let ``CreateValue should create JsonDocument from DateTimeOffset value`` () =
    let dateTimeOffset = DateTimeOffset(2023, 12, 25, 10, 30, 0, TimeSpan(2, 0, 0))
    let result = JsonRuntime.CreateValue(dateTimeOffset, "")
    match result.JsonValue with
    | JsonValue.String s -> s |> should contain "2023-12-25T10:30:00"
    | _ -> failwith "Expected JsonValue.String"

[<Test>]
let ``CreateValue should create JsonDocument from array`` () =
    let array = [| "hello"; "world" |]
    let result = JsonRuntime.CreateValue(array, "")
    match result.JsonValue with
    | JsonValue.Array elements ->
        elements.[0] |> should equal (JsonValue.String "hello")
        elements.[1] |> should equal (JsonValue.String "world")
    | _ -> failwith "Expected JsonValue.Array"

[<Test>]
let ``CreateRecord should create JsonDocument from properties array`` () =
    let properties = [| ("name", "John" :> obj); ("age", 30 :> obj) |]
    let result = JsonRuntime.CreateRecord(properties, "")
    
    match result.JsonValue with
    | JsonValue.Record props -> 
        props |> should contain ("name", JsonValue.String "John")
        props |> should contain ("age", JsonValue.Number 30m)
    | _ -> failwith "Expected JsonValue.Record"

[<Test>]
let ``CreateRecord should handle empty properties array`` () =
    let properties = [||]
    let result = JsonRuntime.CreateRecord(properties, "")
    
    match result.JsonValue with
    | JsonValue.Record props -> props |> should equal [||]
    | _ -> failwith "Expected JsonValue.Record"

[<Test>]
let ``CreateArray should create JsonDocument from object array`` () =
    let elements = [| "hello" :> obj; 42 :> obj; true :> obj |]
    let result = JsonRuntime.CreateArray(elements, "")
    
    match result.JsonValue with
    | JsonValue.Array elems ->
        elems |> should contain (JsonValue.String "hello")
        elems |> should contain (JsonValue.Number 42m)
        elems |> should contain (JsonValue.Boolean true)
    | _ -> failwith "Expected JsonValue.Array"

[<Test>]
let ``CreateArray should handle empty array`` () =
    let elements = [||]
    let result = JsonRuntime.CreateArray(elements, "")
    
    match result.JsonValue with
    | JsonValue.Array elems -> elems |> should equal [||]
    | _ -> failwith "Expected JsonValue.Array"

[<Test>]
let ``CreateArray should flatten nested arrays`` () =
    let nestedArray = [| JsonValue.String "a"; JsonValue.String "b" |] |> JsonValue.Array
    let elements = [| nestedArray :> obj; "c" :> obj |]
    let result = JsonRuntime.CreateArray(elements, "")
    
    match result.JsonValue with
    | JsonValue.Array elems ->
        elems.Length |> should equal 3
        elems.[0] |> should equal (JsonValue.String "a")
        elems.[1] |> should equal (JsonValue.String "b")
        elems.[2] |> should equal (JsonValue.String "c")
    | _ -> failwith "Expected JsonValue.Array"

// ============================================
// JsonDocument Coverage Tests - Focus on accessible methods
// ============================================

[<Test>]
let ``JsonDocument from TextReader should parse simple JSON correctly`` () =
    let jsonText = """{"name": "test", "value": 42}"""
    use reader = new StringReader(jsonText)
    
    // Create via reflection to avoid compiler warnings
    let docType = typeof<JsonDocument>
    let createMethod = docType.GetMethod("Create", [| typeof<TextReader> |])
    let doc = createMethod.Invoke(null, [| reader |]) :?> IJsonDocument
    
    match doc.JsonValue with
    | JsonValue.Record props ->
        props |> should contain ("name", JsonValue.String "test")
        props |> should contain ("value", JsonValue.Number 42m)
    | _ -> failwith "Expected JsonValue.Record"

[<Test>]
let ``JsonDocument from TextReader should handle empty JSON`` () =
    let jsonText = "{}"
    use reader = new StringReader(jsonText)
    
    let docType = typeof<JsonDocument>
    let createMethod = docType.GetMethod("Create", [| typeof<TextReader> |])
    let doc = createMethod.Invoke(null, [| reader |]) :?> IJsonDocument
    
    match doc.JsonValue with
    | JsonValue.Record props -> props |> should equal [||]
    | _ -> failwith "Expected JsonValue.Record"

[<Test>]
let ``JsonDocument from TextReader should handle JSON arrays`` () =
    let jsonText = """[1, 2, 3]"""
    use reader = new StringReader(jsonText)
    
    let docType = typeof<JsonDocument>
    let createMethod = docType.GetMethod("Create", [| typeof<TextReader> |])
    let doc = createMethod.Invoke(null, [| reader |]) :?> IJsonDocument
    
    match doc.JsonValue with
    | JsonValue.Array values ->
        values |> should equal [| JsonValue.Number 1m; JsonValue.Number 2m; JsonValue.Number 3m |]
    | _ -> failwith "Expected JsonValue.Array"

[<Test>]
let ``JsonDocument.CreateList should parse single JSON array`` () =
    let jsonText = """[{"id": 1}, {"id": 2}]"""
    use reader = new StringReader(jsonText)
    
    let docType = typeof<JsonDocument>
    let createListMethod = docType.GetMethod("CreateList", [| typeof<TextReader> |])
    let docs = createListMethod.Invoke(null, [| reader |]) :?> IJsonDocument[]
    
    docs.Length |> should equal 2
    
    match docs.[0].JsonValue with
    | JsonValue.Record props -> props |> should contain ("id", JsonValue.Number 1m)
    | _ -> failwith "Expected JsonValue.Record"

[<Test>]
let ``JsonDocument.CreateList should handle multiple JSON objects`` () =
    let jsonText = """{"a": 1}
{"b": 2}
{"c": 3}"""
    use reader = new StringReader(jsonText)
    
    let docType = typeof<JsonDocument>
    let createListMethod = docType.GetMethod("CreateList", [| typeof<TextReader> |])
    let docs = createListMethod.Invoke(null, [| reader |]) :?> IJsonDocument[]
    
    docs.Length |> should equal 3