module FSharp.Data.Tests.CsvProvider

open NUnit.Framework
open FsUnit
open System
open System.IO
open FSharp.Data.UnitSystems.SI.UnitNames
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.CsvInference
open System.Globalization

let [<Literal>] simpleCsv = """
  Column1,Column2,Column3
  TRUE,no,3
  "yes", "false", 1.92 """

type SimpleCsv = CsvProvider<simpleCsv>

let [<Literal>] csvWithBitValues = """
  Flag,Status,Score
  0,yes,42
  1,no,7
  1,yes,3 """

// With StrictBooleans=true, 0/1 infer as int and yes/no infer as string
type StrictBoolCsv = CsvProvider<csvWithBitValues, StrictBooleans=true>
// Without StrictBooleans, 0/1 infer as bool and yes/no infer as bool (default)
type NonStrictBoolCsv = CsvProvider<csvWithBitValues>

[<Test>]
let ``Bool column correctly inferred and accessed`` () =
  let csv = SimpleCsv.GetSample()
  let first = csv.Rows |> Seq.head
  let actual:bool = first.Column1
  actual |> should be True

[<Test>]
let ``StrictBooleans: 0 and 1 are inferred as int not bool`` () =
  let csv = StrictBoolCsv.GetSample()
  let first = csv.Rows |> Seq.head
  let flagAsInt: int = first.Flag  // Should compile: Flag is int, not bool
  flagAsInt |> should equal 0

[<Test>]
let ``StrictBooleans: yes and no are inferred as string not bool`` () =
  let csv = StrictBoolCsv.GetSample()
  let first = csv.Rows |> Seq.head
  let statusAsString: string = first.Status  // Should compile: Status is string, not bool
  statusAsString |> should equal "yes"

[<Test>]
let ``Without StrictBooleans: 0 and 1 are inferred as bool by default`` () =
  let csv = NonStrictBoolCsv.GetSample()
  let first = csv.Rows |> Seq.head
  let flagAsBool: bool = first.Flag  // Should compile: Flag is bool
  flagAsBool |> should be False

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

let [<Literal>] csvWithGermanDate = """Preisregelung_ID;Messgebiet_Nr;gueltig_seit;gueltig_bis;ID;Status_ID;Internet;Bemerkung;Erfasser;Ersterfassung;letzte_Pruefung;letzte_Aenderung
1;184370001;01.01.2006;30.09.2007;3;2300;;;1;27.09.2006;11.07.2008;11.07.2008
2;214230001;01.02.2006;20.03.2007;2;2000;;;1;27.09.2006;28.11.2007;10.04.2007"""

[<Test>]
let ``Inference of german dates`` () =
  let csv = CsvProvider<csvWithGermanDate, ";", InferRows = 0, Culture = "de-DE", PreferDateOnly = true>.GetSample()
  let rows = csv.Rows |> Seq.toArray

  let row = rows.[1]

  let d1:DateOnly = row.Gueltig_seit
  d1 |> should equal (DateOnly(2006,02,01))

let [<Literal>] csvWithEmptyValues = """
Float1,Float2,Float3,Float4,Int,Float5,Float6,Date
1,1,1,1,,,,
2.0,#N/A,,1,1,1,,2010-01-10
,,2.0,#N/A,1,#N/A,2.0,"""

[<Test>]
let ``Inference of numbers with empty values`` () =
  let csv = CsvProvider<csvWithEmptyValues, PreferDateOnly = true>.GetSample()
  let rows = csv.Rows |> Seq.toArray

  let row = rows.[0]

  let _f1:float = row.Float1
  let _f2:float = row.Float2
  let _f3:float = row.Float3
  let _f4:float = row.Float4
  let _i:Nullable<int> = row.Int
  let _f5:float = row.Float5
  let _f6:float = row.Float6
  let _d:option<DateOnly> = row.Date

  let expected = 1.0, 1.0, 1.0, 1.0, Nullable<int>(), Double.NaN, Double.NaN, (None: DateOnly option)
  let actual = row.Float1, row.Float2, row.Float3, row.Float4, row.Int, row.Float5, row.Float6, row.Date
  actual |> should equal expected

  let row = rows.[1]
  let expected = 2.0, Double.NaN, Double.NaN, 1.0, Nullable 1, 1.0, Double.NaN, Some(DateOnly(2010, 01,10))
  let actual = row.Float1, row.Float2, row.Float3, row.Float4, row.Int, row.Float5, row.Float6, row.Date
  actual |> should equal expected

  let row = rows.[2]
  let expected = Double.NaN, Double.NaN, 2.0, Double.NaN, Nullable 1, Double.NaN, 2.0, (None: DateOnly option)
  let actual = row.Float1, row.Float2, row.Float3, row.Float4, row.Int, row.Float5, row.Float6, row.Date
  actual |> should equal expected

