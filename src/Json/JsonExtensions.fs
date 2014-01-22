/// Unsafe extension methods that can be used to work with JsonValue in a less safe, but shorter way.
/// This module also provides the dynamic operator.
module FSharp.Data.Json.Extensions

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
  member x.AsString(?culture) =
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match JsonConversions.AsString (*useNoneForNullOrEmpty*)false culture x with
    | Some s -> s
    | _ -> failwithf "Not a string - %A" x

  /// Sets the property with the given name to the given string value
  member x.Set(propertyName, value:string) =
    match x with
    | JsonValue.Object properties ->
        match Map.tryFind propertyName properties with 
        | Some (JsonValue.String x) -> 
               JsonValue.Object(properties |> Map.remove propertyName |> Map.add propertyName (JsonValue.String value))
        | _ -> failwithf "Didn't find string property '%s' in %A" propertyName x
    | _ -> failwithf "Not an object - %A" x

  /// Get a number as an integer (assuming that the value fits in integer)
  member x.AsInteger(?culture) = 
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match JsonConversions.AsInteger culture x with
    | Some i -> i
    | _ -> failwithf "Not an int - %A" x

  /// Sets the property with the given name to the given integer value
  member x.Set(propertyName, value:int) = x.Set(propertyName,decimal value)

  /// Get a number as a 64-bit integer (assuming that the value fits in 64-bit integer)
  member x.AsInteger64(?culture) = 
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match JsonConversions.AsInteger64 culture x with
    | Some i -> i
    | _ -> failwithf "Not an int64 - %A" x

  /// Sets the property with the given name to the given int64 value
  member x.Set(propertyName, value:int64) = x.Set(propertyName,decimal value)

  /// Get a number as a decimal (assuming that the value fits in decimal)
  member x.AsDecimal(?culture) = 
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match JsonConversions.AsDecimal culture x with
    | Some d -> d
    | _ -> failwithf "Not a decimal - %A" x

  /// Sets the property with the given name to the given decimal value
  member x.Set(propertyName, value:decimal) =
    match x with
    | JsonValue.Object properties ->
        match Map.tryFind propertyName properties with 
        | Some (JsonValue.Number x) -> 
             JsonValue.Object(properties |> Map.remove propertyName |> Map.add propertyName (JsonValue.Number value))
        | _ -> failwithf "Didn't find number property '%s' in %A" propertyName x
    | _ -> failwithf "Not an object - %A" x

  /// Get a number as a float (assuming that the value is convertible to a float)
  member x.AsFloat(?culture, ?missingValues) = 
    let culture = defaultArg culture CultureInfo.InvariantCulture
    let missingValues = defaultArg missingValues TextConversions.DefaultMissingValues
    match JsonConversions.AsFloat missingValues (*useNoneForMissingValues*)false culture x with
    | Some f -> f
    | _ -> failwithf "Not a float - %A" x

  /// Sets the property with the given name to the given float value
  member x.Set(propertyName, value:float) = x.Set(propertyName,decimal value)

  /// Get the boolean value of an element (assuming that the value is a boolean)
  member x.AsBoolean(?culture) =
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match JsonConversions.AsBoolean culture x with
    | Some b -> b
    | _ -> failwithf "Not a boolean - %A" x

  /// Sets the property with the given name to the given boolean value
  member x.Set(propertyName, value:bool) =
    match x with
    | JsonValue.Object properties ->
        match Map.tryFind propertyName properties with 
        | Some (JsonValue.Boolean x) -> 
               JsonValue.Object(properties |> Map.remove propertyName |> Map.add propertyName (JsonValue.Boolean value))
        | _ -> failwithf "Didn't find boolean property '%s' in %A" propertyName x
    | _ -> failwithf "Not an object - %A" x

  /// Get the datetime value of an element (assuming that the value is a string
  /// containing well-formed ISO date or MSFT JSON date)
  member x.AsDateTime(?culture) = 
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match JsonConversions.AsDateTime culture x with
    | Some d -> d
    | _ -> failwithf "Not a datetime - %A" x

  /// Sets the property with the given name to the given DateTime value
  member x.Set(propertyName, value:DateTime, ?culture) =
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match x with
    | JsonValue.Object properties ->
        match Map.tryFind propertyName properties with 
        | Some (JsonValue.String x) -> 
               JsonValue.Object(properties |> Map.remove propertyName |> Map.add propertyName (JsonValue.String (value.ToString culture)))
        | _ -> failwithf "Didn't find datetime property '%s' in %A" propertyName x
    | _ -> failwithf "Not an object - %A" x

  /// Get the guid value of an element (assuming that the value is a guid)
  member x.AsGuid() =
    match JsonConversions.AsGuid x with
    | Some g -> g
    | _ -> failwithf "Not a guid - %A" x

  /// Sets the property with the given name to the given Guid value
  member x.Set(propertyName, value:Guid) =
    match x with
    | JsonValue.Object properties ->
        match Map.tryFind propertyName properties with 
        | Some (JsonValue.String x) -> 
               JsonValue.Object(properties |> Map.remove propertyName |> Map.add propertyName (JsonValue.String (value.ToString())))
        | _ -> failwithf "Didn't find boolean property '%s' in %A" propertyName x
    | _ -> failwithf "Not an object - %A" x

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