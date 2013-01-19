namespace FSharp.Data.Json

open FSharp.Data.Json.JsonReader
open FSharp.Data.StructureInference

/// Underlying representation of the generated JSON types
type JsonDocument private (json:JsonValue) =
  /// Returns the raw JSON value that is represented by the generated type
  member x.JsonValue = json
  static member Create(json:JsonValue) =
    JsonDocument(json)

type JsonOperations = 
  // Trivial operations that return primitive values
  static member GetString(value:JsonValue) = value.AsString
  static member GetBoolean(value:JsonValue) = value.AsBoolean
  static member GetFloat(value:JsonValue) = value.AsFloat
  static member GetDecimal(value:JsonValue) = value.AsDecimal
  static member GetInteger(value:JsonValue) = value.AsInteger
  static member GetInteger64(value:JsonValue) = value.AsInteger64
  static member GetProperty(doc:JsonValue, name) = (?) doc name

  /// Converts JSON array to array of target types
  /// The `packer` function rebuilds representation type (such as
  /// `JsonDocument`) which is then passed to projection function `f`.
  static member ConvertArray<'P, 'R>
      (value:JsonValue, packer:JsonValue -> 'P, f:'P -> 'R) : 'R[] = 
    [| for v in value -> f (packer (v)) |]

  /// Get optional property of a specified type
  static member ConvertOptionalProperty<'P, 'R>
      (doc:JsonValue, name, packer:JsonValue -> 'P, f:'P -> 'R) : 'R option = 
    match doc with 
    | JsonValue.Object o -> 
        match o.TryFind name with
        | None | Some JsonValue.Null -> None
        | Some it -> Some (f (packer it))
    | _ -> None

  /// Returns all array values that match the specified tag
  /// (Follows the same pattern as ConvertXyz functions above)
  static member GetArrayChildrenByTypeTag(doc:JsonValue, tag, pack, f) = 
    let tag = InferedTypeTag.ParseCode tag
    let matchesTag = function
      | JsonValue.Null -> false
      | JsonValue.Boolean _ -> tag = InferedTypeTag.Boolean
      | JsonValue.Number _ -> tag = InferedTypeTag.Number
      | JsonValue.BigNumber _ -> tag = InferedTypeTag.Number
      | JsonValue.Array _ -> tag = InferedTypeTag.Collection
      | JsonValue.Object _ -> tag = InferedTypeTag.Record None
      | JsonValue.String _ -> tag = InferedTypeTag.String
    match doc with
    | JsonValue.Array ar ->
        ar 
        |> List.filter matchesTag 
        |> Array.ofList
        |> Array.map (pack >> f)
    | _ -> failwith "JSON mismatch: Expected Array node"

  /// Returns single or no value from an array matching the specified tag
  static member TryGetArrayChildByTypeTag(doc:JsonValue, tag, pack, f) = 
    match JsonOperations.GetArrayChildrenByTypeTag(doc, tag, pack, f) with
    | [| the |] -> Some the
    | [| |] -> None
    | _ -> failwith "JSON mismatch: Expected Array with single or no elements."

  /// Returns a single array children that matches the specified tag
  static member GetArrayChildByTypeTag(value:JsonValue, tag) = 
    match JsonOperations.GetArrayChildrenByTypeTag(value, tag, id, id) with
    | [| the |] -> the
    | _ -> failwith "JSON mismatch: Expected single value, but found multiple."

  /// Returns a single or no value by tag type
  static member TryGetValueByTypeTag(value:JsonValue, tag, pack, f) = 
    // Build a fake array and reuse `GetArrayChildByTypeTag`
    let arrayValue = JsonValue.Array [value]
    JsonOperations.TryGetArrayChildByTypeTag(arrayValue, tag, pack, f) 
