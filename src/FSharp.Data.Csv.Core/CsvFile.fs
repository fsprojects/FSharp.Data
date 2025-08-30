// --------------------------------------------------------------------------------------
// Untyped CSV api
// --------------------------------------------------------------------------------------

namespace FSharp.Data

open System
open System.Globalization
open System.IO
open System.Runtime.InteropServices
open FSharp.Data.Runtime
open FSharp.Data.Runtime.IO
open System.Text

[<StructuredFormatDisplay("{Columns}")>]
/// Represents a CSV row.
type CsvRow(parent: CsvFile, columns: string[]) =

    /// The columns of the row
    member _.Columns = columns

    /// Gets a column by index
    member _.GetColumn index = columns.[index]

    /// Gets a column by name
    member _.GetColumn columnName =
        columns.[parent.GetColumnIndex columnName]

    /// Gets a column by index
    member _.Item
        with get index = columns.[index]

    /// Gets a column by name
    member _.Item
        with get columnName = columns.[parent.GetColumnIndex columnName]

/// <summary>
/// Represents a CSV file. The lines are read on demand from <c>reader</c>.
/// Columns are delimited by one of the chars passed by <c>separators</c> (defaults to just <c>,</c>), and
/// to escape the separator chars, the <c>quote</c> character will be used (defaults to <c>"</c>).
/// If <c>hasHeaders</c> is true (the default), the first line read by <c>reader</c> will not be considered part of data.
/// If <c>ignoreErrors</c> is true (the default is false), rows with a different number of columns from the header row
/// (or the first row if headers are not present) will be ignored.
/// The first <c>skipRows</c> lines will be skipped.
/// </summary>
and CsvFile
    private
    (
        readerFunc: Func<TextReader>,
        [<Optional>] ?separators,
        [<Optional>] ?quote,
        [<Optional>] ?hasHeaders,
        [<Optional>] ?ignoreErrors,
        [<Optional>] ?skipRows
    ) as this =
    inherit
        CsvFile<CsvRow>(
            Func<_, _, _>(fun this columns -> CsvRow(this :?> CsvFile, columns)),
            Func<_, _>(fun row -> row.Columns),
            readerFunc,
            defaultArg separators "",
            defaultArg quote '"',
            defaultArg hasHeaders true,
            defaultArg ignoreErrors false,
            defaultArg skipRows 0
        )

    let headerDic =
        match this.Headers with
        | Some headers -> headers |> Seq.mapi (fun index header -> header, index) |> dict
        | None -> [] |> dict

    /// Returns the index of the column with the given name
    member _.GetColumnIndex columnName = headerDic.[columnName]

    /// Returns the index of the column with the given name, or returns None if no column is found
    member _.TryGetColumnIndex columnName =
        match headerDic.TryGetValue columnName with
        | true, index -> Some index
        | false, _ -> None

    /// Parses the specified CSV content
    static member Parse
        (
            text,
            [<Optional>] ?separators,
            [<Optional>] ?quote,
            [<Optional>] ?hasHeaders,
            [<Optional>] ?ignoreErrors,
            [<Optional>] ?skipRows
        ) =
        let readerFunc = Func<_>(fun () -> new StringReader(text) :> TextReader)

        new CsvFile(
            readerFunc,
            ?separators = separators,
            ?quote = quote,
            ?hasHeaders = hasHeaders,
            ?ignoreErrors = ignoreErrors,
            ?skipRows = skipRows
        )

    /// Loads CSV from the specified stream
    static member Load
        (
            stream: Stream,
            [<Optional>] ?separators,
            [<Optional>] ?quote,
            [<Optional>] ?hasHeaders,
            [<Optional>] ?ignoreErrors,
            [<Optional>] ?skipRows
        ) =
        let firstTime = ref true

        let readerFunc =
            Func<_>(fun () ->
                if firstTime.Value then
                    firstTime := false
                else
                    stream.Position <- 0L

                new StreamReader(stream) :> TextReader)

        new CsvFile(
            readerFunc,
            ?separators = separators,
            ?quote = quote,
            ?hasHeaders = hasHeaders,
            ?ignoreErrors = ignoreErrors,
            ?skipRows = skipRows
        )

    /// Loads CSV from the specified reader
    static member Load
        (
            reader: TextReader,
            [<Optional>] ?separators,
            [<Optional>] ?quote,
            [<Optional>] ?hasHeaders,
            [<Optional>] ?ignoreErrors,
            [<Optional>] ?skipRows
        ) =
        let firstTime = ref true

        let readerFunc =
            Func<_>(fun () ->
                if firstTime.Value then
                    firstTime := false
                elif reader :? StreamReader then
                    let sr = reader :?> StreamReader
                    sr.BaseStream.Position <- 0L
                    sr.DiscardBufferedData()
                else
                    invalidOp "The underlying source stream is not re-entrant. Use the Cache method to cache the data."

                reader)

        new CsvFile(
            readerFunc,
            ?separators = separators,
            ?quote = quote,
            ?hasHeaders = hasHeaders,
            ?ignoreErrors = ignoreErrors,
            ?skipRows = skipRows
        )

    /// Loads CSV from the specified uri
    static member Load
        (
            uri: string,
            [<Optional>] ?separators,
            [<Optional>] ?quote,
            [<Optional>] ?hasHeaders,
            [<Optional>] ?ignoreErrors,
            [<Optional>] ?skipRows,
            [<Optional>] ?encoding
        ) =
        CsvFile.AsyncLoad(
            uri,
            ?separators = separators,
            ?quote = quote,
            ?hasHeaders = hasHeaders,
            ?ignoreErrors = ignoreErrors,
            ?skipRows = skipRows,
            ?encoding = encoding
        )
        |> Async.RunSynchronously

    /// Loads CSV from the specified uri asynchronously
    static member AsyncLoad
        (
            uri: string,
            [<Optional>] ?separators,
            [<Optional>] ?quote,
            [<Optional>] ?hasHeaders,
            [<Optional>] ?ignoreErrors,
            [<Optional>] ?skipRows,
            [<Optional>] ?encoding
        ) =
        async {
            let separators = defaultArg separators ""

            let separators =
                if
                    String.IsNullOrEmpty separators
                    && uri.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase)
                then
                    "\t"
                else
                    separators

            let encoding = defaultArg encoding Encoding.UTF8
            let! reader = asyncReadTextAtRuntime false "" "" "CSV" encoding.WebName uri
            let firstTime = ref true

            let readerFunc =
                Func<_>(fun () ->
                    if firstTime.Value then
                        firstTime := false
                        reader
                    else
                        asyncReadTextAtRuntime false "" "" "CSV" encoding.WebName uri
                        |> Async.RunSynchronously)

            return
                new CsvFile(
                    readerFunc,
                    separators,
                    ?quote = quote,
                    ?hasHeaders = hasHeaders,
                    ?ignoreErrors = ignoreErrors,
                    ?skipRows = skipRows
                )
        }
