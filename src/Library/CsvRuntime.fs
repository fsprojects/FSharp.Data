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
    let rec readLines lineNumber = seq {
      match reader.Peek() with
      | -1 -> ()
      | Char '\r' | Char '\n' -> reader.Read() |> ignore; yield! readLines (lineNumber + 1)
      | _ -> 
          yield readLine [] [] |> List.rev |> Array.ofList, lineNumber
          yield! readLines (lineNumber + 1) }
  
    readLines 0

// --------------------------------------------------------------------------------------

module private CsvHelpers = 

  let tryConvert (stringArrayToRow:Func<obj, string[], 'RowType>) this row = 
    try 
      stringArrayToRow.Invoke(this, row) |> Choice1Of2 
    with exn -> 
      Choice2Of2 exn

// --------------------------------------------------------------------------------------

type CsvFile<'RowType> private (rowToStringArray:Func<'RowType,string[]>, reader:TextReader, data:seq<'RowType>, headers, numberOfColumns, separators, quote) =

  member __.Data = data
  member __.Headers = headers
  member __.NumberOfColumns = numberOfColumns
  member __.Separators = separators
  member __.Quote = quote
  
  interface IDisposable with
    member __.Dispose() = 
      if reader <> null then
        reader.Dispose()

  new (stringArrayToRow, rowToStringArray, reader, separators, quote, hasHeaders, ignoreErrors) as this =
  
    let separators = if String.IsNullOrEmpty separators then "," else separators
  
    let linesIterator = (CsvReader.readCsvFile reader separators quote).GetEnumerator()
  
    let firstLine = 
      if linesIterator.MoveNext() then
        linesIterator.Current
      else
        linesIterator.Dispose()
        if hasHeaders then
          failwithf "Invalid CSV file: header row not found" 
        else
          failwithf "Invalid CSV file: no data rows found"

    let headers = 
      if hasHeaders then 
        firstLine |> fst |> Array.map (fun columnName -> columnName.Trim()) |> Some
      else 
        None
  
    let numberOfColumns =
      match headers with
      | Some headers -> headers |> Array.length
      | None -> firstLine |> fst |> Array.length

    let data = seq {
      try
        if not hasHeaders then
          yield firstLine
        while linesIterator.MoveNext() do
          yield linesIterator.Current
      finally
        linesIterator.Dispose()
    }

    // Ignore rows with different number of columns when ignoreErrors is set to true
    let data = 
      if ignoreErrors
      then data |> Seq.filter (fun (row, _) -> row.Length = numberOfColumns)
      else data

    let data = seq {
      for row, lineNumber in data do
        // Always ignore empty rows
        if not (row |> Seq.forall String.IsNullOrWhiteSpace) then
          // Try to convert rows to 'RowType      
          if not ignoreErrors && row.Length <> numberOfColumns then
            failwithf "Couldn't parse row %d according to schema: Expected %d columns, got %d" lineNumber numberOfColumns row.Length
          let convertedRow = CsvHelpers.tryConvert stringArrayToRow this row
          match convertedRow, ignoreErrors with
          | Choice1Of2 convertedRow, _ -> yield convertedRow
          | Choice2Of2 _, true -> ()
          | Choice2Of2 exn, false -> failwithf "Couldn't parse row %d according to schema: %s" lineNumber exn.Message
    }
  
    new CsvFile<'RowType>(rowToStringArray, reader, data, headers, numberOfColumns, separators, quote)

  /// Saves CSV to the specified writer
  member x.Save(writer:TextWriter, ?separator, ?quote) =

    let separator = (defaultArg separator x.Separators.[0]).ToString()
    let quote = (defaultArg quote x.Quote).ToString()
    let doubleQuote = quote + quote

    use writer = writer

    let writeLine writeItem (items:string[]) =
      for i = 0 to items.Length-2 do
        writeItem items.[i]
        writer.Write separator
      writeItem items.[items.Length-1]
      writer.WriteLine()

    match x.Headers with
    | Some headers -> headers |> writeLine writer.Write
    | None -> ()

    for row in x.Data do
      row |> rowToStringArray.Invoke |> writeLine (fun item -> 
        if item.Contains separator then
          writer.Write quote
          writer.Write (item.Replace(quote, doubleQuote))
          writer.Write quote
        else
          writer.Write item)

  /// Saves CSV to the specified stream
  member x.Save(stream:Stream, ?separator, ?quote) = 
    let writer = new StreamWriter(stream)
    x.Save(writer, ?separator=separator, ?quote=quote)

#if FX_NO_LOCAL_FILESYSTEM
#else
  /// Saves CSV to the specified file
  member x.Save(path:string, ?separator, ?quote) = 
    let writer = new StreamWriter(File.OpenWrite(path))
    x.Save(writer, ?separator=separator, ?quote=quote)
#endif

  /// Saves CSV to a string
  member x.SaveToString(?separator, ?quote) = 
     let writer = new StringWriter()
     x.Save(writer, ?separator=separator, ?quote=quote)
     writer.ToString()

  member inline private x.map f =
    new CsvFile<'RowType>(rowToStringArray, null, x.Data |> f, x.Headers, x.NumberOfColumns, x.Separators, x.Quote)

  /// Returns a new csv with the same rows as the original but which guarantees
  /// that each row will be only be read and parsed from the input at most once.
  member x.Cache() =   
    Seq.cache |> x.map

  /// Returns a new csv containing only the rows for which the given predicate returns "true".
  member x.Filter predicate = 
    Seq.filter predicate |> x.map
  
  /// Returns a new csv with only the first N rows of the underlying csv.
  member x.Take count = 
    Seq.take count |> x.map
  
  /// Returns a csv that, when iterated, yields rowswhile the given predicate
  /// returns <c>true</c>, and then returns no further rows.
  member x.TakeWhile predicate = 
    Seq.takeWhile predicate |> x.map
  
  /// Returns a csv that skips N rows and then yields the remaining rows.
  member x.Skip count = 
    Seq.skip count |> x.map
  
  /// Returns a csv that, when iterated, skips rows while the given predicate returns
  /// <c>true</c>, and then yields the remaining rows.
  member x.SkipWhile predicate = 
    Seq.skipWhile predicate |> x.map
  
  /// Returns a csv that when enumerated returns at most N rows.
  member x.Truncate count = 
    Seq.truncate count |> x.map
