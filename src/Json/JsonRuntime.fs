// --------------------------------------------------------------------------------------
// JSON type provider - methods that are called from the generated erased code
// --------------------------------------------------------------------------------------
namespace FSharp.Data.Runtime

open System
open System.ComponentModel
open System.Globalization
open System.IO
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Data.Runtime.StructuralTypes

#nowarn "10001"

/// [omit]
type IJsonDocument =
    abstract JsonValue : JsonValue
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
    abstract CreateNew : JsonValue -> IJsonDocument

/// [omit]
/// Underlying representation of the generated JSON types
type JsonDocument = 

  // NOTE: Using a record here to hide the ToString, GetHashCode & Equals
  // (but since this is used across multiple files, we have explicit Create method)
  { JsonValue : JsonValue }

  interface IJsonDocument with 
    member x.JsonValue = x.JsonValue
    member x.CreateNew json = JsonDocument.Create json

  /// Creates a JsonDocument representing the specified value
  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member Create(value:JsonValue) = 
    { JsonValue = value } :> IJsonDocument

  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member Create(reader:TextReader, culture) = 
    use reader = reader
    let text = reader.ReadToEnd()
    let culture = TextRuntime.GetCulture culture
    let value = JsonValue.Parse(text, culture)
    { JsonValue = value } :> IJsonDocument

  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member CreateList(reader:TextReader, culture) = 
    use reader = reader
    let text = reader.ReadToEnd()
    let culture = TextRuntime.GetCulture culture
    try
      JsonValue.Parse(text, culture).AsArray()
      |> Array.map JsonDocument.Create
    with _ ->
      text.Split('\n', '\r')
      |> Array.filter (not << String.IsNullOrWhiteSpace)
      |> Array.map (fun text -> JsonValue.Parse(text, culture) |> JsonDocument.Create)

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
  static member ConvertArray<'T>(doc:IJsonDocument, mapping:Func<IJsonDocument,'T>) = 
    doc.JsonValue.AsArray() |> Array.map (doc.CreateNew >> mapping.Invoke)

  /// Get json property and wrap in json document
  static member GetPropertyPacked(doc:IJsonDocument, name) =
    doc.CreateNew(doc.JsonValue.GetProperty(name))

  /// Get optional json property
  static member TryGetPropertyUnpacked(doc:IJsonDocument, name) =
    doc.JsonValue.TryGetProperty(name)
    |> Option.bind (function JsonValue.Null -> None | x -> Some x) 

  /// Get optional json property and convert to a specified type
  static member ConvertOptionalProperty<'T>(doc:IJsonDocument, name, mapping:Func<IJsonDocument,'T>) =
    JsonRuntime.TryGetPropertyUnpacked(doc, name)
    |> Option.map (doc.CreateNew >> mapping.Invoke)

  /// Returns all array values that match the specified tag
  /// (Follows the same pattern as ConvertXyz functions above)
  static member GetArrayChildrenByTypeTag<'T>(doc:IJsonDocument, culture, tag, mapping:Func<IJsonDocument,'T>) =     
    let matchesTag =
        match InferedTypeTag.ParseCode tag with
        | InferedTypeTag.Number -> 
            let culture = TextRuntime.GetCulture culture
            fun json -> (JsonConversions.AsDecimal culture json).IsSome ||
                        (JsonConversions.AsFloat [| |] (*useNoneForMissingValues*)true culture json).IsSome
        | InferedTypeTag.Boolean -> 
            let culture = TextRuntime.GetCulture culture
            JsonConversions.AsBoolean culture >> Option.isSome
        | InferedTypeTag.String -> 
            let culture = TextRuntime.GetCulture culture
            JsonConversions.AsString (*useNoneForNullOrEmpty*)true culture >> Option.isSome
        | InferedTypeTag.DateTime -> 
            let culture = TextRuntime.GetCulture culture
            JsonConversions.AsDateTime culture >> Option.isSome
        | InferedTypeTag.Guid -> 
            JsonConversions.AsGuid >> Option.isSome
        | InferedTypeTag.Collection -> 
            function JsonValue.Array _ -> true | _ -> false
        | InferedTypeTag.Record _ -> 
            function JsonValue.Object _ -> true | _ -> false
        | InferedTypeTag.Null -> 
            failwith "Null type not supported"
        | InferedTypeTag.Heterogeneous -> 
            failwith "Heterogeneous type not supported"
    match doc.JsonValue with
    | JsonValue.Array ar ->
        ar 
        |> Array.filter matchesTag 
        |> Array.map (doc.CreateNew >> mapping.Invoke)
    | _ -> failwith "JSON mismatch: Expected Array node"

  /// Returns single or no value from an array matching the specified tag
  static member TryGetArrayChildByTypeTag<'T>(doc, culture, tag, mapping:Func<IJsonDocument,'T>) = 
    match JsonRuntime.GetArrayChildrenByTypeTag(doc, culture, tag, mapping) with
    | [| the |] -> Some the
    | [| |] -> None
    | _ -> failwith "JSON mismatch: Expected Array with single or no elements."

  /// Returns a single array children that matches the specified tag
  static member GetArrayChildByTypeTag(doc, culture, tag) = 
    match JsonRuntime.GetArrayChildrenByTypeTag(doc, culture, tag, Func<_,_>(id)) with
    | [| the |] -> the
    | _ -> failwith "JSON mismatch: Expected single value, but found multiple."

  /// Returns a single or no value by tag type
  static member TryGetValueByTypeTag<'T>(doc:IJsonDocument, culture, tag, mapping:Func<IJsonDocument,'T>) = 
    // Build a fake array and reuse `GetArrayChildByTypeTag`
    let arrayValue = JsonValue.Array [| doc.JsonValue |] |> doc.CreateNew
    JsonRuntime.TryGetArrayChildByTypeTag(arrayValue, culture, tag, mapping)
