module FSharp.Data.Tests.JsonProvider.Writer.Tests

open NUnit.Framework
open System.Xml.Linq
open FSharp.Data
open FSharp.Data.Json.Extensions
open FsUnit

type InlinedJSON = JsonProvider<"""{ "firstName": "Max", "lastName": "Mustermann", "age": 26, "isCool": true, "size":42.42 }""">

//[<Test>]
//let ``Can set properties in inlined properties``() = 
//    let person = InlinedJSON.GetSample()
//
//    person.FirstName <- "John"
//    person.FirstName |> should equal "John"
//
//    person.LastName <- "Doe"
//    person.LastName |> should equal "Doe"
//
//    person.Age <- 30
//    person.Age |> should equal 30
//
//    person.IsCool <- false
//    person.IsCool |> should equal false
//
//    person.Size <- 43.43
//    person.Size |> should equal 43.43

type AuthorsJSON = JsonProvider<"""{ "authors": [{ "name": "Steffen" }, { "name": "Tomas", "age": 29, "isCool": true, "size":42.42 }]}""">

//[<Test>]
//let ``Can set optional properties in inlined JSON``() = 
//    let inlined = AuthorsJSON.GetSample()
//
//    let author = inlined.Authors |> Seq.head
//
//    author.Age <- None
//    author.Age |> should equal None
//
//    author.Age <- Some 42
//    author.Age |> should equal (Some 42)
//
//    author.IsCool <- None
//    author.IsCool |> should equal None
//
//    author.IsCool <- Some true
//    author.IsCool |> should equal (Some true)
//
//    author.Size <- None
//    author.Size |> should equal None
//
//    author.Size <- Some 42.45
//    author.Size |> should equal (Some 42.45)

//[<Test>]
//let ``Can add author in inlined JSON``() = 
//    let inlined = AuthorsJSON.GetSample()
//
//    let author = inlined.NewAuthor()
//    author.Name <- "John"
//    
//    inlined.AddAuthor author
//
//    let authors = inlined.Authors
//    authors.Length |> should equal 3
//
//    authors.[0].Name |> should equal "Steffen"
//    authors.[1].Name |> should equal "Tomas"
//    authors.[2].Name |> should equal "John"

[<Test>]
let ``Can serialize the json``() =
    let inlined = AuthorsJSON.GetSample()
    let json = inlined.ToString()
    json |> should equal """{"authors":[{"name":"Steffen"},{"age":29,"isCool":true,"name":"Tomas","size":42.42}]}"""

[<Test>]
let ``Can convert the json to xml``() =
    let inlined = AuthorsJSON.GetSample()
    let xml = inlined.JsonValue.ToXml() |> Seq.head 
    let expectedXml = XDocument.Parse("<authors><item name=\"Steffen\" /><item age=\"29\" isCool=\"true\" name=\"Tomas\" size=\"42.42\" /></authors>")
    xml.ToString() |> should equal (expectedXml.ToString())
