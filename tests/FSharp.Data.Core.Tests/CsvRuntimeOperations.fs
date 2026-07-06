module FSharp.Data.Tests.CsvRuntimeOperations

open NUnit.Framework
open FsUnit
open System
open System.IO
open FSharp.Data
open FSharp.Data.CsvExtensions

let private sampleCsv =
    "Name,Age,City\r\nAlice,30,London\r\nBob,25,Paris\r\nCarol,35,Berlin\r\nDave,22,Tokyo\r\nEve,28,Sydney"

// ============================================================
// Filter
// ============================================================

[<Test>]
let ``Filter keeps only rows matching the predicate`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let filtered = csv.Filter(fun row -> row.["Age"].AsInteger() >= 28)
    filtered.Rows |> Seq.length |> should equal 3

[<Test>]
let ``Filter returns empty rows when no rows match`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let filtered = csv.Filter(fun row -> row.["Age"].AsInteger() > 100)
    filtered.Rows |> Seq.length |> should equal 0

[<Test>]
let ``Filter preserves header information`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let filtered = csv.Filter(fun row -> row.["City"] = "London")
    filtered.Headers |> should equal (Some [| "Name"; "Age"; "City" |])

[<Test>]
let ``Filter returns all rows when all match`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let filtered = csv.Filter(fun row -> row.["Age"].AsInteger() > 0)
    filtered.Rows |> Seq.length |> should equal 5

// ============================================================
// Take
// ============================================================

[<Test>]
let ``Take returns the first N rows`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let taken = csv.Take 3
    let rows = taken.Rows |> Seq.toArray
    rows |> should haveLength 3
    rows.[0].["Name"] |> should equal "Alice"
    rows.[2].["Name"] |> should equal "Carol"

[<Test>]
let ``Take 0 returns empty rows`` () =
    let csv = CsvFile.Parse(sampleCsv)
    csv.Take(0).Rows |> Seq.length |> should equal 0

[<Test>]
let ``Take preserves headers`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let taken = csv.Take 2
    taken.Headers |> should equal (Some [| "Name"; "Age"; "City" |])

// ============================================================
// TakeWhile
// ============================================================

[<Test>]
let ``TakeWhile yields rows while predicate is true`` () =
    let csv = CsvFile.Parse(sampleCsv)
    // Ages: 30, 25, 35, 22, 28 — take while Age > 26; Bob=25 fails first
    let taken = csv.TakeWhile(fun row -> row.["Age"].AsInteger() > 26)
    let rows = taken.Rows |> Seq.toArray
    rows |> should haveLength 1
    rows.[0].["Name"] |> should equal "Alice"

[<Test>]
let ``TakeWhile returns all rows when predicate always true`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let taken = csv.TakeWhile(fun row -> row.["Age"].AsInteger() > 0)
    taken.Rows |> Seq.length |> should equal 5

[<Test>]
let ``TakeWhile returns empty when first row fails predicate`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let taken = csv.TakeWhile(fun row -> row.["Age"].AsInteger() > 100)
    taken.Rows |> Seq.length |> should equal 0

// ============================================================
// Skip
// ============================================================

[<Test>]
let ``Skip omits the first N rows`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let skipped = csv.Skip 3
    let rows = skipped.Rows |> Seq.toArray
    rows |> should haveLength 2
    rows.[0].["Name"] |> should equal "Dave"
    rows.[1].["Name"] |> should equal "Eve"

[<Test>]
let ``Skip 0 returns all rows`` () =
    let csv = CsvFile.Parse(sampleCsv)
    csv.Skip(0).Rows |> Seq.length |> should equal 5

[<Test>]
let ``Skip preserves headers`` () =
    let csv = CsvFile.Parse(sampleCsv)
    csv.Skip(2).Headers |> should equal (Some [| "Name"; "Age"; "City" |])

// ============================================================
// SkipWhile
// ============================================================

[<Test>]
let ``SkipWhile skips rows while predicate is true then yields rest`` () =
    let csv = CsvFile.Parse(sampleCsv)
    // Ages: 30, 25, 35, 22, 28 — skip while Age >= 25 (Alice=30, Bob=25, Carol=35, then Dave=22 stops)
    let skipped = csv.SkipWhile(fun row -> row.["Age"].AsInteger() >= 25)
    let rows = skipped.Rows |> Seq.toArray
    rows |> should haveLength 2
    rows.[0].["Name"] |> should equal "Dave"

[<Test>]
let ``SkipWhile returns all rows when first row fails predicate`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let skipped = csv.SkipWhile(fun row -> row.["Age"].AsInteger() > 100)
    skipped.Rows |> Seq.length |> should equal 5

// ============================================================
// Truncate
// ============================================================

[<Test>]
let ``Truncate returns at most N rows`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let truncated = csv.Truncate 2
    truncated.Rows |> Seq.length |> should equal 2

[<Test>]
let ``Truncate with count larger than row count returns all rows`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let truncated = csv.Truncate 100
    truncated.Rows |> Seq.length |> should equal 5