let [<Literal>] csvData = """DateOnly,DateWithOffset,MixedDate,OffsetOption (datetimeoffset option),OffsetNullable (datetimeoffset?)
2018-04-21,2018-04-20+10:00,2018-04-19+11:00,2018-04-20+10:00,2018-04-20+10:00
2018-04-18,2018-04-17-06:00,2018-04-16,,"""

[<Test>]
let ``Can infer DateTime and DateTimeOffset types correctly`` () =
  let csv = CsvProvider<csvData, ",", InferRows = 0, PreferDateOnly = true>.GetSample()
  let firstRow = csv.Rows |> Seq.head
  let secondRow = csv.Rows |> Seq.item 1

  firstRow.DateOnly.GetType() |> should equal typeof<DateOnly>
  firstRow.DateWithOffset.GetType() |> should equal typeof<DateTimeOffset>
  firstRow.MixedDate.GetType() |> should equal typeof<DateTime>
  firstRow.OffsetOption.GetType() |> should equal typeof<DateTimeOffset Option>
  firstRow.OffsetNullable.GetType() |> should equal typeof<DateTimeOffset>
  secondRow.OffsetOption |> should equal None
  secondRow.OffsetNullable |> should equal null


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
  let actual : string array = [| for r in csv.Rows -> r.Column1 |]
  let expected : string array = [||]
  actual |> should equal expected

[<Literal>]
let norwayCultureName = "nb-NO"

[<Test>]
let ``Does not treat invariant culture number such as 3.14 as a date in cultures using 3,14`` () =
  let targetCulture = CultureInfo(norwayCultureName)
  // Make sure assumptions about the culture hold:
  targetCulture.DateTimeFormat.DateSeparator |> should equal "."
  targetCulture.DateTimeFormat.TimeSeparator |> should equal ":" // See https://github.com/fsprojects/FSharp.Data/issues/767
  targetCulture.NumberFormat.NumberDecimalSeparator |> should equal ","
  let csv = CsvProvider<"Data/DnbHistoriskeKurser.csv", ",", 10, Culture=norwayCultureName, PreferDateOnly = true>.GetSample()
  let row = csv.Rows |> Seq.head
  (row.Dato, row.USD) |> should equal (DateOnly(2013, 2, 7), "5.4970")

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
        CsvProvider<"Data/Adwords.csv", InferRows=4, AssumeMissingValues=true>.GetSample().Rows
        |> Seq.skip 4 |> Seq.head
    let parentId : Nullable<int> = rowWithMissingParentIdNullable.``Parent ID``
    parentId |> should equal null

    let rowWithMissingParentIdOptional =
        CsvProvider<"Data/Adwords.csv", InferRows=4, AssumeMissingValues=true, PreferOptionals=true>.GetSample().Rows
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

[<Test>]
let ``Uses UTF8 for sample file when encoding not specified``() =
    let utf8 = UTF8.GetSample()
    let row2 = utf8.Rows |> Seq.skip 1 |> Seq.head
    row2 |> should equal (2, "NaN (�񐔒l)")

// #if USE_MSBUILD // only valid when running with the .NET Framework compiler
// type CP932 = CsvProvider<"Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">

// [<Test>]
// let ``Respects encoding when specified``() =
//     let cp932 = CP932.GetSample()
//     let row2 = cp932.Rows |> Seq.skip 1 |> Seq.head
//     row2 |> should equal (2, Double.NaN)
// #endif

[<Test>]
let ``Disposing CsvProvider shouldn't throw``() =
    let _csv =
        use csv = CsvProvider<"Data/TabSeparated.csv", HasHeaders=false>.GetSample()
        csv.Rows |> Seq.iter (fun _ -> ())
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
    let data = PercentageCsv.GetSample().Rows |> Seq.item 1
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
  let row = reParsed.Rows |> Seq.item 3
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
  csv.Rows |> Seq.item 0 |> should equal row1
  csv.Rows |> Seq.item 1 |> should equal row2

  csv.Headers.Value.[1]  |> should equal "ColumnB"
  let s = csv.SaveToString()
  s |> should not' (equal "")


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
  (new SimpleWithStrCsv(rows)).SaveToString() |> should equal ("Column1,ColumnB,Column3" + "\r\n" + "false,abc,31" + "\r\n" + "true, def,42" + "\r\n")
  // (new SimpleWithStrCsv(rows)).SaveToString() |> should equal ("Column1,ColumnB,Column3" + Environment.NewLine + "false,abc,31" + Environment.NewLine + "true, def,42" + Environment.NewLine)

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

