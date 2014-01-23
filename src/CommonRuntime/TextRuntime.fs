namespace FSharp.Data.Runtime

open System
open System.Globalization

/// [omit]
/// Static helper methods called from the generated code
type TextRuntime = 

  /// Returns CultureInfo matching the specified culture string
  /// (or InvariantCulture if the argument is null or empty)
  static member GetCulture(culture) =
    if String.IsNullOrEmpty culture then CultureInfo.InvariantCulture 
    else CultureInfo culture

  // --------------------------------------------------------------------------------------
  // string option -> type

  static member ConvertString(text:string option) = text

  static member ConvertInteger(culture, text) = 
    text |> Option.bind (fun text -> text |> TextConversions.AsInteger (TextRuntime.GetCulture culture))
  
  static member ConvertInteger64(culture, text) = 
    text |> Option.bind (fun text -> text |> TextConversions.AsInteger64 (TextRuntime.GetCulture culture))

  static member ConvertDecimal(culture, text) =
    text |> Option.bind (fun text -> text |> TextConversions.AsDecimal (TextRuntime.GetCulture culture))

  static member ConvertFloat(culture, missingValues:string, text) = 
    text |> Option.bind (fun text -> text |> TextConversions.AsFloat (missingValues.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)) 
                                                                     (*useNoneForMissingValues*)true
                                                                     (TextRuntime.GetCulture culture))

  static member ConvertBoolean(culture, text) = 
    text |> Option.bind (fun text -> text |> TextConversions.AsBoolean (TextRuntime.GetCulture culture))

  static member ConvertDateTime(culture, text) = 
    text |> Option.bind (fun text -> text |> TextConversions.AsDateTime (TextRuntime.GetCulture culture))

  static member ConvertGuid(text) = 
    text |> Option.bind TextConversions.AsGuid

  // --------------------------------------------------------------------------------------
  // type -> string

  static member ConvertStringBack(value) = defaultArg value ""

  static member ConvertIntegerBack(culture, value:int option) = 
    match value with
    | Some value -> value.ToString(TextRuntime.GetCulture culture)
    | None -> ""
  
  static member ConvertInteger64Back(culture, value:int64 option) = 
    match value with
    | Some value -> value.ToString(TextRuntime.GetCulture culture)
    | None -> ""
  
  static member ConvertDecimalBack(culture, value:decimal option) = 
    match value with
    | Some value -> value.ToString(TextRuntime.GetCulture culture)
    | None -> ""
  
  static member ConvertFloatBack(culture, missingValues:string, value:float option) = 
    match value with
    | Some value ->
        if Double.IsNaN value then
          let missingValues = missingValues.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
          if missingValues.Length = 0 then (TextRuntime.GetCulture culture).NumberFormat.NaNSymbol else missingValues.[0]
        else
          value.ToString(TextRuntime.GetCulture culture)
    | None -> ""
  
  //culture is ignored for now, but might not be in the future, so we're keeping in in the API
  static member ConvertBooleanBack(_culture, value:bool option, use0and1) =     
    match value with
    | Some value when use0and1 -> if value then "1" else "0"
    | Some value -> if value then "true" else "false"
    | None -> ""

  static member ConvertDateTimeBack(culture, value:DateTime option) = 
    match value with
    | Some value -> value.ToString(TextRuntime.GetCulture culture)
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

  /// Turn a sync operation into an async operation
  static member AsyncMap<'T, 'R>(valueAsync:Async<'T>, mapping:Func<'T, 'R>) = 
    async { let! value = valueAsync in return mapping.Invoke value }
