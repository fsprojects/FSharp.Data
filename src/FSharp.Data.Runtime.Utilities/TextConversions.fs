// --------------------------------------------------------------------------------------
// Helper operations for converting converting string values to other types
// --------------------------------------------------------------------------------------

namespace FSharp.Data

open System
open System.Globalization
open System.Text.RegularExpressions

// --------------------------------------------------------------------------------------

[<AutoOpen>]
module private Helpers =

    /// Convert the result of TryParse to option type
    let asOption =
        function
        | true, v -> Some v
        | _ -> None

    [<return: Struct>]
    let (|StringEqualsIgnoreCase|_|) (s1: string) s2 =
        if s1.Equals(s2, StringComparison.OrdinalIgnoreCase) then
            ValueSome()
        else
            ValueNone

    [<return: Struct>]
    let (|OneOfIgnoreCase|_|) set str =
        if Array.exists (fun s -> StringComparer.OrdinalIgnoreCase.Compare(s, str) = 0) set then
            ValueSome()
        else
            ValueNone

    // note on the regex we have /Date()/ and not \/Date()\/ because the \/ escaping
    // is already taken care of before AsDateTime is called
    let msDateRegex =
        lazy Regex(@"^/Date\((-?\d+)([-+]\d+)?\)/$", RegexOptions.Compiled)

    let dateTimeStyles =
        DateTimeStyles.AllowWhiteSpaces ||| DateTimeStyles.RoundtripKind

    let ParseISO8601FormattedDateTime (text: string) cultureInfo =
        match DateTime.TryParse(text, cultureInfo, dateTimeStyles) with
        | true, d -> d |> ValueSome
        | false, _ -> ValueNone

// --------------------------------------------------------------------------------------

