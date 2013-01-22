// --------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation 2005-2011.
// This sample code is provided "as is" without warranty of any kind. 
// We disclaim all warranties, either express or implied, including the 
// warranties of merchantability and fitness for a particular purpose. 
// --------------------------------------------------------------------------------------

namespace FSharp.Data.RuntimeImplementation

open System
open System.Globalization

[<AutoOpen>]
module private Helpers =

    /// Convert the result of TryParse to option type
    let asOption = function true, v -> Some v | _ -> None

    let (|StringEquals|_|) (s1:string) s2 = 
      if s1.Equals(s2, StringComparison.OrdinalIgnoreCase) 
        then Some () else None

type Operations =

  /// Returns CultureInfo matching the specified culture string
  /// (or InvariantCulture if the argument is null or empty)
  static member GetCulture(culture) =
    if String.IsNullOrEmpty culture then CultureInfo.InvariantCulture else
    Globalization.CultureInfo(culture)

  // Operations that convert string to supported primitive types
  static member ConvertString str = Option.map (fun (s:string) -> s) str
  static member ConvertDateTime(culture:CultureInfo, text) = 
    Option.bind (fun s -> DateTime.TryParse(s, culture, DateTimeStyles.None) |> asOption) text
  static member ConvertInteger(culture:CultureInfo, text) = 
    Option.bind (fun s -> Int32.TryParse(s, NumberStyles.Any, culture) |> asOption) text
  static member ConvertInteger64(culture:CultureInfo, text) = 
    Option.bind (fun s -> Int64.TryParse(s, NumberStyles.Any, culture) |> asOption) text
  static member ConvertDecimal(culture:CultureInfo, text) =
    Option.bind (fun s -> Decimal.TryParse(s, NumberStyles.Any, culture) |> asOption) text
  static member ConvertFloat(culture:CultureInfo, text) = 
    Option.bind (fun (s:string) -> 
        match s.Trim() with
        | StringEquals "#N/A" -> Some Double.NaN
        | _ -> Double.TryParse(s, NumberStyles.Any, culture) |> asOption)
        text
  static member ConvertBoolean b = b |> Option.bind (fun (s:string) ->
      match s.Trim() with
      | StringEquals "true" | StringEquals "yes" -> Some true
      | StringEquals "false" | StringEquals "no" -> Some false
      | _ -> None)

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
    | None when typeof<'T> = typeof<string> -> Unchecked.defaultof<'T>
    | None when typeof<'T> = typeof<DateTime> -> Unchecked.defaultof<'T>
    | _ -> failwithf "Mismatch: %s is missing" name