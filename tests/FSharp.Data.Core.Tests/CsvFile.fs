// --------------------------------------------------------------------------------------
// Tests for the CsvFile and CsvRow functionality
// --------------------------------------------------------------------------------------

module FSharp.Data.Tests.CsvFile

open NUnit.Framework
open FsUnit
open System.IO
open FSharp.Data
open FSharp.Data.CsvExtensions

// Sample CSV data for testing
let sampleCsvData = """Name,Age,City
John,25,New York
Jane,30,London
Bob,35,Paris"""

let sampleCsvNoHeaders = """John,25,New York
Jane,30,London
Bob,35,Paris"""

let sampleCsvWithQuotes = """Name,Description,Score
Alice,"Software Engineer, Senior",95
Bob,"Product Manager""s Assistant",88
Charlie,"Data Scientist
with ML focus",92"""

[<Test>]
let ``CsvFile.Parse works with headers`` () =
    let csv = CsvFile.Parse(sampleCsvData)
    
    csv.Headers |> should equal (Some [| "Name"; "Age"; "City" |])
    csv.NumberOfColumns |> should equal 3
    let rows = csv.Rows |> Array.ofSeq
    rows.Length |> should equal 3
    rows.[0].Columns |> should equal [| "John"; "25"; "New York" |]

[<Test>]
let ``CsvFile.Parse works without headers`` () =
    let csv = CsvFile.Parse(sampleCsvNoHeaders, hasHeaders=false)
    
    csv.Headers |> should equal None
    csv.NumberOfColumns |> should equal 3
    let rows = csv.Rows |> Array.ofSeq
    rows.Length |> should equal 3
    rows.[0].Columns |> should equal [| "John"; "25"; "New York" |]

[<Test>]
let ``CsvFile.Parse handles custom separators`` () =
    let csvData = "Name;Age;City\nJohn;25;New York"
    let csv = CsvFile.Parse(csvData, separators=";")
    
    csv.Headers |> should equal (Some [| "Name"; "Age"; "City" |])
    let row = csv.Rows |> Seq.head
    row.Columns |> should equal [| "John"; "25"; "New York" |]

