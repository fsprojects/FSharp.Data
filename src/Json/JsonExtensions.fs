/// Unsafe extension methods that can be used to work with JsonValue in a less safe, but shorter way.
/// This module also provides the dynamic operator.
module FSharp.Data.JsonExtensions

open System
open System.Globalization
open FSharp.Data.Runtime

type JsonValue with

  /// Get all elements of a JSON object (assuming that the value is an array)
  member x.AsArray() = 
    match x with
    | JsonValue.Array elements -> elements
    | JsonValue.Null -> [| |]
    | _ -> failwithf "Not an array - %A" x

  /// Assuming the value is an array, get the value at a specified index
  member x.Item with get(index) = x.AsArray().[index]

  /// Assuming the value is an object, get value with the specified name
  member x.Item with get(propertyName) = x.GetProperty(propertyName)

  /// Get all elements of a JSON object (assuming that the value is an array)
  member x.GetEnumerator() = x.AsArray().GetEnumerator()

  /// Get the string value of an element (assuming that the value is a scalar)
  /// Returns the empty string for JsonValue.Null
  member x.AsString(?cultureInfo ) =
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    match JsonConversions.AsString (*useNoneForNullOrEmpty*)false cultureInfo x with
    | Some s -> s
    | _ -> failwithf "Not a string - %A" x  

  /// Get a number as an integer (assuming that the value fits in integer)
  member x.AsInteger(?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    match JsonConversions.AsInteger cultureInfo x with
    | Some i -> i
    | _ -> failwithf "Not an int - %A" x  

  /// Get a number as a 64-bit integer (assuming that the value fits in 64-bit integer)
  member x.AsInteger64(?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    match JsonConversions.AsInteger64 cultureInfo x with
    | Some i -> i
    | _ -> failwithf "Not an int64 - %A" x  

  /// Get a number as a decimal (assuming that the value fits in decimal)
  member x.AsDecimal(?cultureInfo ) = 
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    match JsonConversions.AsDecimal cultureInfo x with
    | Some d -> d
    | _ -> failwithf "Not a decimal - %A" x

  /// Get a number as a float (assuming that the value is convertible to a float)
  member x.AsFloat(?cultureInfo, ?missingValues) = 
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    let missingValues = defaultArg missingValues TextConversions.DefaultMissingValues
    match JsonConversions.AsFloat missingValues (*useNoneForMissingValues*)false cultureInfo x with
    | Some f -> f
    | _ -> failwithf "Not a float - %A" x

  /// Get the boolean value of an element (assuming that the value is a boolean)
  member x.AsBoolean(?cultureInfo) =
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    match JsonConversions.AsBoolean cultureInfo x with
    | Some b -> b
    | _ -> failwithf "Not a boolean - %A" x

  /// Get the datetime value of an element (assuming that the value is a string
  /// containing well-formed ISO date or MSFT JSON date)
  member x.AsDateTime(?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo  CultureInfo.InvariantCulture
    match JsonConversions.AsDateTime cultureInfo x with
    | Some d -> d
    | _ -> failwithf "Not a datetime - %A" x

  /// Get the guid value of an element (assuming that the value is a guid)
  member x.AsGuid() =
    match JsonConversions.AsGuid x with
    | Some g -> g
    | _ -> failwithf "Not a guid - %A" x

  /// Get inner text of an element - this includes just string nodes and
  /// string nodes in an array (e.g. multi-line string represented as array)
  /// (assuming that the value is a string or array of strings)
  member x.InnerText = 
    match x with
    | JsonValue.String t -> t
    | JsonValue.Array a -> a |> Seq.map (fun e -> e.InnerText) |> String.concat ""
    | _ -> failwithf "Contains non-text element - %A" x

  /// Get a sequence of key-value pairs representing the properties of an object
  /// (assuming that the value is an object)
  member x.Properties = 
    match x with
    | JsonValue.Object map -> Map.toSeq map |> Seq.sortBy fst
    | _ -> failwithf "Not an object - %A" x

  /// Try get property of a JSON object. Returns None if the value is not
  /// an object or if the property is not present.
  member x.TryGetProperty(propertyName) = 
    match x with
    | JsonValue.Object properties -> 
        Map.tryFind propertyName properties 
    | _ -> None

  /// Get property of a JSON object. Fails if the value is not an object
  /// or if the property is not present
  member x.GetProperty(propertyName) = 
    match x with
    | JsonValue.Object properties -> 
        match Map.tryFind propertyName properties with 
        | Some res -> res
        | None -> failwithf "Didn't find property '%s' in %A" propertyName x
    | _ -> failwithf "Not an object - %A" x

/// Get property of a JSON object (assuming that the value is an object)
let (?) (jsonObject:JsonValue) propertyName = jsonObject.GetProperty(propertyName)
