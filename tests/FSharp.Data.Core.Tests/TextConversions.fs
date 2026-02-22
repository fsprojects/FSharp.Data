module FSharp.Data.Tests.Conversions

open NUnit.Framework
open FsUnit
open System
open System.Globalization
open FSharp.Data

[<Test>]
let ``Boolean conversions``() = 
  let asBoolean = TextConversions.AsBoolean

  asBoolean "yEs"  |> should equal (Some true)
  asBoolean "trUe" |> should equal (Some true)
  asBoolean "1"    |> should equal (Some true)

  asBoolean "nO"    |> should equal (Some false)
  asBoolean "faLSe" |> should equal (Some false)
  asBoolean "0"     |> should equal (Some false)

  asBoolean "rubbish" |> should equal None

[<Test>]
let ``Boolean conversions with whitespace``() =
  let asBoolean = TextConversions.AsBoolean

  asBoolean " yes "  |> should equal (Some true)
  asBoolean "\ttrue\t" |> should equal (Some true)
  asBoolean "\n1\n"    |> should equal (Some true)

  asBoolean " no "     |> should equal (Some false)
  asBoolean "\tfalse\t" |> should equal (Some false)
  asBoolean "\n0\n"    |> should equal (Some false)

[<Test>]
let ``Boolean conversions with additional valid values``() =
  let asBoolean = TextConversions.AsBoolean

  asBoolean "YES"  |> should equal (Some true)
  asBoolean "True" |> should equal (Some true)
  asBoolean "TRUE" |> should equal (Some true)

  asBoolean "NO"    |> should equal (Some false)
  asBoolean "False" |> should equal (Some false)
  asBoolean "FALSE" |> should equal (Some false)

[<Test>]
let ``Integer conversions``() = 
  let culture = CultureInfo.InvariantCulture
  
  TextConversions.AsInteger culture "123" |> should equal (Some 123)
  TextConversions.AsInteger culture "-456" |> should equal (Some -456)
  TextConversions.AsInteger culture "0" |> should equal (Some 0)
  TextConversions.AsInteger culture " 789 " |> should equal (Some 789)
  TextConversions.AsInteger culture "1000" |> should equal (Some 1000)
  
  // Currency adorners should be removed
  TextConversions.AsInteger culture "$100" |> should equal (Some 100)
  TextConversions.AsInteger culture "€200" |> should equal (Some 200)
  TextConversions.AsInteger culture "¥300" |> should equal (Some 300)
  
  // Non-currency adorners should be removed  
  TextConversions.AsInteger culture "50%" |> should equal (Some 50)
  TextConversions.AsInteger culture "25‰" |> should equal (Some 25)
  
  // Invalid values
  TextConversions.AsInteger culture "abc" |> should equal None
  TextConversions.AsInteger culture "12.34" |> should equal None
  TextConversions.AsInteger culture "" |> should equal None

[<Test>]
let ``Integer64 conversions``() = 
  let culture = CultureInfo.InvariantCulture
  
  TextConversions.AsInteger64 culture "9223372036854775807" |> should equal (Some 9223372036854775807L)
  TextConversions.AsInteger64 culture "-9223372036854775808" |> should equal (Some -9223372036854775808L)
  TextConversions.AsInteger64 culture "0" |> should equal (Some 0L)
  TextConversions.AsInteger64 culture " 12345678901234 " |> should equal (Some 12345678901234L)
  TextConversions.AsInteger64 culture "1000000000" |> should equal (Some 1000000000L)
  
  // Currency adorners
  TextConversions.AsInteger64 culture "$1000000" |> should equal (Some 1000000L)
  
  // Invalid values
  TextConversions.AsInteger64 culture "abc" |> should equal None
  TextConversions.AsInteger64 culture "12.34" |> should equal None

