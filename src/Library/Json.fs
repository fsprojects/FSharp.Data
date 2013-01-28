// --------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation 2005-2012.
// This sample code is provided "as is" without warranty of any kind. 
// We disclaim all warranties, either express or implied, including the 
// warranties of merchantability and fitness for a particular purpose. 
//
// A simple F# portable parser for JSON data
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Json

open System
open System.IO
open System.Text
open System.Globalization
open System.Xml.Linq
open FSharp.Data
open FSharp.Data.RuntimeImplementation
open ProviderImplementation.HttpUtility

/// Represents a JSON value. Large numbers that do not fit in the 
/// Decimal type are represented using the Float case, while
/// smaller numbers are represented as decimals to avoid precision loss.
[<RequireQualifiedAccess>]
type JsonValue =
  | String of string
  | Number of decimal
  | Float of float 
  | Object of Map<string, JsonValue>
  | Array of JsonValue[]
  | Boolean of bool
  | Null

  override this.ToString() = 

    let rec serialize (sb:StringBuilder) = function
      | Null -> sb.Append "null"
      | Boolean b -> sb.Append(b.ToString().ToLowerInvariant())
      | Number number -> sb.Append(number.ToString(CultureInfo.InvariantCulture))
      | Float number -> sb.Append(number.ToString(CultureInfo.InvariantCulture))
      | String s -> 
          sb.Append("\"" + JavaScriptStringEncode(s) + "\"")
      | Object properties -> 
          let isNotFirst = ref false
          sb.Append "{"  |> ignore
          for KeyValue(k, v) in properties |> Seq.sortBy (fun (KeyValue(k, v)) -> k) do
            if !isNotFirst then sb.Append "," |> ignore else isNotFirst := true
            sb.AppendFormat("\"{0}\":", k)  |> ignore
            serialize sb v |> ignore
          sb.Append "}"
      | Array elements -> 
          let isNotFirst = ref false
          sb.Append "[" |> ignore
          for element in elements do
            if !isNotFirst then sb.Append "," |> ignore else isNotFirst := true
            serialize sb element |> ignore
          sb.Append "]"

    (serialize (new StringBuilder()) this).ToString()

// --------------------------------------------------------------------------------------
// JSON parser
// --------------------------------------------------------------------------------------

type private JsonParser(jsonText:string, culture:CultureInfo option) =

    let culture = defaultArg culture CultureInfo.InvariantCulture

    let mutable i = 0
    let s = jsonText

    // Helper functions
    let skipWhitespace() =
      while i < s.Length && (s.[i]=' ' || s.[i]='\t' || s.[i]='\r' || s.[i]='\n') do
        i <- i + 1    
    let isNumChar c = 
      Char.IsDigit c || c='.' || c='e' || c='E' || c='+' || c='-'
    let throw() = 
      let msg = 
        sprintf 
          "Invalid Json starting at character %d, snippet = \n----\n%s\n-----\njson = \n------\n%s\n-------" 
          i (jsonText.[(max 0 (i-10)).. (min (jsonText.Length-1) (i+10))]) jsonText      
      raise <| new Exception(msg)
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
                    let unicodeGraphShort (s:string) =
                        if s.Length <> 4 then failwith "unicodegraph";
                        uint16 (hexdigit s.[0] * 4096 + hexdigit s.[1] * 256 + hexdigit s.[2] * 16 + hexdigit s.[3])
                    let makeUnicodeChar (c:int) =  [| byte(c % 256); byte(c / 256) |]
                    let bytes = makeUnicodeChar(int(unicodeGraphShort(s.Substring(i+2, 4))))
                    let chars = UnicodeEncoding.Unicode.GetChars(bytes)
                    buf.Append(chars) |> ignore
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
        match Operations.AsDecimal culture (s.Substring(start,len)) with  
        | Some x -> JsonValue.Number x
        | _ -> 
            match Operations.AsFloat culture (s.Substring(start,len)) with  
            | Some x -> JsonValue.Float x
            | _ -> throw()

    and parsePair() =
        let key = parseString().Trim('"')
        skipWhitespace()
        ensure(i < s.Length && s.[i] = ':')
        i <- i + 1
        skipWhitespace()
        key, parseValue()

    and parseObject() =
        ensure(i < s.Length && s.[i] = '{')
        i <- i + 1
        skipWhitespace()
        let pairs = ResizeArray<_>()
        if i<s.Length && s.[i]='"' then
            pairs.Add(parsePair())
            skipWhitespace()
            while i<s.Length && s.[i]=',' do
                i <- i + 1
                skipWhitespace()
                pairs.Add(parsePair())
                skipWhitespace()
        ensure(i < s.Length && s.[i] = '}')
        i <- i + 1
        JsonValue.Object(pairs |> Map.ofSeq)

    and parseArray() =
        ensure(i < s.Length && s.[i] = '[')
        i <- i + 1
        skipWhitespace()
        let vals = ResizeArray<_>()
        if i<s.Length && s.[i]<>']' then
            vals.Add(parseValue())
            skipWhitespace()
            while i<s.Length && s.[i]=',' do
                i <- i + 1
                skipWhitespace()
                vals.Add(parseValue())
                skipWhitespace()
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
    member x.Parse() = parseValue()

type JsonValue with

  /// Parse the specified JSON string
  static member Parse(input:string, ?culture) = JsonParser(input, culture).Parse()

  /// Loads JSON from the specified stream
  static member Load(stream:Stream, ?culture) = 
    use reader = new StreamReader(stream)
    JsonParser(reader.ReadToEnd(), culture).Parse()

// --------------------------------------------------------------------------------------
// Unsafe extensions for simple JSON processing
// --------------------------------------------------------------------------------------

