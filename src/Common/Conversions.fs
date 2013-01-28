// --------------------------------------------------------------------------------------
// Helper operations for converting converting string values to other types
// --------------------------------------------------------------------------------------

namespace FSharp.Data.RuntimeImplementation

open System
open System.Globalization
open System.Text.RegularExpressions

[<AutoOpen>]
module private Helpers =

  /// Convert the result of TryParse to option type
  let asOption = function true, v -> Some v | _ -> None

  let (|StringEquals|_|) (s1:string) s2 = 
    if s1.Equals(s2, StringComparison.OrdinalIgnoreCase) 
      then Some () else None

  let regexOptions = 
#if FX_NO_REGEX_COMPILATION
    RegexOptions.None
#else
    RegexOptions.Compiled
#endif
  let msDateRegex = lazy (new Regex(@"^/Date\((-?\d+)(?:-\d+)?\)/$", regexOptions))

type Operations = 

  /// Turns empty or null string value into None, otherwise returns Some
  static member AsOption str =
    if String.IsNullOrWhiteSpace str then None else Some str

  /// Parse date time using either MS JSON format or using ISO 8601
  /// that is, either "Date(<msec-since-1/1/1970>)" or something
  /// along the lines of "2013-01-28T00:37Z"
  static member AsDateTime culture (text:string) =
    // Try parse "Date(<msec>)" style format
    let matchesMS = msDateRegex.Value.Match (text.Trim())
    if matchesMS.Success then
      matchesMS.Groups.[1].Value 
      |> Double.Parse 
      |> DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds 
      |> Some
    else
      // Parse ISO 8601 format, fixing time zone if needed
      let dateTimeStyles = 
        if text.IndexOf("Z", StringComparison.OrdinalIgnoreCase) <> -1 then
          DateTimeStyles.AssumeUniversal ||| DateTimeStyles.AdjustToUniversal 
            ||| DateTimeStyles.AllowWhiteSpaces
        else
          DateTimeStyles.AssumeLocal ||| DateTimeStyles.AllowWhiteSpaces
      match DateTime.TryParse(text, culture, dateTimeStyles) with
      | true, d -> Some d
      | _ -> None

  // Try parse string into standard types and returns None if failed
  static member AsInteger culture text = 
    Int32.TryParse(text, NumberStyles.Integer, culture) |> asOption
  
  static member AsInteger64 culture text = 
    Int64.TryParse(text, NumberStyles.Integer, culture) |> asOption
  
  static member AsDecimal culture  text =
    Decimal.TryParse(text, NumberStyles.Number, culture) |> asOption
  
  static member AsFloat culture (text:string) = 
    match text.Trim() with
    | StringEquals "#N/A" -> Some Double.NaN
    | _ -> Double.TryParse(text, NumberStyles.Float, culture) |> asOption
  
  static member AsBoolean culture (text:string) = 
    match text.Trim() with
    | StringEquals "true" | StringEquals "yes" -> Some true
    | StringEquals "false" | StringEquals "no" -> Some false
    | _ -> None

  /// Returns CultureInfo matching the specified culture string
  /// (or InvariantCulture if the argument is null or empty)
  static member GetCulture culture =
    if String.IsNullOrEmpty culture then CultureInfo.InvariantCulture 
    else Globalization.CultureInfo(culture)

  // Operations that convert string to supported primitive types
  static member ConvertString text = 
    text |> Option.map (fun (s:string) -> s.Trim())

  static member ConvertDateTime(culture, text) = 
    text |> Option.bind (fun s -> Operations.AsDateTime (Operations.GetCulture culture) s)

  static member ConvertInteger(culture, text) = 
    text |> Option.bind (fun s -> Operations.AsInteger (Operations.GetCulture culture) s)
  
  static member ConvertInteger64(culture, text) = 
    text |> Option.bind (fun s -> Operations.AsInteger64 (Operations.GetCulture culture) s)
  
  static member ConvertDecimal(culture, text) =
    text |> Option.bind (fun s -> Operations.AsDecimal (Operations.GetCulture culture) s)
  
  static member ConvertFloat(culture, text) = 
    text |> Option.bind (fun s -> Operations.AsFloat (Operations.GetCulture culture) s)
  
  static member ConvertBoolean(culture, text) = 
    text |> Option.bind (fun s -> Operations.AsBoolean (Operations.GetCulture culture) s)

  /// Operation that extracts the value from an option and reports a
  /// meaningful error message when the value is not there
  ///
  /// We could just return defaultof<'T> if the value is None, but that is not
  /// really correct, because this operation is used when the inference engine
  /// inferred that the value is always present. The user should update their
  /// sample to infer it as optional (and get None). If we use defaultof<'T> we
  /// might return 0 and the user would not be able to distinguish between 0
  /// and missing value.
  static member GetNonOptionalValue<'T>(name:string, opt:option<'T>) : 'T = 
    match opt with 
    | Some v -> v
    | None when typeof<'T> = typeof<double> -> box Double.NaN :?> 'T
    | None when typeof<'T> = typeof<string> -> box "" :?> 'T
    | _ -> failwithf "Mismatch: %s is missing" name

  /// Turn an F# option type Option<'T> containing a primitive 
  /// value type into a .NET type Nullable<'T>
  static member ToNullable opt =
    match opt with 
    | Some v -> Nullable v
    | _ -> Nullable()
    