[<Test>]
let ``Decimal conversions``() = 
  TextConversions.AsDecimal CultureInfo.InvariantCulture "¤50" |> should equal (Some 50M)
  TextConversions.AsDecimal (CultureInfo "en-GB") "£50" |> should equal (Some 50M)
  TextConversions.AsDecimal (CultureInfo "en-GB") "$50" |> should equal (Some 50M)
  TextConversions.AsDecimal CultureInfo.InvariantCulture "(10,000,000.99)" |> should equal (Some -10000000.99M)

[<Test>]
let ``Decimal conversions with various formats``() = 
  let culture = CultureInfo.InvariantCulture
  
  TextConversions.AsDecimal culture "123.45" |> should equal (Some 123.45M)
  TextConversions.AsDecimal culture "-67.89" |> should equal (Some -67.89M)
  TextConversions.AsDecimal culture "0.0" |> should equal (Some 0M)
  TextConversions.AsDecimal culture ".5" |> should equal (Some 0.5M)
  TextConversions.AsDecimal culture "1000.00" |> should equal (Some 1000M)
  TextConversions.AsDecimal culture " 123.456 " |> should equal (Some 123.456M)
  
  // Percentage adorner
  TextConversions.AsDecimal culture "25.5%" |> should equal (Some 25.5M)
  
  // Invalid values
  TextConversions.AsDecimal culture "abc" |> should equal None
  TextConversions.AsDecimal culture "12.34.56" |> should equal None

[<Test>]
let ``Float conversions with missing values``() = 
  let culture = CultureInfo.InvariantCulture
  let missingValues = [| "NaN"; "NA"; "N/A"; "#N/A"; ":"; "-"; "TBA"; "TBD" |]
  
  // Normal values
  TextConversions.AsFloat missingValues false culture "123.45" |> should equal (Some 123.45)
  TextConversions.AsFloat missingValues false culture "-67.89" |> should equal (Some -67.89)
  TextConversions.AsFloat missingValues false culture "0.0" |> should equal (Some 0.0)
  
  // Scientific notation
  TextConversions.AsFloat missingValues false culture "1.23e10" |> should equal (Some 1.23e10)
  TextConversions.AsFloat missingValues false culture "1.23e-5" |> should equal (Some 1.23e-5)
  TextConversions.AsFloat missingValues false culture "1.23E+3" |> should equal (Some 1230.0)
  
  // Special values
  TextConversions.AsFloat missingValues false culture "Infinity" |> should equal (Some Double.PositiveInfinity)
  TextConversions.AsFloat missingValues false culture "-Infinity" |> should equal (Some Double.NegativeInfinity)
  
  // Missing values with useNoneForMissingValues = true
  for missingValue in missingValues do
    TextConversions.AsFloat missingValues true culture missingValue |> should equal None
    
  // Missing values with useNoneForMissingValues = false  
  for missingValue in missingValues do
    TextConversions.AsFloat missingValues false culture missingValue |> should equal (Some Double.NaN)
    
  // Invalid values
  TextConversions.AsFloat missingValues false culture "abc" |> should equal None

[<Test>]
let ``DateTime conversions`` () =
  let case sample result =
    TextConversions.AsDateTime CultureInfo.InvariantCulture sample
    |> should equal (Some result)

  case "2016-11-21T10:29:05Z"      <| System.DateTime(2016,11,21,10,29,05, System.DateTimeKind.Utc)
  case "2016-11-21T10:29:05"       <| System.DateTime(2016,11,21,10,29,05, System.DateTimeKind.Local)
  case "2016-11-21T13:29:05+03:00" <| System.DateTime(2016,11,21,10,29,05, System.DateTimeKind.Utc).ToLocalTime()

