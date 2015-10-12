(**
# F# Data: CSV Parser

The F# [CSV Type Provider](CsvProvider.html) is built on top of an efficient CSV parser written
in F#. There's also a simple API that can be used to access values dynamically.

When working with well-defined CSV documents, it is easier to use the 
[type provider](CsvProvider.html), but in a more dynamic scenario or when writing
quick and simple scripts, the parser might be a simpler option.

## Loading CSV documents

To load a sample CSV document, we first need to reference the `FSharp.Data.dll` library
(when using F# Interactive) or to add reference to a project. 
*)

#r "../../../bin/FSharp.Data.dll"
open FSharp.Data

(**
The `FSharp.Data` namespace contains the `CsvFile` type that provides two static methods
for loading data. The `Parse` method can be used if we have the data in a `string` value.
The `Load` method allows reading the data from a file or from a web resource (and there's
also an asynchronous `AsyncLoad` version). The following sample calls `Load` with a URL that
points to a live CSV file on the Yahoo finance web site:
*)
 
// Download the stock prices
let msft = CsvFile.Load("http://ichart.finance.yahoo.com/table.csv?s=MSFT").Cache()

// Print the prices in the HLOC format
for row in msft.Rows do
  printfn "HLOC: (%s, %s, %s)" (row.GetColumn "High") (row.GetColumn "Low") (row.GetColumn "Date")

(**

Note that unlike `CsvProvider`, `CsvFile` works in streaming mode for performance reasons, which means
that `Rows` can only be iterated once. If you need to iterate multiple times, use the `Cache` method, 
but please note that this will increase memory usage and should not be used in large datasets.

## Using CSV extensions

Now we look at a number of extensions that become available after 
opening the `FSharp.Data.CsvExtensions` namespace. Once opened, we can write:

 * `row?column` uses the dynamic operator to obtain the column value named `column`;
    alternatively, you can also use an indexer `row.[column]`.
 * `value.AsBoolean()` returns the value as boolean if it is either `true` or `false`
 * `value.AsInteger()` returns the value as integer if it is numeric and can be
   converted to an integer; `value.AsInteger64()`, `value.AsDecimal()` and
   `value.AsFloat()` behave similarly.
 * `value.AsDateTime()` returns the value as a `DateTime` value using either the
    [ISO 8601](http://en.wikipedia.org/wiki/ISO_8601) format, or using the 
    `\/Date(...)\/` JSON format containing number of milliseconds since 1/1/1970.
 * `value.AsGuid()` returns the value as a `Guid` value.

Methods that may need to parse a numeric value or date (such as `AsFloat` and
`AsDateTime`) receive an optional culture parameter.

The following example shows how to process the sample previous CSV sample using these extensions:
*)

open FSharp.Data.CsvExtensions

for row in msft.Rows do
  printfn "HLOC: (%f, %M, %O)" (row.["High"].AsFloat()) (row?Low.AsDecimal()) (row?Date.AsDateTime())

(**

## Transforming CSV files

In addition to reading, `CsvFiles` also has support for transforming CSV files. The operations
available are `Filter`, `Take`, `TakeWhile`, `Skip`, `SkipWhile`, and `Truncate`. After transforming
you can save the results by using one of the overloads of the `Save` method. You can choose different
separator and quote characters when saving.
*)

// Saving the first 10 stock prices where the closing price is higher than the opening price in TSV format:
msft.Filter(fun row -> row?Close.AsFloat() > row?Open.AsFloat())
    .Truncate(10)
    .SaveToString('\t')

(**

## Related articles

 * [F# Data: CSV Type Provider](CsvProvider.html) - discusses F# type provider
   that provides type-safe access to CSV data
 * [API Reference: CsvFile class](../reference/fsharp-data-csvfile.html)
 * [API Reference: CsvRow class](../reference/fsharp-data-csvrow.html)
 * [API Reference: CsvExtensions module](../reference/fsharp-data-csvextensions.html)

*)