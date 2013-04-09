// --------------------------------------------------------------------------------------
// CSV type provider - runtime components (parsing and type representing CSV)
// --------------------------------------------------------------------------------------

namespace FSharp.Data.RuntimeImplementation

open System
open System.ComponentModel
open System.IO
open System.Text

// Parser for the CSV format 
module internal CsvReader = 

  /// Lazily reads the specified CSV file using the specified separators
  /// (Handles most of the RFC 4180 - most notably quoted values and also
  /// quoted newline characters in columns)
  let readCsvFile (reader:TextReader) (separators:string) quote =
    let separators = separators.ToCharArray()
    let inline (|Char|) (n:int) = char n
    let inline (|Quote|_|) (n:int) = if char n = quote then Some() else None
    let inline (|Separator|_|) (n:int) = if Array.exists ((=) (char n)) separators then Some() else None

    /// Read quoted string value until the end (ends with end of stream or
    /// the " character, which can be encoded using double ")
    let rec readString chars = 
      match reader.Read() with
      | -1 -> chars
      | Quote when reader.Peek() = int quote ->
          reader.Read() |> ignore
          readString (quote::chars)
      | Quote -> chars
      | Char c -> readString (c::chars)
  
    /// Reads a line with data that are separated using specified separators
    /// and may be quoted. Ends with newline or end of input.
    let rec readLine data chars = 
      match reader.Read() with
      | -1 | Char '\r' | Char '\n' -> 
          let item = new string(chars |> List.rev |> Array.ofList)
          item::data
      | Separator -> 
          let item = new string(chars |> List.rev |> Array.ofList)
          readLine (item::data) [] 
      | Quote ->
          readLine data (readString chars)
      | Char c ->
          readLine data (c::chars)

    /// Reads multiple lines from the input, skipping newline characters
    let rec readLines () = seq {
      match reader.Peek() with
      | -1 -> ()
      | Char '\r' | Char '\n' -> reader.Read() |> ignore; yield! readLines()
      | _ -> 
          yield readLine [] [] |> List.rev |> Array.ofList
          yield! readLines() }
  
    readLines() 

type CsvFile<'RowType>(f:Func<string[],'RowType>, reader:TextReader, separators, quote, hasHeaders, ignoreErrors) =

  let separators = if String.IsNullOrEmpty separators then "," else separators

  /// Read the input and cache it (we can read input only once)
  let lines = 
    CsvReader.readCsvFile reader separators quote 
    |> Seq.cache

  do 
    if Seq.isEmpty lines then
      failwithf "Invalid CSV file: header row not found" 

  let headers =
    if hasHeaders then 
      lines 
      |> Seq.head
    else 
      // use the number of columns of the first data row
      lines 
      |> Seq.head 
      |> Array.length 
      |> Array.zeroCreate
    
  let rawData = 
    if hasHeaders 
    then lines |> Seq.skip 1
    else lines
    |> Seq.mapi (fun index line -> index, line)

  let data = 
    // Ignore rows with different number of columns when ignoreErrors is set to true
    if ignoreErrors
    then rawData |> Seq.filter (fun (_, row) -> row.Length = headers.Length)
    else rawData
    // Always ignore empty rows
    |> Seq.filter (fun (_, row) -> not (row |> Seq.forall String.IsNullOrWhiteSpace))
    // Try to convert rows to 'RowType
    |> Seq.choose (fun (index, row) ->
      if not ignoreErrors && row.Length <> headers.Length then
        failwithf "Couldn't parse row %d according to schema: Expected %d columns, got %d" index headers.Length row.Length
      let convertedRow = 
        try 
          f.Invoke row |> Choice1Of2 
        with exn -> 
          Choice2Of2 exn
      match convertedRow, ignoreErrors with
      | Choice1Of2 convertedRow, _ -> Some convertedRow
      | Choice2Of2 _, true -> None
      | Choice2Of2 exn, false -> failwithf "Couldn't parse row %d according to schema: %s" index exn.Message
    )
    |> Seq.cache

  member __.Data = data
  member __.Headers = headers

  member internal __.Separators = separators
  member internal __.Quote = quote

  interface IDisposable with
    member __.Dispose() = reader.Dispose()

/// Represents a CSV file. The lines are read on demand from 'reader'.
/// Columns are delimited by one of the chars passed by 'separators' (defaults to just ','), and
/// to escape the separator chars, the 'quote' character will be used (defaults to '"').
/// If 'hasHeaders' is true (the default), the first line read by 'reader' will not be considered part of data.
/// If 'ignoreErrors' is true (the default is false), rows with a different number of columns from the header row
/// (or the first row if headers are not present) will be ignored
type CsvFile(reader:TextReader, ?separators, ?quote, ?hasHeaders, ?ignoreErrors) =
    inherit CsvFile<string[]>(Func<_,_> id, reader, defaultArg separators "", defaultArg quote '"', 
                              defaultArg hasHeaders true, defaultArg ignoreErrors false)
