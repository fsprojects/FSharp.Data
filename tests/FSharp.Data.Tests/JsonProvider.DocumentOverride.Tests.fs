module FSharp.Data.Tests.JsonProvider.DocumentOverride.Tests

open NUnit.Framework
open FSharp.Data
open System.Xml
open FsUnit

type WikiSample =
    JsonProvider<
        """{  
                 "firstName": "John",
                 "lastName" : "Smith",
                 "age"      : 25
           }""">

let newJson = 
    """{  
            "firstName": "Jane",
            "lastName" : "Doe",
            "age"      : 23
    }"""

let newJson2 = 
    """{  
            "firstName": "Jim",
            "lastName" : "Smith",
            "age"      : 24
    }"""

let document = WikiSample.Parse(newJson)
let document2 = WikiSample.Parse(newJson2)

[<Test>]
let ``Jane should have first name of Jane``() = 
    document.FirstName |> should equal "Jane"

[<Test>]
let ``Jane should have a last name of Doe``() = 
    document.LastName |> should equal "Doe"

[<Test>]
let ``Jane should have an age of 23``() = 
    document.Age |> should equal 23

[<Test>]
let ``Jim should have a first name of Jim``() = 
    document2.FirstName |> should equal "Jim"

[<Test>]
let ``Jim should have a last name of Smith``() = 
    document2.LastName |> should equal "Smith"

[<Test>]
let ``Jim should have an age of 24``() = 
    document2.Age |> should equal 24
