/// Unsafe extension methods that can be used to work with CsvRow in a less safe, but shorter way.
/// This module also provides the dynamic operator.
module FSharp.Data.Csv.Extensions

open System
open System.Globalization
open FSharp.Data.Runtime
  
type String with

  member x.AsInteger(?culture) = 
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match TextConversions.AsInteger culture x with
    | Some i -> i
    | _ -> failwithf "Not an int - %s" x

  member x.AsInteger64(?culture) = 
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match TextConversions.AsInteger64 culture x with
    | Some i -> i
    | _ -> failwithf "Not an int64 - %s" x

  member x.AsDecimal(?culture) = 
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match TextConversions.AsDecimal culture x with
    | Some d -> d
    | _ -> failwithf "Not a decimal - %s" x

  member x.AsFloat(?culture, ?missingValues) = 
    let culture = defaultArg culture CultureInfo.InvariantCulture
    let missingValues = defaultArg missingValues TextConversions.DefaultMissingValues
    match TextConversions.AsFloat missingValues (*useNoneForMissingValues*)false culture x with
    | Some f -> f
    | _ -> failwithf "Not a float - %s" x

  member x.AsBoolean(?culture) =
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match TextConversions.AsBoolean culture x with
    | Some b -> b
    | _ -> failwithf "Not a boolean - %s" x

  member x.AsDateTime(?culture) = 
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match TextConversions.AsDateTime culture x with 
    | Some d -> d
    | _ -> failwithf "Not a datetime - %s" x

  member x.AsGuid(?culture) =
    let culture = defaultArg culture CultureInfo.InvariantCulture
    match x |> TextConversions.AsGuid with
    | Some g -> g
    | _ -> failwithf "Not a guid - %s" x

/// Get column of a CsvRow
let (?) (csvRow:CsvRow) (columnName:string) = csvRow.[columnName]
