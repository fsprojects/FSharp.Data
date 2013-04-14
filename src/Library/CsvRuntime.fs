// --------------------------------------------------------------------------------------
// CSV type provider - runtime components (parsing and type representing CSV)
// --------------------------------------------------------------------------------------

namespace FSharp.Data.RuntimeImplementation

open System
open System.IO

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

type CsvFile<'RowType>(f:Func<_,_,'RowType>, reader:TextReader, separators, quote, hasHeaders, ignoreErrors) as this =

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
      lines |> Seq.head |> Array.map (fun columnName -> columnName.Trim())
    else 
      // use the number of columns of the first data row
      Array.create (lines |> Seq.head |> Array.length) ""    

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
          f.Invoke(this, row) |> Choice1Of2 
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

  interface IDisposable with
    member __.Dispose() = reader.Dispose()
