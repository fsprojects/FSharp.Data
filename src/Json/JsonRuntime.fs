// --------------------------------------------------------------------------------------
// JSON type provider - methods that are called from the generated erased code
// --------------------------------------------------------------------------------------
namespace FSharp.Data.Runtime

open System
open System.ComponentModel
open System.Globalization
open System.IO
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open FSharp.Data.Runtime.StructuralTypes

#nowarn "10001"

/// [omit]
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
    let culture = TextRuntime.GetCulture culture
    let value = JsonValue.Parse(text, culture)
    { JsonValue = value }

  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member AsyncCreate(readerAsync:Async<TextReader>, culture) = async {
    use! reader = readerAsync
    let text = reader.ReadToEnd()
    let culture = TextRuntime.GetCulture culture
    let value = JsonValue.Parse(text, culture)
    return { JsonValue = value }
  }

  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member CreateList(reader:TextReader, culture) = 
    use reader = reader
    let text = reader.ReadToEnd()
    let culture = TextRuntime.GetCulture culture
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
    let culture = TextRuntime.GetCulture culture
    return
      try
        JsonValue.Load(reader, culture).AsArray()
        |> Array.map (fun value -> { JsonValue = value })
      with _ ->
        reader.ReadToEnd().Split('\n', '\r')
        |> Array.filter (not << String.IsNullOrWhiteSpace)
        |> Array.map (fun text -> { JsonValue = JsonValue.Parse(text, culture) })
  }

/// [omit]
/// Static helper methods called from the generated code
type JsonRuntime = 

  // --------------------------------------------------------------------------------------
  // json option -> type

  static member ConvertString(culture, json) = 
    json |> Option.bind (fun json -> json |> JsonConversions.AsString (*useNoneForNullOrEmpty*)true (TextRuntime.GetCulture culture))
  
  static member ConvertInteger(culture, json) = 
    json |> Option.bind (fun json -> json |> JsonConversions.AsInteger (TextRuntime.GetCulture culture))
  
  static member ConvertInteger64(culture, json) = 
    json |> Option.bind (fun json -> json |> JsonConversions.AsInteger64 (TextRuntime.GetCulture culture))

  static member ConvertDecimal(culture, json) =
    json |> Option.bind (fun json -> json |> JsonConversions.AsDecimal (TextRuntime.GetCulture culture))

  static member ConvertFloat(culture, missingValues:string, json) = 
    json |> Option.bind (fun json -> json |> JsonConversions.AsFloat (missingValues.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)) 
                                                                     (*useNoneForMissingValues*)true
                                                                     (TextRuntime.GetCulture culture))

  static member ConvertBoolean(culture, json) = 
    json |> Option.bind (fun json -> json |> JsonConversions.AsBoolean (TextRuntime.GetCulture culture))

  static member ConvertDateTime(culture, json) = 
    json |> Option.bind (fun json -> json |> JsonConversions.AsDateTime (TextRuntime.GetCulture culture))

  static member ConvertGuid(json) = 
    json |> Option.bind JsonConversions.AsGuid

  /// Operation that extracts the value from an option and reports a meaningful error message when the value is not there
  /// If the originalValue is a scalar, for missing strings we return "", and for missing doubles we return NaN
  /// For other types an error is thrown
  static member GetNonOptionalValue<'T>(name:string, opt:option<'T>, originalValue) : 'T = 
    match opt, originalValue with 
    | Some value, _ -> value
    | None, Some (JsonValue.Array _) | None, Some (JsonValue.Object _) -> failwithf "Expecting %s in %s, got %A" (typeof<'T>.Name) name originalValue
    | None, _ when typeof<'T> = typeof<string> -> "" |> unbox
    | None, _ when typeof<'T> = typeof<float> -> Double.NaN |> unbox
    | None, None -> failwithf "%s is missing" name
    | None, Some originalValue-> failwithf "Expecting %s in %s, got %O" (typeof<'T>.Name) name originalValue

  /// Converts JSON array to array of target types
  /// The `packer` function rebuilds representation type (such as
  /// `JsonDocument`) which is then passed to projection function `f`.
  /// The `unpacker` function does the opposite
  static member ConvertArray<'Representation, 'T>(doc:'Representation,
                                                  unpacker:Func<'Representation,JsonValue>, 
                                                  packer:Func<JsonValue,'Representation>, 
                                                  mapper:Func<'Representation,'T>) = 
    unpacker.Invoke(doc).AsArray() |> Array.map (packer.Invoke >> mapper.Invoke)

  /// Converts JSON array to array of target types, asynchronously
  /// The `packer` function rebuilds representation type (such as
  /// `JsonDocument`) which is then passed to projection function `f`.
  /// The `unpacker` function does the opposite
  static member AsyncConvertArray<'Representation, 'T>(docAsync:Async<'Representation>, 
                                                       unpacker:Func<'Representation,JsonValue>, 
                                                       packer:Func<JsonValue,'Representation>, 
                                                       mapper:Func<'Representation,'T>) = async {
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
  static member GetArrayChildrenByTypeTag<'Representation,'T>(json:JsonValue, tag:string, packer:Func<JsonValue,'Representation>, mapper:Func<'Representation,'T>) = 
    let tag = InferedTypeTag.ParseCode tag
    let matchesTag = function
      | JsonValue.Null -> false
      | JsonValue.Boolean _ -> tag = InferedTypeTag.Boolean
      | JsonValue.Number _ -> tag = InferedTypeTag.Number
      | JsonValue.Float _ -> tag = InferedTypeTag.Number
      | JsonValue.Array _ -> tag = InferedTypeTag.Collection
      | JsonValue.Object _ -> tag = InferedTypeTag.Record None
      | JsonValue.String _ -> tag = InferedTypeTag.String
    match json with
    | JsonValue.Array ar ->
        ar 
        |> Array.filter matchesTag 
        |> Array.map (packer.Invoke >> mapper.Invoke)
    | _ -> failwith "JSON mismatch: Expected Array node"

  /// Returns single or no value from an array matching the specified tag
  static member TryGetArrayChildByTypeTag<'Representation,'T>(json:JsonValue, tag:string, packer:Func<JsonValue,'Representation>, mapper:Func<'Representation,'T>) = 
    match JsonRuntime.GetArrayChildrenByTypeTag(json, tag, packer, mapper) with
    | [| the |] -> Some the
    | [| |] -> None
    | _ -> failwith "JSON mismatch: Expected Array with single or no elements."

  /// Returns a single array children that matches the specified tag
  static member GetArrayChildByTypeTag(json:JsonValue, tag:string) = 
    match JsonRuntime.GetArrayChildrenByTypeTag(json, tag, Func<_,_>(id), Func<_,_>(id)) with
    | [| the |] -> the
    | _ -> failwith "JSON mismatch: Expected single value, but found multiple."

  /// Returns a single or no value by tag type
  static member TryGetValueByTypeTag<'Representation,'T>(json:JsonValue, tag:string, packer:Func<JsonValue,'Representation>, mapper:Func<'Representation,'T>) = 
    // Build a fake array and reuse `GetArrayChildByTypeTag`
    let arrayValue = JsonValue.Array [| json |]
    JsonRuntime.TryGetArrayChildByTypeTag(arrayValue, tag, packer, mapper) 
