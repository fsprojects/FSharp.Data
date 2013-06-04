// --------------------------------------------------------------------------------------
// Tests for the CSV parsing code
// --------------------------------------------------------------------------------------

module FSharp.Data.Tests.JsonProvider.Tests

open NUnit.Framework
open FsUnit
open System
open System.IO
open FSharp.Data

type NumericFields = JsonProvider<""" [ {"a":12.3}, {"a":1.23, "b":1999.0} ] """, SampleList=true>
type DecimalFields = JsonProvider<""" [ {"a":9999999999999999999999999999999999.3}, {"a":1.23, "b":1999.0} ] """, SampleList=true>

[<Test>]
let ``Decimal required field is read correctly`` () = 
  let prov = NumericFields.Parse(""" {"a":123} """)
  prov.A |> should equal 123M

[<Test>]
let ``Decimal optional field is read as None`` () = 
  let prov = NumericFields.Parse(""" {"a":123} """)
  prov.B |> should equal None

[<Test>]
let ``Reading a required field that is null throws an exception`` () = 
  let prov = NumericFields.Parse(""" {"a":null, "b":123} """)
  (fun () -> prov.A |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``Reading a required field that is missing throws an exception`` () = 
  let prov = NumericFields.Parse(""" {"b":123} """)
  (fun () -> prov.A |> ignore)|> should throw typeof<Exception>

[<Test>]
let ``Reading a required decimal that is not a valid decimal throws an exception`` () = 
  let prov = NumericFields.Parse(""" {"a":"hello", "b":123} """)
  (fun () -> prov.A |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``Reading a required float that is not a valid float throws an exception`` () = 
  let prov = DecimalFields.Parse(""" {"a":"hello", "b":123} """)
  (fun () -> prov.A |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``Optional int correctly infered`` () = 
  let prov = JsonProvider<""" [ {"a":123}, {"a":null} ] """>.GetSample()
  let i = prov.[0].A.Number
  i |> should equal (Some 123)

[<Test>]
let ``SampleList for json correctly handled``() = 
    Path.Combine(__SOURCE_DIRECTORY__, "Data/TwitterSample.json")
    |> File.ReadLines 
    |> Seq.filter (not << String.IsNullOrWhiteSpace)
    |> Seq.sumBy (fun line ->
        let twitter = JsonProvider<"Data/TwitterSample.json", SampleList=true>.Parse line
        match twitter.Text with
        | Some _ -> 0
        | None -> 1)
    |> should equal 2

[<Test>]
let ``Null values correctly handled``() = 
    let tweetStr = 
        Path.Combine(__SOURCE_DIRECTORY__, "Data/TwitterSample.json")
        |> File.ReadLines 
        |> Seq.head
    let tweet = JsonProvider<"Data/TwitterSample.json", SampleList=true>.Parse tweetStr
    tweet.Place |> should equal None
