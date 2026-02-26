module FSharp.Data.Tests.YamlDocument

open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.Runtime.BaseTypes
open System.IO
open System.Reflection

// Access the "generated code only" methods via reflection
let private parseToJsonValue (text: string) : JsonValue =
    let m = typeof<YamlDocument>.GetMethod("ParseToJsonValue", [| typeof<string> |])
    m.Invoke(null, [| text |]) :?> JsonValue

let private parseToJsonValueForInference (text: string) : JsonValue =
    let m = typeof<YamlDocument>.GetMethod("ParseToJsonValueForInference", [| typeof<string> |])
    m.Invoke(null, [| text |]) :?> JsonValue

let private createFromReader (reader: TextReader) : IJsonDocument =
    let m = typeof<YamlDocument>.GetMethod("Create", [| typeof<TextReader> |])
    m.Invoke(null, [| reader |]) :?> IJsonDocument

let private createListFromReader (reader: TextReader) : IJsonDocument[] =
    let m = typeof<YamlDocument>.GetMethod("CreateList", [| typeof<TextReader> |])
    m.Invoke(null, [| reader |]) :?> IJsonDocument[]

// ── ParseToJsonValue (runtime) ────────────────────────────────────────────────

[<Test>]
let ``YamlDocument parses plain string scalar`` () =
    let v = parseToJsonValue "name: Alice"
    v.["name"].AsString() |> should equal "Alice"

[<Test>]
let ``YamlDocument parses integer scalar`` () =
    let v = parseToJsonValue "age: 42"
    v.["age"].AsInteger() |> should equal 42

[<Test>]
let ``YamlDocument parses float scalar`` () =
    let v = parseToJsonValue "score: 3.14"
    v.["score"].AsFloat() |> should (equalWithin 0.001) 3.14

[<Test>]
let ``YamlDocument parses boolean true`` () =
    let v = parseToJsonValue "active: true"
    v.["active"].AsBoolean() |> should equal true

[<Test>]
let ``YamlDocument parses boolean false`` () =
    let v = parseToJsonValue "enabled: false"
    v.["enabled"].AsBoolean() |> should equal false

[<Test>]
let ``YamlDocument parses null scalar`` () =
    let v = parseToJsonValue "value: null"
    v.["value"] |> should equal JsonValue.Null

[<Test>]
let ``YamlDocument parses tilde as null`` () =
    let v = parseToJsonValue "value: ~"
    v.["value"] |> should equal JsonValue.Null

[<Test>]
let ``YamlDocument parses sequence as array`` () =
    let v = parseToJsonValue "tags:\n  - fsharp\n  - dotnet"
    let tags = v.["tags"].AsArray()
    tags |> should haveLength 2
    tags.[0].AsString() |> should equal "fsharp"
    tags.[1].AsString() |> should equal "dotnet"

[<Test>]
let ``YamlDocument parses nested mapping`` () =
    let yaml = "address:\n  city: Springfield\n  zip: 01234"
    let v = parseToJsonValue yaml
    v.["address"].["city"].AsString() |> should equal "Springfield"

[<Test>]
let ``YamlDocument runtime: quoted string is returned as-is`` () =
    // At runtime, a quoted "01234" should be the original string value
    let v = parseToJsonValue "zip: \"01234\""
    v.["zip"].AsString() |> should equal "01234"

[<Test>]
let ``YamlDocument parses empty document as Null`` () =
    let v = parseToJsonValue ""
    v |> should equal JsonValue.Null

// ── ParseToJsonValueForInference (design-time) ────────────────────────────────

[<Test>]
let ``YamlDocument inference: quoted numeric string is inferred as string sentinel`` () =
    // "01234" is quoted in YAML → must be typed as string, not int
    let v = parseToJsonValueForInference "zip: \"01234\""
    // The sentinel value "s" is returned; it must parse as string
    match v.["zip"] with
    | JsonValue.String _ -> () // pass
    | other -> failwithf "Expected JsonValue.String but got %A" other

[<Test>]
let ``YamlDocument inference: plain integer is inferred as number`` () =
    let v = parseToJsonValueForInference "age: 30"
    match v.["age"] with
    | JsonValue.Number _ -> () // pass
    | other -> failwithf "Expected JsonValue.Number but got %A" other

[<Test>]
let ``YamlDocument inference: single-quoted numeric string is inferred as string`` () =
    let v = parseToJsonValueForInference "code: '007'"
    match v.["code"] with
    | JsonValue.String _ -> () // pass
    | other -> failwithf "Expected JsonValue.String but got %A" other

[<Test>]
let ``YamlDocument inference: quoted non-numeric string passes through unchanged`` () =
    let v = parseToJsonValueForInference "name: \"Alice\""
    v.["name"].AsString() |> should equal "Alice"

[<Test>]
let ``YamlDocument inference: plain boolean is inferred as boolean`` () =
    let v = parseToJsonValueForInference "active: true"
    match v.["active"] with
    | JsonValue.Boolean true -> () // pass
    | other -> failwithf "Expected JsonValue.Boolean true but got %A" other

// ── Create / CreateList ───────────────────────────────────────────────────────

[<Test>]
let ``YamlDocument.Create from TextReader returns IJsonDocument`` () =
    use reader = new StringReader("name: Bob\nage: 25")
    let doc = createFromReader reader
    doc |> should not' (be null)
    doc.JsonValue.["name"].AsString() |> should equal "Bob"
    doc.JsonValue.["age"].AsInteger() |> should equal 25

[<Test>]
let ``YamlDocument.CreateList returns one document per YAML sequence item`` () =
    let yaml = "- id: 1\n- id: 2"
    use reader = new StringReader(yaml)
    let docs = createListFromReader reader
    docs |> should haveLength 2
    docs.[0].JsonValue.["id"].AsInteger() |> should equal 1
    docs.[1].JsonValue.["id"].AsInteger() |> should equal 2

[<Test>]
let ``YamlDocument.CreateList wraps single mapping in array`` () =
    use reader = new StringReader("id: 1")
    let docs = createListFromReader reader
    docs |> should haveLength 1
    docs.[0].JsonValue.["id"].AsInteger() |> should equal 1

[<Test>]
let ``YamlDocument.CreateList unwraps top-level YAML sequence`` () =
    let yaml = "- id: 1\n- id: 2\n- id: 3"
    use reader = new StringReader(yaml)
    let docs = createListFromReader reader
    docs |> should haveLength 3
    docs.[2].JsonValue.["id"].AsInteger() |> should equal 3
