module FSharp.Data.Tests.JsonExtensions

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

open System.Globalization
open NUnit.Framework
open FsUnit
open FSharp.Data.Json
open FSharp.Data.RuntimeImplementation
open FSharp.Data.Json.Extensions

[<Test>]
let ``Boolean conversions``() = 
  let asBoolean = JsonConversions.AsBoolean CultureInfo.InvariantCulture

  JsonValue.Boolean true  |> asBoolean |> should equal (Some true)
  JsonValue.Number 1M     |> asBoolean |> should equal (Some true)
  JsonValue.String "yEs"  |> asBoolean |> should equal (Some true)
  JsonValue.String "trUe" |> asBoolean |> should equal (Some true)
  JsonValue.String "1"    |> asBoolean |> should equal (Some true)

  JsonValue.Boolean false  |> asBoolean |> should equal (Some false)
  JsonValue.Number 0M      |> asBoolean |> should equal (Some false)
  JsonValue.String "nO"    |> asBoolean |> should equal (Some false)
  JsonValue.String "faLSe" |> asBoolean |> should equal (Some false)
  JsonValue.String "0"     |> asBoolean |> should equal (Some false)

  JsonValue.Null          |> asBoolean |> should equal None
  JsonValue.Number 2M     |> asBoolean |> should equal None
  JsonValue.String "blah" |> asBoolean |> should equal None
