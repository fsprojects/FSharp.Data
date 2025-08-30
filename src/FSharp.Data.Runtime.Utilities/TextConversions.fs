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

    let ParseISO8601FormattedDateTime text cultureInfo =
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

    static member AsInteger cultureInfo text =
        Int32.TryParse(TextConversions.RemoveAdorners text, NumberStyles.Integer, cultureInfo)
        |> asOption

    static member AsInteger64 cultureInfo text =
        Int64.TryParse(TextConversions.RemoveAdorners text, NumberStyles.Integer, cultureInfo)
        |> asOption

    static member AsDecimal cultureInfo text =
        Decimal.TryParse(TextConversions.RemoveAdorners text, NumberStyles.Currency, cultureInfo)
        |> asOption

    /// if useNoneForMissingValues is true, NAs are returned as None, otherwise Some Double.NaN is used
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

    static member AsBoolean(text: string) =
        // Fast path: check for common single-character boolean values without allocation
        if text.Length = 1 then
            match text.[0] with
            | '1' -> Some true
            | '0' -> Some false
            | _ -> None
        else
            // Fast path: check if text needs trimming
            let needsTrimming =
                text.Length > 0
                && (Char.IsWhiteSpace(text.[0]) || Char.IsWhiteSpace(text.[text.Length - 1]))

            let processedText = if needsTrimming then text.Trim() else text

            // Optimized case-insensitive matching using direct comparisons for common values
            match processedText.Length with
            | 2 when
                (processedText.[0] = 'n' || processedText.[0] = 'N')
                && (processedText.[1] = 'o' || processedText.[1] = 'O')
                ->
                Some false
            | 3 when
                (processedText.[0] = 'y' || processedText.[0] = 'Y')
                && (processedText.[1] = 'e' || processedText.[1] = 'E')
                && (processedText.[2] = 's' || processedText.[2] = 'S')
                ->
                Some true
            | 4 when
                (processedText.[0] = 't' || processedText.[0] = 'T')
                && (processedText.[1] = 'r' || processedText.[1] = 'R')
                && (processedText.[2] = 'u' || processedText.[2] = 'U')
                && (processedText.[3] = 'e' || processedText.[3] = 'E')
                ->
                Some true
            | 5 when
                (processedText.[0] = 'f' || processedText.[0] = 'F')
                && (processedText.[1] = 'a' || processedText.[1] = 'A')
                && (processedText.[2] = 'l' || processedText.[2] = 'L')
                && (processedText.[3] = 's' || processedText.[3] = 'S')
                && (processedText.[4] = 'e' || processedText.[4] = 'E')
                ->
                Some false
            | _ ->
                // Fallback to original method for other cases
                match processedText with
                | StringEqualsIgnoreCase "true"
                | StringEqualsIgnoreCase "yes"
                | StringEqualsIgnoreCase "1" -> Some true
                | StringEqualsIgnoreCase "false"
                | StringEqualsIgnoreCase "no"
                | StringEqualsIgnoreCase "0" -> Some false
                | _ -> None

    /// Parse date time using either the JSON milliseconds format or using ISO 8601
    /// that is, either `/Date(<msec-since-1/1/1970>)/` or something
    /// along the lines of `2013-01-28T00:37Z`
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

    static member AsDateTimeOffset cultureInfo (text: string) =
        // get TimeSpan presentation from 4-digit integers like 0000 or -0600
        let getTimeSpanFromHourMin (hourMin: int) =
            let hr = (hourMin / 100) |> float |> TimeSpan.FromHours
            let min = (hourMin % 100) |> float |> TimeSpan.FromMinutes
            hr.Add min

        let offset str =
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

    static member AsTimeSpan (cultureInfo: CultureInfo) (text: string) =
        match TimeSpan.TryParse(text, cultureInfo) with
        | true, t -> Some t
        | _ -> None

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
