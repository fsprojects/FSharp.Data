module FSharp.Data.Tests.TextRuntime

open NUnit.Framework
open FsUnit
open System
open System.Globalization
open FSharp.Data
open FSharp.Data.Runtime

[<Test>]
let ``GetCulture returns InvariantCulture for null or empty``() = 
    TextRuntime.GetCulture(null) |> should equal CultureInfo.InvariantCulture
    TextRuntime.GetCulture("") |> should equal CultureInfo.InvariantCulture
    TextRuntime.GetCulture("  ") |> should equal CultureInfo.InvariantCulture

[<Test>]
let ``GetCulture returns correct culture for valid string``() = 
    let culture = TextRuntime.GetCulture("en-US")
    culture.Name |> should equal "en-US"
    
    let culture2 = TextRuntime.GetCulture("de-DE")
    culture2.Name |> should equal "de-DE"

[<Test>]
let ``GetCulture caches cultures correctly``() = 
    let culture1 = TextRuntime.GetCulture("fr-FR")
    let culture2 = TextRuntime.GetCulture("fr-FR")
    // Should be same reference (cached)
    Object.ReferenceEquals(culture1, culture2) |> should equal true

[<Test>]
let ``GetMissingValues returns default for null or empty``() = 
    let defaultValues = TextRuntime.GetMissingValues(null)
    defaultValues |> should equal TextConversions.DefaultMissingValues
    
    let emptyValues = TextRuntime.GetMissingValues("")
    emptyValues |> should equal TextConversions.DefaultMissingValues
    
    let whitespaceValues = TextRuntime.GetMissingValues("  ")
    whitespaceValues |> should equal TextConversions.DefaultMissingValues

[<Test>]
let ``GetMissingValues parses comma-separated values``() = 
    let values = TextRuntime.GetMissingValues("NA,NULL,#N/A")
    values |> should equal [|"NA"; "NULL"; "#N/A"|]
    
    let valuesWithSpaces = TextRuntime.GetMissingValues("N/A, null , missing")
    valuesWithSpaces |> should equal [|"N/A"; " null "; " missing"|]

[<Test>]
let ``ConvertString returns the option as-is``() = 
    TextRuntime.ConvertString(Some "test") |> should equal (Some "test")
    TextRuntime.ConvertString(None) |> should equal None

[<Test>]
let ``ConvertInteger with culture``() = 
    TextRuntime.ConvertInteger("en-US", Some "123") |> should equal (Some 123)
    TextRuntime.ConvertInteger("de-DE", Some "456") |> should equal (Some 456)
    TextRuntime.ConvertInteger("en-US", Some "invalid") |> should equal None
    TextRuntime.ConvertInteger("en-US", None) |> should equal None

[<Test>]
let ``ConvertInteger64 with culture``() = 
    TextRuntime.ConvertInteger64("en-US", Some "9223372036854775807") |> should equal (Some 9223372036854775807L)
    TextRuntime.ConvertInteger64("en-US", Some "invalid") |> should equal None
    TextRuntime.ConvertInteger64("en-US", None) |> should equal None

[<Test>]
let ``ConvertDecimal with culture``() = 
    TextRuntime.ConvertDecimal("en-US", Some "123.45") |> should equal (Some 123.45m)
    TextRuntime.ConvertDecimal("en-US", Some "67.89") |> should equal (Some 67.89m)
    TextRuntime.ConvertDecimal("en-US", Some "invalid") |> should equal None
    TextRuntime.ConvertDecimal("en-US", None) |> should equal None

[<Test>]
let ``ConvertFloat with culture and missing values``() = 
    TextRuntime.ConvertFloat("en-US", "NA,NULL", Some "123.45") |> should equal (Some 123.45)
    TextRuntime.ConvertFloat("en-US", "NA,NULL", Some "NA") |> should equal None
    TextRuntime.ConvertFloat("en-US", "NA,NULL", Some "NULL") |> should equal None
    TextRuntime.ConvertFloat("en-US", "NA,NULL", Some "invalid") |> should equal None
    TextRuntime.ConvertFloat("en-US", "NA,NULL", None) |> should equal None

