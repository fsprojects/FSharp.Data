module FSharp.Data.Tests.JsonProvider.Reader.Tests

open NUnit.Framework
open FSharp.Data
open FsUnit

type InlinedJSON = JsonProvider<"""{ "firstName": "Max", "lastName": "Mustermann", "age": 26, "isCool": true }""">

[<Test>]
let ``Can parse inlined properties``() = 
    let person = InlinedJSON.GetSample()

    person.FirstName
    |> should equal "Max"

    person.LastName
    |> should equal "Mustermann"

    person.Age
    |> should equal 26

    person.IsCool
    |> should equal true

[<Test>]
let ``Can parse inlined properties but read from file``() = 
    let person = InlinedJSON.Load("Simple.json")

    person.FirstName
    |> should equal "John"

    person.LastName
    |> should equal "Doe"

    person.Age
    |> should equal 25

    person.IsCool
    |> should equal true


type SimpleJSON = JsonProvider<"Simple.json">

let simple = SimpleJSON.GetSample()

[<Test>]
let ``Can parse properties``() = 
    simple.FirstName
    |> should equal "John"

    simple.LastName
    |> should equal "Doe"

    simple.Age
    |> should equal 25

    simple.IsCool
    |> should equal true

type NestedJSON = JsonProvider<"Nested.json">

let nested = NestedJSON.GetSample()

[<Test>]
let ``Can parse nested properties``() = 
    nested.Main.FirstName
    |> should equal "John"

    nested.Main.LastName
    |> should equal "Doe"

    nested.Main.Age
    |> should equal 25

    nested.Main.IsCool
    |> should equal true

type DoubleNestedJSON = JsonProvider<"DoubleNested.json">

let doubleNested = DoubleNestedJSON.GetSample()

[<Test>]
let ``Can parse double nested properties``() = 
    doubleNested.Main.Title
    |> should equal "example"

    doubleNested.Main.Nested.NestedTitle
    |> should equal "sub"

type SimpleArrayJSON = JsonProvider<"SimpleArray.json">

let simpleArray = SimpleArrayJSON.GetSample()

[<Test>]
let ``Can parse simple arrays``() = 
    let items = simpleArray.Items
    items.[0].Id
    |> should equal "Open"

    items.[1].Id
    |> should equal "Pause"

type OptionalValuesInJSON = JsonProvider<"OptionValues.json">

let optionalValuesInJSON = OptionalValuesInJSON.GetSample()

[<Test>]
let ``Can parse optional values in arrays``() = 
    let authors = optionalValuesInJSON.Authors
    authors.[0].Name
    |> should equal "Steffen"

    authors.[0].Age
    |> should equal (Some 29)

    authors.[1].Name
    |> should equal "Tomas"

    authors.[1].Age
    |> should equal None

[<Test>]
let ``Can compare typed JSON documents``() = 
    let simple1 = SimpleJSON()
    let simple2 = SimpleJSON()
    let nested = NestedJSON()

    Assert.AreEqual(simple1,simple2)
    Assert.AreNotEqual(nested,simple2)

type JsonArray = JsonProvider<"""["Adam","Eve","Bonnie","Clyde","Donald","Daisy","Han","Leia"]""">

[<Test>]
let ``Can parse simple array``() = 
    let inlined = JsonArray()    
    inlined.ToString()
      |> should equal """["Adam","Eve","Bonnie","Clyde","Donald","Daisy","Han","Leia"]"""

type MultipleJsonArray = JsonProvider<"""[["Adam","Eve"],["Bonnie","Clyde"],["Donald","Daisy"],["Han","Leia"]]""">

[<Test>]
let ``Can parse multidimensional arrays``() = 
    let inlined = MultipleJsonArray()
    inlined.ToString()
      |> should equal """[["Adam","Eve"],["Bonnie","Clyde"],["Donald","Daisy"],["Han","Leia"]]"""