/// Conversions from string to string/int/int64/decimal/float/boolean/datetime/timespan/guid options
type TextConversions private () =

    /// `NaN` `NA` `N/A` `#N/A` `:` `-` `TBA` `TBD`
    static member val DefaultMissingValues = [| "NaN"; "NA"; "N/A"; "#N/A"; ":"; "-"; "TBA"; "TBD" |]

    /// `%` `‰` `‱`
    static member val DefaultNonCurrencyAdorners = [| '%'; '‰'; '‱' |] |> Set.ofArray

    /// `¤` `$` `¢` `£` `¥` `₱` `﷼` `₤` `₭` `₦` `₨` `₩` `₮` `€` `฿` `₡` `៛` `؋` `₴` `₪` `₫` `₹` `ƒ`
    static member val DefaultCurrencyAdorners =
        [| '¤'
           '$'
           '¢'
           '£'
           '¥'
           '₱'
           '﷼'
           '₤'
           '₭'
           '₦'
           '₨'
           '₩'
           '₮'
           '€'
           '฿'
           '₡'
           '៛'
           '؋'
           '₴'
           '₪'
           '₫'
           '₹'
           'ƒ' |]
        |> Set.ofArray

    static member val private DefaultRemovableAdornerCharacters =
        Set.union TextConversions.DefaultNonCurrencyAdorners TextConversions.DefaultCurrencyAdorners

    //This removes any adorners that might otherwise cause the inference to infer string. A notable a change is
    //Currency Symbols are now treated as an Adorner like a '%' sign thus are now independent
    //of the culture. Which is probably better since we have lots of scenarios where we want to
    //consume values prefixed with € or $ but in a different culture.
    static member private RemoveAdorners(value: string) =
        // Fast path: check if any adorners exist before doing expensive operations
        let mutable hasAdorners = false

        for i = 0 to value.Length - 1 do
            if TextConversions.DefaultRemovableAdornerCharacters.Contains(value.[i]) then
                hasAdorners <- true

        if not hasAdorners then
            // No adorners found, return original string to avoid allocation
            value
        else
            // Adorners found, perform filtering
            String(
                value.ToCharArray()
                |> Array.filter (not << TextConversions.DefaultRemovableAdornerCharacters.Contains)
            )

    /// Turns empty or null string value into None, otherwise returns Some
    static member AsString str =
        if String.IsNullOrWhiteSpace str then None else Some str

    /// <summary>Attempts to parse the string as an integer using the given culture.</summary>
    /// <param name="cultureInfo">The culture to use for parsing.</param>
    /// <param name="text">The string to parse. Currency and percentage adorners are removed before parsing.</param>
    static member AsInteger cultureInfo text =
        Int32.TryParse(TextConversions.RemoveAdorners text, NumberStyles.Integer, cultureInfo)
        |> asOption

    /// <summary>Attempts to parse the string as a 64-bit integer using the given culture.</summary>
    /// <param name="cultureInfo">The culture to use for parsing.</param>
    /// <param name="text">The string to parse. Currency and percentage adorners are removed before parsing.</param>
    static member AsInteger64 cultureInfo text =
        Int64.TryParse(TextConversions.RemoveAdorners text, NumberStyles.Integer, cultureInfo)
        |> asOption

    /// <summary>Attempts to parse the string as a decimal using the given culture.</summary>
    /// <param name="cultureInfo">The culture to use for parsing.</param>
    /// <param name="text">The string to parse. Currency and percentage adorners are removed before parsing.</param>
    static member AsDecimal cultureInfo text =
        Decimal.TryParse(TextConversions.RemoveAdorners text, NumberStyles.Currency, cultureInfo)
        |> asOption

    /// <summary>Attempts to parse the string as a float using the given culture.</summary>
    /// <param name="missingValues">Values to treat as missing. If matched, returns None or Some NaN depending on <paramref name="useNoneForMissingValues"/>.</param>
    /// <param name="useNoneForMissingValues">If true, missing values and NaN are returned as None; otherwise Some Double.NaN is used.</param>
    /// <param name="cultureInfo">The culture to use for parsing.</param>
    /// <param name="text">The string to parse.</param>
    static member AsFloat missingValues useNoneForMissingValues cultureInfo (text: string) =
        match text.Trim() with
        | OneOfIgnoreCase missingValues -> if useNoneForMissingValues then None else Some Double.NaN
        | _ ->
            Double.TryParse(text, NumberStyles.Any, cultureInfo)
            |> asOption
            |> Option.bind (fun f ->
                if useNoneForMissingValues && Double.IsNaN f then
                    None
                else
                    Some f)

    /// <summary>Attempts to parse the string as a boolean. Accepts "true", "false", "yes", "no", "1", "0" (case-insensitive).</summary>
    /// <param name="text">The string to parse.</param>
    static member AsBoolean(text: string) =
        match text.Trim() with
        | StringEqualsIgnoreCase "true"
        | StringEqualsIgnoreCase "yes"
        | StringEqualsIgnoreCase "1" -> Some true
        | StringEqualsIgnoreCase "false"
        | StringEqualsIgnoreCase "no"
        | StringEqualsIgnoreCase "0" -> Some false
        | _ -> None

    /// <summary>Attempts to parse the string as a DateTime using ISO 8601 format or MSFT JSON date format.</summary>
    /// <param name="cultureInfo">The culture to use for parsing.</param>
    /// <param name="text">The string to parse. Accepts e.g. <c>2013-01-28T00:37Z</c> or <c>/Date(1234567890)/</c>.</param>
    static member AsDateTime cultureInfo (text: string) =
        // Try parse "Date(<msec>)" style format
        let matchesMS = msDateRegex.Value.Match(text.Trim())

        if matchesMS.Success then
            matchesMS.Groups.[1].Value
            |> Double.Parse
            |> DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds
            |> Some
        else
            // Parse ISO 8601 format, fixing time zone if needed
            match ParseISO8601FormattedDateTime text cultureInfo with
            | ValueSome d when d.Kind = DateTimeKind.Unspecified -> new DateTime(d.Ticks, DateTimeKind.Local) |> Some
            | ValueSome x -> Some x
            | ValueNone -> None

    /// <summary>Attempts to parse the string as a DateTimeOffset using ISO 8601 format or MSFT JSON date with offset.</summary>
    /// <param name="cultureInfo">The culture to use for parsing.</param>
    /// <param name="text">The string to parse. The timezone offset must be present.</param>
    static member AsDateTimeOffset cultureInfo (text: string) =
        // get TimeSpan presentation from 4-digit integers like 0000 or -0600
        let getTimeSpanFromHourMin (hourMin: int) =
            let hr = (hourMin / 100) |> float |> TimeSpan.FromHours
            let min = (hourMin % 100) |> float |> TimeSpan.FromMinutes
            hr.Add min

        let offset (str: string) =
            match Int32.TryParse str with
            | true, v -> getTimeSpanFromHourMin v |> ValueSome
            | false, _ -> ValueNone

        let matchesMS = msDateRegex.Value.Match(text.Trim())

        if
            matchesMS.Success
            && matchesMS.Groups.[2].Success
            && matchesMS.Groups.[2].Value.Length = 5
        then
            // only if the timezone offset is specified with '-' or '+' prefix, after the millis
            // e.g. 1231456+1000, 123123+0000, 123123-0500, etc.
            match offset matchesMS.Groups.[2].Value with
            | ValueSome ofst ->
                matchesMS.Groups.[1].Value
                |> Double.Parse
                |> DateTimeOffset(1970, 1, 1, 0, 0, 0, ofst).AddMilliseconds
                |> Some
            | ValueNone -> None
        else
            match ParseISO8601FormattedDateTime text cultureInfo with
            | ValueSome d when d.Kind <> DateTimeKind.Unspecified ->
                match DateTimeOffset.TryParse(text, cultureInfo, dateTimeStyles) with
                | true, dto -> dto |> Some
                | false, _ -> None
            | _ -> None

    /// <summary>Attempts to parse the string as a TimeSpan using the given culture.</summary>
    /// <param name="cultureInfo">The culture to use for parsing.</param>
    /// <param name="text">The string to parse.</param>
    static member AsTimeSpan (cultureInfo: CultureInfo) (text: string) =
        match TimeSpan.TryParse(text, cultureInfo) with
        | true, t -> Some t
        | _ -> None

