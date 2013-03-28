(** 
# F# Data: CSV Type Provider

This article demonstrates how to use the CSV type provider to read CSV files
in a statically typed way. We look how to download stock prices from the Yahoo
Finance web site and then also look how the type provider supports units of measure.
The type provider is based on the same code as the one used on [Try F#](http://www.tryfsharp.org)
web site in the "Financial Computing" tutorial, so you can find additional examples there.

The CSV type provider takes a sample CSV file as an input and generates
a type based on the first (header) row. The types are inferred from either the
entire document or from first few rows.

## Introducing the provider

The type provider is located in the `FSharp.Data.dll` assembly. Assuming the assembly 
is located in the `../bin` directory, we can load it in F# Interactive as follows
(to get CSV files from file system and the internet, we also open `System.IO` and
`System.Net`):
*)

#r "../../bin/FSharp.Data.dll"
open FSharp.Data

(**

### Parsing stock prices

The Yahoo Finance web site provides daily stock prices in a CSV format that has the
following structure (you can find a larger example in the [`docs/MSFT.csv`](docs/MSFT.csv) file):

    Date,Open,High,Low,Close,Volume,Adj Close
    2012-01-27,29.45,29.53,29.17,29.23,44187700,29.23
    2012-01-26,29.61,29.70,29.40,29.50,49102800,29.50
    2012-01-25,29.07,29.65,29.07,29.56,59231700,29.56
    2012-01-24,29.47,29.57,29.18,29.34,51703300,29.34

As usual with CSV files, the first row contains the headers (names of individial columns) 
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
property is infered to be a `DateTime` (because the values in the sample file can all
be parsed as dates) while HLOC prices are infered as `decimal`.

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
If the header includes the name of one of the standard SI units, then the generated type
returns values annotated with the appropriate unit. 

In this section, we use a simple file [`docs/SmallTest.csv`](docs/SmallTest.csv) which
looks as follows:

    Name,  Distance (metre), Time (second)
    First, 50.0,             3.7

As you can see, the second and third columns are annotated with `metre` and `second`,
respectively. To use units of measure in our code, we need to open the namespace with
standard unit names. Then we pass the `SmallTest.csv` file to the type provider as
a static argument. Also note that in this case we're using the same data at runtime,
so there's no need to use the Load method, we can just call the default constructor.
*)
open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames

let small = new CsvProvider<"../docs/SmallTest.csv">()

(**
As in the previous example the `small` value exposes the rows using the `Data` property.
The generated properties `Distance` and `Time` are now annotated with units. Look at the
following simple calculation:
*)

for row in small.Data do
  let speed = row.Distance / row.Time
  if speed > 15.0M<meter/second> then 
    printfn "%s (%A m/s)" row.Name speed

(**
The numerical values of `Distance` and `Time` are both inferred as `decimal` (because they
are small enough). Thus the type of `speed` becomes `decimal<meter/second>`. The compiler
can then statically check that we're not comparing incompatible values - e.g. number in
meters per second against a value in kilometers per hour.
*)

(**
## Using custom separators
By default, the CSV type provider uses comma (`,`) as a separator. However, CSV
files sometime use a different separator character than `,`. In some European
countries a semicolon is used. The `CsvProvider` has an optional parameter where you can 
specify what to use as separator:
*)

let airQuality = new CsvProvider<"../docs/AirQuality.csv", ";">()

(**
The air quality dataset used above is used in a lots of samples for the Statistical
Computing language R. A short description of the dataset can be found 
[in the R language manual](http://stat.ethz.ch/R-manual/R-devel/library/datasets/html/airquality.html).

It is quite common for statistical datasets that some values are missing. If
you open the [`docs/AirQuality.csv`](docs/AirQuality.csv) file you will see
that some values for the Ozone observations are marked `#N/A`. Such values are
parsed as float and will in F# be marked with `Double.NaN`.
*)
for row in airQuality.Data do
  if row.Month > 6 then 
    printfn "Temp: %i Ozone: %f " row.Temp row.Ozone

(** 
The following snippet calculates the mean of the ozone observations
excluding the `Double.NaN` values. We first obtain the `Ozone` property for
each row, then remove missing values and then use the standard `Seq.average` function:
*)

let mean = 
  airQuality.Data 
  |> Seq.map (fun row -> row.Ozone) 
  |> Seq.filter (fun elem -> not (System.Double.IsNaN(elem))) 
  |> Seq.average 

(**
Finally, note that it is also possible to specify multiple different separators
for the `CsvProvider`. This might be useful if a file is irregular and contains 
rows separated by either semicolon or a colon. You can use:
`CsvProvider<"../docs/AirQuality.csv", Separator=";,">`.

## Related articles

 * [F# Data: Type Providers](../fsharpdata.html) - gives more information about other
   type providers in the `FSharp.Data` package.
*)