/// Adds extension methods that can be used to get work with the JSON value
/// in a less safe, but shorter way. The module also provides dynamic operator
module Extensions = 
  
  type JsonValue with

    /// Get all elements of a JSON object (assuming that the value is an array)
    member x.AsArray() = 
      match x with
      | JsonValue.Array elements -> elements
      | JsonValue.Null -> [| |]
      | _ -> failwithf "JSON mismatch: Not an array - %A" x

    /// Assuming the value is an array, get the value at a specified index
    member x.Item with get(index) = x.AsArray().[index]
    /// Assuming the value is an object, get value with the specified name
    member x.Item with get(prpoertyName) = x.GetProperty(prpoertyName)

    /// Get all elements of a JSON object (assuming that the value is an array)
    member x.GetEnumerator() = x.AsArray().GetEnumerator()

    /// Get the string value of an element (assuming that the value is a string)
    member x.AsString() =
      match x with
      | JsonValue.String s -> s
      | JsonValue.Null -> null
      | JsonValue.Boolean b -> if b then "true" else "false"
      | _ -> failwithf "JSON mismatch: Not a string - %A" x

    /// Get the datetime value of an element (assuming that the value is a string
    /// containing well-formed ISO date or MSFT JSON date)
    member x.AsDateTime(?culture) = 
      match x with
      | JsonValue.String s -> 
          match Operations.AsDateTime (defaultArg culture CultureInfo.InvariantCulture) s with 
          | Some d -> d
          | _ -> failwithf "JSON mismatch: Not a datetime - %A" x
      | _ -> failwithf "JSON mismatch: Not a datetime - %A" x

    /// Get a number as a float (assuming that the value is convertible to a float)
    member x.AsFloat(?culture) = 
      match x with
      | JsonValue.Float n -> n
      | JsonValue.Number n -> float n
      | JsonValue.String s -> 
          match Operations.AsFloat (defaultArg culture CultureInfo.InvariantCulture) s with
          | Some n -> n
          | _ -> failwithf "JSON mismatch: Not a number - %A" x
      | _ -> failwithf "JSON mismatch: Not a number - %A" x

    /// Get a number as a decimal (assuming that the value fits in decimal)
    member x.AsDecimal(?culture) = 
      match x with
      | JsonValue.Number n -> n
      | JsonValue.Float n -> decimal n
      | JsonValue.String s -> 
          match Operations.AsDecimal (defaultArg culture CultureInfo.InvariantCulture) s with
          | Some n -> n
          | _ -> failwithf "JSON mismatch: Not a number - %A" x
      | _ -> failwithf "JSON mismatch: Not a number - %A" x
  
    /// Get a number as an integer (assuming that the value fits in integer)
    member x.AsInteger(?culture) = 
      match x with
      | JsonValue.Number n -> int n
      | JsonValue.Float n -> int n
      | JsonValue.String s -> 
          match Operations.AsInteger (defaultArg culture CultureInfo.InvariantCulture) s with
          | Some n -> n
          | _ -> failwithf "JSON mismatch: Not a number - %A" x
      | _ -> failwithf "JSON mismatch: Not a number - %A" x  

    /// Get a number as a 64-bit integer (assuming that the value fits in 64-bit integer)
    member x.AsInteger64(?culture) = 
      match x with
      | JsonValue.Number n -> int64 n 
      | JsonValue.Float n -> int64 n
      | JsonValue.String s -> 
          match Operations.AsInteger64 (defaultArg culture CultureInfo.InvariantCulture) s with
          | Some n -> n
          | _ -> failwithf "JSON mismatch: Not a number - %A" x
      | _ -> failwithf "JSON mismatch: Not a number - %A" x

    /// Get the boolean value of an element (assuming that the value is a boolean)
    member x.AsBoolean(?culture) =
      match x with
      | JsonValue.Boolean t -> t
      | JsonValue.String s -> 
          match Operations.AsBoolean (defaultArg culture CultureInfo.InvariantCulture) s with
          | Some n -> n
          | _ -> failwithf "JSON mismatch: Not a number - %A" x
      | _ -> failwithf "JSON mismatch: Not a boolean - %A" x

    /// Get inner text of an element - this includes just string nodes and
    /// string nodes in an array (e.g. multi-line string represented as array)
    /// (assuming that the value is a string or array of strings)
    member x.InnerText = 
      match x with
      | JsonValue.String t -> t
      | JsonValue.Array a -> a |> Seq.map (fun e -> e.InnerText) |> String.concat ""
      | _ -> failwithf "JSON mismatch: Contains non-text element - %A" x

    /// Get a sequence of key-value pairs representing the properties of an object
    /// (assuming that the value is an object)
    member x.Properties = 
      match x with
      | JsonValue.Object map -> Map.toSeq map
      | _ -> failwithf "JSON mismatch: Not an object - %A" x

    /// Try get property of a JSON object. Returns None if the value is not
    /// an object or if the property is not present.
    member x.TryGetProperty(propertyName) = 
      match x with
      | JsonValue.Object properties -> 
          Map.tryFind propertyName properties 
      | _ -> None

    /// Get property of a JSON object. Fails if the value is not an object
    /// or if the property is not present
    member x.GetProperty(propertyName) = 
      match x with
      | JsonValue.Object properties -> 
          Map.find propertyName properties
      | _ -> failwithf "JSON mismatch: Not an object - %A" x

  /// Get property of a JSON object (assuming that the value is an object)
  let (?) jsonObject propertyName = 
    match jsonObject with
    | JsonValue.Object properties -> 
        match Map.tryFind propertyName properties with 
        | Some res -> res
        | None -> failwithf "JSON mismatch: Didn't find property '%s' in %A" propertyName jsonObject
    | _ -> failwithf "JSON mismatch: Not an object - %A" jsonObject