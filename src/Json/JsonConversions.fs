// --------------------------------------------------------------------------------------
// Helper operations for converting converting json values to other types
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Runtime

open System
open FSharp.Data
open FSharp.Data.Runtime

/// Conversions from JsonValue to string/int/int64/decimal/float/boolean/datetime/guid options
type JsonConversions =

  static member AsString useNoneForNullOrEmpty (cultureInfo:IFormatProvider) = function
    | JsonValue.String s -> if useNoneForNullOrEmpty && String.IsNullOrEmpty s then None else Some s
    | JsonValue.Boolean b -> Some <| if b then "true" else "false"
    | JsonValue.Number n -> Some <| n.ToString cultureInfo
    | JsonValue.Float f -> Some <| f.ToString cultureInfo
    | JsonValue.Null when not useNoneForNullOrEmpty -> Some ""
    | _ -> None

  static member AsInteger cultureInfo = function
    | JsonValue.Number n -> Some <| int n
    | JsonValue.Float n -> Some <| int n
    | JsonValue.String s -> TextConversions.AsInteger cultureInfo s
    | _ -> None

  static member AsInteger64 cultureInfo = function
    | JsonValue.Number n -> Some <| int64 n
    | JsonValue.Float n -> Some <| int64 n
    | JsonValue.String s -> TextConversions.AsInteger64 cultureInfo s
    | _ -> None

  static member AsDecimal cultureInfo = function
    | JsonValue.Number n -> Some n
    | JsonValue.Float n -> Some <| decimal n
    | JsonValue.String s -> TextConversions.AsDecimal cultureInfo s
    | _ -> None

  static member AsFloat missingValues useNoneForMissingValues cultureInfo = function
    | JsonValue.Float n -> Some n
    | JsonValue.Number n -> Some <| float n
    | JsonValue.String s -> TextConversions.AsFloat missingValues useNoneForMissingValues cultureInfo s
    | _ -> None

  static member AsBoolean = function
    | JsonValue.Boolean b -> Some b
    | JsonValue.Number 1M -> Some true
    | JsonValue.Number 0M -> Some false
    | JsonValue.String s -> TextConversions.AsBoolean s
    | _ -> None

  static member AsDateTime cultureInfo = function
    | JsonValue.String s -> TextConversions.AsDateTime cultureInfo s
    | _ -> None

  static member AsGuid = function
    | JsonValue.String s -> TextConversions.AsGuid s
    | _ -> None
