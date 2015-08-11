module FSharp.Data.Tests.CsvProvider

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
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
let ``Bool column correctly inferred and accessed`` () = 
  let csv = SimpleCsv.GetSample()
  let first = csv.Rows |> Seq.head
  let actual:bool = first.Column1
  actual |> should be True

[<Test>]
let ``Decimal column correctly inferred and accessed`` () = 
  let csv = SimpleCsv.GetSample()
  let first = csv.Rows |> Seq.head
  let actual:decimal = first.Column3
  actual |> should equal 3.0M

[<Test>]
let ``Guid column correctly inferred and accessed from mislabeled TSV`` () = 
  let csv = CsvProvider<"Data/TabSeparated.csv", HasHeaders=false>.GetSample()
  let first = csv.Rows |> Seq.head
  let actual:Guid option = first.Column3
  actual |> should equal (Some (Guid.Parse("f1b1cf71-bd35-4e99-8624-24a6e15f133a")))

[<Test>]
let ``Guid column correctly infered and accessed`` () = 
  let csv = CsvProvider<"Data/LastFM.tsv", HasHeaders=false>.GetSample()
  let first = csv.Rows |> Seq.head
  let actual:Guid option = first.Column3
  actual |> should equal (Some (Guid.Parse("f1b1cf71-bd35-4e99-8624-24a6e15f133a")))

let [<Literal>] csvWithEmptyValues = """
Float1,Float2,Float3,Float4,Int,Float5,Float6,Date
1,1,1,1,,,,
2.0,#N/A,,1,1,1,,2010-01-10
,,2.0,#N/A,1,#N/A,2.0,"""

[<Test>]
let ``Inference of numbers with empty values`` () = 
  let csv = CsvProvider<csvWithEmptyValues>.GetSample()
  let rows = csv.Rows |> Seq.toArray
  
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
  let csv = CsvProvider<"Data/SmallTest.csv">.GetSample()
  let row1 = csv.Rows |> Seq.head 
  row1.Distance |> should equal 50.<metre>
  let time = row1.Time
  time |> should equal 3.7<second>

[<Test>]
let ``CsvFile.Rows is re-entrant if the underlying stream is``() =
  let csv = CsvFile.Load(Path.Combine(__SOURCE_DIRECTORY__, "Data/SmallTest.csv"))
  let twice = [ yield! csv.Rows; yield! csv.Rows ]
  twice |> Seq.length |> should equal 6

[<Test>] 
let ``Can parse sample file with whitespace in the name``() =
  let csv = CsvProvider<"Data/file with spaces.csv">.GetSample()
  let row1 = csv.Rows |> Seq.head 
  row1.Distance |> should equal 50.<metre>
  let time = row1.Time
  time |> should equal 3.7<second>

[<Test>]
let ``Infers type of an emtpy CSV file`` () = 
  let csv = CsvProvider<"Column1, Column2">.GetSample()
  let actual : string list = [ for r in csv.Rows -> r.Column1 ]
  actual |> shouldEqual []

[<Test>]
let ``Does not treat invariant culture number such as 3.14 as a date in cultures using 3,14`` () =
  let csv = CsvProvider<"Data/DnbHistoriskeKurser.csv", ",", 10, Culture="fr-FR">.GetSample()
  let row = csv.Rows |> Seq.head
  (row.Dato, row.USD) |> shouldEqual (DateTime(2013, 2, 7), "5.4970")

[<Test>]
let ``Empty lines are skipped and don't make everything optional`` () =
  let csv = CsvProvider<"Data/banklist.csv">.GetSample()
  let row = csv.Rows |> Seq.head
  row.``Bank Name`` |> should equal "Alabama Trust Bank, National Association"
  row.``CERT #`` |> should equal 35224

[<Literal>]
let csvWithRepeatedAndEmptyColumns = """Foo3,Foo3,Bar,
,2,3,
,4,6,"""

[<Test>]
let ``Repeated and empty column names``() = 
  let csv = CsvProvider<csvWithRepeatedAndEmptyColumns>.GetSample()
  let row = csv.Rows |> Seq.head
  row.Foo3.GetType() |> should equal typeof<string>
  row.Foo4.GetType() |> should equal typeof<int>
  row.Bar.GetType() |> should equal typeof<int>
  row.Column4.GetType() |> should equal typeof<string>  

[<Literal>]
let csvWithSpuriousTrailingEmptyHeaderColumn = """A,B,C,
1,2,3
4,5,6"""

[<Test>]
let ``Header with trailing empty column that doesn't appear in data rows``()=
  let csv = CsvProvider<csvWithSpuriousTrailingEmptyHeaderColumn>.GetSample()
  let row = csv.Rows |> Seq.head
  row |> should equal (1,2,3)
  let row = csv.Rows |> Seq.skip 1 |> Seq.head
  row |> should equal (4,5,6)

