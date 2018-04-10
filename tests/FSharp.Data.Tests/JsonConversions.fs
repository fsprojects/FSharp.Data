#if INTERACTIVE
#r "../../bin/lib/net45/FSharp.Data.dll"
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/test/FsUnit/lib/net46/FsUnit.NUnit.dll"
#else
module FSharp.Data.Tests.JsonConversions
#endif

open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.Runtime

[<Test>]
let ``Boolean conversions``() = 
  let asBoolean = JsonConversions.AsBoolean

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