[<Literal>]
let csvWithMultilineCells = """Id,Text
1,"abc,"
2,"def
ghi"
"""

[<Test>]
let ``Multiline cells saved correctly``() =
    let csv = CsvProvider<csvWithMultilineCells>.GetSample()
    csv.Rows |> Seq.map (fun r -> r.Id, r.Text.Replace("\r", "")) |> Seq.toList |> should equal [1, "abc,"; 2, "def\nghi"]
    csv.SaveToString().Replace("\r", "") |> should equal (csvWithMultilineCells.Replace("\r", ""))

[<Test>]
let ``Fields with quotes should be quoted and escaped when saved``() =
    let rowWithQuoteInField = new SimpleWithStrCsv.Row(true, "f\"oo", 1.3M)
    let csv = new SimpleWithStrCsv([rowWithQuoteInField])
    let roundTripped = SimpleWithStrCsv.Parse(csv.SaveToString())
    let rowRoundTripped = roundTripped.Rows |> Seq.exactlyOne
    rowRoundTripped |> should equal rowWithQuoteInField



type MappingType = CsvProvider<"IndicatorName, CountryName, Type, Code",
                                                        Schema = "IndicatorName (string), CountryName (string), Type (string), Code (string)",
                                                        Encoding="windows-1252",
                                                        HasHeaders=true,
                                                        SkipRows=0>
type WithName = {
    Name: string
}

type Mapping = {
    Indicator: WithName
    Country: WithName
    RelationshipType: WithName
    Code: string
}