#if NET6_0_OR_GREATER
    /// <summary>Attempts to parse the string as a DateOnly using the given culture.
    /// Strings that also parse as a DateTime with a non-zero time component are rejected.</summary>
    /// <param name="cultureInfo">The culture to use for parsing.</param>
    /// <param name="text">The string to parse.</param>
    static member AsDateOnly (cultureInfo: CultureInfo) (text: string) =
        let mutable d = DateOnly.MinValue

        if DateOnly.TryParse(text.Trim(), cultureInfo, Globalization.DateTimeStyles.AllowWhiteSpaces, &d) then
            // Reject strings that also parse as DateTime with a non-zero time component,
            // e.g. "2022-06-12T01:02:03" should be DateTime, not DateOnly.
            match DateTime.TryParse(text.Trim(), cultureInfo, Globalization.DateTimeStyles.AllowWhiteSpaces) with
            | true, dt when dt.TimeOfDay <> TimeSpan.Zero -> None
            | _ -> Some d
        else
            None

    /// <summary>Attempts to parse the string as a TimeOnly using the given culture.
    /// Strings that also parse as a DateTime with a specific non-today date are rejected.</summary>
    /// <param name="cultureInfo">The culture to use for parsing.</param>
    /// <param name="text">The string to parse.</param>
    static member AsTimeOnly (cultureInfo: CultureInfo) (text: string) =
        let mutable t = TimeOnly.MinValue

        if TimeOnly.TryParse(text.Trim(), cultureInfo, Globalization.DateTimeStyles.AllowWhiteSpaces, &t) then
            // Reject strings that also parse as DateTime with a specific real date
            // (not today's date used as fill-in), e.g. "2016-10-05T04:05:03" is DateTime, not TimeOnly.
            match DateTime.TryParse(text.Trim(), cultureInfo, Globalization.DateTimeStyles.AllowWhiteSpaces) with
            | true, dt when dt.Date <> DateTime.Today -> None
            | _ -> Some t
        else
            None
#endif

    /// <summary>Attempts to parse the string as a Guid.</summary>
    /// <param name="text">The string to parse. Leading and trailing whitespace is trimmed before parsing.</param>
    static member AsGuid(text: string) = Guid.TryParse(text.Trim()) |> asOption

module internal UnicodeHelper =

    // used http://en.wikipedia.org/wiki/UTF-16#Code_points_U.2B010000_to_U.2B10FFFF as a guide below
    let getUnicodeSurrogatePair num =
        // only code points U+010000 to U+10FFFF supported
        // for conversion to UTF16 surrogate pair
        let codePoint = num - 0x010000u
        let HIGH_TEN_BIT_MASK = 0xFFC00u // 1111|1111|1100|0000|0000
        let LOW_TEN_BIT_MASK = 0x003FFu // 0000|0000|0011|1111|1111
        let leadSurrogate = (codePoint &&& HIGH_TEN_BIT_MASK >>> 10) + 0xD800u
        let trailSurrogate = (codePoint &&& LOW_TEN_BIT_MASK) + 0xDC00u
        char leadSurrogate, char trailSurrogate
