/// Unsafe extension methods that can be used to work with CsvRow in a less safe, but shorter way.
/// This module also provides the dynamic operator.
namespace FSharp.Data

open System
open System.Globalization
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open FSharp.Data
open FSharp.Data.Runtime

[<Extension>]
type CsvExtensions =

  [<Extension>]
  static member AsInteger(x:String, [<Optional;DefaultParameterValue(null)>] ?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
    match TextConversions.AsInteger cultureInfo x with
    | Some i -> i
    | _ -> failwithf "Not an int: %s" x

  [<Extension>]
  static member AsInteger64(x:String, [<Optional;DefaultParameterValue(null)>] ?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
    match TextConversions.AsInteger64 cultureInfo x with
    | Some i -> i
    | _ -> failwithf "Not an int64: %s" x

  [<Extension>]
  static member AsDecimal(x:String, [<Optional;DefaultParameterValue(null)>] ?cultureInfo) =
    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
    match TextConversions.AsDecimal cultureInfo x with
    | Some d -> d
    | _ -> failwithf "Not a decimal: %s" x

  [<Extension>]
  static member AsFloat(x:String, [<Optional;DefaultParameterValue(null)>] ?cultureInfo, [<Optional;DefaultParameterValue(null)>] ?missingValues) = 
    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
    let missingValues = defaultArg missingValues TextConversions.DefaultMissingValues
    match TextConversions.AsFloat missingValues (*useNoneForMissingValues*)false cultureInfo x with
    | Some f -> f
    | _ -> failwithf "Not a float: %s" x
  
  [<Extension>]
  static member AsBoolean(x:String, [<Optional;DefaultParameterValue(null)>] ?cultureInfo) =
    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
    match TextConversions.AsBoolean cultureInfo x with
    | Some b -> b
    | _ -> failwithf "Not a boolean: %s" x

  [<Extension>]
  static member AsDateTime(x:String, [<Optional;DefaultParameterValue(null)>] ?cultureInfo) = 
    let cultureInfo = defaultArg cultureInfo CultureInfo.InvariantCulture
    match TextConversions.AsDateTime cultureInfo x with 
    | Some d -> d
    | _ -> failwithf "Not a datetime: %s" x

  [<Extension>]
  static member AsGuid(x:String) =
    match x |> TextConversions.AsGuid with
    | Some g -> g
    | _ -> failwithf "Not a guid: %s" x

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CsvExtensions =
  /// Get column of a CsvRow
  let (?) (csvRow:CsvRow) (columnName:string) = csvRow.[columnName]