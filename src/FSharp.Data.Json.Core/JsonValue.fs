// --------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation 2005-2012.
// This sample code is provided "as is" without warranty of any kind.
// We disclaim all warranties, either express or implied, including the
// warranties of merchantability and fitness for a particular purpose.
//
// A simple F# portable parser for JSON data
// --------------------------------------------------------------------------------------

namespace FSharp.Data

open System
open System.IO
open System.ComponentModel
open System.Globalization
open System.Runtime.InteropServices
open System.Text
open FSharp.Data.Runtime

/// Specifies the formatting behaviour of JSON values
[<RequireQualifiedAccess>]
type JsonSaveOptions =
    /// Format (indent) the JsonValue
    | None = 0

    /// Print the JsonValue in one line in a compact way
    | DisableFormatting = 1

    /// Print the JsonValue in one line in a compact way,
    /// but place a single space after every comma
    /// https://github.com/fsprojects/FSharp.Data/issues/1482
    | CompactSpaceAfterComma = 2

/// Represents a JSON value. Large numbers that do not fit in the
/// Decimal type are represented using the Float case, while
/// smaller numbers are represented as decimals to avoid precision loss.
[<RequireQualifiedAccess>]
[<StructuredFormatDisplay("{_Print}")>]
type JsonValue =
    | String of string
    | Number of decimal
    | Float of float
    | Record of properties: (string * JsonValue)[]
    | Array of elements: JsonValue[]
    | Boolean of bool
    | Null

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    member x._Print =
        let str = x.ToString()

        if str.Length > 512 then
            str.Substring(0, 509) + "..."
        else
            str

    /// Serializes the JsonValue to the specified System.IO.TextWriter.
    member x.WriteTo(w: TextWriter, saveOptions) =

        let newLine =
            if saveOptions = JsonSaveOptions.None then
                fun indentation plus ->
                    w.WriteLine()
                    System.String(' ', indentation + plus) |> w.Write
            else
                fun _ _ -> ()

        let propSep = if saveOptions = JsonSaveOptions.None then "\": " else "\":"

        let comma () =
            match saveOptions with
            | JsonSaveOptions.None -> w.Write ","
            | JsonSaveOptions.DisableFormatting -> w.Write ","
            | JsonSaveOptions.CompactSpaceAfterComma -> w.Write ", "
            | _ -> failwith "Invalid JsonSaveOptions"

        let rec serialize indentation =
            function
            | Null -> w.Write "null"
            | Boolean b -> w.Write(if b then "true" else "false")
            | Number number -> w.Write number
            | Float v when Double.IsInfinity v || Double.IsNaN v -> w.Write "null"
            | Float number -> w.Write number
            | String s ->
                w.Write "\""
                JsonValue.JsonStringEncodeTo w s
                w.Write "\""
            | Record properties ->
                w.Write "{"

                for i = 0 to properties.Length - 1 do
                    let k, v = properties.[i]
                    if i > 0 then comma ()
                    newLine indentation 2
                    w.Write "\""
                    JsonValue.JsonStringEncodeTo w k
                    w.Write propSep
                    serialize (indentation + 2) v

                newLine indentation 0
                w.Write "}"
            | Array elements ->
                w.Write "["

                for i = 0 to elements.Length - 1 do
                    if i > 0 then comma ()
                    newLine indentation 2
                    serialize (indentation + 2) elements.[i]

                if elements.Length > 0 then newLine indentation 0
                w.Write "]"

        serialize 0 x

    // Encode characters that are not valid in JS string. The implementation is based
    // on https://github.com/mono/mono/blob/master/mcs/class/System.Web/System.Web/HttpUtility.cs
    static member internal JsonStringEncodeTo (w: TextWriter) (value: string) =
        if not (String.IsNullOrEmpty value) then
            for i = 0 to value.Length - 1 do
                let c = value.[i]
                let ci = int c

                if ci >= 0 && ci <= 7
                   || ci = 11
                   || ci >= 14 && ci <= 31 then
                    w.Write("\\u{0:x4}", ci) |> ignore
                else
                    match c with
                    | '\b' -> w.Write "\\b"
                    | '\t' -> w.Write "\\t"
                    | '\n' -> w.Write "\\n"
                    | '\f' -> w.Write "\\f"
                    | '\r' -> w.Write "\\r"
                    | '"' -> w.Write "\\\""
                    | '\\' -> w.Write "\\\\"
                    | _ -> w.Write c

    member x.ToString saveOptions =
        let w = new StringWriter(CultureInfo.InvariantCulture)
        x.WriteTo(w, saveOptions)
        w.GetStringBuilder().ToString()

    override x.ToString() = x.ToString(JsonSaveOptions.None)

