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
open System.Text
open System.Globalization

/// Represents a JSON value. Large numbers that do not fit in the 
/// System.Decimal type are represented using the BigNumber case, while
/// smaller numbers are represented as decimals to avoid precision loss.
[<RequireQualifiedAccess>]
type JsonValue =
  | String of string
  | Number of decimal
  | BigNumber of float 
  | Object of Map<string, JsonValue>
  | Array of JsonValue list
  | Boolean of bool
  | Null
  override this.ToString() = 
    let invariant = System.Globalization.CultureInfo.InvariantCulture
    let rec serialize (sb:StringBuilder) = function
      | JsonValue.Null -> sb.Append "null"
      | JsonValue.Boolean b -> sb.Append(b.ToString().ToLowerInvariant())
      | JsonValue.Number number -> sb.Append(number.ToString(invariant))
      | JsonValue.BigNumber number -> sb.Append(number.ToString(invariant))
      | JsonValue.String t -> 
          sb.Append("\"" + System.Web.HttpUtility.JavaScriptStringEncode(t) + "\"")
      | JsonValue.Object properties -> 
          let isNotFirst = ref false
          sb.Append "{"  |> ignore
          for (KeyValue(k, v)) in properties do
            if !isNotFirst then sb.Append "," |> ignore else isNotFirst := true
            sb.AppendFormat("\"{0}\":", k)  |> ignore
            serialize sb v |> ignore
          sb.Append "}"
      | JsonValue.Array elements -> 
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

type private JsonParser(jsonText:string) =
    let mutable i = 0
    let s = jsonText

    // Helper functions
    let skipWhitespace() =
      while i < s.Length && (s.[i]=' ' || s.[i]='\t' || s.[i]='\r' || s.[i]='\n') do
        i <- i + 1    
    let isNumChar c = 
      System.Char.IsDigit c || c='.' || c='e' || c='E' || c='+' || c='-'
    let throw() = 
      let msg = 
        sprintf 
          "Invalid Json starting at character %d, snippet = \n----\n%s\n-----\njson = \n------\n%s\n-------" 
          i (jsonText.[(max 0 (i-10)).. (min (jsonText.Length-1) (i+10))]) jsonText      
      raise <| new System.Exception(msg)
    let ensure cond = 
      if not cond then throw()  

    // Recursive descent parser for JSON that uses global mutable index
    let rec parseValue() =
        skipWhitespace()
        ensure(i < s.Length)
        match s.[i] with
        | '"' -> JsonValue.String(parseString())
        | '-' -> parseNum()
        | c when System.Char.IsDigit(c) -> parseNum()
        | '{' -> parseObject()
        | '[' -> parseArray()
        | 't' -> parseLiteral("true", JsonValue.Boolean true)
        | 'f' -> parseLiteral("false", JsonValue.Boolean false)
        | 'n' -> parseLiteral("null", JsonValue.Null)
        | _ -> throw()

    and parseString() =
        ensure(i < s.Length && s.[i] = '"')
        i <- i + 1
        let buf = new System.Text.StringBuilder()
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
                    let chars = System.Text.UnicodeEncoding.Unicode.GetChars(bytes)
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
        match System.Decimal.TryParse(s.Substring(start,len), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture) with  
        | true, x -> JsonValue.Number x
        | _ -> 
            match System.Double.TryParse(s.Substring(start,len), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture) with  
            | true, x -> JsonValue.BigNumber x
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
        JsonValue.Array(vals |> Seq.toList)

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
  static member Parse(input:string) = JsonParser(input).Parse()

// --------------------------------------------------------------------------------------
// Unsafe extensions for simple JSON processing
// --------------------------------------------------------------------------------------

/// Adds extension methods that can be used to get work with the JSON value
/// in a less safe, but shorter way. The module also provides dynamic operator
module JsonReader = 

  /// Get property of a JSON object (assuming that the value is an object)
  let (?) (jsonObject) (property:string) = 
    match jsonObject with
    | JsonValue.Object o -> o.[property]
    | _ -> failwith "JSON mismatch: Not an object"

  type JsonValue with
    /// Get all elements of a JSON object (assuming that the value is an array)
    member x.GetEnumerator() = 
      match x with
      | JsonValue.Array things -> (things :> seq<_>).GetEnumerator()
      | _ -> failwith "JSON mismatch: Not an array"

    /// Get the string value of an elements (assuming that the value is a string)
    member x.AsString = 
      match x with
      | JsonValue.Null -> null
      | JsonValue.String t -> t
      | _ -> failwith "JSON mismatch: Not a string"

    /// Get a number as a float (assuming that the value is convertible to a float)
    member x.AsFloat = 
      match x with
      | JsonValue.Number n -> float n
      | JsonValue.BigNumber n -> n
      | _ -> failwith "JSON mismatch: Not a number"

    /// Get a number as a decimal (assuming that the value fits in decimal)
    member x.AsDecimal = 
      match x with
      | JsonValue.Number n -> n
      | JsonValue.BigNumber n -> decimal n
      | _ -> failwith "JSON mismatch: Not a number"

    /// Get a number as an integer (assuming that the value fits in integer)
    member x.AsInteger = 
      match x with
      | JsonValue.Number n -> int n
      | JsonValue.BigNumber n -> int n
      | _ -> failwith "JSON mismatch: Not a number"

    /// Get a number as an integer (assuming that the value fits in integer)
    member x.AsInteger64 = 
      match x with
      | JsonValue.Number n -> int64 n 
      | JsonValue.BigNumber n -> int64 n
      | _ -> failwith "JSON mismatch: Not a number"

    /// Get a boolean value of an elements (assuming that the value is a boolean)
    member x.AsBoolean = 
      match x with
      | JsonValue.Boolean t -> t
      | _ -> failwith "JSON mismatch: Not a boolean"

    /// Get inner text of an element - this includes just string nodes and
    /// string nodes in an array (e.g. multi-line string represented as array)
    /// (assuming that the value is a string or array of strings)
    member x.InnerText = 
      match x with
      | JsonValue.String t -> t
      | JsonValue.Array a -> a |> List.map (fun e -> e.InnerText) |> String.concat ""
      | _ -> failwith "JSON mismatch: Contains non-text element"

    /// Get a sequence of key-value pairs representing the properties of an object
    /// (assuming that the value is an object)
    member x.Properties = 
      match x with
      | JsonValue.Object map -> seq { for (KeyValue(k, v)) in map -> k, v }
      | _ -> failwith "JSON mismatch: Not an object"