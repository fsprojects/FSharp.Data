module FSharp.Data.Tests.Conversions

#if INTERACTIVE
#r "../../bin/lib/net45/FSharp.Data.dll"
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/test/FsUnit/lib/net46/FsUnit.NUnit.dll"
#endif

open NUnit.Framework
open FsUnit
open System.Globalization
open FSharp.Data

[<Test>]
let ``Boolean conversions``() = 
  let asBoolean = TextConversions.AsBoolean

  asBoolean "yEs"  |> should equal (Some true)
  asBoolean "trUe" |> should equal (Some true)
  asBoolean "1"    |> should equal (Some true)

  asBoolean "nO"    |> should equal (Some false)
  asBoolean "faLSe" |> should equal (Some false)
  asBoolean "0"     |> should equal (Some false)

  asBoolean "rubbish" |> should equal None

[<Test>]
let ``Decimal conversions``() = 
  TextConversions.AsDecimal CultureInfo.InvariantCulture "¤50" |> should equal (Some 50M)
  TextConversions.AsDecimal (CultureInfo "en-GB") "£50" |> should equal (Some 50M)
  TextConversions.AsDecimal (CultureInfo "en-GB") "$50" |> should equal (Some 50M)
  TextConversions.AsDecimal CultureInfo.InvariantCulture "(10,000,000.99)" |> should equal (Some -10000000.99M)

[<Test>]
let ``DateTime conversions`` () =
  let case sample result =
    TextConversions.AsDateTime CultureInfo.InvariantCulture sample
    |> should equal (Some result)

  case "2016-11-21T10:29:05Z"      <| System.DateTime(2016,11,21,10,29,05, System.DateTimeKind.Utc)
  case "2016-11-21T10:29:05"       <| System.DateTime(2016,11,21,10,29,05, System.DateTimeKind.Local)
  case "2016-11-21T13:29:05+03:00" <| System.DateTime(2016,11,21,10,29,05, System.DateTimeKind.Utc).ToLocalTime()