[<Test>]
let ``CsvFile.Parse handles custom quotes`` () =
    let csvData = "Name,Description\nAlice,'Software Engineer, Senior'"
    let csv = CsvFile.Parse(csvData, quote=''')
    
    let row = csv.Rows |> Seq.head
    row.Columns |> should equal [| "Alice"; "Software Engineer, Senior" |]

[<Test>]
let ``CsvFile.Parse handles skipRows`` () =
    let csvData = """Comment line 1
Comment line 2
Name,Age
John,25
Jane,30"""
    let csv = CsvFile.Parse(csvData, skipRows=2)
    
    csv.Headers |> should equal (Some [| "Name"; "Age" |])
    let rows = csv.Rows |> Array.ofSeq
    rows.Length |> should equal 2
    rows.[0].Columns |> should equal [| "John"; "25" |]

[<Test>]
let ``CsvFile.Parse handles ignoreErrors`` () =
    let csvData = """Name,Age,City
John,25,New York
Jane,30  // This row has only 2 columns
Bob,35,Paris"""
    let csv = CsvFile.Parse(csvData, ignoreErrors=true)
    
    let rows = csv.Rows |> Array.ofSeq
    // Should ignore the malformed row
    rows.Length |> should equal 2
    rows.[0].Columns |> should equal [| "John"; "25"; "New York" |]
    rows.[1].Columns |> should equal [| "Bob"; "35"; "Paris" |]

[<Test>]
let ``CsvFile.Load from stream works`` () =
    use stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sampleCsvData))
    let csv = CsvFile.Load(stream)
    
    csv.Headers |> should equal (Some [| "Name"; "Age"; "City" |])
    let rows = csv.Rows |> Array.ofSeq
    rows.Length |> should equal 3

[<Test>]
let ``CsvFile.Load from TextReader works`` () =
    use reader = new StringReader(sampleCsvData)
    let csv = CsvFile.Load(reader)
    
    csv.Headers |> should equal (Some [| "Name"; "Age"; "City" |])
    let rows = csv.Rows |> Array.ofSeq
    rows.Length |> should equal 3

[<Test>]
let ``GetColumnIndex returns correct index for valid column`` () =
    let csv = CsvFile.Parse(sampleCsvData)
    
    csv.GetColumnIndex("Name") |> should equal 0
    csv.GetColumnIndex("Age") |> should equal 1
    csv.GetColumnIndex("City") |> should equal 2

[<Test>]
let ``GetColumnIndex throws for invalid column`` () =
    let csv = CsvFile.Parse(sampleCsvData)
    
    Assert.Throws<System.Collections.Generic.KeyNotFoundException>(fun () -> csv.GetColumnIndex("InvalidColumn") |> ignore) |> ignore

[<Test>]
let ``TryGetColumnIndex returns Some for valid column`` () =
    let csv = CsvFile.Parse(sampleCsvData)
    
    csv.TryGetColumnIndex("Name") |> should equal (Some 0)
    csv.TryGetColumnIndex("Age") |> should equal (Some 1)
    csv.TryGetColumnIndex("City") |> should equal (Some 2)

[<Test>]
let ``TryGetColumnIndex returns None for invalid column`` () =
    let csv = CsvFile.Parse(sampleCsvData)
    
    csv.TryGetColumnIndex("InvalidColumn") |> should equal None

[<Test>]
let ``CsvRow GetColumn by index works`` () =
    let csv = CsvFile.Parse(sampleCsvData)
    let row = csv.Rows |> Seq.head
    
    row.GetColumn(0) |> should equal "John"
    row.GetColumn(1) |> should equal "25"
    row.GetColumn(2) |> should equal "New York"

[<Test>]
let ``CsvRow GetColumn by name works`` () =
    let csv = CsvFile.Parse(sampleCsvData)
    let row = csv.Rows |> Seq.head
    
    row.GetColumn("Name") |> should equal "John"
    row.GetColumn("Age") |> should equal "25"
    row.GetColumn("City") |> should equal "New York"

[<Test>]
let ``CsvRow indexer by int works`` () =
    let csv = CsvFile.Parse(sampleCsvData)
    let row = csv.Rows |> Seq.head
    
    row.[0] |> should equal "John"
    row.[1] |> should equal "25"
    row.[2] |> should equal "New York"

[<Test>]
let ``CsvRow indexer by string works`` () =
    let csv = CsvFile.Parse(sampleCsvData)
    let row = csv.Rows |> Seq.head
    
    row.["Name"] |> should equal "John"
    row.["Age"] |> should equal "25"
    row.["City"] |> should equal "New York"

[<Test>]
let ``CsvRow dynamic operator works`` () =
    let csv = CsvFile.Parse(sampleCsvData)
    let row = csv.Rows |> Seq.head
    
    row?Name |> should equal "John"
    row?Age |> should equal "25"
    row?City |> should equal "New York"

// Tests for StringExtensions
[<Test>]
let ``StringExtensions.AsInteger works with valid input`` () =
    "123".AsInteger() |> should equal 123
    "-456".AsInteger() |> should equal -456

[<Test>]
let ``StringExtensions.AsInteger throws with invalid input`` () =
    Assert.Throws<System.Exception>(fun () -> "not a number".AsInteger() |> ignore) |> ignore

[<Test>]
let ``StringExtensions.AsInteger64 works with valid input`` () =
    "123456789012345".AsInteger64() |> should equal 123456789012345L
    "-987654321098765".AsInteger64() |> should equal -987654321098765L

[<Test>]
let ``StringExtensions.AsInteger64 throws with invalid input`` () =
    Assert.Throws<System.Exception>(fun () -> "not a number".AsInteger64() |> ignore) |> ignore

[<Test>]
let ``StringExtensions.AsDecimal works with valid input`` () =
    "123.45".AsDecimal() |> should equal 123.45m
    "-67.89".AsDecimal() |> should equal -67.89m

[<Test>]
let ``StringExtensions.AsDecimal throws with invalid input`` () =
    Assert.Throws<System.Exception>(fun () -> "not a decimal".AsDecimal() |> ignore) |> ignore

[<Test>]
let ``StringExtensions.AsFloat works with valid input`` () =
    "123.45".AsFloat() |> should equal 123.45
    "-67.89".AsFloat() |> should equal -67.89

[<Test>]
let ``StringExtensions.AsFloat throws with invalid input`` () =
    Assert.Throws<System.Exception>(fun () -> "not a float".AsFloat() |> ignore) |> ignore

[<Test>]
let ``StringExtensions.AsBoolean works with valid input`` () =
    "true".AsBoolean() |> should equal true
    "false".AsBoolean() |> should equal false
    "True".AsBoolean() |> should equal true
    "False".AsBoolean() |> should equal false
    "1".AsBoolean() |> should equal true
    "0".AsBoolean() |> should equal false

[<Test>]
let ``StringExtensions.AsBoolean throws with invalid input`` () =
    Assert.Throws<System.Exception>(fun () -> "not a boolean".AsBoolean() |> ignore) |> ignore

[<Test>]
let ``StringExtensions.AsDateTime works with valid input`` () =
    let result = "2023-12-25".AsDateTime()
    result.Year |> should equal 2023
    result.Month |> should equal 12
    result.Day |> should equal 25

[<Test>]
let ``StringExtensions.AsDateTime throws with invalid input`` () =
    Assert.Throws<System.Exception>(fun () -> "not a date".AsDateTime() |> ignore) |> ignore

[<Test>]
let ``StringExtensions.AsGuid works with valid input`` () =
    let guidString = "12345678-1234-1234-1234-123456789abc"
    let result = guidString.AsGuid()
    result.ToString() |> should equal guidString

[<Test>]
let ``StringExtensions.AsGuid throws with invalid input`` () =
    Assert.Throws<System.Exception>(fun () -> "not a guid".AsGuid() |> ignore) |> ignore

[<Test>]
let ``Complex CSV with quotes and newlines handled correctly`` () =
    let csv = CsvFile.Parse(sampleCsvWithQuotes)
    let rows = csv.Rows |> Array.ofSeq
    
    rows.Length |> should equal 3
    rows.[0].["Name"] |> should equal "Alice"
    rows.[0].["Description"] |> should equal "Software Engineer, Senior"
    rows.[1].["Description"] |> should equal "Product Manager\"s Assistant"
    rows.[2].["Description"] |> should equal "Data Scientist\nwith ML focus"