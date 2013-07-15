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

[<Test>]
let ``Guid column correctly infered and accessed`` () = 
  let csv = new CsvProvider<"Data/LastFM.tsv", HasHeaders=false>()
  let first = csv.Data |> Seq.head
  let actual:Guid option = first.Column3
  actual |> should equal (Some (Guid.Parse("f1b1cf71-bd35-4e99-8624-24a6e15f133a")))

let [<Literal>] csvWithEmptyValues = """
Float1,Float2,Float3,Float4,Int,Float5,Float6,Date
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
  (row.Dato, row.USD) |> shouldEqual (DateTime(2013, 2, 7), "5.4970")

[<Test>]
let ``Empty lines are skipped and don't make everything optional`` () =
  let csv = new CsvProvider<"Data/banklist.csv">()
  let row = csv.Data |> Seq.head
  row.``Bank Name`` |> should equal "Alabama Trust Bank, National Association"
  row.``CERT #`` |> should equal 35224

[<Literal>]
let csvWithRepeatedAndEmptyColumns = """Foo3,Foo3,Bar,
,2,3,
,4,6,"""

[<Test>]
let ``Repeated and empty column names``() = 
  let csv = new CsvProvider<csvWithRepeatedAndEmptyColumns>()
  let row = csv.Data |> Seq.head
  row.Foo3.GetType() |> should equal typeof<string>
  row.Foo4.GetType() |> should equal typeof<int>
  row.Bar.GetType() |> should equal typeof<int>
  row.Column4.GetType() |> should equal typeof<string>  
  
let [<Literal>] simpleCsvNoHeaders = """
TRUE,no,3
"yes", "false", 1.92 """

[<Test>]
let ``Columns correctly inferred and accessed when headers are missing`` () = 
    let csv = new CsvProvider<simpleCsvNoHeaders, HasHeaders=false>()
    let row = csv.Data |> Seq.head
    let col1:bool = row.Column1
    let col2:bool = row.Column2
    let col3:decimal = row.Column3
    col1 |> should equal true
    col2 |> should equal false
    col3 |> should equal 3.0M
    let row = csv.Data |> Seq.skip 1 |> Seq.head
    let col1:bool = row.Column1
    let col2:bool = row.Column2
    let col3:decimal = row.Column3
    col1 |> should equal true
    col2 |> should equal false
    col3 |> should equal 1.92M

[<Test>]
let ``IgnoreErrors skips lines with wrong number of columns`` () = 
    let csv = new CsvProvider<"a,b,c\n1,2\n0,1,2,3,4\n2,3,4", IgnoreErrors=true>()
    let row = csv.Data |> Seq.head
    row |> should equal (2,3,4)

[<Test>]
let ``Lines with wrong number of columns throw exception when ignore errors is set to false`` () = 
    let csv = CsvProvider<"a,b,c">.Parse("a,b,c\n1,2\n0,1,2,3,4\n2,3,4")
    (fun () -> csv.Data |> Seq.head |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``IgnoreErrors skips lines with wrong number of columns when there's no header`` () = 
    let csv = new CsvProvider<"1,2\n0,1,2,3,4\n2,3,4\n5,6", IgnoreErrors=true, HasHeaders=false>()
    let row1 = csv.Data |> Seq.head
    let row2 = csv.Data |> Seq.skip 1 |> Seq.head
    row1 |> should equal (1,2)
    row2 |> should equal (5,6)

[<Test>]
let ``IgnoreErrors skips lines with wrong types`` () = 
    let csv = new CsvProvider<"a (int),b (int),c (int)\nx,y,c\n2,3,4", IgnoreErrors=true>()
    let row = csv.Data |> Seq.head
    row |> should equal (2,3,4)

[<Test>]
let ``Lines with wrong types throw exception when ignore errors is set to false`` () = 
    let csv = new CsvProvider<"a (int),b (int),c (int)\nx,y,z\n2,3,4">()
    (fun () -> csv.Data |> Seq.head |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``Columns explicitly overrided to string option should return None when empty or whitespace`` () = 
    let csv = new CsvProvider<"a,b,c\n , ,1\na,b,2",Schema=",string option,int option">()
    let rows = csv.Data |> Seq.toArray
    let row1 = rows.[0]
    let row2 = rows.[1]
    row1.a |> should equal ""
    row1.b |> should equal None
    row1.c |> should equal (Some 1)
    row2 |> should equal ("a", Some "b", Some 2)

[<Test>]
let ``NaN's should work correctly when using option types`` () = 
    let csv = new CsvProvider<"a,b\n1,\n:,1.0", Schema="float option,float option">()
    let rows = csv.Data |> Seq.toArray
    let row1 = rows.[0]
    let row2 = rows.[1]
    row1.a |> should equal (Some 1.0)
    row1.b |> should equal None
    row2.a |> should equal None
    row2.b |> should equal (Some 1.0)
    

