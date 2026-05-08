module FSharp.Data.Tests.JsonValueInnerTextAndExtensions

open NUnit.Framework
open FsUnit
open System
open System.Globalization
open FSharp.Data
open FSharp.Data.JsonExtensions

// ============================================================
// JsonExtensions.InnerText — C# [<Extension>] method
// Returns AsString result for scalar values; concatenates array elements
// ============================================================

[<Test>]
let ``InnerText returns string content for a String value`` () =
    JsonValue.String("hello world").InnerText() |> should equal "hello world"

[<Test>]
let ``InnerText returns empty string for Null`` () =
    JsonValue.Null.InnerText() |> should equal ""

[<Test>]
let ``InnerText returns string representation for Boolean true`` () =
    JsonValue.Boolean(true).InnerText() |> should equal "true"

[<Test>]
let ``InnerText returns string representation for Boolean false`` () =
    JsonValue.Boolean(false).InnerText() |> should equal "false"

[<Test>]
let ``InnerText returns string representation for Number`` () =
    JsonValue.Number(42M).InnerText() |> should equal "42"

[<Test>]
let ``InnerText returns string representation for Float`` () =
    JsonValue.Float(3.14).InnerText() |> should startWith "3.14"

[<Test>]
let ``InnerText concatenates string elements of an array`` () =
    let json = JsonValue.Parse """["foo","bar","baz"]"""
    json.InnerText() |> should equal "foobarbaz"

[<Test>]
let ``InnerText returns empty string for an empty array`` () =
    JsonValue.Array([||]).InnerText() |> should equal ""

[<Test>]
let ``InnerText includes numeric elements in a mixed array`` () =
    let json = JsonValue.Parse """["hello", 42, "world"]"""
    json.InnerText() |> should equal "hello42world"

[<Test>]
let ``InnerText returns empty string for a record object`` () =
    let json = JsonValue.Parse """{"key":"value"}"""
    json.InnerText() |> should equal ""

[<Test>]
let ``InnerText handles Unicode strings correctly`` () =
    JsonValue.String("こんにちは").InnerText() |> should equal "こんにちは"

[<Test>]
let ``InnerText handles empty string`` () =
    JsonValue.String("").InnerText() |> should equal ""

[<Test>]
let ``InnerText concatenates array of strings`` () =
    let json = JsonValue.Parse """["a","b","c","d","e"]"""
    json.InnerText() |> should equal "abcde"

// ============================================================
// JsonValue.Properties — F# augmentation member
// ============================================================

[<Test>]
let ``Properties returns all key-value pairs for a Record`` () =
    let json = JsonValue.Parse """{"x":1,"y":2,"z":3}"""
    let props = json.Properties
    props |> should haveLength 3
    props |> Array.map fst |> should equal [| "x"; "y"; "z" |]

[<Test>]
let ``Properties returns empty array for an empty record`` () =
    JsonValue.Record([||]).Properties |> should haveLength 0

[<Test>]
let ``Properties returns empty array for a non-record value`` () =
    JsonValue.String("text").Properties |> should haveLength 0
    JsonValue.Array([||]).Properties |> should haveLength 0
    JsonValue.Null.Properties |> should haveLength 0
    JsonValue.Boolean(false).Properties |> should haveLength 0

// ============================================================
// AsGuid — additional edge cases
// ============================================================

[<Test>]
let ``AsGuid parses lowercase GUID string`` () =
    let j = JsonValue.Parse """{"id":"550e8400-e29b-41d4-a716-446655440000"}"""
    j?id.AsGuid() |> should equal (Guid.Parse "550e8400-e29b-41d4-a716-446655440000")

[<Test>]
let ``AsGuid parses uppercase GUID string`` () =
    let j = JsonValue.Parse """{"id":"550E8400-E29B-41D4-A716-446655440000"}"""
    j?id.AsGuid() |> should equal (Guid.Parse "550E8400-E29B-41D4-A716-446655440000")

[<Test>]
let ``AsGuid throws for invalid GUID string`` () =
    let j = JsonValue.Parse """{"id":"not-a-guid"}"""
    (fun () -> j?id.AsGuid() |> ignore) |> should throw typeof<Exception>

// ============================================================
// AsBoolean — string-to-bool conversion cases
// ============================================================

[<Test>]
let ``AsBoolean parses true string`` () =
    let j = JsonValue.Parse """{"flag":"true"}"""
    j?flag.AsBoolean() |> should equal true

[<Test>]
let ``AsBoolean parses false string`` () =
    let j = JsonValue.Parse """{"flag":"false"}"""
    j?flag.AsBoolean() |> should equal false

[<Test>]
let ``AsBoolean parses yes/no strings`` () =
    let j = JsonValue.Parse """{"a":"yes","b":"no"}"""
    j?a.AsBoolean() |> should equal true
    j?b.AsBoolean() |> should equal false

[<Test>]
let ``AsBoolean parses 1/0 strings`` () =
    let j = JsonValue.Parse """{"a":"1","b":"0"}"""
    j?a.AsBoolean() |> should equal true
    j?b.AsBoolean() |> should equal false

[<Test>]
let ``AsBoolean throws for invalid boolean string`` () =
    let j = JsonValue.Parse """{"flag":"maybe"}"""
    (fun () -> j?flag.AsBoolean() |> ignore) |> should throw typeof<Exception>

// ============================================================
// AsTimeSpan — additional edge cases
// ============================================================

[<Test>]
let ``AsTimeSpan throws for non-timespan value`` () =
    let j = JsonValue.Parse """{"d":"not-a-timespan"}"""
    (fun () -> j?d.AsTimeSpan() |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``AsTimeSpan parses zero duration`` () =
    // Uses .NET TimeSpan.Parse format: "0:00:00"
    let j = JsonValue.Parse """{"d":"0:00:00"}"""
    j?d.AsTimeSpan() |> should equal TimeSpan.Zero

// ============================================================
// Dynamic operator (?) edge cases
// ============================================================

[<Test>]
let ``Dynamic operator accesses nested string property`` () =
    let j = JsonValue.Parse """{"person":{"name":"Alice"}}"""
    j?person?name.AsString() |> should equal "Alice"

[<Test>]
let ``Dynamic operator throws for missing property`` () =
    let j = JsonValue.Parse """{"x":1}"""
    (fun () -> j?missing |> ignore) |> should throw typeof<Exception>
