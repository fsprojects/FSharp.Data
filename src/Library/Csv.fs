// --------------------------------------------------------------------------------------
// CSV untyped API
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Csv

open System
open System.ComponentModel
open System.Globalization
open System.IO
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.ProviderFileSystem

type CsvRow(parent:CsvFile, columns:string[]) =

  member __.Columns = columns
  member __.GetColumn columnName = columns.[parent.GetColumnIndex columnName]

  member internal __.Parent = parent

  [<EditorBrowsable(EditorBrowsableState.Never)>]
  override __.ToString() = columns.ToString()

/// Represents a CSV file. The lines are read on demand from 'reader'.
/// Columns are delimited by one of the chars passed by 'separators' (defaults to just ','), and
/// to escape the separator chars, the 'quote' character will be used (defaults to '"').
/// If 'hasHeaders' is true (the default), the first line read by 'reader' will not be considered part of data.
/// If 'ignoreErrors' is true (the default is false), rows with a different number of columns from the header row
/// (or the first row if headers are not present) will be ignored
and CsvFile private (reader:TextReader, ?separators, ?quote, ?hasHeaders, ?ignoreErrors, ?culture) as this =
  inherit CsvFile<CsvRow>(Func<_,_,_>(fun this columns -> CsvRow(this :?> CsvFile, columns)), reader, defaultArg separators "", defaultArg quote '"', 
                          defaultArg hasHeaders true, defaultArg ignoreErrors false)

  let culture = defaultArg culture CultureInfo.InvariantCulture

  let headerDic = 
    this.Headers
    |> Seq.mapi (fun index header -> header, index)
    |> dict

  member internal __.Culture = culture
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
  
  open System.Globalization

  type CsvRowIndex = 
    { Row : CsvRow
      Index : int }

    member x.AsString() = x.Row.Columns.[x.Index]
    
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    override x.ToString() = x.AsString()

  type CsvRow with
      member x.Item with get(index) = { Row = x; Index = index }
      member x.Item with get(columnName) = { Row = x; Index = x.Parent.GetColumnIndex(columnName) }
  
  let (?) (csvRow:CsvRow) (columnName:string) = csvRow.[columnName]

  type CsvRowIndex with

    member x.AsDateTime(?culture) = 
      match x.AsString() |> Operations.AsDateTime (defaultArg culture x.Row.Parent.Culture) with 
      | Some d -> d
      | _ -> failwithf "Not a datetime - %A" x

    member x.AsFloat(?culture) = 
      match x.AsString() |> Operations.AsFloat (defaultArg culture x.Row.Parent.Culture) with
      | Some n -> n
      | _ -> failwithf "Not a float - %A" x

    member x.AsDecimal(?culture) = 
      match x.AsString() |> Operations.AsDecimal (defaultArg culture x.Row.Parent.Culture) with
      | Some n -> n
      | _ -> failwithf "Not a decimal - %A" x
  
    member x.AsInteger(?culture) = 
      match x.AsString() |> Operations.AsInteger (defaultArg culture x.Row.Parent.Culture) with
      | Some n -> n
      | _ -> failwithf "Not an int - %A" x

    member x.AsInteger64(?culture) = 
      match x.AsString() |> Operations.AsInteger64 (defaultArg culture x.Row.Parent.Culture) with
      | Some n -> n
      | _ -> failwithf "Not an int64 - %A" x

    member x.AsBoolean(?culture) =
      match x.AsString() |> Operations.AsBoolean (defaultArg culture x.Row.Parent.Culture) with
      | Some n -> n
      | _ -> failwithf "Not a bool" x
