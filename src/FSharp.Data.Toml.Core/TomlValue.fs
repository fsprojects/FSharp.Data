// --------------------------------------------------------------------------------------
// TOML type provider - TOML value representation and parser
// --------------------------------------------------------------------------------------

namespace FSharp.Data

open System
open System.IO
open System.Text
open System.Collections.Generic
open System.Globalization
open System.ComponentModel

// --------------------------------------------------------------------------------------
// TOML value type
// --------------------------------------------------------------------------------------

/// Represents a TOML value
[<RequireQualifiedAccess>]
[<StructuredFormatDisplay("{_Print}")>]
type TomlValue =
    | String of string
    | Integer of int64
    | Float of float
    | Boolean of bool
    | OffsetDateTime of DateTimeOffset
    | LocalDateTime of DateTime
    | LocalDate of DateTime
    | LocalTime of TimeSpan
    | Array of TomlValue[]
    | Table of (string * TomlValue)[]

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    member x._Print =
        match x with
        | String s -> sprintf "%A" s
        | Integer i -> sprintf "%d" i
        | Float f -> sprintf "%g" f
        | Boolean b -> if b then "true" else "false"
        | OffsetDateTime dt -> dt.ToString("o", CultureInfo.InvariantCulture)
        | LocalDateTime dt -> dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
        | LocalDate d -> d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        | LocalTime t -> sprintf "%02d:%02d:%02d" t.Hours t.Minutes t.Seconds
        | Array arr -> sprintf "[%d items]" arr.Length
        | Table props -> sprintf "{%d properties}" props.Length

    /// Convert this TOML value to a JSON value for type inference and runtime use.
    /// TOML date/time values are serialized to ISO 8601 strings so that the
    /// existing JSON inference can detect them as date types.
    member x.ToJsonValue() : JsonValue =
        match x with
        | String s -> JsonValue.String s
        | Integer i -> JsonValue.Number(decimal i)
        | Float f -> JsonValue.Float f
        | Boolean b -> JsonValue.Boolean b
        | OffsetDateTime dt -> JsonValue.String(dt.ToString("o", CultureInfo.InvariantCulture))
        | LocalDateTime dt -> JsonValue.String(dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))
        | LocalDate d -> JsonValue.String(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        | LocalTime t -> JsonValue.String(sprintf "%02d:%02d:%02d" t.Hours t.Minutes t.Seconds)
        | Array arr -> JsonValue.Array(arr |> Array.map (fun v -> v.ToJsonValue()))
        | Table props -> JsonValue.Record(props |> Array.map (fun (k, v) -> k, v.ToJsonValue()))

// --------------------------------------------------------------------------------------
// TOML parser (internal mutable representation during parsing)
// --------------------------------------------------------------------------------------

/// Mutable node used during parsing; converted to TomlValue at end
[<NoComparison; NoEquality>]
type private MutableNode =
    | Prim of TomlValue
    | Tbl of OrderedTable * bool // bool = explicitly defined with [header]
    | TblArray of ResizeArray<OrderedTable>

and private OrderedTable() =
    let keys = ResizeArray<string>()
    let dict = Dictionary<string, MutableNode>()

    member _.TryGet(key: string) =
        match dict.TryGetValue(key) with
        | true, v -> Some v
        | _ -> None

    member _.Set(key: string, value: MutableNode) =
        if not (dict.ContainsKey(key)) then
            keys.Add(key)

        dict.[key] <- value

    member _.ContainsKey(key: string) = dict.ContainsKey(key)

    member _.Keys = keys :> seq<string>

    member x.ToTomlValue() : TomlValue =
        let rec nodeToTomlValue (node: MutableNode) : TomlValue =
            match node with
            | Prim v -> v
            | Tbl(t, _) -> t.ToTomlValue()
            | TblArray arr -> TomlValue.Array [| for t in arr -> t.ToTomlValue() |]

        let props = [| for k in keys -> k, nodeToTomlValue dict.[k] |]
        TomlValue.Table props

// --------------------------------------------------------------------------------------
// TOML Parser
// --------------------------------------------------------------------------------------

type private TomlParser(text: string) =

    let mutable pos = 0
    let len = text.Length

    let isAtEnd () = pos >= len

    let current () =
        if pos < len then text.[pos] else '\000'

    let peek n =
        if pos + n < len then text.[pos + n] else '\000'

    let advance () = pos <- pos + 1

    let error msg =
        let snippet = text.[(max 0 (pos - 15)) .. (min (len - 1) (pos + 15))]

        failwithf "TOML parse error at position %d: %s\n  near: ...%s..." pos msg snippet

    // Skip horizontal whitespace (space, tab)
    let skipHws () =
        while pos < len && (text.[pos] = ' ' || text.[pos] = '\t') do
            advance ()

    // Skip a comment (# to end of line)
    let skipComment () =
        if pos < len && text.[pos] = '#' then
            while pos < len && text.[pos] <> '\n' do
                advance ()

    // Skip a single newline sequence (\r\n or \n)
    let skipNewline () =
        if pos < len && text.[pos] = '\r' then
            advance ()

        if pos < len && text.[pos] = '\n' then
            advance ()

    // Skip whitespace + comments + empty lines
    let skipWsAndNewlines () =
        let mutable cont = true

        while cont && not (isAtEnd ()) do
            skipHws ()

            if pos < len && text.[pos] = '#' then
                skipComment ()
                skipNewline ()
            elif pos < len && (text.[pos] = '\n' || text.[pos] = '\r') then
                skipNewline ()
            else
                cont <- false

    // Parse a basic string (between double quotes), assumes pos is on opening "
    let parseBasicString () =
        advance () // skip opening "
        let buf = StringBuilder()

        while pos < len && text.[pos] <> '"' do
            if text.[pos] = '\\' then
                advance ()

                if pos < len then
                    match text.[pos] with
                    | 'b' ->
                        buf.Append('\b') |> ignore
                        advance ()
                    | 't' ->
                        buf.Append('\t') |> ignore
                        advance ()
                    | 'n' ->
                        buf.Append('\n') |> ignore
                        advance ()
                    | 'f' ->
                        buf.Append('\f') |> ignore
                        advance ()
                    | 'r' ->
                        buf.Append('\r') |> ignore
                        advance ()
                    | '"' ->
                        buf.Append('"') |> ignore
                        advance ()
                    | '\\' ->
                        buf.Append('\\') |> ignore
                        advance ()
                    | 'u' ->
                        if pos + 4 < len then
                            let hex = text.[pos + 1 .. pos + 4]

                            try
                                buf.Append(char (Convert.ToInt32(hex, 16))) |> ignore
                            with _ ->
                                error (sprintf "Invalid unicode escape \\u%s" hex)

                            pos <- pos + 5
                        else
                            error "Incomplete \\u escape"
                    | 'U' ->
                        if pos + 8 < len then
                            let hex = text.[pos + 1 .. pos + 8]

                            try
                                let cp = Convert.ToInt32(hex, 16)
                                buf.Append(Char.ConvertFromUtf32(cp)) |> ignore
                            with _ ->
                                error (sprintf "Invalid unicode escape \\U%s" hex)

                            pos <- pos + 9
                        else
                            error "Incomplete \\U escape"
                    | c -> error (sprintf "Invalid escape \\%c" c)
            elif text.[pos] = '\n' then
                error "Newline in basic string"
            else
                buf.Append(text.[pos]) |> ignore
                advance ()

        if pos >= len then
            error "Unterminated basic string"

        advance () // skip closing "
        buf.ToString()

    // Parse a multiline basic string ("""), assumes pos is on first "
    let parseMultilineBasicString () =
        pos <- pos + 3 // skip """
        // Skip optional immediate newline
        if pos < len && text.[pos] = '\n' then
            advance ()
        elif pos < len && text.[pos] = '\r' && pos + 1 < len && text.[pos + 1] = '\n' then
            pos <- pos + 2

        let buf = StringBuilder()
        let mutable found = false

        while not found do
            if
                pos + 2 < len
                && text.[pos] = '"'
                && text.[pos + 1] = '"'
                && text.[pos + 2] = '"'
            then
                // May be followed by 1 or 2 extra quotes that are part of the string
                let extra1 =
                    pos + 3 < len
                    && text.[pos + 3] = '"'
                    && not (pos + 4 < len && text.[pos + 4] = '"')

                let extra2 = pos + 4 < len && text.[pos + 3] = '"' && text.[pos + 4] = '"'

                if extra2 then
                    buf.Append("\"\"") |> ignore
                    pos <- pos + 5
                elif extra1 then
                    buf.Append('"') |> ignore
                    pos <- pos + 4
                else
                    pos <- pos + 3

                found <- true
            elif pos >= len then
                error "Unterminated multiline basic string"
            elif text.[pos] = '\\' then
                advance ()

                if pos < len then
                    match text.[pos] with
                    | '\n' ->
                        advance ()

                        while pos < len
                              && (text.[pos] = ' ' || text.[pos] = '\t' || text.[pos] = '\n' || text.[pos] = '\r') do
                            advance ()
                    | '\r' when pos + 1 < len && text.[pos + 1] = '\n' ->
                        pos <- pos + 2

                        while pos < len
                              && (text.[pos] = ' ' || text.[pos] = '\t' || text.[pos] = '\n' || text.[pos] = '\r') do
                            advance ()
                    | 'b' ->
                        buf.Append('\b') |> ignore
                        advance ()
                    | 't' ->
                        buf.Append('\t') |> ignore
                        advance ()
                    | 'n' ->
                        buf.Append('\n') |> ignore
                        advance ()
                    | 'f' ->
                        buf.Append('\f') |> ignore
                        advance ()
                    | 'r' ->
                        buf.Append('\r') |> ignore
                        advance ()
                    | '"' ->
                        buf.Append('"') |> ignore
                        advance ()
                    | '\\' ->
                        buf.Append('\\') |> ignore
                        advance ()
                    | c -> error (sprintf "Invalid escape \\%c in multiline string" c)
            else
                buf.Append(text.[pos]) |> ignore
                advance ()

        buf.ToString()

    // Parse a literal string (single quotes), assumes pos is on opening '
    let parseLiteralString () =
        advance () // skip opening '
        let buf = StringBuilder()

        while pos < len && text.[pos] <> '\'' do
            if text.[pos] = '\n' then
                error "Newline in literal string"

            buf.Append(text.[pos]) |> ignore
            advance ()

        if pos >= len then
            error "Unterminated literal string"

        advance () // skip closing '
        buf.ToString()

    // Parse a multiline literal string ('''), assumes pos is on first '
    let parseMultilineLiteralString () =
        pos <- pos + 3 // skip '''
        // Skip optional immediate newline
        if pos < len && text.[pos] = '\n' then
            advance ()
        elif pos < len && text.[pos] = '\r' && pos + 1 < len && text.[pos + 1] = '\n' then
            pos <- pos + 2

        let buf = StringBuilder()
        let mutable found = false

        while not found do
            if
                pos + 2 < len
                && text.[pos] = '\''
                && text.[pos + 1] = '\''
                && text.[pos + 2] = '\''
            then
                let extra1 =
                    pos + 3 < len
                    && text.[pos + 3] = '\''
                    && not (pos + 4 < len && text.[pos + 4] = '\'')

                let extra2 = pos + 4 < len && text.[pos + 3] = '\'' && text.[pos + 4] = '\''

                if extra2 then
                    buf.Append("''") |> ignore
                    pos <- pos + 5
                elif extra1 then
                    buf.Append('\'') |> ignore
                    pos <- pos + 4
                else
                    pos <- pos + 3

                found <- true
            elif pos >= len then
                error "Unterminated multiline literal string"
            else
                buf.Append(text.[pos]) |> ignore
                advance ()

        buf.ToString()

    // Parse a string value (basic or literal)
    let parseStringValue () =
        if
            pos + 2 < len
            && text.[pos] = '"'
            && text.[pos + 1] = '"'
            && text.[pos + 2] = '"'
        then
            parseMultilineBasicString ()
        elif
            pos + 2 < len
            && text.[pos] = '\''
            && text.[pos + 1] = '\''
            && text.[pos + 2] = '\''
        then
            parseMultilineLiteralString ()
        elif pos < len && text.[pos] = '"' then
            parseBasicString ()
        elif pos < len && text.[pos] = '\'' then
            parseLiteralString ()
        else
            error "Expected string"

    // Parse a bare key (letters, digits, -, _)
    let parseBareKey () =
        let start = pos

        while pos < len
              && (Char.IsLetterOrDigit(text.[pos]) || text.[pos] = '_' || text.[pos] = '-') do
            advance ()

        if pos = start then
            error "Expected key"

        text.[start .. pos - 1]

    // Parse a key component (bare, basic-string, or literal-string)
    let parseKeyComponent () =
        if pos < len && text.[pos] = '"' then
            parseBasicString ()
        elif pos < len && text.[pos] = '\'' then
            parseLiteralString ()
        else
            parseBareKey ()

    // Parse a potentially dotted key, returning a list of key parts
    let parseKey () =
        let first = parseKeyComponent ()
        let keys = ResizeArray<string>([| first |])
        skipHws ()

        while pos < len && text.[pos] = '.' do
            advance () // skip '.'
            skipHws ()
            keys.Add(parseKeyComponent ())
            skipHws ()

        List.ofSeq keys

    // Try to parse a TOML date/time from string
    let tryParseDateTime (s: string) =
        let odtFormats =
            [| "yyyy-MM-dd'T'HH:mm:sszzz"
               "yyyy-MM-dd'T'HH:mm:ss.fffffffzzz"
               "yyyy-MM-dd'T'HH:mm:ss.ffffffzzz"
               "yyyy-MM-dd'T'HH:mm:ss.fffffzzz"
               "yyyy-MM-dd'T'HH:mm:ss.ffffzzz"
               "yyyy-MM-dd'T'HH:mm:ss.fffzzz"
               "yyyy-MM-dd'T'HH:mm:ss.ffzzz"
               "yyyy-MM-dd'T'HH:mm:ss.fzzz"
               "yyyy-MM-dd HH:mm:sszzz"
               "yyyy-MM-dd HH:mm:ss.fffffffzzz" |]

        let ldtFormats =
            [| "yyyy-MM-dd'T'HH:mm:ss"
               "yyyy-MM-dd'T'HH:mm:ss.fffffff"
               "yyyy-MM-dd'T'HH:mm:ss.ffffff"
               "yyyy-MM-dd'T'HH:mm:ss.fffff"
               "yyyy-MM-dd'T'HH:mm:ss.ffff"
               "yyyy-MM-dd'T'HH:mm:ss.fff"
               "yyyy-MM-dd'T'HH:mm:ss.ff"
               "yyyy-MM-dd'T'HH:mm:ss.f"
               "yyyy-MM-dd HH:mm:ss"
               "yyyy-MM-dd HH:mm:ss.fffffff" |]

        let dateFormats = [| "yyyy-MM-dd" |]

        let timeFormats =
            [| @"hh\:mm\:ss"
               @"hh\:mm\:ss\.fffffff"
               @"hh\:mm\:ss\.ffffff"
               @"hh\:mm\:ss\.fffff"
               @"hh\:mm\:ss\.ffff"
               @"hh\:mm\:ss\.fff"
               @"hh\:mm\:ss\.ff"
               @"hh\:mm\:ss\.f" |]

        // Normalize Z to +00:00 for DateTimeOffset parsing
        let sNorm =
            if s.EndsWith("Z", StringComparison.OrdinalIgnoreCase) then
                s.[0 .. s.Length - 2] + "+00:00"
            else
                s

        match DateTimeOffset.TryParseExact(sNorm, odtFormats, CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | true, dt -> Some(TomlValue.OffsetDateTime dt)
        | _ ->
            match DateTime.TryParseExact(s, ldtFormats, CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | true, dt -> Some(TomlValue.LocalDateTime dt)
            | _ ->
                match DateTime.TryParseExact(s, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None) with
                | true, d -> Some(TomlValue.LocalDate d)
                | _ ->
                    match TimeSpan.TryParseExact(s, timeFormats, CultureInfo.InvariantCulture) with
                    | true, ts -> Some(TomlValue.LocalTime ts)
                    | _ -> None

    // Parse a TOML value
    let rec parseValue () : TomlValue =
        skipHws ()

        match current () with
        | '"' -> TomlValue.String(parseStringValue ())
        | '\'' -> TomlValue.String(parseStringValue ())
        | '[' -> parseArray ()
        | '{' -> parseInlineTable ()
        | 't' when pos + 3 < len && text.[pos .. pos + 3] = "true" ->
            pos <- pos + 4
            TomlValue.Boolean true
        | 'f' when pos + 4 < len && text.[pos .. pos + 4] = "false" ->
            pos <- pos + 5
            TomlValue.Boolean false
        | 'i'
        | '+'
        | 'n' when
            (pos + 2 < len
             && (text.[pos .. pos + 2] = "inf"
                 || text.[pos .. pos + 2] = "nan"
                 || (pos + 3 < len
                     && (text.[pos .. pos + 3] = "+inf" || text.[pos .. pos + 3] = "+nan"))))
            ->
            parseNumOrDate ()
        | '-' when
            pos + 3 < len
            && (text.[pos .. pos + 3] = "-inf" || text.[pos .. pos + 3] = "-nan")
            ->
            parseNumOrDate ()
        | c when c = '-' || c = '+' || Char.IsDigit(c) -> parseNumOrDate ()
        | c -> error (sprintf "Unexpected character '%c' in value" c)

    and parseNumOrDate () =
        let start = pos

        // Check for special float values
        let special3 = if pos + 2 < len then text.[pos .. pos + 2] else ""

        let special4 = if pos + 3 < len then text.[pos .. pos + 3] else ""

        match special3, special4 with
        | "nan", _ ->
            pos <- pos + 3
            TomlValue.Float Double.NaN
        | "inf", _ ->
            pos <- pos + 3
            TomlValue.Float Double.PositiveInfinity
        | _ when special4 = "+inf" ->
            pos <- pos + 4
            TomlValue.Float Double.PositiveInfinity
        | _ when special4 = "-inf" ->
            pos <- pos + 4
            TomlValue.Float Double.NegativeInfinity
        | _ when special4 = "+nan" ->
            pos <- pos + 4
            TomlValue.Float Double.NaN
        | _ when special4 = "-nan" ->
            pos <- pos + 4
            TomlValue.Float Double.NaN
        | _ ->

            let mutable hasDecimalOrExp = false
            let mutable hasDash = false
            let mutable hasColon = false
            let mutable hasTorSpace = false

            // Collect the full token
            while pos < len
                  && (let c = text.[pos]

                      Char.IsDigit(c)
                      || c = '.'
                      || c = 'e'
                      || c = 'E'
                      || c = '+'
                      || c = '-'
                      || c = '_'
                      || c = ':'
                      || c = 'T'
                      || c = 't'
                      || c = 'Z'
                      || c = 'z'
                      || c = 'x'
                      || c = 'o'
                      || c = 'b'
                      || c = 'a'
                      || c = 'A'
                      || c = 'B'
                      || c = 'c'
                      || c = 'C'
                      || c = 'd'
                      || c = 'D'
                      || c = 'f'
                      || c = 'F'
                      || (c = ' ' && hasDash && pos + 1 < len && Char.IsDigit(text.[pos + 1]))) do
                let c = text.[pos]

                match c with
                | '.'
                | 'e'
                | 'E' -> hasDecimalOrExp <- true
                | '-' when pos > start -> hasDash <- true
                | ':' -> hasColon <- true
                | 'T'
                | 't' -> hasTorSpace <- true
                | ' ' -> hasTorSpace <- true
                | _ -> ()

                advance ()

            let token = text.[start .. pos - 1].Replace("_", "")

            // Date-only: has dashes but no colons, T, or decimal (distinguishes from negative-exponent floats)
            let isDateOnly = hasDash && not hasColon && not hasTorSpace && not hasDecimalOrExp
            // DateTime: has dashes with colons or T separator
            let isDateTime = hasDash && (hasColon || hasTorSpace)
            // Time-only: has colons but no dashes
            let isTime = not hasDash && hasColon
            let isDateLike = isDateOnly || isDateTime || isTime

            if isDateLike then
                match tryParseDateTime token with
                | Some v -> v
                | None -> error (sprintf "Invalid date/time value: '%s'" token)
            elif hasDecimalOrExp then
                match
                    Double.TryParse(
                        token,
                        NumberStyles.Float ||| NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture
                    )
                with
                | true, v -> TomlValue.Float v
                | _ -> error (sprintf "Invalid float: '%s'" token)
            else
                // Integer (decimal, hex 0x, octal 0o, binary 0b)
                let tokenTrimmed = token.TrimStart('+')

                if tokenTrimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) then
                    try
                        TomlValue.Integer(Convert.ToInt64(tokenTrimmed.[2..], 16))
                    with _ ->
                        error (sprintf "Invalid hex integer: '%s'" token)
                elif tokenTrimmed.StartsWith("0o", StringComparison.OrdinalIgnoreCase) then
                    try
                        TomlValue.Integer(Convert.ToInt64(tokenTrimmed.[2..], 8))
                    with _ ->
                        error (sprintf "Invalid octal integer: '%s'" token)
                elif tokenTrimmed.StartsWith("0b", StringComparison.OrdinalIgnoreCase) then
                    try
                        TomlValue.Integer(Convert.ToInt64(tokenTrimmed.[2..], 2))
                    with _ ->
                        error (sprintf "Invalid binary integer: '%s'" token)
                else
                    match
                        Int64.TryParse(
                            token,
                            NumberStyles.Integer ||| NumberStyles.AllowLeadingSign,
                            CultureInfo.InvariantCulture
                        )
                    with
                    | true, v -> TomlValue.Integer v
                    | _ -> error (sprintf "Invalid integer: '%s'" token)

    and parseArray () =
        advance () // skip '['
        let items = ResizeArray<TomlValue>()
        skipWsAndNewlines ()

        while pos < len && text.[pos] <> ']' do
            items.Add(parseValue ())
            skipHws ()
            skipComment ()
            skipWsAndNewlines ()

            if pos < len && text.[pos] = ',' then
                advance ()
                skipWsAndNewlines ()

        if pos >= len then
            error "Unterminated array"

        advance () // skip ']'
        TomlValue.Array(items.ToArray())

    and parseInlineTable () =
        advance () // skip '{'
        skipHws ()
        let tbl = OrderedTable()

        if pos < len && text.[pos] <> '}' then
            parseInlineKeyValue tbl
            skipHws ()

            while pos < len && text.[pos] = ',' do
                advance ()
                skipHws ()
                parseInlineKeyValue tbl
                skipHws ()

        if pos >= len || text.[pos] <> '}' then
            error "Unterminated inline table"

        advance () // skip '}'
        tbl.ToTomlValue()

    and parseInlineKeyValue (tbl: OrderedTable) =
        let keys = parseKey ()
        skipHws ()

        if pos >= len || text.[pos] <> '=' then
            error "Expected '=' in inline table"

        advance ()
        skipHws ()
        let value = parseValue ()
        setInTable tbl keys (Prim value)

    and setInTable (tbl: OrderedTable) (keys: string list) (value: MutableNode) =
        match keys with
        | [] -> ()
        | [ k ] ->
            if tbl.ContainsKey(k) then
                error (sprintf "Duplicate key '%s'" k)

            tbl.Set(k, value)
        | k :: rest ->
            let sub =
                match tbl.TryGet(k) with
                | Some(Tbl(t, _)) -> t
                | None ->
                    let t = OrderedTable()
                    tbl.Set(k, Tbl(t, false))
                    t
                | _ -> error (sprintf "Key '%s' already has a non-table value" k)

            setInTable sub rest value

    // Navigate to the table identified by the given path, creating intermediate tables as needed
    and navigateToTable (root: OrderedTable) (path: string list) (isArrayTable: bool) : OrderedTable =
        match path with
        | [] -> root
        | [ k ] ->
            if isArrayTable then
                match root.TryGet(k) with
                | Some(TblArray arr) ->
                    let newTbl = OrderedTable()
                    arr.Add(newTbl)
                    newTbl
                | None ->
                    let arr = ResizeArray<OrderedTable>()
                    let newTbl = OrderedTable()
                    arr.Add(newTbl)
                    root.Set(k, TblArray arr)
                    newTbl
                | _ -> error (sprintf "Key '%s' conflicts with an existing non-array-of-tables value" k)
            else
                match root.TryGet(k) with
                | Some(Tbl(t, false)) ->
                    // Intermediate table created implicitly; now we're defining it explicitly
                    root.Set(k, Tbl(t, true))
                    t
                | Some(Tbl(_, true)) -> error (sprintf "Table '[%s]' defined more than once" k)
                | Some(TblArray arr) ->
                    // Navigate into last element of array-of-tables
                    arr.[arr.Count - 1]
                | None ->
                    let t = OrderedTable()
                    root.Set(k, Tbl(t, true))
                    t
                | _ -> error (sprintf "Key '%s' conflicts with an existing value" k)
        | k :: rest ->
            let sub =
                match root.TryGet(k) with
                | Some(Tbl(t, _)) -> t
                | Some(TblArray arr) ->
                    // Navigate into last element of array-of-tables
                    arr.[arr.Count - 1]
                | None ->
                    let t = OrderedTable()
                    root.Set(k, Tbl(t, false))
                    t
                | _ -> error (sprintf "Key '%s' conflicts with an existing non-table value" k)

            navigateToTable sub rest isArrayTable

    member _.Parse() : TomlValue =
        let root = OrderedTable()
        let mutable currentTable = root

        skipWsAndNewlines ()

        while not (isAtEnd ()) do
            skipHws ()

            if isAtEnd () then
                ()
            elif current () = '#' then
                skipComment ()
                skipNewline ()
                skipWsAndNewlines ()
            elif current () = '\n' || current () = '\r' then
                skipNewline ()
                skipWsAndNewlines ()
            elif current () = '[' && peek 1 = '[' then
                // Array-of-tables header [[key]]
                pos <- pos + 2
                skipHws ()
                let keys = parseKey ()
                skipHws ()

                if not (pos + 1 < len && text.[pos] = ']' && text.[pos + 1] = ']') then
                    error "Expected ']]' to close array-of-tables header"

                pos <- pos + 2
                skipHws ()
                skipComment ()
                skipNewline ()
                skipWsAndNewlines ()
                currentTable <- navigateToTable root keys true
            elif current () = '[' then
                // Table header [key]
                advance ()
                skipHws ()
                let keys = parseKey ()
                skipHws ()

                if pos >= len || text.[pos] <> ']' then
                    error "Expected ']' to close table header"

                advance ()
                skipHws ()
                skipComment ()
                skipNewline ()
                skipWsAndNewlines ()
                currentTable <- navigateToTable root keys false
            else
                // Key-value pair
                let keys = parseKey ()
                skipHws ()

                if pos >= len || text.[pos] <> '=' then
                    error (sprintf "Expected '=' after key, got '%c'" (current ()))

                advance ()
                skipHws ()
                let value = parseValue ()
                skipHws ()
                skipComment ()
                skipNewline ()
                skipWsAndNewlines ()
                setInTable currentTable keys (Prim value)

        root.ToTomlValue()

type TomlValue with

    /// Parse a TOML document from a string
    static member Parse(text: string) : TomlValue = TomlParser(text).Parse()

    /// Attempt to parse a TOML document; returns None on failure
    static member TryParse(text: string) : TomlValue option =
        try
            Some(TomlParser(text).Parse())
        with _ ->
            None

    /// Load a TOML document from a stream
    static member Load(stream: Stream) : TomlValue =
        use reader = new StreamReader(stream)
        let text = reader.ReadToEnd()
        TomlParser(text).Parse()

    /// Load a TOML document from a text reader
    static member Load(reader: TextReader) : TomlValue =
        let text = reader.ReadToEnd()
        TomlParser(text).Parse()
