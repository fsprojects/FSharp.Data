/// Unsafe extension methods that can be used to work with CsvRow in a less safe, but shorter way.
/// This module also provides the dynamic operator.
module FSharp.Data.CsvExtensions

open System
open System.Globalization
open FSharp.Data.Runtime
  
type String with

  member x.AsInteger(?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
    match TextConversions.AsInteger cultureInfo x with
    | Some i -> i
    | _ -> failwithf "Not an int: %s" x

  member x.AsInteger64(?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
    match TextConversions.AsInteger64 cultureInfo x with
    | Some i -> i
    | _ -> failwithf "Not an int64: %s" x

  member x.AsDecimal(?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
    match TextConversions.AsDecimal cultureInfo x with
    | Some d -> d
    | _ -> failwithf "Not a decimal: %s" x

  member x.AsFloat(?cultureInfo, ?missingValues) = 
    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
    let missingValues = defaultArg missingValues TextConversions.DefaultMissingValues
    match TextConversions.AsFloat missingValues (*useNoneForMissingValues*)false cultureInfo x with
    | Some f -> f
    | _ -> failwithf "Not a float: %s" x

  member x.AsBoolean(?cultureInfo) =
    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
    match TextConversions.AsBoolean cultureInfo x with
    | Some b -> b
    | _ -> failwithf "Not a boolean: %s" x

  member x.AsDateTime(?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
    match TextConversions.AsDateTime cultureInfo x with 
    | Some d -> d
    | _ -> failwithf "Not a datetime: %s" x

  member x.AsGuid() =
    match x |> TextConversions.AsGuid with
    | Some g -> g
    | _ -> failwithf "Not a guid: %s" x

/// Get column of a CsvRow
let (?) (csvRow:CsvRow) (columnName:string) = csvRow.[columnName]
