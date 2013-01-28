// --------------------------------------------------------------------------------------
// Tests for the CSV parsing code
// --------------------------------------------------------------------------------------

module FSharp.Data.Tests.JsonProvider.Tests

open NUnit.Framework
open System
open System.IO
open FSharp.Data

type NumericFields = JsonProvider<""" [ {"a":12.3}, {"a":1.23, "b":1999.0} ] """, SampleList=true>

[<Test>]
let ``Decimal required field is read correctly`` () = 
  let prov = NumericFields.Parse(""" {"a":123} """)
  Assert.AreEqual(123M, prov.A)

[<Test>]
let ``Decimal optional field is read as None`` () = 
  let prov = NumericFields.Parse(""" {"a":123} """)
  Assert.AreEqual(None, prov.B)

[<Test>]
let ``Reading a required field that is null throws an exception`` () = 
  let prov = NumericFields.Parse(""" {"a":null, "b":123} """)
  Assert.Throws<Exception>(fun () -> prov.A |> ignore) |> ignore

[<Test>]
let ``Reading a required field that is missing throws an exception`` () = 
  let prov = NumericFields.Parse(""" {"b":123} """)
  Assert.Throws<Exception>(fun () -> prov.A |> ignore) |> ignore

[<Test>]
let ``Optional int correctly infered`` () = 
  let prov = JsonProvider<""" [ {"a":123}, {"a":null} ] """>.GetSample()
  let i:int option = prov.[0].A
  Assert.AreEqual(i, Some 123)