/// <exclude />
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module JsonValue =

    /// Active Pattern to view a `JsonValue.Record of (string * JsonValue)[]` as a `JsonValue.Object of Map<string, JsonValue>` for
    /// backwards compatibility reaons
    [<Obsolete("Please use JsonValue.Record instead")>]
    let (|Object|_|) x =
        match x with
        | JsonValue.Record properties -> Map.ofArray properties |> Some
        | _ -> None

    /// Constructor to create a `JsonValue.Record of (string * JsonValue)[]` as a `JsonValue.Object of Map<string, JsonValue>` for
    /// backwards compatibility reaons
    [<Obsolete("Please use JsonValue.Record instead")>]
    let Object = Map.toArray >> JsonValue.Record

// --------------------------------------------------------------------------------------
// JSON parser
// --------------------------------------------------------------------------------------

type private JsonParser(jsonText: string) =

    let mutable i = 0
    let s = jsonText

    let buf = StringBuilder() // pre-allocate buffers for strings

    // Helper functions

    let isNumChar c =
        Char.IsDigit c
        || c = '.'
        || c = 'e'
        || c = 'E'
        || c = '+'
        || c = '-'

    let throw () =
        let msg =
            sprintf
                "Invalid JSON starting at character %d, snippet = \n----\n%s\n-----\njson = \n------\n%s\n-------"
                i
                (jsonText.[(max 0 (i - 10)) .. (min (jsonText.Length - 1) (i + 10))])
                (if jsonText.Length > 1000 then
                     jsonText.Substring(0, 1000)
                 else
                     jsonText)

        failwith msg

    let ensure cond = if not cond then throw ()


    let rec skipCommentsAndWhitespace () =
        let skipComment () =
            // Supported comment syntax:
            // - // ...{newLine}
            // - /* ... */
            if i < s.Length && s.[i] = '/' then
                i <- i + 1

                if i < s.Length && s.[i] = '/' then
                    i <- i + 1

                    while i < s.Length && (s.[i] <> '\r' && s.[i] <> '\n') do
                        i <- i + 1
                else if i < s.Length && s.[i] = '*' then
                    i <- i + 1

                    while i + 1 < s.Length
                          && s.[i] <> '*'
                          && s.[i + 1] <> '/' do
                        i <- i + 1

                    ensure (i + 1 < s.Length && s.[i] = '*' && s.[i + 1] = '/')
                    i <- i + 2

                true

            else
                false

        let skipWhitespace () =
            let initialI = i

            while i < s.Length && Char.IsWhiteSpace s.[i] do
                i <- i + 1

            initialI <> i // return true if some whitespace was skipped

        if skipWhitespace () || skipComment () then
            skipCommentsAndWhitespace ()

    // Recursive descent parser for JSON that uses global mutable index
    let rec parseValue cont =
        skipCommentsAndWhitespace ()
        ensure (i < s.Length)

        match s.[i] with
        | '"' -> cont (JsonValue.String(parseString ()))
        | '-' -> cont (parseNum ())
        | c when Char.IsDigit(c) -> cont (parseNum ())
        | '{' -> parseObject cont
        | '[' -> parseArray cont
        | 't' -> cont (parseLiteral ("true", JsonValue.Boolean true))
        | 'f' -> cont (parseLiteral ("false", JsonValue.Boolean false))
        | 'n' -> cont (parseLiteral ("null", JsonValue.Null))
        | _ -> throw ()

    and parseString () =
        ensure (i < s.Length && s.[i] = '"')
        i <- i + 1

        while i < s.Length && s.[i] <> '"' do
            if s.[i] = '\\' then
                ensure (i + 1 < s.Length)

                match s.[i + 1] with
                | 'b' -> buf.Append('\b') |> ignore
                | 'f' -> buf.Append('\f') |> ignore
                | 'n' -> buf.Append('\n') |> ignore
                | 't' -> buf.Append('\t') |> ignore
                | 'r' -> buf.Append('\r') |> ignore
                | '\\' -> buf.Append('\\') |> ignore
                | '/' -> buf.Append('/') |> ignore
                | '"' -> buf.Append('"') |> ignore
                | 'u' ->
                    ensure (i + 5 < s.Length)

                    let hexdigit d =
                        if d >= '0' && d <= '9' then int32 d - int32 '0'
                        elif d >= 'a' && d <= 'f' then int32 d - int32 'a' + 10
                        elif d >= 'A' && d <= 'F' then int32 d - int32 'A' + 10
                        else failwith "hexdigit"

                    let unicodeChar (s: string) =
                        if s.Length <> 4 then failwith "unicodeChar"

                        char (
                            hexdigit s.[0] * 4096
                            + hexdigit s.[1] * 256
                            + hexdigit s.[2] * 16
                            + hexdigit s.[3]
                        )

                    let ch = unicodeChar (s.Substring(i + 2, 4))
                    buf.Append(ch) |> ignore
                    i <- i + 4 // the \ and u will also be skipped past further below
                | 'U' ->
                    ensure (i + 9 < s.Length)

                    let unicodeChar (s: string) =
                        if s.Length <> 8 then failwithf "unicodeChar (%O)" s
                        if s.[0..1] <> "00" then failwithf "unicodeChar (%O)" s

                        UnicodeHelper.getUnicodeSurrogatePair
                        <| System.UInt32.Parse(s, NumberStyles.HexNumber)

                    let lead, trail = unicodeChar (s.Substring(i + 2, 8))
                    buf.Append(lead) |> ignore
                    buf.Append(trail) |> ignore
                    i <- i + 8 // the \ and u will also be skipped past further below
                | _ -> throw ()

                i <- i + 2 // skip past \ and next char
            else
                buf.Append(s.[i]) |> ignore
                i <- i + 1

        ensure (i < s.Length && s.[i] = '"')
        i <- i + 1
        let str = buf.ToString()
        buf.Clear() |> ignore
        str

    and parseNum () =
        let start = i

        while i < s.Length && (isNumChar s.[i]) do
            i <- i + 1

        let len = i - start
        let sub = s.Substring(start, len)

        match TextConversions.AsDecimal CultureInfo.InvariantCulture sub with
        | Some x -> JsonValue.Number x
        | _ ->
            match TextConversions.AsFloat [||] false CultureInfo.InvariantCulture sub with
            | Some x -> JsonValue.Float x
            | _ -> throw ()

    and parsePair cont =
        let key = parseString ()
        skipCommentsAndWhitespace ()
        ensure (i < s.Length && s.[i] = ':')
        i <- i + 1
        skipCommentsAndWhitespace ()
        parseValue (fun v -> cont (key, v))

    and parseObject cont =
        ensure (i < s.Length && s.[i] = '{')
        i <- i + 1
        skipCommentsAndWhitespace ()
        let pairs = ResizeArray<_>()

        let parseObjectEnd () =
            ensure (i < s.Length && s.[i] = '}')
            i <- i + 1
            let res = pairs.ToArray() |> JsonValue.Record
            cont res

        if i < s.Length && s.[i] = '"' then
            parsePair (fun p ->
                pairs.Add(p)
                skipCommentsAndWhitespace ()

                let rec parsePairItem () =
                    if i < s.Length && s.[i] = ',' then
                        i <- i + 1
                        skipCommentsAndWhitespace ()

                        parsePair (fun p ->
                            pairs.Add(p)
                            skipCommentsAndWhitespace ()
                            parsePairItem ())
                    else
                        parseObjectEnd ()

                parsePairItem ())
        else
            parseObjectEnd ()

    and parseArray cont =
        ensure (i < s.Length && s.[i] = '[')
        i <- i + 1
        skipCommentsAndWhitespace ()
        let vals = ResizeArray<_>()

        let parseArrayEnd () =
            ensure (i < s.Length && s.[i] = ']')
            i <- i + 1
            let res = vals.ToArray() |> JsonValue.Array
            cont res

        if i < s.Length && s.[i] <> ']' then
            parseValue (fun v ->
                vals.Add(v)
                skipCommentsAndWhitespace ()

                let rec parseArrayItem () =
                    if i < s.Length && s.[i] = ',' then
                        i <- i + 1
                        skipCommentsAndWhitespace ()

                        parseValue (fun v ->
                            vals.Add(v)
                            skipCommentsAndWhitespace ()
                            parseArrayItem ())
                    else
                        parseArrayEnd ()

                parseArrayItem ())
        else
            parseArrayEnd ()

    and parseLiteral (expected, r) =
        ensure (i + expected.Length <= s.Length)

        for j in 0 .. expected.Length - 1 do
            ensure (s.[i + j] = expected.[j])

        i <- i + expected.Length
        r

    // Start by parsing the top-level value
    member x.Parse() =
        let value = parseValue id
        skipCommentsAndWhitespace ()
        if i <> s.Length then throw ()
        value

    member x.ParseMultiple() =
        seq {
            while i <> s.Length do
                yield parseValue id
                skipCommentsAndWhitespace ()
        }

