// --------------------------------------------------------------------------------------
// Untyped CSV api
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Csv

open System
open System.Globalization
open System.IO
open System.Runtime.InteropServices
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.ProviderFileSystem

[<StructuredFormatDisplay("{Columns}")>]
type CsvRow(parent:CsvFile, columns:string[]) =

  member __.Columns = columns
  
  member __.GetColumn index = columns.[index]
  member __.GetColumn columnName = columns.[parent.GetColumnIndex columnName]  

  member __.Item with get index = columns.[index]
  member __.Item with get columnName = columns.[parent.GetColumnIndex columnName]

/// Represents a CSV file. The lines are read on demand from 'reader'.
/// Columns are delimited by one of the chars passed by 'separators' (defaults to just ','), and
/// to escape the separator chars, the 'quote' character will be used (defaults to '"').
/// If 'hasHeaders' is true (the default), the first line read by 'reader' will not be considered part of data.
/// If 'ignoreErrors' is true (the default is false), rows with a different number of columns from the header row
/// (or the first row if headers are not present) will be ignored
and CsvFile private (readerFunc:Func<TextReader>, ?separators, ?quote, ?hasHeaders, ?ignoreErrors) as this =
  inherit CsvFile<CsvRow>(
    Func<_,_,_>(fun this columns -> CsvRow(this :?> CsvFile, columns)),
    Func<_,_>(fun row -> row.Columns),
    readerFunc, 
    defaultArg separators "", 
    defaultArg quote '"', 
    defaultArg hasHeaders true, 
    defaultArg ignoreErrors false)

  let headerDic = 
    match this.Headers with
    | Some headers ->
        headers
        |> Seq.mapi (fun index header -> header, index)
        |> dict
    | None -> [] |> dict

  member internal __.GetColumnIndex columnName = headerDic.[columnName]

  /// Parses the specified CSV content
  static member Parse(text, [<Optional>] ?separators, [<Optional>] ?quote, [<Optional>] ?hasHeaders, [<Optional>] ?ignoreErrors) = 
    let readerFunc = Func<_>(fun () -> new StringReader(text) :> TextReader)
    new CsvFile(readerFunc, ?separators=separators, ?quote=quote, ?hasHeaders=hasHeaders, ?ignoreErrors=ignoreErrors)

  /// Loads CSV from the specified stream
  static member Load(stream:Stream, [<Optional>] ?separators, [<Optional>] ?quote, [<Optional>] ?hasHeaders, [<Optional>] ?ignoreErrors) = 
    let firstTime = ref true
    let readerFunc = Func<_>(fun () -> 
      if firstTime.Value then firstTime := false
      else stream.Position <- 0L
      new StreamReader(stream) :> TextReader)
    new CsvFile(readerFunc, ?separators=separators, ?quote=quote, ?hasHeaders=hasHeaders, ?ignoreErrors=ignoreErrors)

  /// Loads CSV from the specified reader
  static member Load(reader:TextReader, [<Optional>] ?separators, [<Optional>] ?quote, [<Optional>] ?hasHeaders, [<Optional>] ?ignoreErrors) = 
    let firstTime = ref true
    let readerFunc = Func<_>(fun () ->  
      if firstTime.Value then firstTime := false
      elif reader :? StreamReader then
        let sr = reader :?> StreamReader
        sr.BaseStream.Position <- 0L
        sr.DiscardBufferedData()
      else invalidOp "The underlying source stream is not re-entrant. Use the Cache method to cache the data."
      reader)
    new CsvFile(readerFunc, ?separators=separators, ?quote=quote, ?hasHeaders=hasHeaders, ?ignoreErrors=ignoreErrors)

  /// Loads CSV from the specified uri
  static member Load(uri:string, [<Optional>] ?separators, [<Optional>] ?quote, [<Optional>] ?hasHeaders, [<Optional>] ?ignoreErrors) = 
    let separators = defaultArg separators ""    
    let separators = 
        if String.IsNullOrEmpty separators && uri.EndsWith(".tsv" , StringComparison.OrdinalIgnoreCase) 
        then "\t" else separators
    let readerFunc = Func<_>(fun () -> asyncReadTextAtRuntime false "" "" uri |> Async.RunSynchronously)
    new CsvFile(readerFunc, separators, ?quote=quote, ?hasHeaders=hasHeaders, ?ignoreErrors=ignoreErrors)

  /// Loads CSV from the specified uri asynchronously
  static member AsyncLoad(uri:string, [<Optional>] ?separators, [<Optional>] ?quote, [<Optional>] ?hasHeaders, [<Optional>] ?ignoreErrors) = async {
    let separators = defaultArg separators ""    
    let separators = 
        if String.IsNullOrEmpty separators && uri.EndsWith(".tsv" , StringComparison.OrdinalIgnoreCase)
        then "\t" else separators
    let! reader = asyncReadTextAtRuntime false "" "" uri
    let firstTime = ref true
    let readerFunc = Func<_>(fun () ->  
      if firstTime.Value then firstTime := false; reader
      else asyncReadTextAtRuntime false "" "" uri |> Async.RunSynchronously)
    return new CsvFile(readerFunc, separators, ?quote=quote, ?hasHeaders=hasHeaders, ?ignoreErrors=ignoreErrors)
  }

// --------------------------------------------------------------------------------------
// Unsafe extensions for simple CSV processing
// --------------------------------------------------------------------------------------

/// Adds extension methods that can be used to work with CsvRow in a more convenient, but
/// less safe way. The module also provides the dynamic operator.
module Extensions = 
  
  let (?) (csvRow:CsvRow) (columnName:string) = csvRow.[columnName]

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