[<Test>]
let ``DateTime conversions with various formats``() =
  let culture = CultureInfo.InvariantCulture
  
  // Basic date formats
  TextConversions.AsDateTime culture "2020-01-01" |> Option.isSome |> should equal true
  TextConversions.AsDateTime culture "01/01/2020" |> Option.isSome |> should equal true
  TextConversions.AsDateTime culture "Jan 1, 2020" |> Option.isSome |> should equal true
  
  // Microsoft JSON date format
  TextConversions.AsDateTime culture "/Date(1577836800000)/" |> Option.isSome |> should equal true
  TextConversions.AsDateTime culture "/Date(1577836800000+0000)/" |> Option.isSome |> should equal true
  
  // Invalid values
  TextConversions.AsDateTime culture "not-a-date" |> should equal None
  TextConversions.AsDateTime culture "" |> should equal None

[<Test>]
let ``DateTimeOffset conversions`` () =
  let shouldBe expected actual =
    TextConversions.AsDateTimeOffset CultureInfo.InvariantCulture actual
    |> should equal (Some expected)

  let shouldFail sample =
    TextConversions.AsDateTimeOffset CultureInfo.InvariantCulture sample
    |> should equal None
  
  "2018-04-25+10:00"            |> shouldBe (System.DateTimeOffset(2018, 4, 25, 0, 0, 0, System.TimeSpan.FromHours(10.)))
  "2018-04-25 23:04:00-06:30"   |> shouldBe (System.DateTimeOffset(2018, 4, 25, 23, 4, 0, System.TimeSpan.FromHours(-6.5)))
  "2018-04-25T00:00:00Z"        |> shouldBe (System.DateTimeOffset(2018, 4, 25, 0, 0, 0, System.TimeSpan.FromHours(0.)))
  "garbage"                     |> shouldFail

[<Test>]
let ``DateTimeOffset conversions with additional formats``() =
  let culture = CultureInfo.InvariantCulture
  
  // More timezone formats
  TextConversions.AsDateTimeOffset culture "2020-01-01T12:00:00+05:30" |> Option.isSome |> should equal true
  TextConversions.AsDateTimeOffset culture "2020-01-01T12:00:00-08:00" |> Option.isSome |> should equal true
  
  // Invalid values
  TextConversions.AsDateTimeOffset culture "2020-01-01" |> should equal None
  TextConversions.AsDateTimeOffset culture "invalid" |> should equal None

[<Test>]
let ``TimeSpan conversions``() = 
  let culture = CultureInfo.InvariantCulture
  
  TextConversions.AsTimeSpan culture "12:30:45" |> should equal (Some (TimeSpan(12, 30, 45)))
  TextConversions.AsTimeSpan culture "01:05:30" |> should equal (Some (TimeSpan(1, 5, 30)))
  TextConversions.AsTimeSpan culture "00:00:00" |> should equal (Some TimeSpan.Zero)
  TextConversions.AsTimeSpan culture "1.12:30:45" |> should equal (Some (TimeSpan(1, 12, 30, 45)))
  TextConversions.AsTimeSpan culture " 05:15:00 " |> should equal (Some (TimeSpan(5, 15, 0)))
  
  // Invalid values
  TextConversions.AsTimeSpan culture "invalid" |> should equal None
  TextConversions.AsTimeSpan culture "invalid:00:00" |> should equal None
  TextConversions.AsTimeSpan culture "" |> should equal None

#if NET6_0_OR_GREATER
[<Test>]
let ``DateOnly conversions`` () =
  let culture = CultureInfo.InvariantCulture

  TextConversions.AsDateOnly culture "2023-01-15" |> should equal (Some (DateOnly(2023, 1, 15)))
  TextConversions.AsDateOnly culture "2000-12-31" |> should equal (Some (DateOnly(2000, 12, 31)))

  // A datetime string should NOT match (DateOnly can't parse time components)
  TextConversions.AsDateOnly culture "2023-01-15T10:30:00" |> should equal None
  TextConversions.AsDateOnly culture "invalid" |> should equal None
  TextConversions.AsDateOnly culture "" |> should equal None

