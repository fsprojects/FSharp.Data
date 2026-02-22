namespace FSharp.Data.Runtime

open System
open System.Collections.Generic
open System.Globalization
open FSharp.Data

/// Static helper methods called from the generated code for working with text
type TextRuntime =

    [<ThreadStatic>]
    [<DefaultValue>]
    static val mutable private cultureInfoCache: Dictionary<string, CultureInfo>

    /// Returns CultureInfo matching the specified culture string
    /// (or InvariantCulture if the argument is null or empty)
    static member GetCulture(cultureStr) =
        if String.IsNullOrWhiteSpace cultureStr then
            CultureInfo.InvariantCulture
        else
            let mutable cache = TextRuntime.cultureInfoCache

            if isNull cache then
                cache <- Dictionary<string, CultureInfo>()
                TextRuntime.cultureInfoCache <- cache

            match cache.TryGetValue cultureStr with
            | true, v -> v
            | _, _ ->
                let v = CultureInfo cultureStr
                cache.[cultureStr] <- v
                v

    static member GetMissingValues(missingValuesStr) =
        if String.IsNullOrWhiteSpace missingValuesStr then
            TextConversions.DefaultMissingValues
        else
            missingValuesStr.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)

    // --------------------------------------------------------------------------------------
    // string option -> type

    static member ConvertString(text: string option) = text

    static member ConvertInteger(cultureStr, text) =
        text
        |> Option.bind (TextConversions.AsInteger(TextRuntime.GetCulture cultureStr))

    static member ConvertInteger64(cultureStr, text) =
        text
        |> Option.bind (TextConversions.AsInteger64(TextRuntime.GetCulture cultureStr))

    static member ConvertDecimal(cultureStr, text) =
        text
        |> Option.bind (TextConversions.AsDecimal(TextRuntime.GetCulture cultureStr))

    static member ConvertFloat(cultureStr, missingValuesStr, text) =
        text
        |> Option.bind (
            TextConversions.AsFloat
                (TextRuntime.GetMissingValues missingValuesStr)
                true
                (TextRuntime.GetCulture cultureStr)
        )

    static member ConvertBoolean(text) =
        text |> Option.bind TextConversions.AsBoolean

    static member ConvertDateTime(cultureStr, text) =
        text
        |> Option.bind (TextConversions.AsDateTime(TextRuntime.GetCulture cultureStr))

    static member ConvertDateTimeOffset(cultureStr, text) =
        let culture = TextRuntime.GetCulture cultureStr

        text
        |> Option.bind (fun s ->
            match TextConversions.AsDateTimeOffset culture s with
            | Some dto -> Some dto
            | None ->
                // Fall back for xs:dateTime values without timezone (DateTimeKind.Unspecified).
                // AsDateTime converts Unspecified to Local, so DateTimeOffset(dt) uses the local offset.
                TextConversions.AsDateTime culture s |> Option.map DateTimeOffset)

    static member ConvertTimeSpan(cultureStr, text) =
        text
        |> Option.bind (TextConversions.AsTimeSpan(TextRuntime.GetCulture cultureStr))

#if NET6_0_OR_GREATER
    static member ConvertDateOnly(cultureStr, text) =
        text
        |> Option.bind (TextConversions.AsDateOnly(TextRuntime.GetCulture cultureStr))

    static member ConvertTimeOnly(cultureStr, text) =
        text
        |> Option.bind (TextConversions.AsTimeOnly(TextRuntime.GetCulture cultureStr))
#endif

    static member ConvertGuid(text) =
        text |> Option.bind TextConversions.AsGuid

    // --------------------------------------------------------------------------------------
    // type -> string

    static member ConvertStringBack(value) = defaultArg value ""

    static member ConvertIntegerBack(cultureStr, value: int option) =
        match value with
        | Some value -> value.ToString(TextRuntime.GetCulture cultureStr)
        | None -> ""

    static member ConvertInteger64Back(cultureStr, value: int64 option) =
        match value with
        | Some value -> value.ToString(TextRuntime.GetCulture cultureStr)
        | None -> ""

    static member ConvertDecimalBack(cultureStr, value: decimal option) =
        match value with
        | Some value -> value.ToString(TextRuntime.GetCulture cultureStr)
        | None -> ""

    static member ConvertFloatBack(cultureStr, missingValuesStr, value: float option) =
        match value with
        | Some value ->
            if Double.IsNaN value then
                let missingValues = TextRuntime.GetMissingValues missingValuesStr

                if missingValues.Length = 0 then
                    (TextRuntime.GetCulture cultureStr).NumberFormat.NaNSymbol
                else
                    missingValues.[0]
            else
                value.ToString(TextRuntime.GetCulture cultureStr)
        | None -> ""

    static member ConvertBooleanBack(value: bool option, use0and1) =
        match value with
        | Some value when use0and1 -> if value then "1" else "0"
        | Some value -> if value then "true" else "false"
        | None -> ""

    static member ConvertDateTimeBack(cultureStr, value: DateTime option) =
        match value with
        | Some value -> value.ToString("O", TextRuntime.GetCulture cultureStr)
        | None -> ""

    static member ConvertDateTimeOffsetBack(cultureStr, value: DateTimeOffset option) =
        match value with
        | Some value -> value.ToString("O", TextRuntime.GetCulture cultureStr)
        | None -> ""

    static member ConvertTimeSpanBack(cultureStr, value: TimeSpan option) =
        match value with
        | Some value -> value.ToString("g", TextRuntime.GetCulture cultureStr)
        | None -> ""

#if NET6_0_OR_GREATER
    static member ConvertDateOnlyBack(cultureStr, value: DateOnly option) =
        match value with
        | Some value -> value.ToString("O", TextRuntime.GetCulture cultureStr)
        | None -> ""

    static member ConvertTimeOnlyBack(cultureStr, value: TimeOnly option) =
        match value with
        | Some value -> value.ToString("O", TextRuntime.GetCulture cultureStr)
        | None -> ""
#endif

    static member ConvertGuidBack(value: Guid option) =
        match value with
        | Some value -> value.ToString()
        | None -> ""

    // --------------------------------------------------------------------------------------

    /// Operation that extracts the value from an option and reports a meaningful error message when the value is not there
    /// For missing strings we return "", and for missing doubles we return NaN
    /// For other types an error is thrown
    static member GetNonOptionalValue<'T>(name: string, opt: option<'T>, originalValue) : 'T =
        match opt, originalValue with
        | Some value, _ -> value
        | None, _ when typeof<'T> = typeof<string> -> "" |> unbox
        | None, _ when typeof<'T> = typeof<float> -> Double.NaN |> unbox
        | None, None -> failwithf "%s is missing" name
        | None, Some originalValue -> failwithf "Expecting %s in %s, got %s" (typeof<'T>.Name) name originalValue

    /// Turn an F# option type Option<'T> containing a primitive
    /// value type into a .NET type Nullable<'T>
    static member OptionToNullable opt =
        match opt with
        | Some v -> Nullable v
        | _ -> Nullable()

    /// Turn a .NET type Nullable<'T> to an F# option type Option<'T>
    static member NullableToOption(nullable: Nullable<_>) =
        if nullable.HasValue then Some nullable.Value else None

    /// Turn a sync operation into an async operation
    static member AsyncMap<'T, 'R>(valueAsync: Async<'T>, mapping: Func<'T, 'R>) =
        async {
            let! value = valueAsync
            return mapping.Invoke value
        }