[<Literal>]
let csvWithLegitimateTrailingEmptyColumn = """A,B,C,
1,2,3,4
5,6,7,8"""

[<Test>]
let ``Header with trailing empty column that does appear in data rows``() =
  let csv = CsvProvider<csvWithLegitimateTrailingEmptyColumn>.GetSample()
  let row = csv.Rows |> Seq.head
  row |> should equal (1,2,3,4)
  let row = csv.Rows |> Seq.skip 1 |> Seq.head
  row |> should equal (5,6,7,8)
  
let [<Literal>] simpleCsvNoHeaders = """
TRUE,no,3
"yes", "false", 1.92 """

[<Test>]
let ``Columns correctly inferred and accessed when headers are missing`` () = 
    let csv = CsvProvider<simpleCsvNoHeaders, HasHeaders=false>.GetSample()
    let row = csv.Rows |> Seq.head
    let col1:bool = row.Column1
    let col2:bool = row.Column2
    let col3:decimal = row.Column3
    col1 |> should equal true
    col2 |> should equal false
    col3 |> should equal 3.0M
    let row = csv.Rows |> Seq.skip 1 |> Seq.head
    let col1:bool = row.Column1
    let col2:bool = row.Column2
    let col3:decimal = row.Column3
    col1 |> should equal true
    col2 |> should equal false
    col3 |> should equal 1.92M

[<Test>]
let ``IgnoreErrors skips lines with wrong number of columns`` () = 
    let csv = CsvProvider<"a,b,c\n1,2\n0,1,2,3,4\n2,3,4", IgnoreErrors=true>.GetSample()
    let row = csv.Rows |> Seq.head
    row |> should equal (2,3,4)