[<Test>]
let ``ConvertBoolean``() = 
    TextRuntime.ConvertBoolean(Some "true") |> should equal (Some true)
    TextRuntime.ConvertBoolean(Some "false") |> should equal (Some false)
    TextRuntime.ConvertBoolean(Some "1") |> should equal (Some true)
    TextRuntime.ConvertBoolean(Some "0") |> should equal (Some false)
    TextRuntime.ConvertBoolean(Some "invalid") |> should equal None
    TextRuntime.ConvertBoolean(None) |> should equal None

[<Test>]
let ``ConvertDateTime with culture``() = 
    let result = TextRuntime.ConvertDateTime("en-US", Some "2023-12-25")
    result |> should not' (equal None)
    result.Value.Year |> should equal 2023
    result.Value.Month |> should equal 12
    result.Value.Day |> should equal 25
    
    TextRuntime.ConvertDateTime("en-US", Some "invalid") |> should equal None
    TextRuntime.ConvertDateTime("en-US", None) |> should equal None

[<Test>]
let ``ConvertDateTimeOffset with culture``() = 
    let result = TextRuntime.ConvertDateTimeOffset("en-US", Some "2023-12-25T10:30:00+02:00")
    result |> should not' (equal None)
    result.Value.Year |> should equal 2023
    result.Value.Offset.Hours |> should equal 2
    
    TextRuntime.ConvertDateTimeOffset("en-US", Some "invalid") |> should equal None
    TextRuntime.ConvertDateTimeOffset("en-US", None) |> should equal None

[<Test>]
let ``ConvertTimeSpan with culture``() = 
    let result = TextRuntime.ConvertTimeSpan("en-US", Some "02:30:45")
    result |> should not' (equal None)
    result.Value.Hours |> should equal 2
    result.Value.Minutes |> should equal 30
    result.Value.Seconds |> should equal 45
    
    TextRuntime.ConvertTimeSpan("en-US", Some "invalid") |> should equal None
    TextRuntime.ConvertTimeSpan("en-US", None) |> should equal None

[<Test>]
let ``ConvertGuid``() = 
    let guid = Guid.NewGuid()
    let result = TextRuntime.ConvertGuid(Some (guid.ToString()))
    result |> should equal (Some guid)
    
    TextRuntime.ConvertGuid(Some "invalid") |> should equal None
    TextRuntime.ConvertGuid(None) |> should equal None

[<Test>]
let ``ConvertStringBack``() = 
    TextRuntime.ConvertStringBack(Some "test") |> should equal "test"
    TextRuntime.ConvertStringBack(None) |> should equal ""

[<Test>]
let ``ConvertIntegerBack with culture``() = 
    TextRuntime.ConvertIntegerBack("en-US", Some 1234) |> should equal "1234"
    TextRuntime.ConvertIntegerBack("de-DE", Some 1234) |> should equal "1234"
    TextRuntime.ConvertIntegerBack("en-US", None) |> should equal ""

[<Test>]
let ``ConvertInteger64Back with culture``() = 
    TextRuntime.ConvertInteger64Back("en-US", Some 1234567890123456789L) |> should equal "1234567890123456789"
    TextRuntime.ConvertInteger64Back("en-US", None) |> should equal ""

[<Test>]
let ``ConvertDecimalBack with culture``() = 
    let result = TextRuntime.ConvertDecimalBack("en-US", Some 123.45m)
    result |> should equal "123.45"
    TextRuntime.ConvertDecimalBack("en-US", None) |> should equal ""

[<Test>]
let ``ConvertFloatBack with NaN and missing values``() = 
    TextRuntime.ConvertFloatBack("en-US", "NA,NULL", Some 123.45) |> should equal "123.45"
    TextRuntime.ConvertFloatBack("en-US", "NA,NULL", Some Double.NaN) |> should equal "NA"
    TextRuntime.ConvertFloatBack("en-US", "", Some Double.NaN) |> should equal "NaN"
    TextRuntime.ConvertFloatBack("en-US", "NA,NULL", None) |> should equal ""

