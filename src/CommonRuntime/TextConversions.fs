// --------------------------------------------------------------------------------------
// Helper operations for converting converting string values to other types
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Runtime

open System
open System.Globalization
open System.Text.RegularExpressions

// --------------------------------------------------------------------------------------

[<AutoOpen>]
module private Helpers =

  /// Convert the result of TryParse to option type
  let asOption = function true, v -> Some v | _ -> None

  let (|StringEquals|_|) (s1:string) s2 = 
    if s1.Equals(s2, StringComparison.OrdinalIgnoreCase) 
      then Some () else None

  let (|OneOf|_|) set str = 
    if Array.exists ((=) str) set then Some() else None

  let regexOptions = 
#if FX_NO_REGEX_COMPILATION
    RegexOptions.None
#else
    RegexOptions.Compiled
#endif
  // note on the regex we have /Date()/ and not \/Date()\/ because the \/ escaping 
  // is already taken care of before AsDateTime is called
  let msDateRegex = lazy Regex(@"^/Date\((-?\d+)(?:-\d+)?\)/$", regexOptions)

// --------------------------------------------------------------------------------------

/// Conversions from string to string/int/int64/decimal/float/boolean/datetime/guid options
type TextConversions = 

  static member DefaultMissingValues = [|"NaN"; "NA"; "#N/A"; ":"; "&nbsp;"; "&nbsp"|]

  /// Turns empty or null string value into None, otherwise returns Some
  static member AsString str =
    if String.IsNullOrWhiteSpace str then None else Some str

  static member AsInteger cultureInfo text = 
    Int32.TryParse(text, NumberStyles.Integer, cultureInfo) |> asOption
  
  static member AsInteger64 cultureInfo text = 
    Int64.TryParse(text, NumberStyles.Integer, cultureInfo) |> asOption
  
  static member AsDecimal cultureInfo text =
    Decimal.TryParse(text, NumberStyles.Number ||| NumberStyles.AllowCurrencySymbol, cultureInfo) |> asOption
  
  /// if useNoneForMissingValues is true, NAs are returned as None, otherwise Some Double.NaN is used
  static member AsFloat missingValues useNoneForMissingValues cultureInfo (text:string) = 
    match text.Trim() with
    | OneOf missingValues -> if useNoneForMissingValues then None else Some Double.NaN
    | _ -> Double.TryParse(text, NumberStyles.Float, cultureInfo)
           |> asOption
           |> Option.bind (fun f -> if useNoneForMissingValues && Double.IsNaN f then None else Some f)
  
  // cultureInfo is ignored for now, but might not be in the future, so we're keeping in in the API
  static member AsBoolean (_cultureInfo:IFormatProvider) (text:string) =     
    match text.Trim() with
    | StringEquals "true" | StringEquals "yes" | StringEquals "1" -> Some true
    | StringEquals "false" | StringEquals "no" | StringEquals "0" -> Some false
    | _ -> None

  /// Parse date time using either the JSON milliseconds format or using ISO 8601
  /// that is, either "\/Date(<msec-since-1/1/1970>)\/" or something
  /// along the lines of "2013-01-28T00:37Z"
  static member AsDateTime cultureInfo (text:string) =
    // Try parse "Date(<msec>)" style format
    let matchesMS = msDateRegex.Value.Match (text.Trim())
    if matchesMS.Success then
      matchesMS.Groups.[1].Value 
      |> Double.Parse 
      |> DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds 
      |> Some
    else
      // Parse ISO 8601 format, fixing time zone if needed
      let dateTimeStyles = DateTimeStyles.AllowWhiteSpaces ||| DateTimeStyles.RoundtripKind
      match DateTime.TryParse(text, cultureInfo, dateTimeStyles) with
      | true, d ->
          if d.Kind = DateTimeKind.Unspecified then 
            new DateTime(d.Ticks, DateTimeKind.Local) |> Some
          else 
            Some d
      | _ -> None

  static member AsGuid (text:string) = 
    Guid.TryParse(text.Trim()) |> asOption
