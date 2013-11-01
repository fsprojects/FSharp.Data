module FSharp.Data.Tests.JsonValueWriter

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

open NUnit.Framework
open System
open System.Globalization
open System.Threading
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open FsUnit

[<Test>] 
let ``Can modify a document with text and integer``() =
    let j = JsonValue.Parse "{\"firstName\": \"John\", \"lastName\": \"Smith\", \"age\": 25}"
    let j' = j.Set("firstName","Max").Set("age",30)

    j'?firstName.AsString() |> should equal "Max"
    j'?lastName.AsString() |> should equal "Smith"
    j'?age.AsInteger()  |> should equal 30

    // original json not touched
    j?firstName.AsString() |> should equal "John"
    j?lastName.AsString() |> should equal "Smith"
    j?age.AsInteger()  |> should equal 25

[<Test>] 
let ``Can serialize a modified document``() =
    let j = JsonValue.Parse "{\"firstName\": \"John\", \"lastName\": \"Smith\", \"age\": 25, \"cool\": false}"
    let j' = j.Set("firstName","Don").Set("age",28).Set("cool",true)

    j'.ToString() |> should equal "{\"age\":28,\"cool\":true,\"firstName\":\"Don\",\"lastName\":\"Smith\"}"

    // original json not touched
    j.ToString() |> should equal "{\"age\":25,\"cool\":false,\"firstName\":\"John\",\"lastName\":\"Smith\"}"

[<Test>]
let ``Can modfiy document with iso date``() =
    let j = JsonValue.Parse "{\"anniversary\": \"2009-05-19 14:39:22.500\"}"

    let j' = j.Set("anniversary",new DateTime(2010, 05, 21, 14, 44, 22, 500, DateTimeKind.Local))
    
    j'.ToString() |> should equal "{\"anniversary\":\"05/21/2010 14:44:22\"}"

    // original json not touched
    j.ToString() |> should equal "{\"anniversary\":\"2009-05-19 14:39:22.500\"}"