[<Test>]
let ``ConvertBooleanBack with different formats``() = 
    TextRuntime.ConvertBooleanBack(Some true, false) |> should equal "true"
    TextRuntime.ConvertBooleanBack(Some false, false) |> should equal "false"
    TextRuntime.ConvertBooleanBack(Some true, true) |> should equal "1"
    TextRuntime.ConvertBooleanBack(Some false, true) |> should equal "0"
    TextRuntime.ConvertBooleanBack(None, false) |> should equal ""
    TextRuntime.ConvertBooleanBack(None, true) |> should equal ""

[<Test>]
let ``ConvertDateTimeBack with ISO format``() = 
    let dateTime = DateTime(2023, 12, 25, 15, 30, 45, DateTimeKind.Utc)
    let result = TextRuntime.ConvertDateTimeBack("en-US", Some dateTime)
    result |> should startWith "2023-12-25T15:30:45"
    TextRuntime.ConvertDateTimeBack("en-US", None) |> should equal ""

[<Test>]
let ``ConvertDateTimeOffsetBack with ISO format``() = 
    let dto = DateTimeOffset(2023, 12, 25, 15, 30, 45, TimeSpan.FromHours(2))
    let result = TextRuntime.ConvertDateTimeOffsetBack("en-US", Some dto)
    result |> should contain "2023-12-25T15:30:45"
    result |> should contain "+02:00"
    TextRuntime.ConvertDateTimeOffsetBack("en-US", None) |> should equal ""

[<Test>]
let ``ConvertTimeSpanBack``() = 
    let timeSpan = TimeSpan(2, 30, 45)
    let result = TextRuntime.ConvertTimeSpanBack("en-US", Some timeSpan)
    result |> should equal "2:30:45"
    TextRuntime.ConvertTimeSpanBack("en-US", None) |> should equal ""

[<Test>]
let ``ConvertGuidBack``() = 
    let guid = Guid.NewGuid()
    TextRuntime.ConvertGuidBack(Some guid) |> should equal (guid.ToString())
    TextRuntime.ConvertGuidBack(None) |> should equal ""

[<Test>]
let ``GetNonOptionalValue with Some values``() = 
    TextRuntime.GetNonOptionalValue("test", Some "value", None) |> should equal "value"
    TextRuntime.GetNonOptionalValue("test", Some 42, None) |> should equal 42

[<Test>]
let ``GetNonOptionalValue with None for string returns empty``() = 
    TextRuntime.GetNonOptionalValue<string>("test", None, None) |> should equal ""

[<Test>]
let ``GetNonOptionalValue with None for float returns NaN``() = 
    let result = TextRuntime.GetNonOptionalValue<float>("test", None, None)
    Double.IsNaN(result) |> should equal true

[<Test>]
let ``GetNonOptionalValue throws for missing non-special types``() = 
    (fun () -> TextRuntime.GetNonOptionalValue<int>("test", None, None) |> ignore)
    |> should throw typeof<Exception>

[<Test>]
let ``GetNonOptionalValue with original value provides helpful error``() = 
    (fun () -> TextRuntime.GetNonOptionalValue<int>("test", None, Some "invalid") |> ignore)
    |> should throw typeof<Exception>

[<Test>]
let ``OptionToNullable conversion``() = 
    let nullable = TextRuntime.OptionToNullable(Some 42)
    nullable.HasValue |> should equal true
    nullable.Value |> should equal 42
    
    let nullableNone = TextRuntime.OptionToNullable<int>(None)
    nullableNone.HasValue |> should equal false

[<Test>]
let ``NullableToOption conversion``() = 
    let nullable = Nullable(42)
    TextRuntime.NullableToOption(nullable) |> should equal (Some 42)
    
    let nullableEmpty = Nullable<int>()
    TextRuntime.NullableToOption(nullableEmpty) |> should equal None

[<Test>]
let ``AsyncMap transformation``() = 
    async {
        let valueAsync = async { return 5 }
        let mapping = Func<int, string>(fun x -> sprintf "Value: %d" x)
        let! result = TextRuntime.AsyncMap(valueAsync, mapping)
        result |> should equal "Value: 5"
    } |> Async.RunSynchronously