[<Test>]
let ``TimeOnly conversions`` () =
  let culture = CultureInfo.InvariantCulture

  TextConversions.AsTimeOnly culture "10:30:45" |> should equal (Some (TimeOnly(10, 30, 45)))
  TextConversions.AsTimeOnly culture "00:00:00" |> should equal (Some TimeOnly.MinValue)
  TextConversions.AsTimeOnly culture "23:59:59" |> should equal (Some (TimeOnly(23, 59, 59)))

  // A date string should NOT match
  TextConversions.AsTimeOnly culture "2023-01-15" |> should equal None
  TextConversions.AsTimeOnly culture "invalid" |> should equal None
  TextConversions.AsTimeOnly culture "" |> should equal None
#endif

[<Test>]
let ``Guid conversions``() = 
  let validGuid = Guid.NewGuid()
  let guidString = validGuid.ToString()
  
  TextConversions.AsGuid guidString |> should equal (Some validGuid)
  TextConversions.AsGuid (guidString.ToUpper()) |> should equal (Some validGuid)
  TextConversions.AsGuid (" " + guidString + " ") |> should equal (Some validGuid)
  
  // Different GUID formats
  TextConversions.AsGuid "6F9619FF-8B86-D011-B42D-00C04FC964FF" |> Option.isSome |> should equal true
  TextConversions.AsGuid "{6F9619FF-8B86-D011-B42D-00C04FC964FF}" |> Option.isSome |> should equal true
  TextConversions.AsGuid "(6F9619FF-8B86-D011-B42D-00C04FC964FF)" |> Option.isSome |> should equal true
  TextConversions.AsGuid "6F9619FF8B86D011B42D00C04FC964FF" |> Option.isSome |> should equal true
  
  // Invalid values
  TextConversions.AsGuid "invalid-guid" |> should equal None
  TextConversions.AsGuid "" |> should equal None
  TextConversions.AsGuid "6F9619FF-8B86-D011-B42D" |> should equal None

[<Test>]
let ``String conversions``() = 
  TextConversions.AsString "hello" |> should equal (Some "hello")
  TextConversions.AsString "" |> should equal None
  TextConversions.AsString "  test  " |> should equal (Some "  test  ")
  TextConversions.AsString "   " |> should equal None

[<Test>]
let ``Currency adorner removal``() = 
  let culture = CultureInfo.InvariantCulture
  
  // Various currency symbols
  TextConversions.AsDecimal culture "$123.45" |> should equal (Some 123.45M)
  TextConversions.AsDecimal culture "€123.45" |> should equal (Some 123.45M)  
  TextConversions.AsDecimal culture "£123.45" |> should equal (Some 123.45M)
  TextConversions.AsDecimal culture "¥123" |> should equal (Some 123M)
  TextConversions.AsDecimal culture "₹123.45" |> should equal (Some 123.45M)
  
  // Non-currency adorners
  TextConversions.AsDecimal culture "25%" |> should equal (Some 25M)
  TextConversions.AsDecimal culture "15‰" |> should equal (Some 15M)
  TextConversions.AsDecimal culture "5‱" |> should equal (Some 5M)

[<Test>]  
let ``Missing values handling``() =
  let culture = CultureInfo.InvariantCulture
  let defaultMissingValues = TextConversions.DefaultMissingValues
  
  // All default missing values should be handled
  for missingValue in defaultMissingValues do
    TextConversions.AsFloat defaultMissingValues true culture missingValue |> should equal None
    TextConversions.AsFloat defaultMissingValues false culture missingValue |> should equal (Some Double.NaN)

[<Test>]
let ``Edge cases with whitespace and special characters``() = 
  let culture = CultureInfo.InvariantCulture
  
  // Whitespace handling
  TextConversions.AsInteger culture "  123  " |> should equal (Some 123)
  TextConversions.AsDecimal culture "\t45.67\t" |> should equal (Some 45.67M)
  TextConversions.AsBoolean "\n true \n" |> should equal (Some true)
  
  // Empty and null strings
  TextConversions.AsInteger culture "" |> should equal None
  TextConversions.AsDecimal culture "" |> should equal None
  TextConversions.AsBoolean "" |> should equal None

