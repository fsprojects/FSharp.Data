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
open System.Text
open System.Globalization
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.HttpUtils

/// Specifies the formatting behaviour of JSON values
[<RequireQualifiedAccess>]
type JsonSaveOptions = 
  /// Format (indent) the JsonValue
  | None = 0
  /// Print the JsonValue in one line in a compact way
  | DisableFormatting = 1

/// Represents a JSON value. Large numbers that do not fit in the 
/// Decimal type are represented using the Float case, while
/// smaller numbers are represented as decimals to avoid precision loss.
[<RequireQualifiedAccess>]
[<StructuredFormatDisplay("{_Print}")>]
type JsonValue =
  | String of string
  | Number of decimal
  | Float of float 
  | Record of properties:(string * JsonValue)[]
  | Array of elements:JsonValue[]
  | Boolean of bool
  | Null

  /// [omit]
  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
  member x._Print = x.ToString()

  override x.ToString() = x.ToString(JsonSaveOptions.None)

  member x.ToString saveOptions = 

    let rec serialize (sb:StringBuilder) indentation json =
      let append (str:string) = 
        sb.Append str 
        |> ignore
      let appendFormat (format:string) (str:string) = 
        sb.AppendFormat(format, str) 
        |> ignore
      let newLine plus =
        if saveOptions = JsonSaveOptions.None then
          sb.AppendLine() |> ignore
          System.String(' ', indentation + plus) |> append
      match json with
      | Null -> append "null"
      | Boolean b -> append (if b then "true" else "false")
      | Number number -> append <| number.ToString(CultureInfo.InvariantCulture)
      | Float number -> append <| number.ToString(CultureInfo.InvariantCulture)
      | String s -> 
          append "\""
          append <| JavaScriptStringEncode s
          append "\""
      | Record properties -> 
          let isNotFirst = ref false
          append "{"
          for k, v in properties do
            if !isNotFirst 
            then append "," 
            else isNotFirst := true
            newLine 2
            if saveOptions = JsonSaveOptions.None then
              appendFormat "\"{0}\": " (JavaScriptStringEncode k)
            else
              appendFormat "\"{0}\":" (JavaScriptStringEncode k)
            serialize sb (indentation + 2) v
          newLine 0
          append "}"
      | Array elements -> 
          let isNotFirst = ref false
          append "["
          for element in elements do
            if !isNotFirst 
            then append ","
            else isNotFirst := true
            newLine 2
            serialize sb (indentation + 2) element
          if elements.Length > 0 then 
            newLine 0
          append "]"

    let sb = StringBuilder()
    serialize sb 0 x
    sb.ToString()

/// [omit]
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

