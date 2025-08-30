module FSharp.Data.Tests.JsonConversions

open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.Runtime

[<Test>]
let ``Boolean conversions``() = 
  let asBoolean = JsonConversions.AsBoolean

  JsonValue.Boolean true  |> asBoolean |> should equal (Some true)
  JsonValue.Number 1M     |> asBoolean |> should equal (Some true)
  JsonValue.String "yEs"  |> asBoolean |> should equal (Some true)
  JsonValue.String "trUe" |> asBoolean |> should equal (Some true)
  JsonValue.String "1"    |> asBoolean |> should equal (Some true)

  JsonValue.Boolean false  |> asBoolean |> should equal (Some false)
  JsonValue.Number 0M      |> asBoolean |> should equal (Some false)
  JsonValue.String "nO"    |> asBoolean |> should equal (Some false)
  JsonValue.String "faLSe" |> asBoolean |> should equal (Some false)
  JsonValue.String "0"     |> asBoolean |> should equal (Some false)

  JsonValue.Null          |> asBoolean |> should equal None
  JsonValue.Number 2M     |> asBoolean |> should equal None
  JsonValue.String "blah" |> asBoolean |> should equal None

[<Test>]
let ``Integer conversions from JsonValue`` () =
    let asInteger = JsonConversions.AsInteger System.Globalization.CultureInfo.InvariantCulture

    JsonValue.Number 42M |> asInteger |> should equal (Some 42)
    JsonValue.Number -123M |> asInteger |> should equal (Some -123)
    JsonValue.Number (decimal System.Int32.MaxValue) |> asInteger |> should equal (Some System.Int32.MaxValue)
    JsonValue.Number (decimal System.Int32.MinValue) |> asInteger |> should equal (Some System.Int32.MinValue)
    
    JsonValue.Float 42.0 |> asInteger |> should equal (Some 42)
    JsonValue.Float -123.0 |> asInteger |> should equal (Some -123)
    JsonValue.String "42" |> asInteger |> should equal (Some 42)
    JsonValue.String "-123" |> asInteger |> should equal (Some -123)
    
    JsonValue.Number 42.5M |> asInteger |> should equal None // Not an integer
    JsonValue.Float 42.5 |> asInteger |> should equal None   // Not an integer
    JsonValue.String "not_a_number" |> asInteger |> should equal None
    JsonValue.Boolean true |> asInteger |> should equal None
    JsonValue.Null |> asInteger |> should equal None

[<Test>]
let ``Integer64 conversions from JsonValue`` () =
    let asInteger64 = JsonConversions.AsInteger64 System.Globalization.CultureInfo.InvariantCulture

    JsonValue.Number 42M |> asInteger64 |> should equal (Some 42L)
    JsonValue.Number (decimal System.Int64.MaxValue) |> asInteger64 |> should equal (Some System.Int64.MaxValue)
    JsonValue.Number (decimal System.Int64.MinValue) |> asInteger64 |> should equal (Some System.Int64.MinValue)
    
    JsonValue.Float 42.0 |> asInteger64 |> should equal (Some 42L)
    JsonValue.String "9223372036854775807" |> asInteger64 |> should equal (Some System.Int64.MaxValue)
    JsonValue.String "-9223372036854775808" |> asInteger64 |> should equal (Some System.Int64.MinValue)
    
    JsonValue.Number 42.5M |> asInteger64 |> should equal None // Not an integer
    JsonValue.Float 42.5 |> asInteger64 |> should equal None   // Not an integer
    JsonValue.String "not_a_number" |> asInteger64 |> should equal None
    JsonValue.Array [||] |> asInteger64 |> should equal None

[<Test>]
let ``Decimal conversions from JsonValue`` () =
    let asDecimal = JsonConversions.AsDecimal System.Globalization.CultureInfo.InvariantCulture

    JsonValue.Number 42.5M |> asDecimal |> should equal (Some 42.5M)
    JsonValue.Number -123.456M |> asDecimal |> should equal (Some -123.456M)
    JsonValue.String "42.5" |> asDecimal |> should equal (Some 42.5M)
    JsonValue.String "-123.456" |> asDecimal |> should equal (Some -123.456M)
    
    JsonValue.String "not_a_number" |> asDecimal |> should equal None
    JsonValue.Boolean false |> asDecimal |> should equal None
    JsonValue.Null |> asDecimal |> should equal None
    JsonValue.Array [||] |> asDecimal |> should equal None

[<Test>]
let ``Float conversions from JsonValue`` () =
    let asFloat = JsonConversions.AsFloat [| |] false System.Globalization.CultureInfo.InvariantCulture

    JsonValue.Float 42.5 |> asFloat |> should equal (Some 42.5)
    JsonValue.Number 42.5M |> asFloat |> should equal (Some 42.5)
    JsonValue.String "42.5" |> asFloat |> should equal (Some 42.5)
    JsonValue.String "-123.456" |> asFloat |> should equal (Some -123.456)
    JsonValue.String "1.23e10" |> asFloat |> should equal (Some 1.23e10)
    
    JsonValue.String "not_a_number" |> asFloat |> should equal None
    JsonValue.Boolean true |> asFloat |> should equal None
    JsonValue.Null |> asFloat |> should equal None
    JsonValue.Record [||] |> asFloat |> should equal None