[<Test>]
let ``Truncate 0 returns empty rows`` () =
    let csv = CsvFile.Parse(sampleCsv)
    csv.Truncate(0).Rows |> Seq.length |> should equal 0

// ============================================================
// Append
// ============================================================

[<Test>]
let ``Append adds rows from another sequence`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let first2 = csv.Take 2
    let last3 = csv.Skip 2
    let combined = first2.Append(last3.Rows)
    combined.Rows |> Seq.length |> should equal 5

[<Test>]
let ``Append preserves existing rows`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let first1 = csv.Take 1
    let extra = csv.Take 1
    let combined = first1.Append(extra.Rows)
    let rows = combined.Rows |> Seq.toArray
    rows |> should haveLength 2
    rows.[0].["Name"] |> should equal "Alice"
    rows.[1].["Name"] |> should equal "Alice"

// ============================================================
// Cache
// ============================================================

[<Test>]
let ``Cache preserves all rows`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let cached = csv.Cache()
    cached.Rows |> Seq.length |> should equal 5

[<Test>]
let ``Cache can be enumerated multiple times`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let cached = csv.Cache()
    cached.Rows |> Seq.length |> should equal 5
    cached.Rows |> Seq.length |> should equal 5

// ============================================================
// SaveToString
// ============================================================

[<Test>]
let ``SaveToString produces header row and all data rows`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let result = csv.SaveToString()
    result |> should contain "Name,Age,City"
    result |> should contain "Alice,30,London"
    result |> should contain "Eve,28,Sydney"

[<Test>]
let ``SaveToString round-trips CSV correctly`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let serialised = csv.SaveToString()
    let reparsed = CsvFile.Parse(serialised)
    reparsed.Rows |> Seq.length |> should equal 5
    reparsed.Rows |> Seq.head |> fun r -> r.["Name"] |> should equal "Alice"

[<Test>]
let ``SaveToString uses CRLF line endings per RFC 4180`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let result = csv.SaveToString()
    result |> should contain "\r\n"

[<Test>]
let ``SaveToString quotes fields containing the separator`` () =
    let csv = CsvFile.Parse("Name,Note\r\nAlice,\"hello, world\"")
    let result = csv.SaveToString()
    result |> should contain "\"hello, world\""

[<Test>]
let ``SaveToString with custom separator uses it in output`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let result = csv.SaveToString(separator = ';')
    result |> should startWith "Name;Age;City"

// ============================================================
// Save to TextWriter
// ============================================================

[<Test>]
let ``Save to TextWriter produces the same output as SaveToString`` () =
    let csv = CsvFile.Parse(sampleCsv)
    use writer = new StringWriter()
    csv.Save(writer)
    let fromSave = writer.ToString()
    let fromSaveToString = csv.SaveToString()
    fromSave |> should equal fromSaveToString

// ============================================================
// Chaining operations
// ============================================================

[<Test>]
let ``Chaining Filter and Take works correctly`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let result = csv.Filter(fun row -> row.["Age"].AsInteger() >= 28).Take(2)
    let rows = result.Rows |> Seq.toArray
    rows |> should haveLength 2

[<Test>]
let ``Chaining Skip and Truncate works correctly`` () =
    let csv = CsvFile.Parse(sampleCsv)
    let result = csv.Skip(1).Truncate(2)
    let rows = result.Rows |> Seq.toArray
    rows |> should haveLength 2
    rows.[0].["Name"] |> should equal "Bob"

// ============================================================
// Save round-trip fidelity
// ============================================================

[<Test>]
let ``SaveToString quotes headers containing the separator`` () =
    let csv = CsvFile.Parse("\"Name, First\",Age\r\nJohn,25")
    let saved = csv.SaveToString()
    saved |> should startWith "\"Name, First\",Age"
    let reloaded = CsvFile.Parse(saved)
    reloaded.Headers |> should equal (Some [| "Name, First"; "Age" |])

[<Test>]
let ``SaveToString quotes fields containing a lone carriage return`` () =
    // a bare CR terminates a row when re-parsing, so it must be quoted like LF
    let csv = CsvFile.Parse("A,B\r\n\"x\ry\",2")
    let saved = csv.SaveToString()
    let reloaded = CsvFile.Parse(saved)
    let rows = reloaded.Rows |> Seq.toArray
    rows |> should haveLength 1
    rows.[0].Columns |> should equal [| "x\ry"; "2" |]

[<Test>]
let ``SaveToString quotes fields containing any of the document separators`` () =
    let csv = CsvFile.Parse("A;B\r\n1;\"x,y\"", separators = ";,")
    let saved = csv.SaveToString()
    let reloaded = CsvFile.Parse(saved, separators = ";,")
    let rows = reloaded.Rows |> Seq.toArray
    rows |> should haveLength 1
    rows.[0].Columns |> should equal [| "1"; "x,y" |]

[<Test>]
let ``Save does not dispose the caller-supplied writer`` () =
    let csv = CsvFile.Parse(sampleCsv)
    use sw = new StringWriter()
    csv.Save(sw)
    // writing after Save must work: the writer belongs to the caller
    sw.Write("still-open")
    sw.ToString() |> should contain "still-open"
