﻿// --------------------------------------------------------------------------------------
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
  let asOption = function true, v -> Some v | _ -> None

  let (|StringEqualsIgnoreCase|_|) (s1:string) s2 = 
    if s1.Equals(s2, StringComparison.OrdinalIgnoreCase) 
      then Some () else None

  let (|OneOfIgnoreCase|_|) set str = 
    if Array.exists (fun s -> StringComparer.OrdinalIgnoreCase.Compare(s, str) = 0) set then Some() else None

  // note on the regex we have /Date()/ and not \/Date()\/ because the \/ escaping 
  // is already taken care of before AsDateTime is called
  let msDateRegex = lazy Regex(@"^/Date\((-?\d+)(?:[-+]\d+)?\)/$", RegexOptions.Compiled)

// --------------------------------------------------------------------------------------

/// Conversions from string to string/int/int64/decimal/float/boolean/datetime/guid options
type TextConversions private() = 

  /// `NaN` `NA` `N/A` `#N/A` `:` `-` `TBA` `TBD`
  static member val DefaultMissingValues = [| "NaN"; "NA"; "N/A"; "#N/A"; ":"; "-"; "TBA"; "TBD" |]
  
  /// `%` `‰` `‱`
  static member val DefaultNonCurrencyAdorners = [| '%'; '‰'; '‱' |] |> Set.ofArray
  
  /// `¤` `$` `¢` `£` `¥` `₱` `﷼` `₤` `₭` `₦` `₨` `₩` `₮` `€` `฿` `₡` `៛` `؋` `₴` `₪` `₫` `₹` `ƒ`
  static member val DefaultCurrencyAdorners = [| '¤'; '$'; '¢'; '£'; '¥'; '₱'; '﷼'; '₤'; '₭'; '₦'; '₨'; '₩'; '₮'; '€'; '฿'; '₡'; '៛'; '؋'; '₴'; '₪'; '₫'; '₹'; 'ƒ' |] |> Set.ofArray

  static member val private DefaultRemovableAdornerCharacters = 
    Set.union TextConversions.DefaultNonCurrencyAdorners TextConversions.DefaultCurrencyAdorners
  
  //This removes any adorners that might otherwise casue the inference to infer string. A notable a change is
  //Currency Symbols are now treated as an Adorner like a '%' sign thus are now independant
  //of the culture. Which is probably better since we have lots of scenarios where we want to
  //consume values prefixed with € or $ but in a different culture. 
  static member private RemoveAdorners (value:string) = 
    String(value.ToCharArray() |> Array.filter (not << TextConversions.DefaultRemovableAdornerCharacters.Contains))

  /// Turns empty or null string value into None, otherwise returns Some
  static member AsString str =
    if String.IsNullOrWhiteSpace str then None else Some str

  static member AsInteger cultureInfo text = 
    Int32.TryParse(TextConversions.RemoveAdorners text, NumberStyles.Integer, cultureInfo) |> asOption
  
  static member AsInteger64 cultureInfo text = 
    Int64.TryParse(TextConversions.RemoveAdorners text, NumberStyles.Integer, cultureInfo) |> asOption
  
  static member AsDecimal cultureInfo text =
    Decimal.TryParse(TextConversions.RemoveAdorners text, NumberStyles.Currency, cultureInfo) |> asOption
  
  /// if useNoneForMissingValues is true, NAs are returned as None, otherwise Some Double.NaN is used
  static member AsFloat missingValues useNoneForMissingValues cultureInfo (text:string) = 
    match text.Trim() with
    | OneOfIgnoreCase missingValues -> if useNoneForMissingValues then None else Some Double.NaN
    | _ -> Double.TryParse(text, NumberStyles.Any, cultureInfo)
           |> asOption
           |> Option.bind (fun f -> if useNoneForMissingValues && Double.IsNaN f then None else Some f)
  
  static member AsBoolean (text:string) =     
    match text.Trim() with
    | StringEqualsIgnoreCase "true" | StringEqualsIgnoreCase "yes" | StringEqualsIgnoreCase "1" -> Some true
    | StringEqualsIgnoreCase "false" | StringEqualsIgnoreCase "no" | StringEqualsIgnoreCase "0" -> Some false
    | _ -> None

  /// Parse date time using either the JSON milliseconds format or using ISO 8601
  /// that is, either `/Date(<msec-since-1/1/1970>)/` or something
  /// along the lines of `2013-01-28T00:37Z`
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

module internal UnicodeHelper =

    // used http://en.wikipedia.org/wiki/UTF-16#Code_points_U.2B010000_to_U.2B10FFFF as a guide below
    let getUnicodeSurrogatePair num =
        // only code points U+010000 to U+10FFFF supported
        // for coversion to UTF16 surrogate pair
        let codePoint = num - 0x010000u
        let HIGH_TEN_BIT_MASK = 0xFFC00u                     // 1111|1111|1100|0000|0000
        let LOW_TEN_BIT_MASK = 0x003FFu                      // 0000|0000|0011|1111|1111
        let leadSurrogate = (codePoint &&& HIGH_TEN_BIT_MASK >>> 10) + 0xD800u
        let trailSurrogate = (codePoint &&& LOW_TEN_BIT_MASK) + 0xDC00u
        char leadSurrogate, char trailSurrogate