type private JsonParser(jsonText:string, cultureInfo, tolerateErrors) =

    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture

    let mutable i = 0
    let s = jsonText

    // Helper functions
    let skipWhitespace() =
      while i < s.Length && Char.IsWhiteSpace s.[i] do
        i <- i + 1
    let decimalSeparator = cultureInfo.NumberFormat.NumberDecimalSeparator.[0]
    let isNumChar c = 
      Char.IsDigit c || c=decimalSeparator || c='e' || c='E' || c='+' || c='-'
    let throw() = 
      let msg = 
        sprintf 
          "Invalid JSON starting at character %d, snippet = \n----\n%s\n-----\njson = \n------\n%s\n-------" 
          i (jsonText.[(max 0 (i-10))..(min (jsonText.Length-1) (i+10))]) (if jsonText.Length > 1000 then jsonText.Substring(0, 1000) else jsonText)
      failwith msg
    let ensure cond = 
      if not cond then throw()  

    // Recursive descent parser for JSON that uses global mutable index
    let rec parseValue() =
        skipWhitespace()
        ensure(i < s.Length)
        match s.[i] with
        | '"' -> JsonValue.String(parseString())
        | '-' -> parseNum()
        | c when Char.IsDigit(c) -> parseNum()
        | '{' -> parseObject()
        | '[' -> parseArray()
        | 't' -> parseLiteral("true", JsonValue.Boolean true)
        | 'f' -> parseLiteral("false", JsonValue.Boolean false)
        | 'n' -> parseLiteral("null", JsonValue.Null)
        | _ -> throw()

    and parseRootValue() =
        skipWhitespace()
        ensure(i < s.Length)
        match s.[i] with
        | '{' -> parseObject()
        | '[' -> parseArray()
        | _ -> throw()

    and parseString() =
        ensure(i < s.Length && s.[i] = '"')
        i <- i + 1
        let buf = new StringBuilder()
        while i < s.Length && s.[i] <> '"' do
            if s.[i] = '\\' then
                ensure(i+1 < s.Length)
                match s.[i+1] with
                | 'b' -> buf.Append('\b') |> ignore
                | 'f' -> buf.Append('\f') |> ignore
                | 'n' -> buf.Append('\n') |> ignore
                | 't' -> buf.Append('\t') |> ignore
                | 'r' -> buf.Append('\r') |> ignore
                | '\\' -> buf.Append('\\') |> ignore
                | '/' -> buf.Append('/') |> ignore
                | '"' -> buf.Append('"') |> ignore
                | 'u' ->
                    ensure(i+5 < s.Length)
                    let hexdigit d = 
                        if d >= '0' && d <= '9' then int32 d - int32 '0'
                        elif d >= 'a' && d <= 'f' then int32 d - int32 'a' + 10
                        elif d >= 'A' && d <= 'F' then int32 d - int32 'A' + 10
                        else failwith "hexdigit" 
                    let unicodeChar (s:string) =
                        if s.Length <> 4 then failwith "unicodeChar";
                        char (hexdigit s.[0] * 4096 + hexdigit s.[1] * 256 + hexdigit s.[2] * 16 + hexdigit s.[3])
                    let ch = unicodeChar (s.Substring(i+2, 4))
                    buf.Append(ch) |> ignore
                    i <- i + 4  // the \ and u will also be skipped past further below
                | _ -> throw()
                i <- i + 2  // skip past \ and next char
            else
                buf.Append(s.[i]) |> ignore
                i <- i + 1
        ensure(i < s.Length && s.[i] = '"')
        i <- i + 1
        buf.ToString()

    and parseNum() =
        let start = i
        while i < s.Length && isNumChar(s.[i]) do
            i <- i + 1
        let len = i - start
        match TextConversions.AsDecimal cultureInfo (s.Substring(start,len)) with  
        | Some x -> JsonValue.Number x
        | _ -> 
            match TextConversions.AsFloat [| |] (*useNoneForMissingValues*)false cultureInfo (s.Substring(start,len)) with  
            | Some x -> JsonValue.Float x
            | _ -> throw()

    and parsePair() =
        let key = parseString()
        skipWhitespace()
        ensure(i < s.Length && s.[i] = ':')
        i <- i + 1
        skipWhitespace()
        key, parseValue()

    and parseEllipsis() =
        let mutable openingBrace = false
        if i < s.Length && s.[i] = '{' then
            openingBrace <- true
            i <- i + 1
            skipWhitespace()
        while i < s.Length && s.[i] = '.' do
            i <- i + 1
            skipWhitespace()
        if openingBrace && i < s.Length && s.[i] = '}' then
            i <- i + 1
            skipWhitespace()

    and parseObject() =
        ensure(i < s.Length && s.[i] = '{')
        i <- i + 1
        skipWhitespace()
        let pairs = ResizeArray<_>()
        if i < s.Length && s.[i] = '"' then
            pairs.Add(parsePair())
            skipWhitespace()
            while i < s.Length && s.[i] = ',' do
                i <- i + 1
                skipWhitespace()
                if tolerateErrors && s.[i] = '}' then
                    () // tolerate a trailing comma, even though is not valid json
                else
                    pairs.Add(parsePair())
                    skipWhitespace()
        if tolerateErrors && i < s.Length && s.[i] <> '}' then
            parseEllipsis() // tolerate ... or {...}
        ensure(i < s.Length && s.[i] = '}')
        i <- i + 1
        JsonValue.Record(pairs |> Array.ofSeq)

    and parseArray() =
        ensure(i < s.Length && s.[i] = '[')
        i <- i + 1
        skipWhitespace()
        let vals = ResizeArray<_>()
        if i < s.Length && s.[i] <> ']' then
            vals.Add(parseValue())
            skipWhitespace()
            while i < s.Length && s.[i] = ',' do
                i <- i + 1
                skipWhitespace()
                vals.Add(parseValue())
                skipWhitespace()
        if tolerateErrors && i < s.Length && s.[i] <> ']' then
            parseEllipsis() // tolerate ... or {...}
        ensure(i < s.Length && s.[i] = ']')
        i <- i + 1
        JsonValue.Array(vals |> Seq.toArray)

    and parseLiteral(expected, r) =
        ensure(i+expected.Length < s.Length)
        for j in 0 .. expected.Length - 1 do
            ensure(s.[i+j] = expected.[j])
        i <- i + expected.Length
        r

    // Start by parsing the top-level value
    member x.Parse() = 
        let value = parseRootValue()
        skipWhitespace()
        if i <> s.Length then
            throw()
        value

    member x.ParseMultiple() = 
        seq {
            while i <> s.Length do
                yield parseRootValue()
                skipWhitespace() 
        }

