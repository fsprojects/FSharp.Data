(**
# F# Data: CSV Parser and Reader

The F# [CSV Type Provider](CsvProvider.html) is built on top of an efficient CSV parser written
in F#. There's also a simple API that can be used to access values dynamically.

When working with well-defined CSV documents, it is easier to use the 
[type provider](CsvProvider.html), but in a more dynamic scenario or when writing
quick and simple scripts, the parser might be a simpler option.

## Loading CSV documents

To load a sample CSV document, we first need to reference the `FSharp.Data.dll` library
(when using F# Interactive) or to add reference to a project. 
*)

#r "../../bin/FSharp.Data.dll"
open FSharp.Data.Csv

(**
The `FSharp.Data.Csv` namespace contains the `CsvFile` type that provides two static methods
for loading data. The `Parse` method can be used if we have the data in a `string` value.
The `Load` method allows reading the data from a file or from a web resource. The following 
sample calls it with a URL that points to a live CSV file on the Yahoo finance web site:
*)
 
// Download the stock prices
let msft = CsvFile.Load("http://ichart.finance.yahoo.com/table.csv?s=MSFT")

// Print the prices in the HLOC format
for row in msft.Data do
  printfn "HLOC: (%s, %s, %s, %s)" (row.GetColumn "High") (row.GetColumn "Low") (row.GetColumn "Open") (row.GetColumn "Close")

(**

## Using CSV extensions

Now we look at a number of extensions that become available after 
opening the `FSharp.Data.Csv.Extensions` namespace. Once opened, we can write:

 * `row?column` uses the dynamic operator to obtain the column value named `column`;
    alternatively, you can also use an indexer `row.[column]`.
 * `value.AsBoolean()` returns the value as Boolean if it is either `true` or `false`
 * `value.AsInteger()` returns the value as integer if it is numeric and can be
   converted to an integer; `value.AsInteger64()`, `value.AsDecimal()` and
   `value.AsFloat()` behave similarly.
 * `value.AsString()` returns the value as a string
 * `value.AsDateTime()` parse the string as a `DateTime` value using either the
    [ISO 8601](http://en.wikipedia.org/wiki/ISO_8601) format, or using the 
    `\/Date(...)\/` JSON format containing number of milliseconds since 1/1/1970.

Methods that may need to parse a numeric value or date (such as `AsFloat` and
`AsDateTime`) receive an optional culture parameter.

The following example shows how to process the sample previous CSV sample using these extensions:
*)
open FSharp.Data.Csv.Extensions
open System.Globalization

for row in msft.Data do
  printfn "HLOC: (%s, %M, %f, %f)" row?High (row?Low.AsDecimal()) (row.["Open"].AsFloat()) (row?Close.AsFloat(CultureInfo.GetCultureInfo "en-gb"))

(**

## Related articles

 * [F# Data: CSV Type Provider](CsvProvider.html) - discusses F# type provider
   that provides type-safe access to CSV data
*)


