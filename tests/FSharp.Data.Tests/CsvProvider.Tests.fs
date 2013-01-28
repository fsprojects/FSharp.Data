﻿// --------------------------------------------------------------------------------------
// Tests for the CSV parsing code
// --------------------------------------------------------------------------------------

module FSharp.Data.Tests.CsvProvider.Tests

open NUnit.Framework
open FsUnit
open System
open System.IO
open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames
open FSharp.Data

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
  actual |> should be True

[<Test>]
let ``Decimal column correctly infered and accessed`` () = 
  let csv = new SimpleCsv()
  let first = csv.Data |> Seq.head
  let actual:decimal = first.Column3
  actual |> should equal 3.0M

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
  let i:Nullable<int> = row.Int
  let f5:float = row.Float5
  let f6:float = row.Float6
  let d:Nullable<DateTime> = row.Date
  
  let expected = 1.0, 1.0, 1.0, 1.0, Nullable<int>(), Double.NaN, Double.NaN, Nullable<DateTime>()
  let actual = row.Float1, row.Float2, row.Float3, row.Float4, row.Int, row.Float5, row.Float6, row.Date    
  actual |> should equal expected

  let row = rows.[1]
  let expected = 2.0, Double.NaN, Double.NaN, 1.0, Nullable 1, 1.0, Double.NaN, Nullable(new DateTime(2010, 01,10)) 
  let actual = row.Float1, row.Float2, row.Float3, row.Float4, row.Int, row.Float5, row.Float6, row.Date
  actual |> should equal expected

  let row = rows.[2]
  let expected = Double.NaN, Double.NaN, 2.0, Double.NaN, Nullable 1, Double.NaN, 2.0, Nullable<DateTime>()
  let actual = row.Float1, row.Float2, row.Float3, row.Float4, row.Int, row.Float5, row.Float6, row.Date
  actual |> should equal expected

type SmallCsv = CsvProvider<"SmallTest.csv">

[<Test>] 
let ``Can create type for small document``() =
    let row1 = (new SmallCsv()).Data |> Seq.head 

    row1.Distance |> should equal 50.<metre>
    let time = row1.Time
    time |> should equal 3.7<second>
