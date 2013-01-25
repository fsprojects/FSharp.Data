// --------------------------------------------------------------------------------------
// Tests for the CSV parsing code
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Tests

open NUnit.Framework
open System
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
    let csv = new SimpleCsv()
    let first = csv.Data |> Seq.head
    let actual:bool = first.Column1
    Assert.AreEqual(true, actual)

  [<Test>]
  let ``Decimal column correctly infered and accessed`` () = 
    let csv = new SimpleCsv()
    let first = csv.Data |> Seq.head
    let actual:decimal = first.Column3
    Assert.AreEqual(3.0M, actual)

  let [<Literal>] csvWithEmptyValues = """
float1,float2,float3,float4,int,float5,float6,date
1,1,1,1,,,,
2.0,#N/A,,1,1,1,,2010-01-10
,,2.0,#N/A,1,#N/A,2.0,"""

  [<Test>]
  let ``Inference of numbers with empty values`` () = 
    let csv = new CsvProvider<csvWithEmptyValues>()
    let rows = csv.Data |> Seq.toArray
    
    let row = rows.[0]
    
    let f1:float = row.Float1
    let f2:float = row.Float2
    let f3:float = row.Float3
    let f4:float = row.Float4
    let i:option<int> = row.Int
    let f5:float = row.Float5
    let f6:float = row.Float6
    let d:option<DateTime> = row.Date
    
    let expected = 1.0, 1.0, 1.0, 1.0, (None:option<int>), Double.NaN, Double.NaN, (None:option<DateTime>)
    let actual = row.Float1, row.Float2, row.Float3, row.Float4, row.Int, row.Float5, row.Float6, row.Date    
    Assert.AreEqual(expected, actual)

    let row = rows.[1]
    let expected = 2.0, Double.NaN, Double.NaN, 1.0, Some 1, 1.0, Double.NaN, Some(new DateTime(2010, 01,10)) 
    let actual = row.Float1, row.Float2, row.Float3, row.Float4, row.Int, row.Float5, row.Float6, row.Date
    Assert.AreEqual(expected, actual)

    let row = rows.[2]
    let expected = Double.NaN, Double.NaN, 2.0, Double.NaN, Some 1, Double.NaN, 2.0, (None:option<DateTime>)
    let actual = row.Float1, row.Float2, row.Float3, row.Float4, row.Int, row.Float5, row.Float6, row.Date
    Assert.AreEqual(expected, actual)
