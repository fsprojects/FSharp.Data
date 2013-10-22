// --------------------------------------------------------------------------------------
// Common runtime helpers for type provider - methods & types used by the generated erased code
// --------------------------------------------------------------------------------------
namespace FSharp.Data.RuntimeImplementation

open System
open System.Globalization
open FSharp.Data.Json

type CommonRuntime = 

  /// Returns CultureInfo matching the specified culture string
  /// (or InvariantCulture if the argument is null or empty)
  static member GetCulture(culture) =
    if String.IsNullOrEmpty culture then CultureInfo.InvariantCulture 
    else CultureInfo culture

  // --------------------------------------------------------------------------------------
  // string option -> type

  static member ConvertString(text:string option) = text

  static member ConvertInteger(culture, text) = 
    text |> Option.bind (fun text -> text |> TextConversions.AsInteger (CommonRuntime.GetCulture culture))
  
  static member ConvertInteger64(culture, text) = 
    text |> Option.bind (fun text -> text |> TextConversions.AsInteger64 (CommonRuntime.GetCulture culture))

  static member ConvertDecimal(culture, text) =
    text |> Option.bind (fun text -> text |> TextConversions.AsDecimal (CommonRuntime.GetCulture culture))

  static member ConvertFloat(culture, missingValues:string, text) = 
    text |> Option.bind (fun text -> text |> TextConversions.AsFloat (missingValues.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)) 
                                                                     (*useNoneForMissingValues*)true
                                                                     (CommonRuntime.GetCulture culture))

  static member ConvertBoolean(culture, text) = 
    text |> Option.bind (fun text -> text |> TextConversions.AsBoolean (CommonRuntime.GetCulture culture))

  static member ConvertDateTime(culture, text) = 
    text |> Option.bind (fun text -> text |> TextConversions.AsDateTime (CommonRuntime.GetCulture culture))

  static member ConvertGuid(text) = 
    text |> Option.bind TextConversions.AsGuid

  // --------------------------------------------------------------------------------------
  // json option -> type

  static member JsonConvertString(culture, json) = 
    json |> Option.bind (fun json -> json |> JsonConversions.AsString (*useNoneForNullOrEmpty*)true (CommonRuntime.GetCulture culture))
  
  static member JsonConvertInteger(culture, json) = 
    json |> Option.bind (fun json -> json |> JsonConversions.AsInteger (CommonRuntime.GetCulture culture))
  
  static member JsonConvertInteger64(culture, json) = 
    json |> Option.bind (fun json -> json |> JsonConversions.AsInteger64 (CommonRuntime.GetCulture culture))

  static member JsonConvertDecimal(culture, json) =
    json |> Option.bind (fun json -> json |> JsonConversions.AsDecimal (CommonRuntime.GetCulture culture))

  static member JsonConvertFloat(culture, missingValues:string, json) = 
    json |> Option.bind (fun json -> json |> JsonConversions.AsFloat (missingValues.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)) 
                                                                     (*useNoneForMissingValues*)true
                                                                     (CommonRuntime.GetCulture culture))

  static member JsonConvertBoolean(culture, json) = 
    json |> Option.bind (fun json -> json |> JsonConversions.AsBoolean (CommonRuntime.GetCulture culture))

  static member JsonConvertDateTime(culture, json) = 
    json |> Option.bind (fun json -> json |> JsonConversions.AsDateTime (CommonRuntime.GetCulture culture))

  static member JsonConvertGuid(json) = 
    json |> Option.bind JsonConversions.AsGuid

  // --------------------------------------------------------------------------------------
  // type -> string

  static member ConvertStringBack(value) = defaultArg value ""

  static member ConvertIntegerBack(culture, value:int option) = 
    match value with
    | Some value -> value.ToString(CommonRuntime.GetCulture culture)
    | None -> ""
  
  static member ConvertInteger64Back(culture, value:int64 option) = 
    match value with
    | Some value -> value.ToString(CommonRuntime.GetCulture culture)
    | None -> ""
  
  static member ConvertDecimalBack(culture, value:decimal option) = 
    match value with
    | Some value -> value.ToString(CommonRuntime.GetCulture culture)
    | None -> ""
  
  static member ConvertFloatBack(culture, missingValues:string, value:float option) = 
    match value with
    | Some value ->
        if Double.IsNaN value then
          let missingValues = missingValues.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
          if missingValues.Length = 0 then (CommonRuntime.GetCulture culture).NumberFormat.NaNSymbol else missingValues.[0]
        else
          value.ToString(CommonRuntime.GetCulture culture)
    | None -> ""
  
  static member ConvertBooleanBack(culture, value:bool option, use0and1) = 
    match value with
    | Some value when use0and1 -> if value then "1" else "0"
    | Some value -> if value then "true" else "false"
    | None -> ""

  static member ConvertDateTimeBack(culture, value:DateTime option) = 
    match value with
    | Some value -> value.ToString(CommonRuntime.GetCulture culture)
    | None -> ""

  static member ConvertGuidBack(value:Guid option) = 
    match value with
    | Some value -> value.ToString()
    | None -> ""

  // --------------------------------------------------------------------------------------

  /// Operation that extracts the value from an option and reports a meaningful error message when the value is not there
  /// For missing strings we return "", and for missing doubles we return NaN
  /// For other types an error is thrown
  static member GetNonOptionalValue<'T>(name:string, opt:option<'T>, originalValue) : 'T = 
    match opt, originalValue with 
    | Some value, _ -> value
    | None, _ when typeof<'T> = typeof<string> -> "" |> unbox
    | None, _ when typeof<'T> = typeof<float> -> Double.NaN |> unbox
    | None, None -> failwithf "%s is missing" name
    | None, Some originalValue -> failwithf "Expecting %s in %s, got %s" (typeof<'T>.Name) name originalValue

  /// Operation that extracts the value from an option and reports a meaningful error message when the value is not there
  /// If the originalValue is a scalar, for missing strings we return "", and for missing doubles we return NaN
  /// For other types an error is thrown
  static member JsonGetNonOptionalValue<'T>(name:string, opt:option<'T>, originalValue) : 'T = 
    match opt, originalValue with 
    | Some value, _ -> value
    | None, Some (JsonValue.Array _) | None, Some (JsonValue.Object _) -> failwithf "Expecting %s in %s, got %A" (typeof<'T>.Name) name originalValue
    | None, _ when typeof<'T> = typeof<string> -> "" |> unbox
    | None, _ when typeof<'T> = typeof<float> -> Double.NaN |> unbox
    | None, None -> failwithf "%s is missing" name
    | None, Some originalValue-> failwithf "Expecting %s in %s, got %O" (typeof<'T>.Name) name originalValue

  static member GetOptionalValue value = Some value

  /// Turn an F# option type Option<'T> containing a primitive 
  /// value type into a .NET type Nullable<'T>
  static member OptionToNullable opt =
    match opt with 
    | Some v -> Nullable v
    | _ -> Nullable()

  /// Turn a .NET type Nullable<'T> to an F# option type Option<'T>
  static member NullableToOption (nullable:Nullable<_>) =
    if nullable.HasValue then Some nullable.Value else None