type JsonValue with

    /// Parses the specified JSON string
    static member Parse(text) = JsonParser(text).Parse()

    /// Attempts to parse the specified JSON string
    static member TryParse(text) =
        try
            Some <| JsonParser(text).Parse()
        with _ ->
            None

    /// Loads JSON from the specified stream
    static member Load(stream: Stream) =
        use reader = new StreamReader(stream)
        let text = reader.ReadToEnd()
        JsonParser(text).Parse()

    /// Loads JSON from the specified reader
    static member Load(reader: TextReader) =
        let text = reader.ReadToEnd()
        JsonParser(text).Parse()

    /// Loads JSON from the specified uri asynchronously
    static member AsyncLoad(uri: string, [<Optional>] ?encoding) =
        async {
            let encoding = defaultArg encoding Encoding.UTF8
            let! reader = IO.asyncReadTextAtRuntime false "" "" "JSON" encoding.WebName uri
            let text = reader.ReadToEnd()
            return JsonParser(text).Parse()
        }

    /// Loads JSON from the specified uri
    static member Load(uri: string, [<Optional>] ?encoding) =
        JsonValue.AsyncLoad(uri, ?encoding = encoding)
        |> Async.RunSynchronously

    /// Parses the specified string into multiple JSON values
    static member ParseMultiple(text) = JsonParser(text).ParseMultiple()

    member private x.PrepareRequest(httpMethod, headers) =
        let httpMethod = defaultArg httpMethod HttpMethod.Post
        let headers = defaultArg (Option.map List.ofSeq headers) []

        let headers =
            if
                headers
                |> List.exists (fst >> (=) (fst (HttpRequestHeaders.UserAgent "")))
            then
                headers
            else
                HttpRequestHeaders.UserAgent "FSharp.Data JSON Type Provider"
                :: headers

        let headers =
            HttpRequestHeaders.ContentTypeWithEncoding(HttpContentTypes.Json, Encoding.UTF8)
            :: headers

        TextRequest(x.ToString(JsonSaveOptions.DisableFormatting)), headers, httpMethod

    /// Sends the JSON to the specified URL synchronously. Defaults to a POST request.
    member x.Request(url: string, [<Optional>] ?httpMethod, [<Optional>] ?headers: seq<_>) =
        let body, headers, httpMethod = x.PrepareRequest(httpMethod, headers)
        Http.Request(url, body = body, headers = headers, httpMethod = httpMethod)

    /// Sends the JSON to the specified URL asynchronously. Defaults to a POST request.
    member x.RequestAsync(url: string, [<Optional>] ?httpMethod, [<Optional>] ?headers: seq<_>) =
        let body, headers, httpMethod = x.PrepareRequest(httpMethod, headers)
        Http.AsyncRequest(url, body = body, headers = headers, httpMethod = httpMethod)

    [<Obsolete("Please use JsonValue.Request instead")>]
    member x.Post(uri: string, [<Optional>] ?headers) = x.Request(uri, ?headers = headers)
