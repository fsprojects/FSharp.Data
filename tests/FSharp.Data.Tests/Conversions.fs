module FSharp.Data.Tests.Conversions

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

open NUnit.Framework
open FsUnit
open System.Globalization
open FSharp.Data.RuntimeImplementation

[<Test>]
let ``Boolean conversions``() = 
  let culture = CultureInfo.InvariantCulture
  
  Operations.AsBoolean culture "yEs"  |> should equal (Some true)
  Operations.AsBoolean culture "trUe" |> should equal (Some true)
  Operations.AsBoolean culture "1"    |> should equal (Some true)

  Operations.AsBoolean culture "nO"    |> should equal (Some false)
  Operations.AsBoolean culture "faLSe" |> should equal (Some false)
  Operations.AsBoolean culture "0"     |> should equal (Some false)

  Operations.AsBoolean culture "rubbish" |> should equal None

[<Test>]
let ``Decimal conversions``() = 
  Operations.AsDecimal CultureInfo.InvariantCulture "¤50" |> should equal (Some 50M)
  Operations.AsDecimal (CultureInfo "en-GB") "£50" |> should equal (Some 50M)
  Operations.AsDecimal (CultureInfo "en-GB") "$50" |> should equal None