[<Test>]
let ``Having null in a cell should not fail saving to string (issue#978)`` () =

    let Stringify (mappings:seq<Mapping>) =
                    let rows = mappings |> Seq.map(fun x -> MappingType.Row(x.Indicator.Name, x.Country.Name, x.RelationshipType.ToString(), x.Code ))
                    let output = new MappingType( rows )
                    output.SaveToString()


    let data = seq {
        yield {
            Indicator = {Name = "Ind-1" }
            Country = {Name = "Cnt-1" }
            RelationshipType = {Name = "RT-2" }
            Code = "code-1"
        }

        yield {
            Indicator = {Name = "Ind-2" }
            Country = {Name = "Cnt-2" }
            RelationshipType = {Name = "RT-2" }
            Code = ""
        }

        yield {
            Indicator = {Name = "Ind-3" }
            Country = {Name = "Cnt-3" }
            RelationshipType = {Name = "RT-3" }
            Code = null
        }

        yield {
            Indicator = {Name = "Ind-4" }
            Country = {Name = "Cnt-4" }
            RelationshipType = {Name = "RT-4" }
            Code = "null"
        }

        yield {
            Indicator = { Name = "Ind-5" }
            Country = {Name = "Cnt-5" }
            RelationshipType = {Name = "RT-5" }
            Code = "[null]"
        }
    }

    data
    |> Stringify
    |> ignore

[<Test>]
let ``CsvFile.TryGetColumnIndex returns Some matching column if a match``() =
  let csv = CsvFile.Parse simpleCsv
  let nameColumnIndex = csv.TryGetColumnIndex "Column1"
  nameColumnIndex |> should equal (Some 0)

[<Test>]
let ``CsvFile.TryGetColumnIndex returns None if no match``() =
  let csv = CsvFile.Parse simpleCsv
  let nameColumnIndex = csv.TryGetColumnIndex "FirstName"
  nameColumnIndex |> should equal None

type TimeSpanCSV = CsvProvider<"Data/TimeSpans.csv">
let row = TimeSpanCSV.GetSample().Rows |> Seq.head
[<Test>]
let ``Can parse positive time span with day and fraction``() =
    let span = row.PositiveWithDayWithFraction
    span |> should equal (new TimeSpan(1, 3, 16, 50, 500))

[<Test>]
let ``Can parse positive time span without day and without fraction``() =
    let span = row.PositiveWithoutDayWithoutFraction
    span |> should equal (new TimeSpan(0, 30, 0))

[<Test>]
let ``Can parse negative time span with day and fraction``() =
    let span = row.NegativeWithDayWithFraction
    span |> should equal (new TimeSpan(-1, -3, -16, -50, -500))

[<Test>]
let ``Parses timespan greater than max as string`` () =
    let span = row.TimespanOneTickGreaterThanMaxValue
    span.GetType() |> should equal (typeof<string>)

[<Test>]
let ``Parses timespan less than min as string`` () =
    let span = row.TimespanOneTickLessThanMinValue
    span.GetType() |> should equal (typeof<string>)

[<Test>]
let ``InferColumnTypes shall infer empty string as Double``() =
  let csv = CsvFile.Load(Path.Combine(__SOURCE_DIRECTORY__, "Data/emptyMissingValue.csv"))
  let types = csv.InferColumnTypes(2,[|""|], StructuralInference.InferenceMode'.ValuesOnly, System.Globalization.CultureInfo.GetCultureInfo(""), null, false, false, StructuralInference.defaultUnitsOfMeasureProvider)
  let expected = "Double"
  let actual = types.[3].Value.InferedType.Name
  actual |> should equal expected

let [<Literal>] manyColumnCsv = """
"001","2022-01-01 10:00:00","100.00","John S.","+1 (555) 123-4567","john@example.com","","John Smith","California","Los Angeles","123 Main St.","90001","P001","Widget A","2","25.00"
"002","2022-01-02 14:30:00","50.00","Alice T.","+1 (555) 987-6543","alice@example.com","Please deliver after 6pm.","Alice Thompson","New York","Brooklyn","456 Elm St.","10001","P002","Widget B","1","50.00"
"003","2022-01-03 08:15:00","75.00","Bob R.","+1 (555) 555-1212","bob@example.com","","Bob Robertson","Florida","Miami","789 Oak Ave.","33010","P003","Widget C","3","25.00"
"004","2022-01-04 16:00:00","200.00","Jane D.","+1 (555) 555-5555","jane@example.com","Please include gift receipt.","Jane Doe","Texas","Austin","321 Pine St.","78701","P004","Widget D","4","50.00"
"005","2022-01-05 12:00:00","60.00","Sam G.","+1 (555) 555-1212","sam@example.com","","Sam Green","California","San Francisco","987 Oak St.","94101","P005","Widget E","2","30.00"
"""

[<Test>]
let ``Can infer from a multiline schema`` () =
    let csv =
        CsvProvider<manyColumnCsv,
            HasHeaders = false,
            Schema = "OrderNumber (string),
                      OrderCreated (string),
                      OrderTotal (string),
                      FioShort (string),
                      PhoneNumber (string),
                      Email (string),
                      Comment (string),
                      FioFull (string),
                      Region (string),
                      Town (string),
                      Address (string),
                      Postindex (string),
                      ProductId (string),
                      ProductTitle (string),
                      ProductQuantity (string),
                      ProductPrice (string)">.GetSample ()
    let firstRow = csv.Rows |> Seq.head
    csv.NumberOfColumns |> should equal 16
    firstRow.OrderCreated |> should equal "2022-01-01 10:00:00"
    firstRow.FioFull |> should equal "John Smith"

// Regression test for issue #1439: InferRows must count CSV rows, not text lines.
// A multiline quoted field occupies 2 text lines but is only 1 data row.
// With InferRows=2, both data rows should be accessible (the first spans 2 lines).
type MultilineFieldsCsv = CsvProvider<"Data/MultilineFields.csv", InferRows=2>

[<Test>]
let ``InferRows counts CSV rows not text lines for multiline quoted fields`` () =
    let csv = MultilineFieldsCsv.GetSample()
    let rows = csv.Rows |> Seq.toArray
    rows.Length |> should equal 2
    rows.[0].F1 |> should equal "multi-\nline field"
    rows.[0].F2 |> should equal 2
    rows.[1].F1 |> should equal "normal"
    rows.[1].F2 |> should equal 3

let [<Literal>] csvWithLowerCaseHeaders = "lower_col,another_col\n1,hello\n2,world"

type CsvUseOriginalNames = CsvProvider<csvWithLowerCaseHeaders, UseOriginalNames = true>
type CsvCapitalizedNames = CsvProvider<csvWithLowerCaseHeaders>

[<Test>]
let ``CsvProvider UseOriginalNames=true preserves column names as-is`` () =
    let row = CsvUseOriginalNames.GetSample().Rows |> Seq.head
    row.lower_col |> should equal 1
    row.another_col |> should equal "hello"

[<Test>]
let ``CsvProvider default capitalizes first letter of column names`` () =
    let row = CsvCapitalizedNames.GetSample().Rows |> Seq.head
    row.Lower_col |> should equal 1
    row.Another_col |> should equal "hello"
