module FSharp.Data.Tests.JsonProvider.Dates.Tests

open NUnit.Framework
open FSharp.Data
open FsUnit
open System

type DateJSON = JsonProvider<"Data/Dates.json">

[<Test>]
let ``Can parse microsoft format dates``() = 
    let dates = DateJSON.GetSample()
    dates.Birthdate |> should equal (new DateTime(1997, 7, 16, 19, 20, 30, 450)) // 1997-07-16T19:20:30.45+01:00

[<Test>]
let ``Can parse ISO 8601 dates``() =
    let dates = DateJSON.GetSample()
    dates.Anniversary.ToUniversalTime() |> should equal (new DateTime(1997, 7, 16, 18, 20, 30, 450)) 

[<Test>]
let ``Can parse UTC dates``() =
    let dates = DateJSON.GetSample()
    dates.UtcTime.ToUniversalTime() |> should equal (new DateTime(1997, 7, 16, 19, 50, 30, 0)) 

[<Test>]
[<SetCulture("zh-CN")>]
let ``Can parse ISO 8601 dates in the correct culture``() =
    let dates = DateJSON.GetSample()
    dates.NoTimeZone |> should equal (new DateTime(1997, 7, 16, 19, 20, 30, 00, System.DateTimeKind.Local)) 
