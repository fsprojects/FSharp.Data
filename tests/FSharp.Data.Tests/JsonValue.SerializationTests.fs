module FSharp.Data.Tests.JsonValue.SerializationTests

open NUnit.Framework
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open FsUnit

let toString x = x.ToString()

[<Test>]
let ``Can serialize empty document``() = 
    (JsonValue.Object Map.empty).ToString() 
    |> should equal "{}"

[<Test>] 
let ``Can serialize document with single property``() =
    ( [ "firstName", JsonValue.String "John" ]
      |> Map.ofSeq |> JsonValue.Object ).ToString()
    |> should equal "{\"firstName\":\"John\"}"

[<Test>] 
let ``Can serialize document with booleans``() =
    ( [ "aa", JsonValue.Boolean true
        "bb", JsonValue.Boolean false ]
      |> Map.ofSeq |> JsonValue.Object ).ToString()
    |> should equal "{\"aa\":true,\"bb\":false}"

[<Test>]
let ``Can serialize document with array, null and number``() =
    let text = "{\"items\":[{\"id\":\"Open\"},null,{\"id\":25}]}"
    let json = JsonValue.Parse text
    json.ToString() |> should equal text
