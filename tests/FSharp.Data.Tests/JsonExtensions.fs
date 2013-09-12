module FSharp.Data.Tests.JsonExtensions

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

open System.Globalization
open NUnit.Framework
open FsUnit
open FSharp.Data.Json
open FSharp.Data.Json.Extensions

[<Test>]
let ``Boolean conversions``() = 

  (JsonValue.Boolean true).AsBoolean()  |> should equal true
  (JsonValue.Number 1M).AsBoolean()     |> should equal true
  (JsonValue.String "yEs").AsBoolean()  |> should equal true
  (JsonValue.String "trUe").AsBoolean() |> should equal true
  (JsonValue.String "1").AsBoolean()    |> should equal true

  (JsonValue.Boolean false).AsBoolean()  |> should equal false
  (JsonValue.Number 0M).AsBoolean()      |> should equal false
  (JsonValue.String "nO").AsBoolean()    |> should equal false
  (JsonValue.String "faLSe").AsBoolean() |> should equal false
  (JsonValue.String "0").AsBoolean()     |> should equal false
