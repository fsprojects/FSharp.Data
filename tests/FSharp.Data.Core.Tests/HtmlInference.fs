module FSharp.Data.Tests.HtmlInference

open System
open System.Globalization
open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference

// ─── Helpers ────────────────────────────────────────────────────────────────

let private mkParams preferOptionals : HtmlInference.Parameters =
    { MissingValues = TextConversions.DefaultMissingValues
      CultureInfo = CultureInfo.InvariantCulture
      UnitsOfMeasureProvider = defaultUnitsOfMeasureProvider
      PreferOptionals = preferOptionals
      InferenceMode = InferenceMode'.ValuesOnly }

let private defaultParams = mkParams false
let private optionalParams = mkParams true

let private prim typ =
    InferedType.Primitive(typ, None, false, false)

// ─── inferListType ───────────────────────────────────────────────────────────

[<Test>]
let ``inferListType returns Null for empty array`` () =
    HtmlInference.inferListType defaultParams [||] |> should equal InferedType.Null

[<Test>]
let ``inferListType returns Null for whitespace-only values`` () =
    HtmlInference.inferListType defaultParams [| "  "; "\t"; "" |]
    |> should equal InferedType.Null

[<Test>]
let ``inferListType treats nbsp as missing value`` () =
    HtmlInference.inferListType defaultParams [| "&nbsp;"; "&nbsp" |]
    |> should equal InferedType.Null

[<Test>]
let ``inferListType infers int type`` () =
    HtmlInference.inferListType defaultParams [| "1"; "2"; "3" |]
    |> should equal (prim typeof<int>)

[<Test>]
let ``inferListType infers decimal type`` () =
    HtmlInference.inferListType defaultParams [| "1.5"; "2.3"; "3.7" |]
    |> should equal (prim typeof<decimal>)

[<Test>]
let ``inferListType infers float type for scientific notation`` () =
    HtmlInference.inferListType defaultParams [| "1e100"; "2.5e-10" |]
    |> should equal (prim typeof<float>)

[<Test>]
let ``inferListType infers string type`` () =
    HtmlInference.inferListType defaultParams [| "hello"; "world" |]
    |> should equal (prim typeof<string>)

[<Test>]
let ``inferListType infers bool type`` () =
    HtmlInference.inferListType defaultParams [| "true"; "false" |]
    |> should equal (prim typeof<bool>)

[<Test>]
let ``inferListType widens int to decimal when mixed with decimal`` () =
    HtmlInference.inferListType defaultParams [| "1"; "2.5"; "3" |]
    |> should equal (prim typeof<decimal>)

[<Test>]
let ``inferListType widens to float for scientific notation mixed with int`` () =
    HtmlInference.inferListType defaultParams [| "1"; "1.5e10"; "3" |]
    |> should equal (prim typeof<float>)

[<Test>]
let ``inferListType produces Heterogeneous type for mixed numeric and non-numeric`` () =
    let result = HtmlInference.inferListType defaultParams [| "42"; "hello" |]

    match result with
    | InferedType.Heterogeneous _ -> ()
    | _ -> Assert.Fail(sprintf "Expected Heterogeneous, got %A" result)

[<Test>]
let ``inferListType treats NaN as float when preferOptionals is false`` () =
    let result = HtmlInference.inferListType defaultParams [| "NaN" |]
    result |> should equal (prim typeof<float>)

[<Test>]
let ``inferListType treats NaN as Null when preferOptionals is true`` () =
    let result = HtmlInference.inferListType optionalParams [| "NaN" |]
    result |> should equal InferedType.Null

[<Test>]
let ``inferListType treats NA as float when preferOptionals is false`` () =
    let result = HtmlInference.inferListType defaultParams [| "NA" |]
    result |> should equal (prim typeof<float>)

[<Test>]
let ``inferListType infers date type for ISO date strings`` () =
    let result =
        HtmlInference.inferListType defaultParams [| "2023-01-01"; "2023-06-15" |]

    match result with
    | InferedType.Primitive(typ, _, false, _) ->
        (typ = typeof<DateTime> || typ = typeof<DateOnly>)
        |> should equal true
    | _ -> Assert.Fail(sprintf "Expected date primitive, got %A" result)

[<Test>]
let ``inferListType with mixed missing and integer values infers float (allowEmptyValues path)`` () =
    // NaN + int values → float because NaN is treated as float when preferOptionals=false
    let result = HtmlInference.inferListType defaultParams [| "NaN"; "1"; "2" |]
    result |> should equal (prim typeof<float>)

[<Test>]
let ``inferListType with single integer value returns int`` () =
    HtmlInference.inferListType defaultParams [| "42" |]
    |> should equal (prim typeof<int>)

// ─── inferHeaders ────────────────────────────────────────────────────────────

[<Test>]
let ``inferHeaders returns false for empty rows`` () =
    let hasHeaders, names, units, _ = HtmlInference.inferHeaders defaultParams [||]
    hasHeaders |> should equal false
    names |> should equal None
    units |> should equal None

[<Test>]
let ``inferHeaders returns false for single row`` () =
    let hasHeaders, names, _, _ =
        HtmlInference.inferHeaders defaultParams [| [| "Name"; "Age" |] |]

    hasHeaders |> should equal false
    names |> should equal None

[<Test>]
let ``inferHeaders returns false for exactly two rows`` () =
    let rows = [| [| "Name"; "Age" |]; [| "Alice"; "30" |] |]
    let hasHeaders, names, _, _ = HtmlInference.inferHeaders defaultParams rows
    hasHeaders |> should equal false
    names |> should equal None

[<Test>]
let ``inferHeaders returns true when header row differs from data rows`` () =
    // First row is text (string) headers, data rows are numeric → types differ → headers detected
    let rows =
        [| [| "Name"; "Score" |]
           [| "Alice"; "95" |]
           [| "Bob"; "87" |] |]

    let hasHeaders, names, _, _ = HtmlInference.inferHeaders defaultParams rows
    hasHeaders |> should equal true
    names |> should equal (Some [| "Name"; "Score" |])

[<Test>]
let ``inferHeaders returns false when all rows have same type`` () =
    // All rows are strings → header row type = data rows type → no headers inferred
    let rows =
        [| [| "Alice"; "Bob" |]
           [| "Carol"; "Dave" |]
           [| "Eve"; "Frank" |] |]

    let hasHeaders, names, _, _ = HtmlInference.inferHeaders defaultParams rows
    hasHeaders |> should equal false
    names |> should equal None

[<Test>]
let ``inferHeaders returns inferred type for data rows`` () =
    let rows =
        [| [| "Name"; "Age" |]
           [| "Alice"; "30" |]
           [| "Bob"; "25" |] |]

    let hasHeaders, _, _, dataType = HtmlInference.inferHeaders defaultParams rows
    hasHeaders |> should equal true
    dataType |> should not' (equal None)
