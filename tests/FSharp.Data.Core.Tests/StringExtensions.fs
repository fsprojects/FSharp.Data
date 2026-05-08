module FSharp.Data.Tests.StringExtensions

open System
open System.Globalization
open NUnit.Framework
open FsUnit
open FSharp.Data

// Tests for FSharp.Data.StringExtensions — the extension methods on System.String
// that wrap TextConversions and throw on failure.

[<Test>]
let ``AsInteger succeeds for valid integers`` () =
    "42".AsInteger() |> should equal 42
    "-123".AsInteger() |> should equal -123
    "0".AsInteger() |> should equal 0
    " 5 ".AsInteger() |> should equal 5

[<Test>]
let ``AsInteger uses provided culture`` () =
    // The culture affects decimal separator, but AsInteger uses NumberStyles.Integer
    // Test that a culture with different number separators doesn't break integer parsing
    "42".AsInteger(CultureInfo.GetCultureInfo("fr-FR")) |> should equal 42
    "-7".AsInteger(CultureInfo.GetCultureInfo("de-DE")) |> should equal -7

[<Test>]
let ``AsInteger throws for invalid input`` () =
    (fun () -> "abc".AsInteger() |> ignore) |> should throw typeof<Exception>
    (fun () -> "12.5".AsInteger() |> ignore) |> should throw typeof<Exception>
    (fun () -> "".AsInteger() |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``AsInteger64 succeeds for valid int64s`` () =
    "9223372036854775807".AsInteger64() |> should equal Int64.MaxValue
    "-9223372036854775808".AsInteger64() |> should equal Int64.MinValue
    "0".AsInteger64() |> should equal 0L

[<Test>]
let ``AsInteger64 throws for invalid input`` () =
    (fun () -> "not-a-number".AsInteger64() |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``AsDecimal succeeds for valid decimals`` () =
    "3.14".AsDecimal() |> should equal 3.14M
    "-99.99".AsDecimal() |> should equal -99.99M
    "0".AsDecimal() |> should equal 0M

[<Test>]
let ``AsDecimal uses provided culture`` () =
    "1.234,56".AsDecimal(CultureInfo.GetCultureInfo("de-DE")) |> should equal 1234.56M

[<Test>]
let ``AsDecimal throws for invalid input`` () =
    (fun () -> "abc".AsDecimal() |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``AsFloat succeeds for valid floats`` () =
    "3.14".AsFloat() |> should (equalWithin 1e-10) 3.14
    "-2.718".AsFloat() |> should (equalWithin 1e-10) -2.718
    "0.0".AsFloat() |> should equal 0.0

[<Test>]
let ``AsFloat treats default missing values as NaN`` () =
    "N/A".AsFloat() |> Double.IsNaN |> should equal true
    "NA".AsFloat() |> Double.IsNaN |> should equal true

[<Test>]
let ``AsFloat uses custom missing values`` () =
    "MISSING".AsFloat(missingValues = [| "MISSING" |]) |> Double.IsNaN |> should equal true

[<Test>]
let ``AsFloat throws for non-numeric non-missing input`` () =
    (fun () -> "abc".AsFloat() |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``AsBoolean succeeds for recognised values`` () =
    "true".AsBoolean() |> should equal true
    "True".AsBoolean() |> should equal true
    "yes".AsBoolean() |> should equal true
    "1".AsBoolean() |> should equal true
    "false".AsBoolean() |> should equal false
    "False".AsBoolean() |> should equal false
    "no".AsBoolean() |> should equal false
    "0".AsBoolean() |> should equal false

[<Test>]
let ``AsBoolean throws for unrecognised input`` () =
    (fun () -> "maybe".AsBoolean() |> ignore) |> should throw typeof<Exception>
    (fun () -> "".AsBoolean() |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``AsDateTime succeeds for valid ISO 8601 date strings`` () =
    let dt = "2024-06-15T12:00:00Z".AsDateTime()
    dt.Year |> should equal 2024
    dt.Month |> should equal 6
    dt.Day |> should equal 15

[<Test>]
let ``AsDateTime uses provided culture`` () =
    let dt = "2024-01-01".AsDateTime(CultureInfo.InvariantCulture)
    dt.Year |> should equal 2024

[<Test>]
let ``AsDateTime throws for invalid input`` () =
    (fun () -> "not-a-date".AsDateTime() |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``AsDateTimeOffset succeeds for valid offset strings`` () =
    let dto = "2024-06-15T12:00:00+05:30".AsDateTimeOffset()
    dto.Year |> should equal 2024
    dto.Offset |> should equal (TimeSpan.FromHours 5.5)

[<Test>]
let ``AsDateTimeOffset throws for invalid input`` () =
    (fun () -> "2024-01-01".AsDateTimeOffset() |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``AsTimeSpan succeeds for valid time span strings`` () =
    let ts = "01:30:00".AsTimeSpan()
    ts |> should equal (TimeSpan(1, 30, 0))

[<Test>]
let ``AsTimeSpan throws for invalid input`` () =
    (fun () -> "not-a-timespan".AsTimeSpan() |> ignore) |> should throw typeof<Exception>

[<Test>]
let ``AsGuid succeeds for valid GUID strings`` () =
    let g = "6F9619FF-8B86-D011-B42D-00C04FC964FF".AsGuid()
    g |> should not' (equal Guid.Empty)

[<Test>]
let ``AsGuid succeeds for braced GUID strings`` () =
    let g = "{6F9619FF-8B86-D011-B42D-00C04FC964FF}".AsGuid()
    g |> should not' (equal Guid.Empty)

[<Test>]
let ``AsGuid throws for invalid input`` () =
    (fun () -> "not-a-guid".AsGuid() |> ignore) |> should throw typeof<Exception>
    (fun () -> "".AsGuid() |> ignore) |> should throw typeof<Exception>
