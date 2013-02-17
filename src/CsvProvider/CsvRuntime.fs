// --------------------------------------------------------------------------------------
// CSV type provider - runtime components (parsing and type representing CSV)
// --------------------------------------------------------------------------------------

namespace FSharp.Data.RuntimeImplementation

open System
open System.ComponentModel
open System.IO
open System.Text

module CsvReader = 

  let inline (|Char|) (n:int) = char n
  let inline (|Separator|_|) sep (n:int) = if Array.exists ((=) (char n)) sep then Some() else None

  /// Read quoted string value until the end (ends with end of stream or
  /// the " character, which can be encoded using double ")
  let rec readString chars (reader:TextReader) = 
    match reader.Read() with
    | -1 -> chars
    | Char '"' when reader.Peek() = int '"' ->
        reader.Read() |> ignore
        readString ('"'::chars) reader
    | Char '"' -> chars
    | Char c -> readString (c::chars) reader
  
  /// Reads a line with data that are separated using specified Separators
  /// and may be quoted. Ends with newline or end of input.
  let rec readLine data chars sep (reader:TextReader) = 
    match reader.Read() with
    | -1 | Char '\r' | Char '\n' -> 
        let item = new String(chars |> List.rev |> Array.ofList)
        item::data
    | Separator sep  ->
        let item = new String(chars |> List.rev |> Array.ofList)
        readLine (item::data) [] sep reader
    | Char '"' ->
        readLine data (readString chars reader) sep reader
    | Char c ->
        readLine data (c::chars) sep reader

  /// Reads multiple lines from the input, skipping newline characters
  let rec readLines sep (reader:TextReader) = seq {
    match reader.Peek() with
    | -1 -> ()
    | Char '\r' | Char '\n' -> reader.Read() |> ignore; yield! readLines sep reader
    | _ -> 
        yield readLine [] [] sep reader |> List.rev |> Array.ofList
        yield! readLines sep reader }

  /// Lazily reads the specified CSV file using the specified separator
  /// (Handles most of the RFC 4180 - most notably quoted values and also
  /// quoted newline characters in columns)
  let readCsvFile (reader:TextReader) sep =
    readLines sep reader



/// Simple type that represents a single CSV row
[<StructuredFormatDisplay("{Display}")>]
type CsvRow internal (data:string[], headers:string[]) =

  do 
    if data.Length <> headers.Length then
      failwithf "Invalid CSV row: expected %d columns, but got %d: %A" headers.Length data.Length (data |> Seq.map (fun s -> "\"" + s + "\"") |> String.concat " ")

  /// The raw data
  member x.Columns = data

  /// Format the CSV row in the style of F# records
  member private x.Display =
    let sb = new StringBuilder()
    let append (s:string) = sb.Append s |> ignore
    append "{" 
    for (header, data) in Seq.zip headers data do
      append " "
      append header
      append " = "
      append data
      append " ;"
    sb.ToString(0, sb.Length - 1) + "}"

  [<EditorBrowsable(EditorBrowsableState.Never)>]
  override x.ToString() = x.Display

// Simple type wrapping CSV data
type CsvFile (reader:TextReader, ?headers:string, ?skipRow:int, ?sep:string) =

  let sep = defaultArg sep ""
  let sep = if String.IsNullOrEmpty(sep) then "," else sep
  let headerDefns = defaultArg headers ""
  let skipRow = defaultArg skipRow 1

  /// Read the input and cache it (we can read input only once)
  let file = CsvReader.readCsvFile reader (sep.ToCharArray()) |> Seq.cache

  do 
    if Seq.isEmpty file then
      failwithf "Invalid CSV file: header row not found" 

  let headers = 
     if String.IsNullOrEmpty(headerDefns)
     then file |> Seq.skip (skipRow - 1) |> Seq.head
     else
         use sr = new StringReader(headerDefns)
         CsvReader.readLine [] [] (sep.ToCharArray()) sr |> List.rev |> Array.ofList
     |> Seq.filter (fun h -> not <| String.IsNullOrEmpty(h) && not <| String.IsNullOrWhiteSpace(h))
     |> Seq.toArray

  let data = 
    if String.IsNullOrEmpty(headerDefns)
    then file |> Seq.skip skipRow |> Seq.map (fun v -> CsvRow(v, headers))
    else file |> Seq.skip skipRow |> Seq.map (fun v -> CsvRow(v, headers))

 

  member x.Data = data
  member x.Headers = headers

  interface IDisposable with
    member __.Dispose() = reader.Dispose()
