// --------------------------------------------------------------------------------------
// Helper operations for converting converting json values to other types
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Runtime

open System
open FSharp.Data.Json

/// Conversions from JsonValue to string/int/int64/decimal/float/boolean/datetime/guid options
type JsonConversions =

  static member AsString useNoneForNullOrEmpty (culture:IFormatProvider) = function
    | JsonValue.String s -> if useNoneForNullOrEmpty then TextConversions.AsString s else Some s
    | JsonValue.Boolean b -> Some <| if b then "true" else "false"
    | JsonValue.Number n -> Some <| n.ToString culture
    | JsonValue.Float f -> Some <| f.ToString culture
    | JsonValue.Null when not useNoneForNullOrEmpty -> Some ""
    | _ -> None

  static member AsInteger culture = function
    | JsonValue.Number n -> Some <| int n
    | JsonValue.Float n -> Some <| int n
    | JsonValue.String s -> TextConversions.AsInteger culture s
    | _ -> None

  static member AsInteger64 culture = function
    | JsonValue.Number n -> Some <| int64 n
    | JsonValue.Float n -> Some <| int64 n
    | JsonValue.String s -> TextConversions.AsInteger64 culture s
    | _ -> None

  static member AsDecimal culture = function
    | JsonValue.Number n -> Some n
    | JsonValue.Float n -> Some <| decimal n
    | JsonValue.String s -> TextConversions.AsDecimal culture s
    | _ -> None

  static member AsFloat missingValues useNoneForMissingValues culture = function
    | JsonValue.Float n -> Some n
    | JsonValue.Number n -> Some <| float n
    | JsonValue.String s -> TextConversions.AsFloat missingValues useNoneForMissingValues culture s
    | _ -> None

  static member AsBoolean culture = function
    | JsonValue.Boolean b -> Some b
    | JsonValue.Number 1M -> Some true
    | JsonValue.Number 0M -> Some false
    | JsonValue.String s -> TextConversions.AsBoolean culture s
    | _ -> None

  static member AsDateTime culture = function
    | JsonValue.String s -> TextConversions.AsDateTime culture s
    | _ -> None

  static member AsGuid = function
    | JsonValue.String s -> TextConversions.AsGuid s
    | _ -> None
