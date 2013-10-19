// --------------------------------------------------------------------------------------
// JSON type provider - methods that are called from the generated erased code
// --------------------------------------------------------------------------------------
namespace FSharp.Data.RuntimeImplementation

open System
open System.ComponentModel
open System.Globalization
open System.IO
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open FSharp.Data.RuntimeImplementation.StructuralTypes

/// Underlying representation of the generated JSON types
type JsonDocument = 

  // NOTE: Using a record here to hide the ToString, GetHashCode & Equals
  // (but since this is used across multiple files, we have explicit Create method)
  { JsonValue : JsonValue }

  /// Creates a JsonDocument representing the specified value
  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member Create(value:JsonValue) = 
    { JsonValue = value }

  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member Create(reader:TextReader, culture) = 
    use reader = reader
    let text = reader.ReadToEnd()
    let culture = Operations.GetCulture culture
    let value = JsonValue.Parse(text, culture)
    { JsonValue = value }

  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member AsyncCreate(readerAsync:Async<TextReader>, culture) = async {
    use! reader = readerAsync
    let text = reader.ReadToEnd()
    let culture = Operations.GetCulture culture
    let value = JsonValue.Parse(text, culture)
    return { JsonValue = value }
  }

  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member CreateList(reader:TextReader, culture) = 
    use reader = reader
    let text = reader.ReadToEnd()
    let culture = Operations.GetCulture culture
    try
      JsonValue.Parse(text, culture).AsArray()
      |> Array.map (fun value -> { JsonValue = value })
    with _ ->
      text.Split('\n', '\r')
      |> Array.filter (not << String.IsNullOrWhiteSpace)
      |> Array.map (fun text -> { JsonValue = JsonValue.Parse(text, culture) })

  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member AsyncCreateList(readerAsync:Async<TextReader>, culture) = async {
    use! reader = readerAsync
    let culture = Operations.GetCulture culture
    return
      try
        JsonValue.Load(reader, culture).AsArray()
        |> Array.map (fun value -> { JsonValue = value })
      with _ ->
        reader.ReadToEnd().Split('\n', '\r')
        |> Array.filter (not << String.IsNullOrWhiteSpace)
        |> Array.map (fun text -> { JsonValue = JsonValue.Parse(text, culture) })
  }

/// Static helper methods called from the generated code
type JsonOperations = 
  // Trivial operations that return primitive values
  static member GetString(value:JsonValue) = value.AsString()
  static member GetDateTime(value:JsonValue, culture) = value.AsDateTime(Operations.GetCulture(culture))
  static member GetGuid(value:JsonValue) = value.AsGuid()
  static member GetBoolean(value:JsonValue) = value.AsBoolean()
  static member GetFloat(value:JsonValue, culture) = value.AsFloat(Operations.GetCulture(culture))
  static member GetDecimal(value:JsonValue, culture) = value.AsDecimal(Operations.GetCulture(culture))
  static member GetInteger(value:JsonValue, culture) = value.AsInteger(Operations.GetCulture(culture))
  static member GetInteger64(value:JsonValue, culture) = value.AsInteger64(Operations.GetCulture(culture))
  static member GetProperty(value:JsonValue, name) = value.GetProperty(name)

  /// Converts JSON array to array of target types
  /// The `packer` function rebuilds representation type (such as
  /// `JsonDocument`) which is then passed to projection function `f`.
  /// The `unpacker` function does the opposite
  static member ConvertArray<'RepresentationT, 'TRes>(doc:'RepresentationT,
                                                      unpacker:Func<'RepresentationT,JsonValue>, 
                                                      packer:Func<JsonValue,'RepresentationT>, 
                                                      mapper:Func<'RepresentationT,'TRes>) = 
    unpacker.Invoke(doc).AsArray() |> Array.map (packer.Invoke >> mapper.Invoke)

  /// Converts JSON array to array of target types, asynchronously
  /// The `packer` function rebuilds representation type (such as
  /// `JsonDocument`) which is then passed to projection function `f`.
  /// The `unpacker` function does the opposite
  static member AsyncConvertArray<'RepresentationT, 'TRes>(docAsync:Async<'RepresentationT>, 
                                                           unpacker:Func<'RepresentationT,JsonValue>, 
                                                           packer:Func<JsonValue,'RepresentationT>, 
                                                           mapper:Func<'RepresentationT,'TRes>) = async {
    let! doc = docAsync
    return unpacker.Invoke(doc).AsArray() |> Array.map (packer.Invoke >> mapper.Invoke)
  }

  /// Get optional property of a specified type
  static member ConvertOptionalProperty(value:JsonValue, name, packer:Func<_,_>, f:Func<_,_>) =     
    value.TryGetProperty(name) 
    |> Option.bind (function JsonValue.Null -> None | x -> Some x) 
    |> Option.map (packer.Invoke >> f.Invoke)

  /// Returns all array values that match the specified tag
  /// (Follows the same pattern as ConvertXyz functions above)
  static member GetArrayChildrenByTypeTag(doc:JsonValue, tag, pack:Func<_,_>, f:Func<_,_>) = 
    let tag = InferedTypeTag.ParseCode tag
    let matchesTag = function
      | JsonValue.Null -> false
      | JsonValue.Boolean _ -> tag = InferedTypeTag.Boolean
      | JsonValue.Number _ -> tag = InferedTypeTag.Number
      | JsonValue.Float _ -> tag = InferedTypeTag.Number
      | JsonValue.Array _ -> tag = InferedTypeTag.Collection
      | JsonValue.Object _ -> tag = InferedTypeTag.Record None
      | JsonValue.String _ -> tag = InferedTypeTag.String
    match doc with
    | JsonValue.Array ar ->
        ar 
        |> Array.filter matchesTag 
        |> Array.map (pack.Invoke >> f.Invoke)
    | _ -> failwith "JSON mismatch: Expected Array node"

  /// Returns single or no value from an array matching the specified tag
  static member TryGetArrayChildByTypeTag(doc:JsonValue, tag, pack:Func<_,_>, f:Func<_,_>) = 
    match JsonOperations.GetArrayChildrenByTypeTag(doc, tag, pack, f) with
    | [| the |] -> Some the
    | [| |] -> None
    | _ -> failwith "JSON mismatch: Expected Array with single or no elements."

  /// Returns a single array children that matches the specified tag
  static member GetArrayChildByTypeTag(value:JsonValue, tag) = 
    match JsonOperations.GetArrayChildrenByTypeTag(value, tag, new Func<_,_>(id), new Func<_,_>(id)) with
    | [| the |] -> the
    | _ -> failwith "JSON mismatch: Expected single value, but found multiple."

  /// Returns a single or no value by tag type
  static member TryGetValueByTypeTag(value:JsonValue, tag, pack, f) = 
    // Build a fake array and reuse `GetArrayChildByTypeTag`
    let arrayValue = JsonValue.Array [| value |]
    JsonOperations.TryGetArrayChildByTypeTag(arrayValue, tag, pack, f) 
