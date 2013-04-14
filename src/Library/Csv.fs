// --------------------------------------------------------------------------------------
// CSV untyped API
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Csv

open System
open System.Globalization
open System.IO
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.ProviderFileSystem

[<StructuredFormatDisplay("{Columns}")>]
type CsvRow(parent:CsvFile, columns:string[]) =

  member __.Columns = columns
  member __.GetColumn columnName = columns.[parent.GetColumnIndex columnName]

/// Represents a CSV file. The lines are read on demand from 'reader'.
/// Columns are delimited by one of the chars passed by 'separators' (defaults to just ','), and
/// to escape the separator chars, the 'quote' character will be used (defaults to '"').
/// If 'hasHeaders' is true (the default), the first line read by 'reader' will not be considered part of data.
/// If 'ignoreErrors' is true (the default is false), rows with a different number of columns from the header row
/// (or the first row if headers are not present) will be ignored
and CsvFile private (reader:TextReader, ?separators, ?quote, ?hasHeaders, ?ignoreErrors) as this =
  inherit CsvFile<CsvRow>(Func<_,_,_>(fun this columns -> CsvRow(this :?> CsvFile, columns)), reader, defaultArg separators "", defaultArg quote '"', 
                          defaultArg hasHeaders true, defaultArg ignoreErrors false)

  let headerDic = 
    this.Headers
    |> Seq.mapi (fun index header -> header, index)
    |> dict

  member internal __.GetColumnIndex columnName = headerDic.[columnName]

  /// Parses the specified CSV content
  static member Parse(text, ?separators, ?quote, ?hasHeaders, ?ignoreErrors) = 
    let reader = new StringReader(text)
    new CsvFile(reader, ?separators=separators, ?quote=quote, ?hasHeaders=hasHeaders, ?ignoreErrors=ignoreErrors)

  /// Loads CSV from the specified stream
  static member Load(stream:Stream, ?separators, ?quote, ?hasHeaders, ?ignoreErrors) = 
    let reader = new StreamReader(stream)
    new CsvFile(reader, ?separators=separators, ?quote=quote, ?hasHeaders=hasHeaders, ?ignoreErrors=ignoreErrors)

  /// Loads CSV from the specified reader
  static member Load(reader:TextReader, ?separators, ?quote, ?hasHeaders, ?ignoreErrors) = 
    new CsvFile(reader, ?separators=separators, ?quote=quote, ?hasHeaders=hasHeaders, ?ignoreErrors=ignoreErrors)

  /// Loads CSV from the specified uri
  static member Load(uri, ?separators, ?quote, ?hasHeaders, ?ignoreErrors) = 
    let reader = readTextAtRunTime false "" "" uri
    new CsvFile(reader, ?separators=separators, ?quote=quote, ?hasHeaders=hasHeaders, ?ignoreErrors=ignoreErrors)

// --------------------------------------------------------------------------------------
// Unsafe extensions for simple CSV processing
// --------------------------------------------------------------------------------------

/// Adds extension methods that can be used to work with CsvRow in a more convenient, but
/// less safe way. The module also provides the dynamic operator.
module Extensions = 
  
  type CsvRow with
      member x.Item with get(index) = x.Columns.[index]
      member x.Item with get(columnName) = x.GetColumn(columnName)
  
  let (?) (csvRow:CsvRow) (columnName:string) = csvRow.[columnName]

  type String with

    member x.AsDateTime(?culture) = 
      match x |> Operations.AsDateTime (defaultArg culture CultureInfo.InvariantCulture) with 
      | Some d -> d
      | _ -> failwithf "Not a datetime - %s" x

    member x.AsFloat(?culture, ?missingValues) =       
      let missingValues = defaultArg missingValues Operations.DefaultMissingValues
      let culture = defaultArg culture CultureInfo.InvariantCulture
      match x |> Operations.AsFloat missingValues culture with
      | Some n -> n
      | _ -> failwithf "Not a float - %s" x

    member x.AsDecimal(?culture) = 
      match x |> Operations.AsDecimal (defaultArg culture CultureInfo.InvariantCulture) with
      | Some n -> n
      | _ -> failwithf "Not a decimal - %s" x
  
    member x.AsInteger(?culture) = 
      match x |> Operations.AsInteger (defaultArg culture CultureInfo.InvariantCulture) with
      | Some n -> n
      | _ -> failwithf "Not an int - %s" x

    member x.AsInteger64(?culture) = 
      match x |> Operations.AsInteger64 (defaultArg culture CultureInfo.InvariantCulture) with
      | Some n -> n
      | _ -> failwithf "Not an int64 - %s" x

    member x.AsBoolean(?culture) =
      match x |> Operations.AsBoolean (defaultArg culture CultureInfo.InvariantCulture) with
      | Some n -> n
      | _ -> failwithf "Not a bool - %s" x

    member x.AsGuid(?culture) =
      match x |> Operations.AsGuid with
      | Some n -> n
      | _ -> failwithf "Not a guid - %s" x
