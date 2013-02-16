// --------------------------------------------------------------------------------------
// Tests for the CSV parsing code
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Tests

open NUnit.Framework
open System.IO
open FSharp.Data

module CsvSimple =

  let [<Literal>] simpleCsv = """
    Column1,Column2,Column3
    TRUE,no,3
    "yes", "false", 1.92 """

  type SimpleCsv = CsvProvider<simpleCsv>

  [<Test>]
  let ``Bool column correctly infered and accessed`` () = 
    let csv = SimpleCsv.Parse(simpleCsv)
    let first = csv.Data |> Seq.head
    let actual:bool = first.Column1
    Assert.AreEqual(true, actual)

  [<Test>]
  let ``Decimal column correctly infered and accessed`` () = 
    let csv = SimpleCsv.Parse(simpleCsv)
    let first = csv.Data |> Seq.head
    let actual:decimal = first.Column3
    Assert.AreEqual(3.0M, actual)

module CsvSimpleNoHeaders =

  let [<Literal>] simpleCsv = """
    Column1,Column2,Column3
    TRUE,no,3
    "yes", "false", 1.92 """

  type SimpleCsv = CsvProvider<simpleCsv, Headers = "Col1,Col2,Col3">

  [<Test>]
  let ``Bool column correctly infered and accessed`` () = 
    let csv = SimpleCsv.Parse(simpleCsv)
    let first = csv.Data |> Seq.head
    let actual:bool = first.Col1
    Assert.AreEqual(true, actual)

  [<Test>]
  let ``Decimal column correctly infered and accessed`` () = 
    let csv = SimpleCsv.Parse(simpleCsv)
    let first = csv.Data |> Seq.head
    let actual:decimal = first.Col3
    Assert.AreEqual(3.0M, actual)
