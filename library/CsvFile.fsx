(**

*)
#r "nuget: FSharp.Data,8.1.0-beta"
#endif
(**
[![Binder](../img/badge-binder.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Data/gh-pages?filepath=library/CsvFile.ipynb)&emsp;
[![Script](../img/badge-script.svg)](https://fsprojects.github.io/FSharp.Data//library/CsvFile.fsx)&emsp;
[![Notebook](../img/badge-notebook.svg)](https://fsprojects.github.io/FSharp.Data//library/CsvFile.ipynb)

# CSV Parser

The F# [CSV Type Provider](CsvProvider.html) is built on top of an efficient CSV parser written
in F#. There's also a simple API that can be used to access values dynamically.

When working with well-defined CSV documents, it is easier to use the
[type provider](CsvProvider.html), but in a more dynamic scenario or when writing
quick and simple scripts, the parser might be a simpler option.

## Loading CSV documents

To load a sample CSV document, we first need to reference the `FSharp.Data` package.

*)
open FSharp.Data
(**
The `FSharp.Data` namespace contains the [CsvFile](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html) type that provides two static methods
for loading data. The [CsvFile.Parse](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html#Parse) method can be used if we have the data in a `string` value.
The [CsvFile.Load](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html#Load) method allows reading the data from a file or from a web resource (and there's
also an asynchronous [CsvFile.AsyncLoad](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html#AsyncLoad) version). The following sample calls [CsvFile.Load](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html#Load) with a URL that
points to a live CSV file on the Yahoo finance web site:

*)
// Download the stock prices
let msft = CsvFile.Load(__SOURCE_DIRECTORY__ + "/../data/MSFT.csv").Cache()

// Print the prices in the HLOC format
for row in msft.Rows |> Seq.truncate 10 do
    printfn "HLOC: (%s, %s, %s)" (row.GetColumn "High") (row.GetColumn "Low") (row.GetColumn "Date")(* output: 
HLOC: (76.55, 75.86, 9-Oct-17)
HLOC: (76.03, 75.54, 6-Oct-17)
HLOC: (76.12, 74.96, 5-Oct-17)
HLOC: (74.72, 73.71, 4-Oct-17)
HLOC: (74.88, 74.20, 3-Oct-17)
HLOC: (75.01, 74.30, 2-Oct-17)
HLOC: (74.54, 73.88, 29-Sep-17)
HLOC: (73.97, 73.31, 28-Sep-17)
HLOC: (74.17, 73.17, 27-Sep-17)
HLOC: (73.81, 72.99, 26-Sep-17)
val msft: Runtime.CsvFile<CsvRow>
val it: unit = ()*)
(**
Note that unlike `CsvProvider`, [CsvFile](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html) works in streaming mode for performance reasons, which means
that [CsvFile.Rows](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-runtime-csvfile-1.html#Rows) can only be iterated once. If you need to iterate multiple times, use the [CsvFile.Cache](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-runtime-csvfile-1.html#Cache) method,
but please note that this will increase memory usage and should not be used in large datasets.

## Using CSV extensions

Now, we look at a number of extensions that become available after
opening the [CsvExtensions](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvextensionsmodule.html) namespace. Once opened, we can write:

* `row?column` uses the dynamic operator to obtain the column value named `column`;
alternatively, you can also use an indexer `row.[column]`.

* `value.AsBoolean()` returns the value as boolean if it is either `true` or `false` (see [StringExtensions.AsBoolean](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-stringextensions.html#AsBoolean))

* `value.AsInteger()` returns the value as integer if it is numeric and can be
converted to an integer; `value.AsInteger64()`, `value.AsDecimal()` and
`value.AsFloat()` behave similarly.

* `value.AsDateTime()` returns the value as a `DateTime` value using either the
[ISO 8601](http://en.wikipedia.org/wiki/ISO_8601) format, or using the
`\/Date(...)\/` JSON format containing number of milliseconds since 1/1/1970.

* `value.AsDateTimeOffset()` parses the string as a `DateTimeOffset` value using either the
[ISO 8601](http://en.wikipedia.org/wiki/ISO_8601) format, or using the
`\/Date(...[+/-]offset)\/` JSON format containing number of milliseconds since 1/1/1970,
[+/-]() the 4 digit offset. Example- `\/Date(1231456+1000)\/`.

* `value.AsTimeSpan()` parses the string as a `TimeSpan` value.

* `value.AsGuid()` returns the value as a `Guid` value.

Methods that may need to parse a numeric value or date (such as `AsFloat` and
`AsDateTime`) receive an optional culture parameter.

The following example shows how to process the sample previous CSV sample using these extensions:

*)
open FSharp.Data.CsvExtensions

for row in msft.Rows |> Seq.truncate 10 do
    printfn "HLOC: (%f, %M, %O)" (row.["High"].AsFloat()) (row?Low.AsDecimal()) (row?Date.AsDateTime())(* output: 
HLOC: (76.550000, 75.86, 10/9/2017 12:00:00 AM)
HLOC: (76.030000, 75.54, 10/6/2017 12:00:00 AM)
HLOC: (76.120000, 74.96, 10/5/2017 12:00:00 AM)
HLOC: (74.720000, 73.71, 10/4/2017 12:00:00 AM)
HLOC: (74.880000, 74.20, 10/3/2017 12:00:00 AM)
HLOC: (75.010000, 74.30, 10/2/2017 12:00:00 AM)
HLOC: (74.540000, 73.88, 9/29/2017 12:00:00 AM)
HLOC: (73.970000, 73.31, 9/28/2017 12:00:00 AM)
HLOC: (74.170000, 73.17, 9/27/2017 12:00:00 AM)
HLOC: (73.810000, 72.99, 9/26/2017 12:00:00 AM)
val it: unit = ()*)
(**
## Transforming CSV files

In addition to reading, [CsvFile](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html) also has support for transforming CSV files. The operations
available are [CsvFile.Filter](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html), [CsvFile.Take](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html), [CsvFile.TakeWhile](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html), [CsvFile.Skip](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html), [CsvFile.SkipWhile](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html), and [CsvFile.Truncate](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html). After transforming
you can save the results by using one of the overloads of the `Save` method. You can choose different
separator and quote characters when saving.

*)
// Saving the first 10 stock prices where the closing price is higher than the opening price in TSV format:
msft.Filter(fun row -> row?Close.AsFloat() > row?Open.AsFloat()).Truncate(10).SaveToString('\t')(* output: 
val it: string =
  "Date	Open	High	Low	Close	Volume
9-Oct-17	75.97	76.55	75.86	76.29	11386502
6-Oct-17	75.67	76.03	75.54	76.00	13959814
5-Oct-17	75.22	76.12	74.96	75.97	21195261
4-Oct-17	74.09	74.72	73.71	74.69	13317681
29-Sep-17	73.94	74.54	73.88	74.49	17079114
28-Sep-17	73.54	73.97	73.31	73.87	10883787
27-Sep-17	73.55	74.17	73.17	73.85	19375099
22-Sep-17	73.99	74.51	73.85	74.41	14111365
19-Sep-17	75.21	75.71	75.01	75.44	16093344
15-Sep-17	74.83	75.39	74.07	75.31	38578441
"*)
(**
## Related articles

* [CSV Type Provider](CsvProvider.html) - discusses F# type provider
that provides type-safe access to CSV data

* API Reference: [CsvFile](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvfile.html)

* API Reference: [CsvRow](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvrow.html)

* API Reference: [CsvExtensions](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-csvextensionsmodule.html)

*)

