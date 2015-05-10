/// Extension methods that can be used to work with JsonValue in a less safe, but more convenient way.
/// This module also provides the dynamic operator.

namespace FSharp.Data

open System
open System.Globalization
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open FSharp.Data
open FSharp.Data.Runtime
open Microsoft.FSharp.Core

[<Extension>]
/// Extension methods with operations on JSON values
type JsonExtensions =

  /// Get a sequence of key-value pairs representing the properties of an object
  [<Extension>]
  static member Properties(x:JsonValue) =
    match x with
      | JsonValue.Record properties -> properties
      | _ -> [| |]

  /// Get property of a JSON object. Fails if the value is not an object
  /// or if the property is not present
  [<Extension>]
  static member GetProperty(x, propertyName) = 
    match x with
    | JsonValue.Record properties -> 
        match Array.tryFind (fst >> (=) propertyName) properties with 
        | Some (_, value) -> value
        | None -> failwithf "Didn't find property '%s' in %s" propertyName <| x.ToString(JsonSaveOptions.DisableFormatting)
    | _ -> failwithf "Not an object: %s" <| x.ToString(JsonSaveOptions.DisableFormatting)

  /// Try to get a property of a JSON value.
  /// Returns None if the value is not an object or if the property is not present.
  [<Extension>]
  static member TryGetProperty(x, propertyName) = 
    match x with
    | JsonValue.Record properties -> 
        Array.tryFind (fst >> (=) propertyName) properties |> Option.map snd
    | _ -> None

  /// Assuming the value is an object, get value with the specified name
  [<Extension>] 
  static member inline Item(x, propertyName) = JsonExtensions.GetProperty(x, propertyName)

  /// Get all the elements of a JSON value.
  /// Returns an empty array if the value is not a JSON array.
  [<Extension>]
  static member AsArray(x:JsonValue) = 
    match x with
    | (JsonValue.Array elements) -> elements
    | _ -> [| |]

  /// Get all the elements of a JSON value (assuming that the value is an array)
  [<Extension>] 
  static member inline GetEnumerator(x) = JsonExtensions.AsArray(x).GetEnumerator()

  /// Try to get the value at the specified index, if the value is a JSON array.
  [<Extension>] 
  static member inline Item(x, index) = JsonExtensions.AsArray(x).[index]

  /// Get the string value of an element (assuming that the value is a scalar)
  /// Returns the empty string for JsonValue.Null
  [<Extension>] 
  static member AsString(x, [<Optional>] ?cultureInfo) =
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    match JsonConversions.AsString (*useNoneForNullOrEmpty*)false cultureInfo x with
    | Some s -> s
    | _ -> failwithf "Not a string: %s" <| x.ToString(JsonSaveOptions.DisableFormatting)  

  /// Get a number as an integer (assuming that the value fits in integer)
  [<Extension>]
  static member AsInteger(x, [<Optional>] ?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    match JsonConversions.AsInteger cultureInfo x with
    | Some i -> i
    | _ -> failwithf "Not an int: %s" <| x.ToString(JsonSaveOptions.DisableFormatting)  

  /// Get a number as a 64-bit integer (assuming that the value fits in 64-bit integer)
  [<Extension>]
  static member AsInteger64(x, [<Optional>] ?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    match JsonConversions.AsInteger64 cultureInfo x with
    | Some i -> i
    | _ -> failwithf "Not an int64: %s" <| x.ToString(JsonSaveOptions.DisableFormatting)  

  /// Get a number as a decimal (assuming that the value fits in decimal)
  [<Extension>]
  static member AsDecimal(x, [<Optional>] ?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    match JsonConversions.AsDecimal cultureInfo x with
    | Some d -> d
    | _ -> failwithf "Not a decimal: %s" <| x.ToString(JsonSaveOptions.DisableFormatting)

  /// Get a number as a float (assuming that the value is convertible to a float)
  [<Extension>]
  static member AsFloat(x, [<Optional>] ?cultureInfo, [<Optional>] ?missingValues) = 
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    let missingValues = defaultArg missingValues TextConversions.DefaultMissingValues
    match JsonConversions.AsFloat missingValues (*useNoneForMissingValues*)false cultureInfo x with
    | Some f -> f
    | _ -> failwithf "Not a float: %s" <| x.ToString(JsonSaveOptions.DisableFormatting)

  /// Get the boolean value of an element (assuming that the value is a boolean)
  [<Extension>]
  static member AsBoolean(x) =
    match JsonConversions.AsBoolean x with
    | Some b -> b
    | _ -> failwithf "Not a boolean: %s" <| x.ToString(JsonSaveOptions.DisableFormatting)

  /// Get the datetime value of an element (assuming that the value is a string
  /// containing well-formed ISO date or MSFT JSON date)
  [<Extension>]
  static member AsDateTime(x, [<Optional>] ?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    match JsonConversions.AsDateTime cultureInfo x with
    | Some d -> d
    | _ -> failwithf "Not a datetime: %s" <| x.ToString(JsonSaveOptions.DisableFormatting)

  /// Get the guid value of an element (assuming that the value is a guid)
  [<Extension>]
  static member AsGuid(x) =
    match JsonConversions.AsGuid x with
    | Some g -> g
    | _ -> failwithf "Not a guid: %s" <| x.ToString(JsonSaveOptions.DisableFormatting)

  /// Get inner text of an element
  [<Extension>]
  static member InnerText(x) = 
    match JsonConversions.AsString (*useNoneForNullOrEmpty*)false CultureInfo.InvariantCulture x with
    | Some str -> str
    | None -> JsonExtensions.AsArray(x) |> Array.map (fun e -> JsonExtensions.InnerText(e)) |> String.Concat

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Provides the dynamic operator for getting a property of a JSON object
module JsonExtensions =

  /// Get a property of a JSON object  
  let (?) (jsonObject:JsonValue) propertyName = jsonObject.GetProperty(propertyName)

  type JsonValue with
    member x.Properties =
      match x with
      | JsonValue.Record properties -> properties
      | _ -> [| |]

// TODO: needs more consideration
#if ENABLE_JSONEXTENSIONS_OPTIONS

/// Extension methods that can be used to work with JsonValue in more convenient way.
/// This module also provides the dynamic operator.
module Options = 

  open System.Runtime.CompilerServices
  
  type JsonValue with
  
    /// Get a sequence of key-value pairs representing the properties of an object
    member x.Properties = 
      match x with
      | JsonValue.Record properties -> properties
      | _ -> [| |]
  
    /// Try to get a property of a JSON value.
    /// Returns None if the value is not an object or if the property is not present.
    member x.TryGetProperty(propertyName) = 
      match x with
      | JsonValue.Record properties -> 
          Array.tryFind (fst >> (=) propertyName) properties |> Option.map snd
      | _ -> None
  
    /// Try to get a property of a JSON value.
    /// Returns None if the value is not a JSON object or if the property is not present.
    member inline x.Item with get(propertyName) = x.TryGetProperty(propertyName)
  
    /// Get all the elements of a JSON value.
    /// Returns an empty array if the value is not a JSON array.
    member x.AsArray() = 
      match x with
      | JsonValue.Array elements -> elements
      | _ -> [| |]

    /// Get all the elements of a JSON value (assuming that the value is an array)
    member inline x.GetEnumerator() = x.AsArray().GetEnumerator()
  
    /// Try to get the value at the specified index, if the value is a JSON array.
    member inline x.Item with get(index) = x.AsArray().[index]
  
    /// Get the string value of an element (assuming that the value is a scalar)
    member x.AsString(?cultureInfo) =
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      JsonConversions.AsString (*useNoneForNullOrEmpty*)false cultureInfo x
  
    /// Get a number as an integer (assuming that the value fits in integer)
    member x.AsInteger(?cultureInfo) = 
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      JsonConversions.AsInteger cultureInfo x
  
    /// Get a number as a 64-bit integer (assuming that the value fits in 64-bit integer)
    member x.AsInteger64(?cultureInfo) = 
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      JsonConversions.AsInteger64 cultureInfo x
  
    /// Get a number as a decimal (assuming that the value fits in decimal)
    member x.AsDecimal(?cultureInfo) = 
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      JsonConversions.AsDecimal cultureInfo x
  
    /// Get a number as a float (assuming that the value is convertible to a float)
    member x.AsFloat(?cultureInfo, [<Optional>] ?missingValues) = 
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      let missingValues = defaultArg missingValues TextConversions.DefaultMissingValues
      JsonConversions.AsFloat missingValues (*useNoneForMissingValues*)true cultureInfo x
  
    /// Get the boolean value of an element (assuming that the value is a boolean)
    member x.AsBoolean(?cultureInfo) =
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      JsonConversions.AsBoolean cultureInfo x
  
    /// Get the datetime value of an element (assuming that the value is a string
    /// containing well-formed ISO date or MSFT JSON date)
    member x.AsDateTime(?cultureInfo) = 
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      JsonConversions.AsDateTime cultureInfo x
  
    /// Get the guid value of an element (assuming that the value is a guid)
    member x.AsGuid() =
      JsonConversions.AsGuid x
  
    /// Get inner text of an element
    member x.InnerText =     
      match x.AsString() with
      | Some str -> str
      | None -> x.AsArray() |> Array.map (fun e -> e.InnerText) |> String.Concat
  
  [<Extension>] 
  [<AbstractClass>]
  type JsonValueOptionExtensions() = 
  
    /// Get a sequence of key-value pairs representing the properties of an object
    [<Extension>] 
    static member Properties(x) = 
      match x with
      | Some (json:JsonValue) -> json.Properties
      | None -> [| |]
  
    /// Try to get a property of a JSON value.
    /// Returns None if the value is not an object or if the property is not present.
    [<Extension>] 
    static member TryGetProperty(x, propertyName) = 
      match x with
      | Some (JsonValue.Record properties) -> 
          Array.tryFind (fst >> (=) propertyName) properties |> Option.map snd
      | _ -> None
  
    /// Try to get a property of a JSON value.
    /// Returns None if the value is not a JSON object or if the property is not present.
    [<Extension>] 
    static member inline Item(x, propertyName) = JsonValueOptionExtensions.TryGetProperty(x, propertyName)
  
    /// Get all the elements of a JSON value.
    /// Returns an empty array if the value is not a JSON array.
    [<Extension>] 
    static member AsArray(x) = 
      match x with
      | Some (JsonValue.Array elements) -> elements
      | _ -> [| |]

    /// Get all the elements of a JSON value (assuming that the value is an array)
    [<Extension>] 
    static member inline GetEnumerator(x) = JsonValueOptionExtensions.AsArray(x).GetEnumerator()
  
    /// Try to get the value at the specified index, if the value is a JSON array.
    [<Extension>] 
    static member inline Item(x, index) = JsonValueOptionExtensions.AsArray(x).[index]
  
    /// Get the string value of an element (assuming that the value is a scalar)
    [<Extension>] 
    static member AsString(x, [<Optional>] ?cultureInfo) =
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      x |> Option.bind (JsonConversions.AsString (*useNoneForNullOrEmpty*)false cultureInfo)
  
    /// Get a number as an integer (assuming that the value fits in integer)
    [<Extension>] 
    static member AsInteger(x, [<Optional>] ?cultureInfo) = 
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      x |> Option.bind (JsonConversions.AsInteger cultureInfo)
  
    /// Get a number as a 64-bit integer (assuming that the value fits in 64-bit integer)
    [<Extension>] 
    static member AsInteger64(x, [<Optional>] ?cultureInfo) = 
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      x |> Option.bind (JsonConversions.AsInteger64 cultureInfo)
  
    /// Get a number as a decimal (assuming that the value fits in decimal)
    [<Extension>] 
    static member AsDecimal(x, [<Optional>] ?cultureInfo) = 
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      x |> Option.bind (JsonConversions.AsDecimal cultureInfo)
  
    /// Get a number as a float (assuming that the value is convertible to a float)
    [<Extension>] 
    static member AsFloat(x, [<Optional>] ?cultureInfo, [<Optional>] ?missingValues) = 
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      let missingValues = defaultArg missingValues TextConversions.DefaultMissingValues
      x |> Option.bind (JsonConversions.AsFloat missingValues (*useNoneForMissingValues*)true cultureInfo)
  
    /// Get the boolean value of an element (assuming that the value is a boolean)
    [<Extension>] 
    static member AsBoolean(x, [<Optional>] ?cultureInfo) =
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      x |> Option.bind (JsonConversions.AsBoolean cultureInfo)
  
    /// Get the datetime value of an element (assuming that the value is a string
    /// containing well-formed ISO date or MSFT JSON date)
    [<Extension>] 
    static member AsDateTime(x, [<Optional>] ?cultureInfo) = 
      let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
      x |> Option.bind (JsonConversions.AsDateTime cultureInfo)
  
    /// Get the guid value of an element (assuming that the value is a guid)
    [<Extension>] 
    static member AsGuid(x) =
      x |> Option.bind JsonConversions.AsGuid
  
    /// Get inner text of an element
    [<Extension>] 
    static member InnerText(x) =
      match JsonValueOptionExtensions.AsString(x) with
      | Some str -> str
      | None -> JsonValueOptionExtensions.AsArray(x) |> Array.map (fun e -> e.InnerText) |> String.Concat
  
  /// [omit]
  type JsonValueOverloads = JsonValueOverloads with
      static member inline ($) (x:JsonValue                 , JsonValueOverloads) = fun propertyName -> x.TryGetProperty propertyName
      static member inline ($) (x:JsonValue option          , JsonValueOverloads) = fun propertyName -> x |> Option.bind (fun x -> x.TryGetProperty propertyName)
  
  /// Get property of a JSON value (assuming that the value is an object)
  let inline (?) x (propertyName:string) = (x $ JsonValueOverloads) propertyName

#endif
