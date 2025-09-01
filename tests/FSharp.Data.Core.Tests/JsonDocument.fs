module FSharp.Data.Tests.JsonDocument

open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.Runtime.BaseTypes
open System.IO
open System.Reflection

// Use reflection to access the "generated code only" methods for testing
let private getCreateMethod() =
    typeof<JsonDocument>.GetMethod("Create", [| typeof<JsonValue>; typeof<string> |])

let private getCreateFromReaderMethod() =
    typeof<JsonDocument>.GetMethod("Create", [| typeof<System.IO.TextReader> |])

let private getCreateListMethod() =
    typeof<JsonDocument>.GetMethod("CreateList", [| typeof<System.IO.TextReader> |])

[<Test>]
let ``JsonDocument.Create with JsonValue should return IJsonDocument using reflection`` () =
    let createMethod = getCreateMethod()
    let jsonValue = JsonValue.Number 42M
    let doc = createMethod.Invoke(null, [| jsonValue; "/path" |]) :?> IJsonDocument
    
    doc |> should not' (be null)
    doc.JsonValue |> should equal jsonValue

[<Test>]
let ``JsonDocument.Create with TextReader should parse JSON using reflection`` () =
    let createMethod = getCreateFromReaderMethod()
    let json = """{"name": "test", "value": 123}"""
    use reader = new StringReader(json)
    let doc = createMethod.Invoke(null, [| reader |]) :?> IJsonDocument
    
    doc |> should not' (be null)
    doc.JsonValue |> should not' (be null)

[<Test>]
let ``JsonDocument.CreateList with single array should return array elements using reflection`` () =
    let createListMethod = getCreateListMethod()
    let json = """[{"id": 1}, {"id": 2}]"""
    use reader = new StringReader(json)
    let docs = createListMethod.Invoke(null, [| reader |]) :?> IJsonDocument[]
    
    docs |> should haveLength 2
    docs.[0].JsonValue.["id"].AsInteger() |> should equal 1
    docs.[1].JsonValue.["id"].AsInteger() |> should equal 2

[<Test>]
let ``JsonDocument.CreateList with multiple JSON objects should return separate documents using reflection`` () =
    let createListMethod = getCreateListMethod()
    let json = """{"id": 1}{"id": 2}"""
    use reader = new StringReader(json)
    let docs = createListMethod.Invoke(null, [| reader |]) :?> IJsonDocument[]
    
    docs |> should haveLength 2
    docs.[0].JsonValue.["id"].AsInteger() |> should equal 1
    docs.[1].JsonValue.["id"].AsInteger() |> should equal 2

[<Test>]
let ``JsonDocument ToString should return JsonValue string representation using reflection`` () =
    let createMethod = getCreateMethod()
    let jsonValue = JsonValue.Number 42M
    let docObj = createMethod.Invoke(null, [| jsonValue; "/test" |])
    
    docObj.ToString() |> should equal "42"

[<Test>]
let ``JsonDocument JsonValue property should return original JsonValue using reflection`` () =
    let createMethod = getCreateMethod()
    let jsonValue = JsonValue.String "test"
    let doc = createMethod.Invoke(null, [| jsonValue; "/test" |]) :?> IJsonDocument
    
    doc.JsonValue |> should equal jsonValue

[<Test>]
let ``IJsonDocument Path method should return path using reflection`` () =
    let createMethod = getCreateMethod()
    let jsonValue = JsonValue.Boolean true
    let doc = createMethod.Invoke(null, [| jsonValue; "/root/item" |]) :?> IJsonDocument
    
    // Use reflection to call the Path method to avoid the compiler message
    let pathMethod = typeof<IJsonDocument>.GetMethod("Path")
    let path = pathMethod.Invoke(doc, [||]) :?> string
    
    path |> should equal "/root/item"

[<Test>]
let ``IJsonDocument CreateNew should create new document with incremented path using reflection`` () =
    let createMethod = getCreateMethod()
    let jsonValue = JsonValue.Array [| JsonValue.Number 1M; JsonValue.Number 2M |]
    let originalDoc = createMethod.Invoke(null, [| jsonValue; "/root" |]) :?> IJsonDocument
    let newValue = JsonValue.Number 42M
    
    // Use reflection to call CreateNew to avoid the compiler message
    let createNewMethod = typeof<IJsonDocument>.GetMethod("CreateNew")
    let newDoc = createNewMethod.Invoke(originalDoc, [| newValue; "/item[0]" |]) :?> IJsonDocument
    
    newDoc.JsonValue |> should equal newValue
    let pathMethod = typeof<IJsonDocument>.GetMethod("Path")
    let path = pathMethod.Invoke(newDoc, [||]) :?> string
    path |> should equal "/root/item[0]"

[<Test>]
let ``JsonDocument.Create with empty JSON should work using reflection`` () =
    let createMethod = getCreateFromReaderMethod()
    let json = "{}"
    use reader = new StringReader(json)
    let doc = createMethod.Invoke(null, [| reader |]) :?> IJsonDocument
    
    doc |> should not' (be null)
    doc.JsonValue |> should not' (be null)

[<Test>]
let ``JsonDocument.CreateList with empty array should return empty array using reflection`` () =
    let createListMethod = getCreateListMethod()
    let json = "[]"
    use reader = new StringReader(json)
    let docs = createListMethod.Invoke(null, [| reader |]) :?> IJsonDocument[]
    
    docs |> should haveLength 0