type JsonValue with

  /// Parses the specified JSON string
  static member Parse(text, ?cultureInfo) = 
    JsonParser(text, cultureInfo, false).Parse()

  /// Loads JSON from the specified stream
  static member Load(stream:Stream, ?cultureInfo) = 
    use reader = new StreamReader(stream)
    let text = reader.ReadToEnd()
    JsonParser(text, cultureInfo, false).Parse()

  /// Loads JSON from the specified reader
  static member Load(reader:TextReader, ?cultureInfo) = 
    let text = reader.ReadToEnd()
    JsonParser(text, cultureInfo, false).Parse()

  /// Loads JSON from the specified uri asynchronously
  static member AsyncLoad(uri:string, ?cultureInfo) = async {
    let! reader = IO.asyncReadTextAtRuntime false "" "" "JSON" uri
    let text = reader.ReadToEnd()
    return JsonParser(text, cultureInfo, false).Parse()
  }

  /// Loads JSON from the specified uri
  static member Load(uri:string, ?cultureInfo) =
    JsonValue.AsyncLoad(uri, ?cultureInfo=cultureInfo)
    |> Async.RunSynchronously

  /// Parses the specified JSON string, tolerating invalid errors like trailing commans, and ignore content with elipsis ... or {...}
  static member ParseSample(text, ?cultureInfo) =
    JsonParser(text, cultureInfo, true).Parse()

  /// Parses the specified string into multiple JSON values
  static member ParseMultiple(text, ?cultureInfo) =
    JsonParser(text, cultureInfo, false).ParseMultiple()

  /// Sends the JSON to the specified uri. Defaults to a POST request.
  member x.Request(uri:string, ?httpMethod, ?headers) =  
    let httpMethod = defaultArg httpMethod HttpMethod.Post
    let headers = defaultArg headers []
    let headers =
        if headers |> List.exists (fst >> ((=) (fst (HttpRequestHeaders.UserAgent ""))))
        then headers
        else HttpRequestHeaders.UserAgent "F# Data JSON Type Provider" :: headers
    let headers = HttpRequestHeaders.ContentType HttpContentTypes.Json :: headers
    Http.Request(
      uri,
      body = TextRequest (x.ToString(JsonSaveOptions.DisableFormatting)),
      headers = headers,
      httpMethod = httpMethod)

  /// Sends the JSON to the specified uri. Defaults to a POST request.
  member x.RequestAsync(uri:string, ?httpMethod, ?headers) =
    let httpMethod = defaultArg httpMethod HttpMethod.Post
    let headers = defaultArg headers []
    let headers =
        if headers |> List.exists (fst >> ((=) (fst (HttpRequestHeaders.UserAgent ""))))
        then headers
        else HttpRequestHeaders.UserAgent "F# Data JSON Type Provider" :: headers
    let headers = HttpRequestHeaders.ContentType HttpContentTypes.Json :: headers
    Http.AsyncRequest(
      uri,
      body = TextRequest (x.ToString(JsonSaveOptions.DisableFormatting)),
      headers = headers,
      httpMethod = httpMethod)

  [<Obsolete("Please use JsonValue.Request instead")>]
  member x.Post(uri:string, ?headers) =  
    x.Request(uri, ?headers = headers)
