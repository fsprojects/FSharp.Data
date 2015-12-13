#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.JsonZipper
#endif

open NUnit.Framework
open FsUnit
open System
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes

type InlinedJSON = JsonZipper<"""{ "firstName": "Max","lastName": "Mustermann", "age": 26, "isCool": true, "size":42.42 }""">


[<Test>]
let ``Can set properties in inlined JSON``() = 
    let person = InlinedJSON.New()
    person.FirstName |> should equal "" 

//    let updated =
//        person
//            .FirstName.Update("John")
//            .LastName.Update("Doe")
//            .Age.Update(30)
//            .IsCool.Update(false)
//            .Size.Update(decimal 43.43)
//
//    updated.FirstName.GetValue() |> should equal "John"
//    updated.LastName.GetValue() |> should equal "Doe"
//    updated.Age.GetValue() |> should equal 30
//    updated.IsCool.GetValue() |> should equal false
//    updated.Size.GetValue() |> should equal 43.43
//
//    person.FirstName.GetValue() |> should equal "Max"
//    person.LastName.GetValue() |> should equal "Mustermann"
//    person.Age.GetValue() |> should equal 26
//    person.IsCool.GetValue() |> should equal true
//    person.Size.GetValue() |> should equal 42.42
//
//    updated.ToString() |> should equal """{"firstName":"John","lastName":"Doe","age":30,"isCool":false,"size":43.43}"""