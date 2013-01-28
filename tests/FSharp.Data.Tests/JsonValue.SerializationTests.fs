module FSharp.Data.Tests.JsonValue.SerializationTests

open NUnit.Framework
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open FsUnit

let toString x = x.ToString()

[<Test>]
let ``Can serialize empty document``() = 
    JsonValue.emptyObject.ToString() 
    |> should equal "{}"

[<Test>] 
let ``Can serialize document with single property``() =
    JsonValue.emptyObject
        .Add("firstName", "John")
        .ToString()
    |> should equal "{\"firstName\":\"John\"}"

[<Test>] 
let ``Can serialize document with booleans``() =
    JsonValue.emptyObject
        .Add("aa", true)
        .Add("bb", false)
        .ToString()
    |> should equal "{\"aa\":true,\"bb\":false}"

[<Test>]
let ``Can serialize document with array, null and number``() =
    let text = "{\"items\":[{\"id\":\"Open\"},null,{\"id\":25}]}"
    let json = JsonValue.Parse text
    json.ToString() |> should equal text
