(** 
# F# Data: CSV Type Provider

This article demonstrates how to use the CSV type provider to read CSV files
in a statically typed way. We look how to download stock prices from the Yahoo
Finance web site and then also look how the type provider supports units of measure.
The type provider is based on the same code as the one used on [Try F#](http://www.tryfsharp.org)
web site in the "Financial Computing" tutorial, so you can find additional examples there.

The CSV type provider takes a sample CSV as input and generates a type based on the data
present on the columns of that sample. The column names are obtained from the first
(header) row, and the types are inferred from the values present on the subsequent rows.

## Introducing the provider

The type provider is located in the `FSharp.Data.dll` assembly. Assuming the assembly 
is located in the `../bin` directory, we can load it in F# Interactive as follows:
*)

#r "../../bin/FSharp.Data.dll"
open FSharp.Data

(**

### Parsing stock prices

The Yahoo Finance web site provides daily stock prices in a CSV format that has the
following structure (you can find a larger example in the [`docs/MSFT.csv`](../docs/MSFT.csv) file):

    Date,Open,High,Low,Close,Volume,Adj Close
    2012-01-27,29.45,29.53,29.17,29.23,44187700,29.23
    2012-01-26,29.61,29.70,29.40,29.50,49102800,29.50
    2012-01-25,29.07,29.65,29.07,29.56,59231700,29.56
    2012-01-24,29.47,29.57,29.18,29.34,51703300,29.34

As usual with CSV files, the first row contains the headers (names of individual columns) 
and the next rows define the data. We can pass reference to the file to `CsvProvider` to 
get a strongly typed view of the file:
*)

type Stocks = CsvProvider<"../docs/MSFT.csv">

(**
The generated type provides two static methods for loading data. The `Parse` method can be
used if we have the data in a `string` value. The `Load` method allows reading the data from
a file or from a web resource. The following sample calls it with a URL that points to 
a live CSV file on the Yahoo finance web site:
*)
 
// Download the stock prices
let msft = Stocks.Load("http://ichart.finance.yahoo.com/table.csv?s=MSFT")

// Look at the most recent row. Note the 'Date' property
// is of type 'DateTime' and 'Open' has a type 'decimal'
let firstRow = msft.Data |> Seq.head
let lastDate = firstRow.Date
let lastOpen = firstRow.Open

// Print the prices in the HLOC format
for row in msft.Data do
  printfn "HLOC: (%A, %A, %A, %A)" row.High row.Low row.Open row.Close

(**
The generated type has a property `Data` that returns the data from the CSV file as a
collection of rows. We iterate over the rows using `for` loop. As you can see the
(generated) type of row has properties such as `High`, `Low` and `Close` that correspond
to the columns in the CSV file.

As you can see, the type provider also infers types of individual rows. The `Date`
property is inferred to be a `DateTime` (because the values in the sample file can all
be parsed as dates) while HLOC prices are inferred as `decimal`.

### Charting stock prices

We can use the `FSharpChart` library to draw a simple line chart showing how the price
of MSFT stocks changes since the company was founded:
*)

// Load the F# chart library
#load "../lib/FSharpChart.fsx"
open System
open Samples.FSharp.Charting

// Visualize the stock prices
[ for row in msft.Data -> row.Date, row.Open ]
|> Chart.FastLine

(**
As a one more example, we use the `Candlestick` chart to get a more detailed look at the
data over the last month:
*)

// Get last months' prices in HLOC format 
let recent = 
  [ for row in msft.Data do
      if row.Date > DateTime.Now.AddDays(-30.0) then
        yield row.Date, row.High, row.Low, row.Open, row.Close ]

// Visualize prices using Candlestick chart
Chart.Candlestick(recent).AndYAxis(Max = 30.0, Min = 25.0)

(**
## Using units of measure

Another interesting feature of the CSV type provider is that it supports F# units of measure.
If the header includes the name or symbol of one of the standard SI units, then the generated type
returns values annotated with the appropriate unit. 

In this section, we use a simple file [`docs/SmallTest.csv`](../docs/SmallTest.csv) which
looks as follows:

    Name,  Distance (metre), Time (s)
    First, 50.0,             3.7

As you can see, the second and third columns are annotated with `metre` and `s`,
respectively. To use units of measure in our code, we need to open the namespace with
standard unit names. Then we pass the `SmallTest.csv` file to the type provider as
a static argument. Also note that in this case we're using the same data at runtime,
so there's no need to use the Load method, we can just call the default constructor.
*)

let small = new CsvProvider<"../docs/SmallTest.csv">()

(**
As in the previous example, the `small` value exposes the rows using the `Data` property.
The generated properties `Distance` and `Time` are now annotated with units. Look at the
following simple calculation:
*)

open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames

for row in small.Data do
  let speed = row.Distance / row.Time
  if speed > 15.0M<metre/second> then 
    printfn "%s (%A m/s)" row.Name speed

(**
The numerical values of `Distance` and `Time` are both inferred as `decimal` (because they
are small enough). Thus the type of `speed` becomes `decimal<meter/second>`. The compiler
can then statically check that we're not comparing incompatible values - e.g. number in
meters per second against a value in kilometres per hour.

## Custom separators and tab-separated files

By default, the CSV type provider uses comma (`,`) as a separator. However, CSV
files sometime use a different separator character than `,`. In some European
countries, ',' is already used as the numeric decimal separator, so a semicolon is used
instead to separate CSV columns. The `CsvProvider` has an optional `Separator` parameter
where you can specify what to use as separator. This means that you can consume
any textual tabular format. Here is an example using `;` as a separator:
*)

let airQuality = new CsvProvider<"../docs/AirQuality.csv", ";">()

for row in airQuality.Data do
  if row.Month > 6 then 
    printfn "Temp: %i Ozone: %f " row.Temp row.Ozone

(**
The air quality dataset used above is used in a lots of samples for the Statistical
Computing language R. A short description of the dataset can be found 
[in the R language manual](http://stat.ethz.ch/R-manual/R-devel/library/datasets/html/airquality.html).

If you are parsing a tab-separated file that uses `\t` as the separator, you can also
specify the separator explicitly. However, if you're using an url or file that has 
the `.tsv` extensions, the type provider will use `\t` by default. In the following example,
we also set `IgnoreErrors` parameter to `true` so that lines with incorrect number of elements
are automatically skipped (the sample file contains additional unstructured data at the end):
*)

let mortalityNy = new CsvProvider<"../docs/MortalityNY.tsv", IgnoreErrors=true>()

// Find the name of a cause based on code
// (Pedal cyclist injured in an accident)
let cause = mortalityNy.Data |> Seq.find (fun r -> 
  r.``Cause of death Code`` = "V13.4")

// Print the number of injured cyclists 
printfn "CAUSE: %s" cause.``Cause of death``
for r in mortalityNy.Data do
  if r.``Cause of death Code`` = "V13.4" then 
    printfn "%s (%d cases)" r.County r.Count

(**

Finally, note that it is also possible to specify multiple different separators
for the `CsvProvider`. This might be useful if a file is irregular and contains 
rows separated by either semicolon or a colon. You can use:
`CsvProvider<"../docs/AirQuality.csv", Separator=";,">`.

## Missing values

It is quite common in statistical datasets for some values to be missing. If
you open the [`docs/AirQuality.csv`](../docs/AirQuality.csv) file you will see
that some values for the Ozone observations are marked `#N/A`. Such values are
parsed as float and will in F# be marked with `Double.NaN`. The values `#N/A`, `NA`,
and `:` are recognized as missing values by default, but you can customize it by specifying
the `MissingValues` parameter of `CsvProvider`.

The following snippet calculates the mean of the ozone observations
excluding the `Double.NaN` values. We first obtain the `Ozone` property for
each row, then remove missing values and then use the standard `Seq.average` function:
*)

let mean = 
  airQuality.Data 
  |> Seq.map (fun row -> row.Ozone) 
  |> Seq.filter (fun elem -> not (Double.IsNaN elem)) 
  |> Seq.average 

(**

## Controlling the type inference

By default, the CSV type provider checks the first 1000 rows to infer the types, but you can customize
it by specifying the `InferRows` parameter of `CsvProvider`. If you specify 0 the entire file will be used.

If in any row a value is missing, the CSV type provider will infer a nullable (for `int` and `int64`) or an optional
(for `bool` and `DateTime`). When a `decimal` would be inferred but there are missing values, we will generate a
`float` instead, and use `Double.NaN` to represent those missing values. The `string` type is already inherently nullable,
so we never generate a string option.

If you prefer an option instead of a nullable or vice versa, or if you want a column to
be a decimal even though all the values would fit in an int, you can override this default behaviour by specifying the types
in the header column between braces, similar to what can be done to specify the units of measure. Valid types are
`int`, `int64`, `bool`, `float`, `decimal`, `date`, `string`, `int?`, `int64?`, `bool?`, `float?`, `decimal?`, `date?`,
`int option`, `int64 option`, `bool option`, `float option`, `decimal option`, and `date option`.

You can also specify
both the type and a unit (e.g `float<metre>`). Example:

    Name,  Distance (decimal?<metre>), Time (float)
    First, 50,                        3

Additionally, you can also specify some of all the types in the `Schema` parameter of `CsvProvider`. Valid formats are:

* `Type`
* `Type<Measure>`
* `Name (Type)`
* `Name (Type<Measure>)`

What's specified in the `Schema` parameter will always take precedence to what's specified in the column headers.

If the first row of the file is not a header row, you can specify the `HasHeaders` parameter as false to
consider that row as a data row. In that case the columns will be named Column1, Column2, etc..., unless the
names are overridden using the `Schema` parameter. Note that you can override only the name in the `Schema` parameter
and still have the provider infer the type for you. Example:
*)

let csv = new CsvProvider<"1,2,3", HasHeaders = false, Schema = "Duration (float<second>),foo,float option">()
for row in csv.Data do
  printfn "%f %d %f" (row.Duration/1.0<second>) row.foo (defaultArg row.Column3 1.0)

(**

## Transforming CSV files

In addition to reading, `CsvProvider` also has support for transforming CSV files. The operations
available are `Filter`, `Take`, `TakeWhile`, `Skip`, `SkipWhile`, and `Truncate`. All these operations
preserve the schema, so after transforming you can save the results by using one of the overloads of
the `Save` method. If you don't need to save the results in the CSV format, or if your transformations
need to change the shape of the data, you can also use the operations available in the `Seq` module on the the 
sequence of rows exposed via the `Data` property directly.
*)

// Saving the first 10 rows that don't have missing values to a new csv file
airQuality.Filter(fun row -> not (Double.IsNaN row.Ozone) && 
                             not (Double.IsNaN row.``Solar.R``))
          .Truncate(10)
          .SaveToString()

(**
For convenience, you can also treat each row as a tuple by using the `AsTuple` property of the RowType.
This is usefull when want to treat different CSV files with a similar schema in a uniform way:
*)

for row in airQuality.Data do
  printfn "%A" row.AsTuple

(**

## Handling big datasets

By default, the rows are cached so you can iterate over the `Data` property multiple times without worrying.
But if you will only iterate once, you can disable caching by settting the `CacheRows` parameter of `CsvProvider`
to false. If the number of rows is very big, you have to do this otherwise you may exhaust the memory.
You can still cache the data at some point by using the `Cache` method, but only do that if you have already
transformed the dataset to be smaller:
*)

let stocks = new CsvProvider<"http://ichart.finance.yahoo.com/table.csv?s=MSFT", CacheRows=false>()
stocks.Take(10).Cache()

(**
## Related articles

 * [F# Data: Type Providers](../fsharpdata.html) - gives more information about other
   type providers in the `FSharp.Data` package.
 * [F# Data: CSV Parser and Reader](CsvFile.html) - provides more information about 
   working with CSV documents dynamically.

*)