[<Test>]
let ``String conversions from JsonValue with empty handling`` () =
    let asStringNoneForEmpty = JsonConversions.AsString true System.Globalization.CultureInfo.InvariantCulture
    let asStringKeepEmpty = JsonConversions.AsString false System.Globalization.CultureInfo.InvariantCulture

    JsonValue.String "hello" |> asStringNoneForEmpty |> should equal (Some "hello")
    JsonValue.String "hello" |> asStringKeepEmpty |> should equal (Some "hello")
    
    JsonValue.String "" |> asStringNoneForEmpty |> should equal None
    JsonValue.String "" |> asStringKeepEmpty |> should equal (Some "")
    
    JsonValue.Null |> asStringNoneForEmpty |> should equal None
    JsonValue.Null |> asStringKeepEmpty |> should equal (Some "")
    
    JsonValue.Boolean true |> asStringNoneForEmpty |> should equal (Some "true")
    JsonValue.Boolean false |> asStringKeepEmpty |> should equal (Some "false")
    
    JsonValue.Number 42.5M |> asStringNoneForEmpty |> should equal (Some "42.5")
    JsonValue.Float 3.14 |> asStringKeepEmpty |> should equal (Some "3.14")

[<Test>]
let ``String conversions handle various JSON types`` () =
    let asString = JsonConversions.AsString false System.Globalization.CultureInfo.InvariantCulture

    JsonValue.String "test" |> asString |> should equal (Some "test")
    JsonValue.Boolean true |> asString |> should equal (Some "true")
    JsonValue.Boolean false |> asString |> should equal (Some "false")
    JsonValue.Number 123M |> asString |> should equal (Some "123")
    JsonValue.Float 3.14 |> asString |> should equal (Some "3.14")
    JsonValue.Null |> asString |> should equal (Some "")
    
    JsonValue.Array [||] |> asString |> should equal None
    JsonValue.Record [||] |> asString |> should equal None

[<Test>]
let ``DateTime conversions from JsonValue`` () =
    let asDateTime = JsonConversions.AsDateTime System.Globalization.CultureInfo.InvariantCulture

    JsonValue.String "2023-12-25T10:30:45" |> asDateTime |> should equal (Some (System.DateTime(2023, 12, 25, 10, 30, 45)))
    JsonValue.String "2023-12-25" |> asDateTime |> should equal (Some (System.DateTime(2023, 12, 25)))
    
    JsonValue.String "not_a_date" |> asDateTime |> should equal None
    JsonValue.Number 42M |> asDateTime |> should equal None
    JsonValue.Boolean true |> asDateTime |> should equal None
    JsonValue.Null |> asDateTime |> should equal None

[<Test>]
let ``DateTimeOffset conversions from JsonValue`` () =
    let asDateTimeOffset = JsonConversions.AsDateTimeOffset System.Globalization.CultureInfo.InvariantCulture

    JsonValue.String "2023-12-25T10:30:45+02:00" |> asDateTimeOffset 
    |> should equal (Some (System.DateTimeOffset(2023, 12, 25, 10, 30, 45, System.TimeSpan.FromHours(2.0))))
    
    JsonValue.String "not_a_date" |> asDateTimeOffset |> should equal None
    JsonValue.Number 42M |> asDateTimeOffset |> should equal None
    JsonValue.Array [||] |> asDateTimeOffset |> should equal None

[<Test>]
let ``TimeSpan conversions from JsonValue`` () =
    let asTimeSpan = JsonConversions.AsTimeSpan System.Globalization.CultureInfo.InvariantCulture

    JsonValue.String "1:30:45" |> asTimeSpan |> should equal (Some (System.TimeSpan(1, 30, 45)))
    JsonValue.String "10:15:30.500" |> asTimeSpan |> should equal (Some (System.TimeSpan(0, 10, 15, 30, 500)))
    
    JsonValue.String "not_a_timespan" |> asTimeSpan |> should equal None
    JsonValue.Number 42M |> asTimeSpan |> should equal None
    JsonValue.Boolean false |> asTimeSpan |> should equal None

[<Test>]
let ``Guid conversions from JsonValue`` () =
    let asGuid = JsonConversions.AsGuid

    let testGuid = System.Guid.NewGuid()
    JsonValue.String (testGuid.ToString()) |> asGuid |> should equal (Some testGuid)
    JsonValue.String (testGuid.ToString("D")) |> asGuid |> should equal (Some testGuid)
    
    JsonValue.String "not-a-guid" |> asGuid |> should equal None
    JsonValue.String "" |> asGuid |> should equal None
    JsonValue.Number 42M |> asGuid |> should equal None
    JsonValue.Null |> asGuid |> should equal None
    JsonValue.Record [||] |> asGuid |> should equal None

[<Test>]
let ``Conversions handle edge case integer ranges`` () =
    let asInteger = JsonConversions.AsInteger System.Globalization.CultureInfo.InvariantCulture
    let asInteger64 = JsonConversions.AsInteger64 System.Globalization.CultureInfo.InvariantCulture

    // Test values outside int32 range but within int64 range
    let largeValue = (decimal System.Int32.MaxValue) + 1M
    JsonValue.Number largeValue |> asInteger |> should equal None     // Too large for int32
    JsonValue.Number largeValue |> asInteger64 |> should equal (Some (int64 largeValue))  // OK for int64

    let smallValue = (decimal System.Int32.MinValue) - 1M  
    JsonValue.Number smallValue |> asInteger |> should equal None     // Too small for int32
    JsonValue.Number smallValue |> asInteger64 |> should equal (Some (int64 smallValue))  // OK for int64

[<Test>]
let ``Conversions handle different number formats`` () =
    let asDecimal = JsonConversions.AsDecimal System.Globalization.CultureInfo.InvariantCulture
    
    // JSON parsing should work with standard decimal points
    JsonValue.String "42.5" |> asDecimal |> should equal (Some 42.5M)
    JsonValue.String "123.456" |> asDecimal |> should equal (Some 123.456M)
    JsonValue.String "0" |> asDecimal |> should equal (Some 0M)
    JsonValue.String "-99.99" |> asDecimal |> should equal (Some -99.99M)
