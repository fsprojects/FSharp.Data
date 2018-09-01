// --------------------------------------------------------------------------------------
// Helper operations for converting converting json values to other types
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Runtime

open System
open FSharp.Data

[<AutoOpen>]
module private Helpers =
  let inline inRange lo hi v = (v >= decimal lo) && (v <= decimal hi)
  let inline isInteger v = Math.Round(v:decimal) = v

/// Conversions from JsonValue to string/int/int64/decimal/float/boolean/datetime/datetimeoffset/guid options
type JsonConversions =

  static member AsString useNoneForNullOrEmpty (cultureInfo:IFormatProvider) = function
    | JsonValue.String s -> if useNoneForNullOrEmpty && String.IsNullOrEmpty s then None else Some s
    | JsonValue.Boolean b -> Some (if b then "true" else "false")
    | JsonValue.Number n -> n.ToString(cultureInfo) |> Some
    | JsonValue.Float f -> f.ToString(cultureInfo) |> Some
    | JsonValue.Null when not useNoneForNullOrEmpty -> Some ""
    | _ -> None

  static member AsInteger cultureInfo = function
    | JsonValue.Number n when inRange Int32.MinValue Int32.MaxValue n && isInteger n -> Some (int n)
    | JsonValue.String s -> TextConversions.AsInteger cultureInfo s
    | _ -> None

  static member AsInteger64 cultureInfo = function
    | JsonValue.Number n when inRange Int64.MinValue Int64.MaxValue n && isInteger n -> Some (int64 n)
    | JsonValue.String s -> TextConversions.AsInteger64 cultureInfo s
    | _ -> None

  static member AsDecimal cultureInfo = function
    | JsonValue.Number n -> Some n
    | JsonValue.String s -> TextConversions.AsDecimal cultureInfo s
    | _ -> None

  static member AsFloat missingValues useNoneForMissingValues cultureInfo = function
    | JsonValue.Number n -> Some (float n)
    | JsonValue.Float n -> Some n
    | JsonValue.String s -> TextConversions.AsFloat missingValues useNoneForMissingValues cultureInfo s
    | _ -> None

  static member AsBoolean = function
    | JsonValue.Boolean b -> Some b
    | JsonValue.Number 1M -> Some true
    | JsonValue.Number 0M -> Some false
    | JsonValue.String s -> TextConversions.AsBoolean s
    | _ -> None

  static member AsDateTimeOffset cultureInfo = function
    | JsonValue.String s -> TextConversions.AsDateTimeOffset cultureInfo s
    | _ -> None
  
  static member AsDateTime cultureInfo = function
    | JsonValue.String s -> TextConversions.AsDateTime cultureInfo s
    | _ -> None

  static member AsGuid = function
    | JsonValue.String s -> TextConversions.AsGuid s
    | _ -> None
