/// Unsafe extension methods that can be used to work with CsvRow in a less safe, but shorter way.
/// This module also provides the dynamic operator.
namespace FSharp.Data

open System
open System.Globalization
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open FSharp.Data
open FSharp.Data.Runtime

/// Extension methods with conversions from strings to other types
[<Extension>]
type StringExtensions =

    /// <summary>Converts the string to an integer. Fails if the value is not a valid integer.</summary>
    /// <param name="x">The string to convert.</param>
    /// <param name="cultureInfo">Optional culture info for parsing. Defaults to InvariantCulture.</param>
    [<Extension>]
    static member AsInteger(x: String, [<Optional>] ?cultureInfo) =
        let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture

        match TextConversions.AsInteger cultureInfo x with
        | Some i -> i
        | _ -> failwithf "Not an int: %s" x

    /// <summary>Converts the string to a 64-bit integer. Fails if the value is not a valid 64-bit integer.</summary>
    /// <param name="x">The string to convert.</param>
    /// <param name="cultureInfo">Optional culture info for parsing. Defaults to InvariantCulture.</param>
    [<Extension>]
    static member AsInteger64(x: String, [<Optional>] ?cultureInfo) =
        let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture

        match TextConversions.AsInteger64 cultureInfo x with
        | Some i -> i
        | _ -> failwithf "Not an int64: %s" x

    /// <summary>Converts the string to a decimal. Fails if the value is not a valid decimal.</summary>
    /// <param name="x">The string to convert.</param>
    /// <param name="cultureInfo">Optional culture info for parsing. Defaults to InvariantCulture.</param>
    [<Extension>]
    static member AsDecimal(x: String, [<Optional>] ?cultureInfo) =
        let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture

        match TextConversions.AsDecimal cultureInfo x with
        | Some d -> d
        | _ -> failwithf "Not a decimal: %s" x

    /// <summary>Converts the string to a float. Fails if the value is not a valid float.</summary>
    /// <param name="x">The string to convert.</param>
    /// <param name="cultureInfo">Optional culture info for parsing. Defaults to InvariantCulture.</param>
    /// <param name="missingValues">Values to treat as missing (NaN). Defaults to standard missing value strings.</param>
    [<Extension>]
    static member AsFloat(x: String, [<Optional>] ?cultureInfo, [<Optional>] ?missingValues) =
        let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
        let missingValues = defaultArg missingValues TextConversions.DefaultMissingValues

        match TextConversions.AsFloat missingValues false cultureInfo x with
        | Some f -> f
        | _ -> failwithf "Not a float: %s" x

    /// <summary>Converts the string to a boolean. Fails if the value is not a valid boolean.</summary>
    /// <param name="x">The string to convert. Accepts "true", "false", "yes", "no", "1", "0" (case-insensitive).</param>
    [<Extension>]
    static member AsBoolean(x: String) =
        match TextConversions.AsBoolean x with
        | Some b -> b
        | _ -> failwithf "Not a boolean: %s" x

    /// <summary>Converts the string to a DateTime. Fails if the value is not a valid date/time string.</summary>
    /// <param name="x">The string to convert. Accepts ISO 8601 format or MSFT JSON date format.</param>
    /// <param name="cultureInfo">Optional culture info for parsing. Defaults to InvariantCulture.</param>
    [<Extension>]
    static member AsDateTime(x: String, [<Optional>] ?cultureInfo) =
        let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture

        match TextConversions.AsDateTime cultureInfo x with
        | Some d -> d
        | _ -> failwithf "Not a datetime: %s" x

    /// <summary>Converts the string to a DateTimeOffset. Fails if the value is not a valid date/time with offset string.</summary>
    /// <param name="x">The string to convert. Accepts ISO 8601 format with timezone offset or MSFT JSON date with offset.</param>
    /// <param name="cultureInfo">Optional culture info for parsing. Defaults to InvariantCulture.</param>
    [<Extension>]
    static member AsDateTimeOffset(x, [<Optional>] ?cultureInfo) =
        let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture

        match TextConversions.AsDateTimeOffset cultureInfo x with
        | Some d -> d
        | _ -> failwithf "Not a datetime offset: %s" <| x

    /// <summary>Converts the string to a TimeSpan. Fails if the value is not a valid time span string.</summary>
    /// <param name="x">The string to convert.</param>
    /// <param name="cultureInfo">Optional culture info for parsing. Defaults to InvariantCulture.</param>
    [<Extension>]
    static member AsTimeSpan(x: String, [<Optional>] ?cultureInfo) =
        let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture

        match TextConversions.AsTimeSpan cultureInfo x with
        | Some t -> t
        | _ -> failwithf "Not a time span: %s" x

    /// <summary>Converts the string to a Guid. Fails if the value is not a valid GUID string.</summary>
    /// <param name="x">The string to convert.</param>
    [<Extension>]
    static member AsGuid(x: String) =
        match x |> TextConversions.AsGuid with
        | Some g -> g
        | _ -> failwithf "Not a guid: %s" x

/// Provides the dynamic operator for getting column values by name from CSV rows
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CsvExtensions =

    /// Get the value of a column by name from a CSV row
    let (?) (csvRow: CsvRow) (columnName: string) = csvRow.[columnName]
