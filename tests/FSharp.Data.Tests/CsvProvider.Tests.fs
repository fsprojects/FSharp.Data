// --------------------------------------------------------------------------------------
// Tests for the CSV parsing code
// --------------------------------------------------------------------------------------

module FSharp.Data.Tests.CsvProvider.Tests

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

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
  let d:option<DateTime> = row.Date
  
  let expected = 1.0, 1.0, 1.0, 1.0, Nullable<int>(), Double.NaN, Double.NaN, None
  let actual = row.Float1, row.Float2, row.Float3, row.Float4, row.Int, row.Float5, row.Float6, row.Date    
  actual |> shouldEqual expected

  let row = rows.[1]
  let expected = 2.0, Double.NaN, Double.NaN, 1.0, Nullable 1, 1.0, Double.NaN, Some(new DateTime(2010, 01,10)) 
  let actual = row.Float1, row.Float2, row.Float3, row.Float4, row.Int, row.Float5, row.Float6, row.Date
  actual |> should equal expected

  let row = rows.[2]
  let expected = Double.NaN, Double.NaN, 2.0, Double.NaN, Nullable 1, Double.NaN, 2.0, None
  let actual = row.Float1, row.Float2, row.Float3, row.Float4, row.Int, row.Float5, row.Float6, row.Date
  actual |> shouldEqual expected

[<Test>] 
let ``Can create type for small document``() =
  let csv = new CsvProvider<"Data/SmallTest.csv">()
  let row1 = csv.Data |> Seq.head 
  row1.Distance |> should equal 50.<metre>
  let time = row1.Time
  time |> should equal 3.7<second>

[<Test>] 
let ``Can parse sample file with whitespace in the name``() =
  let csv = new CsvProvider<"Data/file with spaces.csv">()
  let row1 = csv.Data |> Seq.head 
  row1.Distance |> should equal 50.<metre>
  let time = row1.Time
  time |> should equal 3.7<second>

[<Test>]
let ``Infers type of an emtpy CSV file`` () = 
  let csv = new CsvProvider<"Column1, Column2">()
  let actual : string list = [ for r in csv.Data -> r.Column1 ]
  actual |> shouldEqual []

[<Test>]
let ``Does not treat invariant culture number such as 3.14 as a date in cultures using 3,14`` () =
  let csv = new CsvProvider<"Data/DnbHistoriskeKurser.csv", ",", "nb-NO", 10>()
  let row = csv.Data |> Seq.head
  (row.Dato, row.Usd) |> shouldEqual (DateTime(2013, 2, 7), "5.4970")
 
let [<Literal>] simpleCsvNoHeaders = """
TRUE,no,3
"yes", "false", 1.92 """

  
[<Test>]
let ``Bool column correctly infered and accessed when headers are given in provider`` () = 
    let csv = new CsvProvider<simpleCsvNoHeaders, Headers = "Col1,Col2,Col3">()
    let first = csv.Data |> Seq.head
    let actual:bool = first.Col1
    Assert.AreEqual(true, actual)

[<Test>]
let ``Decimal column correctly infered and accessed when headers are given in provider`` () = 
    let csv = new CsvProvider<simpleCsvNoHeaders, Headers = "Col1,Col2,Col3">()
    let first = csv.Data |> Seq.head
    let actual:decimal = first.Col3
    Assert.AreEqual(3.0M, actual)

[<Test>]
let ``Can skip rows with no headers``() =
   let csv = new CsvProvider<simpleCsvNoHeaders, Headers = "Col1,Col2,Col3", SkipLines=1>()
   let first = csv.Data |> Seq.head
   let actual:decimal = first.Col3
   Assert.AreEqual(1.92M, actual)  

let [<Literal>] csvWithLinesToignore = """
  ignorethis
  ignorethis
  Column1,Column2,Column3
  TRUE,no,3
  "yes", "false", 1.92 """


[<Test>]
let ``Can skip lines and still infer headers``() =
   let csv = new CsvProvider<csvWithLinesToignore, SkipLines=2>()
   let first = csv.Data |> Seq.head
   let actual:decimal = first.Column3
   Assert.AreEqual(3M, actual)  

[<Test>]
let ``Can skip lines and override headers``() =
   let csv = new CsvProvider<csvWithLinesToignore, Headers="Col1,Col2,Col3", SkipLines=3>()
   let first = csv.Data |> Seq.head
   let actual:decimal = first.Col3
   Assert.AreEqual(3M, actual)  

let [<Literal>] csvWithExtraComma = """
  Column1,Column2,Column3,
  TRUE,no,3,
  "yes", "false", 1.92, """

[<Test>]
let ``Names columns with no names as Unknownx``() =
   let csv = new CsvProvider<csvWithExtraComma>()
   let first = csv.Data |> Seq.head
   let actual = first.Unknown3
   Assert.AreEqual("", actual)  