[<Test>]
let ``Lines with wrong number of columns throw exception when ignore errors is set to false`` () = 
    let csv = CsvProvider<"a,b,c">.Parse("a,b,c\n1,2\n0,1,2,3,4\n2,3,4")
    (fun () -> csv.Rows |> Seq.head |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``IgnoreErrors skips lines with wrong number of columns when there's no header`` () = 
    let csv = CsvProvider<"1,2\n0,1,2,3,4\n2,3,4\n5,6", IgnoreErrors=true, HasHeaders=false>.GetSample()
    let row1 = csv.Rows |> Seq.head
    let row2 = csv.Rows |> Seq.skip 1 |> Seq.head
    row1 |> should equal (1,2)
    row2 |> should equal (5,6)

[<Test>]
let ``IgnoreErrors skips lines with wrong types`` () = 
    let csv = CsvProvider<"a (int),b (int),c (int)\nx,y,c\n2,3,4", IgnoreErrors=true>.GetSample()
    let row = csv.Rows |> Seq.head
    row |> should equal (2,3,4)

[<Test>]
let ``Lines with wrong types throw exception when ignore errors is set to false`` () = 
    let csv = CsvProvider<"a (int),b (int),c (int)\nx,y,z\n2,3,4">.GetSample()
    (fun () -> csv.Rows |> Seq.head |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``Columns explicitly overrided to string option should return None when empty or whitespace`` () = 
    let csv = CsvProvider<"a,b,c\n , ,1\na,b,2",Schema=",string option,int option">.GetSample()
    let rows = csv.Rows |> Seq.toArray
    let row1 = rows.[0]
    let row2 = rows.[1]
    row1.A |> should equal ""
    row1.B |> should equal None
    row1.C |> should equal (Some 1)
    row2 |> should equal ("a", Some "b", Some 2)

[<Test>]
let ``NaN's should work correctly when using option types`` () = 
    let csv = CsvProvider<"a,b\n1,\n:,1.0", Schema="float option,float option">.GetSample()
    let rows = csv.Rows |> Seq.toArray
    let row1 = rows.[0]
    let row2 = rows.[1]
    row1.A |> should equal (Some 1.0)
    row1.B |> should equal None
    row2.A |> should equal None
    row2.B |> should equal (Some 1.0)
    
[<Test>]
let ``Currency symbols on decimal columns should work``() =
    let csv = CsvProvider<"$66.92,0.9458,Jan-13,0,0,0,1", HasHeaders=false, Culture="en-US">.GetSample()
    let row = csv.Rows |> Seq.head
    row.Column1 : decimal |> should equal 66.92M

[<Test>]
let ``AssumeMissingValues works when inferRows limit is reached``() = 
    let errorMessage =
        try
            (CsvProvider<"Data/Adwords.csv", InferRows=4>.GetSample().Rows
             |> Seq.skip 4 |> Seq.head).``Parent ID``.ToString()
        with e -> e.Message
    errorMessage |> should equal "Couldn't parse row 5 according to schema: Parent ID is missing"

    let rowWithMissingParentIdNullable = 
        CsvProvider<"Data/AdWords.csv", InferRows=4, AssumeMissingValues=true>.GetSample().Rows
        |> Seq.skip 4 |> Seq.head
    let parentId : Nullable<int> = rowWithMissingParentIdNullable.``Parent ID``
    parentId |> should equal null

    let rowWithMissingParentIdOptional = 
        CsvProvider<"Data/AdWords.csv", InferRows=4, AssumeMissingValues=true, PreferOptionals=true>.GetSample().Rows
        |> Seq.skip 4 |> Seq.head
    let parentId : Option<int> = rowWithMissingParentIdOptional.``Parent ID``
    parentId |> should equal None

type CsvWithSampleWhichIsAValidFilename = CsvProvider<Sample="1;2;3", HasHeaders=false, Separators=";">

[<Test>]
let ``Sample which also is a valid filename``() = 
    let row = CsvWithSampleWhichIsAValidFilename.GetSample().Rows |> Seq.exactlyOne
    row.Column1 |> should equal 1
    row.Column2 |> should equal 2
    row.Column3 |> should equal 3

type CsvWithoutSample = CsvProvider<Schema="category (string), id (string), timestamp (string)", HasHeaders=false>

[<Test>]
let ``Csv without sample``() = 
    let row = CsvWithoutSample.Parse("1,2,3").Rows |> Seq.exactlyOne
    row.Category |> should equal "1"
    row.Id |> should equal "2"
    row.Timestamp |> should equal "3"

type UTF8 = CsvProvider<"Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
type CP932 = CsvProvider<"Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">

[<Test>]
let ``Uses UTF8 for sample file when encoding not specified``() =
    let utf8 = UTF8.GetSample()
    let row2 = utf8.Rows |> Seq.skip 1 |> Seq.head
    row2 |> should equal (2, "NaN (�񐔒l)")

[<Test>]
let ``Respects encoding when specified``() =
    let cp932 = CP932.GetSample()
    let row2 = cp932.Rows |> Seq.skip 1 |> Seq.head
    row2 |> should equal (2, Double.NaN)

[<Test>]
let ``Disposing CsvProvider shouldn't throw``() =
    let csv = 
        use csv = CsvProvider<"Data/TabSeparated.csv", HasHeaders=false>.GetSample()
        csv.Rows |> Seq.iter (fun x -> ())
    ()

[<Test>]
let ``Whitespace is considered null, not string``() =
    let rows = CsvProvider<"  ,2.3  \n 1,\t", HasHeaders=false>.GetSample().Rows |> Seq.toArray
    rows.[0].Column1 |> should equal null
    rows.[1].Column1 |> should equal (Nullable 1)
    rows.[0].Column2 |> should equal 2.3M
    rows.[1].Column2 |> should equal (Double.NaN)

[<Test>]
let ``Extra whitespace is not removed``() =
    let rows = CsvProvider<" a ,2.3  \n 1,\tb", HasHeaders=false>.GetSample().Rows |> Seq.toArray
    rows.[0].Column1 |> should equal " a "
    rows.[1].Column1 |> should equal " 1"
    rows.[0].Column2 |> should equal "2.3  "
    rows.[1].Column2 |> should equal "\tb"

let [<Literal>] percentageCsv = """
  Column1,Column2,Column3
  TRUE,no,3
  "yes", "false", 1.92%"""
  
type PercentageCsv = CsvProvider<percentageCsv>

[<Test>]
let ``Can handle percentages in the values``() = 
    let data = PercentageCsv.GetSample().Rows |> Seq.nth 1
    data.Column3 |> should equal 1.92M

let [<Literal>] currency = """
  Column1,Column2,Column3
  £1, $2, £3
  £4, $5, £6"""

type Currency = CsvProvider<currency>

[<Test>]
let ``Can handle currency in the values``() = 
   let data = Currency.GetSample().Rows |> Seq.head
   data.Column3 |> should equal 3M

[<Test>]
let ``Can parse http://databank.worldbank.org/data/download/GDP.csv``() = 
   let gdp = new CsvProvider<"Data/GDP.csv", SkipRows=3, MissingValues="..">()
   let firstRow = gdp.Rows |> Seq.head
   firstRow.Ranking |> should equal 1
   firstRow.Column1 |> should equal "USA"
   firstRow.Economy |> should equal "United States"   
   firstRow.``US dollars)`` |> should equal 16800000.


let [<Literal>] simpleWithStrCsv = """
  Column1,ColumnB,Column3
  TRUE,abc,3
  "yes","Freddy", 1.92 """


type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>

[<Test>]
let ``Can duplicate own rows``() = 
  let csv = SimpleWithStrCsv.GetSample()
  let csv' = csv.Append csv.Rows
  let out = csv'.SaveToString()
  let reParsed = SimpleWithStrCsv.Parse(out)
  reParsed.Rows |> Seq.length |> should equal 4
  let row = reParsed.Rows |> Seq.nth 3
  row.Column1 |> should equal true
  row.ColumnB |> should equal "Freddy"
  row.Column3 |> should equal 1.92

[<Test>]
let ``Create particular row``() = 
  let row = new SimpleWithStrCsv.Row(true, "Second col", 42.5M)

  row.Column1 |> should equal true
  row.ColumnB |> should equal "Second col"
  row.Column3 |> should equal 42.5M

[<Test>]
let ``Can set created rows``() = 
  let row1 = new SimpleWithStrCsv.Row(true, "foo", 1.3M)
  let row2 = new SimpleWithStrCsv.Row(column1 = false, columnB = "foo", column3 = 42M)
  let csv = new SimpleWithStrCsv([row1; row2])
  csv.Rows |> Seq.nth 0 |> should equal row1
  csv.Rows |> Seq.nth 1 |> should equal row2

  csv.Headers.Value.[1]  |> should equal "ColumnB"
  let s = csv.SaveToString()
  s |> should notEqual ""


type CsvUom = CsvProvider<"Data/SmallTest.csv">
[<Test>] 
let ``Can create new csv row with units of measure``() =
  let row = new CsvUom.Row("name", 3.5M<metre>, 27M<Data.UnitSystems.SI.UnitSymbols.s>)
  row.Distance |> should equal 3.5M<metre>
  
[<Test>]
let ``Parse single row``() = 
  let rows = SimpleWithStrCsv.ParseRows("""false,"Quoted, col", 31""")
  rows.Length |> should equal 1
  rows.[0].Column1 |> should equal false
  rows.[0].ColumnB |> should equal "Quoted, col"
  rows.[0].Column3 |> should equal 31

[<Test>]
let ``Parse single row with trailing newline``() = 
  let rows = SimpleWithStrCsv.ParseRows("false,abc, 31\n")
  rows.Length |> should equal 1
  rows.[0].Column1 |> should equal false
  rows.[0].ColumnB |> should equal "abc"
  rows.[0].Column3 |> should equal 31

[<Test>]
let ``Parse two rows``() = 
  let rows = SimpleWithStrCsv.ParseRows("false,abc, 31\ntrue, def, 42")
  rows.Length |> should equal 2
  (new SimpleWithStrCsv(rows)).SaveToString() |> should equal ("Column1,ColumnB,Column3" + Environment.NewLine + "false,abc,31" + Environment.NewLine + "true, def,42" + Environment.NewLine)

let [<Literal>] csvWithDataEndingWithSeparator = """
Name|Company |Email|Password
Johnson|ABC|johnson@abc.com|12345i|
Yoda|XYZ|yoda@xyz.com|98123"""

[<Test>]    
let ``Accepts data rows ending with separator when header length matches to the row length``() = 
  let csv = CsvProvider<csvWithDataEndingWithSeparator, Separators="|", IgnoreErrors=true>.GetSample()
  let row1 = csv.Rows |> Seq.head
  row1 |> should equal ("Johnson","ABC","johnson@abc.com","12345i")
  let row2 = csv.Rows |> Seq.skip 1 |> Seq.head
  row2 |> should equal ("Yoda","XYZ","yoda@xyz.com","98123")

let [<Literal>] csvWithDataEndingWithSeparatorFollowedByContent = """
Name|Company |Email|Password
Doe|QWE|johnson@abc.com|32167|x@y.z
Yoda|XYZ|yoda@xyz.com|98123"""

[<Test>]
let ``Rejects data rows ending with extra non-empty content``() = 
  let csv = CsvProvider<csvWithDataEndingWithSeparatorFollowedByContent, Separators="|", IgnoreErrors=true>.GetSample()
  let row = csv.Rows |> Seq.exactlyOne
  row |> should equal ("Yoda","XYZ","yoda@xyz.com", 98123)

let [<Literal>] csvWithDataEndingWithSeparatorFollowedBySeparators = """
Name|Company |Email|Password
Johnson|ABC|johnson@abc.com|12345i||||
Doe|QWE|johnson@abc.com|32167|x@y.z||
Yoda|XYZ|yoda@xyz.com|98123|"""

[<Test>]
let ``Rejects data rows ending with two or more separators when header length matches to the rest of the row length``() = 
  let csv = CsvProvider<csvWithDataEndingWithSeparatorFollowedBySeparators, Separators="|", IgnoreErrors=true>.GetSample()
  let row = csv.Rows |> Seq.exactlyOne
  row |> should equal ("Yoda","XYZ","yoda@xyz.com", 98123)