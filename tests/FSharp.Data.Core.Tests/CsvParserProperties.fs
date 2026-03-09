module FSharp.Data.Tests.CsvParserProperties

open NUnit.Framework
open FsUnit
open System
open System.IO
open FSharp.Data.Runtime.CsvReader
open FsCheck

/// Encodes a CSV field value according to RFC 4180, quoting when necessary.
/// Empty strings are always quoted to avoid producing blank CSV lines.
let private encodeCsvField (separator: char) (quote: char) (value: string) =
    let needsQuoting =
        value = ""
        || value.Contains(separator)
        || value.Contains(quote)
        || value.Contains('\n')
        || value.Contains('\r')

    if needsQuoting then
        let escaped = value.Replace(string quote, string quote + string quote)
        sprintf "%c%s%c" quote escaped quote
    else
        value

/// Encodes a row of field values as a single CSV line.
let private encodeCsvRow (separator: char) (quote: char) (fields: string[]) =
    fields |> Array.map (encodeCsvField separator quote) |> String.concat (string separator)

/// Parses a CSV string and returns all logical rows as arrays of field values.
let private parseCsv (csv: string) (separator: char) (quote: char) =
    use reader = new StringReader(csv)
    readCsvFile reader (string separator) quote |> Seq.map fst |> Array.ofSeq

[<Test>]
let ``CSV roundtrip property: arbitrary string values are preserved when properly encoded`` () =
    let separator = ','
    let quote = '"'

    // Generate non-empty rows of non-null strings
    let fieldGen = Arb.generate<string> |> Gen.map (fun s -> if s = null then "" else s)

    let rowGen =
        Gen.nonEmptyListOf fieldGen |> Gen.map Array.ofList |> Gen.resize 6

    let rowsGen = Gen.nonEmptyListOf rowGen |> Gen.map Array.ofList |> Gen.resize 8

    let prop (rows: string[][]) =
        let csv =
            rows |> Array.map (encodeCsvRow separator quote) |> String.concat "\n"

        let parsed = parseCsv csv separator quote

        parsed.Length = rows.Length
        && Array.forall2
            (fun (expected: string[]) (actual: string[]) ->
                expected.Length = actual.Length && Array.forall2 (=) expected actual)
            rows
            parsed

    Check.One(
        { Config.QuickThrowOnFailure with MaxTest = 500 },
        Prop.forAll (Arb.fromGen rowsGen) prop
    )

[<Test>]
let ``CSV roundtrip: field containing separator is preserved as a single field`` () =
    let fields = [| "value with, comma"; "normal field"; "a,b,c" |]
    let csv = encodeCsvRow ',' '"' fields
    let parsed = parseCsv csv ',' '"'
    parsed.Length |> should equal 1
    parsed.[0] |> should equal fields

[<Test>]
let ``CSV roundtrip: field containing quote character is preserved`` () =
    let fields = [| "say \"hello\" world"; "normal"; "she said \"hi\"" |]
    let csv = encodeCsvRow ',' '"' fields
    let parsed = parseCsv csv ',' '"'
    parsed.Length |> should equal 1
    parsed.[0] |> should equal fields

[<Test>]
let ``CSV roundtrip: field containing newline spans one logical row`` () =
    let fields = [| "line1\nline2"; "normal" |]
    let csv = encodeCsvRow ',' '"' fields
    let parsed = parseCsv csv ',' '"'
    parsed.Length |> should equal 1
    parsed.[0] |> should equal fields

[<Test>]
let ``CSV roundtrip: field containing carriage-return newline is preserved`` () =
    let fields = [| "multi\r\nline"; "normal" |]
    let csv = encodeCsvRow ',' '"' fields
    let parsed = parseCsv csv ',' '"'
    parsed.Length |> should equal 1
    parsed.[0] |> should equal fields

[<Test>]
let ``CSV roundtrip: tab separator is supported`` () =
    let fields = [| "a,b,c"; "d\te"; "normal" |]
    let csv = encodeCsvRow '\t' '"' fields
    let parsed = parseCsv csv '\t' '"'
    parsed.Length |> should equal 1
    parsed.[0] |> should equal fields

[<Test>]
let ``CSV roundtrip: custom quote character is respected`` () =
    let fields = [| "value with, comma"; "value with 'single'" |]
    let csv = encodeCsvRow ',' '\'' fields
    let parsed = parseCsv csv ',' '\''
    parsed.Length |> should equal 1
    parsed.[0] |> should equal fields

[<Test>]
let ``CSV roundtrip: multiple rows with varied content are all preserved`` () =
    let rows =
        [| [| "Alice"; "30"; "New York, NY" |]
           [| "Bob"; "25"; "Los Angeles" |]
           [| "Charlie says \"hello\""; "35"; "Chicago\nIL" |] |]

    let csv = rows |> Array.map (encodeCsvRow ',' '"') |> String.concat "\n"
    let parsed = parseCsv csv ',' '"'
    parsed.Length |> should equal rows.Length

    Array.iter2 (fun expected actual -> actual |> should equal expected) rows parsed

[<Test>]
let ``CSV roundtrip: empty string field is preserved`` () =
    let fields = [| ""; "non-empty"; "" |]
    let csv = encodeCsvRow ',' '"' fields
    let parsed = parseCsv csv ',' '"'
    parsed.Length |> should equal 1
    parsed.[0] |> should equal fields

[<Test>]
let ``CSV roundtrip: single-column empty field row is not lost`` () =
    let rows = [| [| "before" |]; [| "" |]; [| "after" |] |]

    let csv = rows |> Array.map (encodeCsvRow ',' '"') |> String.concat "\n"
    let parsed = parseCsv csv ',' '"'
    parsed.Length |> should equal 3
    parsed.[0] |> should equal [| "before" |]
    parsed.[1] |> should equal [| "" |]
    parsed.[2] |> should equal [| "after" |]
