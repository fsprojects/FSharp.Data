(**
---
category: Type Providers
categoryindex: 1
index: 1
---
*)
(*** condition: prepare ***)
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Runtime.Utilities.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Csv.Core.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.dll"
(*** condition: fsx ***)
#if FSX
#r "nuget: FSharp.Data,{{fsdocs-package-version}}"
#endif
(*** condition: ipynb ***)
#if IPYNB
#r "nuget: FSharp.Data,{{fsdocs-package-version}}"

Formatter.SetPreferredMimeTypesFor(typeof<obj>, "text/plain")
Formatter.Register(fun (x: obj) (writer: TextWriter) -> fprintfn writer "%120A" x)
#endif
(**

[![Binder](../img/badge-binder.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Data/gh-pages?filepath={{fsdocs-source-basename}}.ipynb)&emsp;
[![Script](../img/badge-script.svg)]({{root}}/{{fsdocs-source-basename}}.fsx)&emsp;
[![Notebook](../img/badge-notebook.svg)]({{root}}/{{fsdocs-source-basename}}.ipynb)

# CSV Type Provider

This article demonstrates how to use the CSV type provider to read CSV files
in a statically typed way.

The CSV type provider takes a sample CSV as input and generates a type based on the data
present on the columns of that sample. The column names are obtained from the first
(header) row, and the types are inferred from the values present on the subsequent rows.

## Introducing the provider

<div class="container-fluid" style="margin:15px 0px 15px 0px;">
    <div class="row-fluid">
        <div class="span1"></div>
        <div class="span10" id="anim-holder">
            <a id="lnk" href="../images/csv.gif"><img id="anim" src="../images/csv.gif" /></a>
        </div>
        <div class="span1"></div>
    </div>
</div>

The type provider is located in the `FSharp.Data.dll` assembly. Assuming the package is referenced
we can access its namespace as follows:
*)

open FSharp.Data

(**

### Parsing stock prices

The Yahoo Finance web site provides daily stock prices in a CSV format that has the
following structure (you can find a larger example in the [`data/MSFT.csv`](../data/MSFT.csv) file):

    [lang=text]
    Date,Open,High,Low,Close,Volume,Adj Close
    2012-01-27,29.45,29.53,29.17,29.23,44187700,29.23
    2012-01-26,29.61,29.70,29.40,29.50,49102800,29.50
    2012-01-25,29.07,29.65,29.07,29.56,59231700,29.56
    2012-01-24,29.47,29.57,29.18,29.34,51703300,29.34

As usual with CSV files, the first row contains the headers (names of individual columns)
and the next rows define the data. We can pass reference to the file to `cref:T:FSharp.Data.CsvProvider` to
get a strongly typed view of the file:
*)

[<Literal>]
let ResolutionFolder = __SOURCE_DIRECTORY__

type Stocks = CsvProvider<"../data/MSFT.csv", ResolutionFolder=ResolutionFolder>

(**
The generated type provides two static methods for loading data. The `Parse` method can be
used if we have the data in a `string` value. The `Load` method allows reading the data from
a file or from a web resource (and there's also an asynchronous `AsyncLoad` version). We could also
have used a web URL instead of a local file in the sample parameter of the type provider.
The following sample calls the `Load` method with an URL that points to a live CSV file on the Yahoo finance web site:
*)

// Download the stock prices
let msft = Stocks.Load(__SOURCE_DIRECTORY__ + "/../data/MSFT.csv").Cache()

// Look at the most recent row. Note the 'Date' property
// is of type 'DateTime' by default. Set PreferDateOnly = true
// to use 'DateOnly' on .NET 6+
// and 'Open' has a type 'decimal'
let firstRow = msft.Rows |> Seq.head
let lastDate = firstRow.Date
let lastOpen = firstRow.Open

// Print the first 10 prices in the HLOC format
for row in msft.Rows |> Seq.truncate 10 do
    printfn "HLOC: (%A, %A, %A, %A)" row.High row.Low row.Open row.Close

(*** include-fsi-merged-output ***)

(**
The generated type has a property `Rows` that returns the data from the CSV file as a
collection of rows. We iterate over the rows using a `for` loop. As you can see the
(generated) type for rows has properties such as `High`, `Low` and `Close` that correspond
to the columns in the CSV file.

As you can see, the type provider also infers types of individual rows. The `Date`
property is inferred as `DateTime` by default. When you set `PreferDateOnly = true`
on .NET 6 and later, date-only strings (without a time component) are inferred as `DateOnly`.
HLOC prices are inferred as `decimal`.

## Using units of measure

The CSV type provider supports F# units of measure: if the header includes the name or symbol of one of the standard SI units, then the generated type
returns values annotated with the appropriate unit.

In this section, we use a simple file [`data/SmallTest.csv`](../data/SmallTest.csv) which
looks as follows:

    [lang=text]
    Name,  Distance (metre), Time (s)
    First, 50.0,             3.7

As you can see, the second and third columns are annotated with `metre` and `s`,
respectively. To use units of measure in our code, we need to open the namespace with
standard unit names. Then, we pass the `SmallTest.csv` file to the type provider as
a static argument. Also, note that in this case, we're using the same data at runtime,
so we use the `GetSample` method instead of calling `Load` and passing the same parameter again.
*)

let small =
    CsvProvider<"../data/SmallTest.csv", ResolutionFolder=ResolutionFolder>.GetSample()

(*** include-fsi-merged-output ***)

(**
We can also use the default constructor instead of the `GetSample` static method:
*)

let small2 =
    new CsvProvider<"../data/SmallTest.csv", ResolutionFolder=ResolutionFolder>()

(*** include-fsi-merged-output ***)

(**
but the VisualStudio IntelliSense for the type provider parameters doesn't work when we use a default
constructor for a type provider, so we'll keep using `GetSample` instead.

As in the previous example, the `small` value exposes the rows using the `Rows` property.
The generated properties `Distance` and `Time` are now annotated with units. Look at the
following simple calculation:
*)

open FSharp.Data.UnitSystems.SI.UnitNames

for row in small.Rows do
    let speed = row.Distance / row.Time

    if speed > 15.0M<metre / second> then
        printfn "%s (%A m/s)" row.Name speed

(*** include-fsi-merged-output ***)

(**
The numerical values of `Distance` and `Time` are both inferred as `decimal` (because they
are small enough). Thus the type of `speed` becomes `decimal<metre/second>`. The compiler
can then statically check that we're not comparing incompatible values - e.g. number in
meters per second against a value in kilometres per hour.

## Custom separators and tab-separated files

By default, the CSV type provider uses a comma (`,`) as a separator. However, CSV
files sometimes use a different separator character than `,`. In some European
countries, `,` is already used as the numeric decimal separator, so a semicolon (`;`) is used
instead to separate CSV columns. The `CsvProvider` has an optional `Separators` static parameter
where you can specify what to use as a separator. This means that you can consume
any textual tabular format. Here is an example using `;` as a separator:
*)

type AirQuality = CsvProvider<"../data/AirQuality.csv", ";", ResolutionFolder=ResolutionFolder>

let airQuality = new AirQuality()

for row in airQuality.Rows |> Seq.truncate 10 do
    if row.Month > 6 then
        printfn "Temp: %i Ozone: %f " row.Temp row.Ozone

(*** include-fsi-merged-output ***)

(**
The air quality dataset ([`data/AirQuality.csv`](../data/AirQuality.csv)) is used in many
samples for the Statistical Computing language R. A short description of the dataset can be found
[in the R language manual](http://stat.ethz.ch/R-manual/R-devel/library/datasets/html/airquality.html).

If you are parsing a tab-separated file that uses `\t` as the separator, you can also
specify the separator explicitly. However, if you're using an url or file that has
the `.tsv` extension, the type provider will use `\t` by default. In the following example,
we also set `IgnoreErrors` static parameter to `true` so that lines with an incorrect number of elements
are automatically skipped (the sample file ([`data/MortalityNY.csv`](../data/MortalityNY.tsv)) contains additional unstructured data at the end):
*)

let mortalityNy =
    CsvProvider<"../data/MortalityNY.tsv", IgnoreErrors=true, ResolutionFolder=ResolutionFolder>.GetSample()

// Find the name of a cause based on code
// (Pedal cyclist injured in an accident)
let cause =
    mortalityNy.Rows |> Seq.find (fun r -> r.``Cause of death Code`` = "V13.4")

// Print the number of injured cyclists
printfn "CAUSE: %s" cause.``Cause of death``

for r in mortalityNy.Rows do
    if r.``Cause of death Code`` = "V13.4" then
        printfn "%s (%d cases)" r.County r.Count

(*** include-fsi-merged-output ***)

(**

Finally, note that it is also possible to specify multiple different separators
for the `CsvProvider`. This might be useful if a file is irregular and contains
rows separated by either a semicolon or a colon. You can use:
`CsvProvider<"../data/AirQuality.csv", Separators=";,", ResolutionFolder=ResolutionFolder>`.

## Missing values

It is quite common in statistical datasets for some values to be missing. If
you open the [`data/AirQuality.csv`](../data/AirQuality.csv) file, you will see
that some values for the ozone observations are marked `#N/A`. Such values are
parsed as float and will be marked with `Double.NaN` in F#. The values
`NaN`, `NA`, `N/A`, `#N/A`, `:`, `-`, `TBA`, and `TBD`
are recognized as missing values by default, but you can customize it by specifying
the `MissingValues` static parameter of `CsvProvider` as a comma-separated string.
For example, to ignore `this` and `that` we could do:
*)

CsvProvider<"X,Y,Z\nthis,that,1.0", MissingValues="this,that">.GetSample().Rows
(*** include-fsi-merged-output ***)
(**

The following snippet calculates the mean of the ozone observations
excluding the `Double.NaN` values. We first obtain the `Ozone` property for
each row, then remove missing values and then use the standard `Seq.average` function:
*)
open System

let mean =
    airQuality.Rows
    |> Seq.toArray
    |> Array.map (fun row -> row.Ozone)
    |> Array.filter (fun elem -> not (Double.IsNaN elem))
    |> Array.average

(*** include-fsi-merged-output ***)

(**

If the sample doesn't have missing values on all columns, but at runtime missing values could
appear anywhere, you can set the static parameter `AssumeMissingValues` to `true` in order to force `CsvProvider`
to assume missing values can occur in any column.

## Controlling the column types

By default, the CSV type provider checks the first 1000 rows to infer the types, but you can customize
it by specifying the `InferRows` static parameter of `CsvProvider`. If you specify 0 the entire file will be used.

Columns with only `0`, `1`, `Yes`, `No`, `True`, or `False` will be set to `bool`. Columns with numerical values
will be set to either `int`, `int64`, `decimal`, or `float`, in that order of preference.

On .NET 6 and later, when you set `PreferDateOnly = true`, columns whose values are all date-only strings (without a time component, e.g. `2023-01-15`)
are inferred as `DateOnly`. Time-only strings are inferred as `TimeOnly`. If a column mixes `DateOnly` and `DateTime` values, it is unified to `DateTime`.
By default (`PreferDateOnly = false`), all date values are inferred as `DateTime` for backward compatibility.

If a value is missing in any row, by default the CSV type provider will infer a nullable (for `int`, `int64`, and `DateOnly`) or an optional
(for `bool`, `DateTime`, `DateTimeOffset`, and `Guid`). When a `decimal` would be inferred but there are missing values, we will infer a
`float` instead, and use `Double.NaN` to represent those missing values. The `string` type is already inherently nullable,
so by default, we won't generate a `string option`. If you prefer to use optionals in all cases, you can set the static parameter
`PreferOptionals` to `true`. In that case, you'll never get an empty string or a `Double.NaN` and will always get a `None` instead.

If you have other preferences, e.g. if you want a column to be a `float` instead of a `decimal`,
you can override the default behaviour by specifying the types in the header column between braces, similar to what can be done to
specify the units of measure. This will override both `AssumeMissingValues` and `PreferOptionals`. The valid types are:

* `int`
* `int?`
* `int option`
* `int64`
* `int64?`
* `int64 option`
* `bool`
* `bool?`
* `bool option`
* `float`
* `float?`
* `float option`
* `decimal`
* `decimal?`
* `decimal option`
* `date`
* `date?`
* `date option`
* `datetimeoffset`
* `datetimeoffset?`
* `datetimeoffset option`
* `guid`
* `guid?`
* `guid option`
* `dateonly` (.NET 6+ only)
* `dateonly?` (.NET 6+ only)
* `dateonly option` (.NET 6+ only)
* `timeonly` (.NET 6+ only)
* `timeonly?` (.NET 6+ only)
* `timeonly option` (.NET 6+ only)
* `string`
* `string option`.

You can also specify both the type and a unit (e.g `float<metre>`). Example:

    [lang=text]
    Name,  Distance (decimal?<metre>), Time (float)
    First, 50,                        3

Additionally, you can also specify some or all the types in the `Schema` static parameter of `CsvProvider`. Valid formats are:

* `Type`
* `Type<Measure>`
* `Name (Type)`
* `Name (Type<Measure>)`

What's specified in the `Schema` static parameter will always take precedence to what's specified in the column headers.

If the first row of the file is not a header row, you can specify the `HasHeaders` static parameter to `false` in order to
consider that row as a data row. In that case, the columns will be named `Column1`, `Column2`, etc..., unless the
names are overridden using the `Schema` parameter. Note that you can override only the name in the `Schema` parameter
and still have the provider infer the type for you. Example:
*)
type OneTwoThree = CsvProvider<"1,2,3", HasHeaders=false, Schema="Duration (float<second>),foo,float option">

let csv = OneTwoThree.GetSample()

for row in csv.Rows do
    printfn "%f %d %f" (row.Duration / 1.0<second>) row.Foo (defaultArg row.Column3 1.0)

(*** include-fsi-merged-output ***)

(**

You don't need to override all the columns, you can skip the ones to leave as default.
For example, in the titanic training dataset from Kaggle ([`data/Titanic.csv`](../data/Titanic.csv)),
if you want to rename the 3rd column (the `PClass` column) to `Passenger Class` and override the
6th column (the `Fare` column) to be a `float` instead of a `decimal`, you can define only that, and leave
the other columns as blank in the schema (you also don't need to add all the trailing commas).

*)
type Titanic1 =
    CsvProvider<"../data/Titanic.csv", Schema=",,Passenger Class,,,float", ResolutionFolder=ResolutionFolder>

let titanic1 = Titanic1.GetSample()

for row in titanic1.Rows |> Seq.truncate 10 do
    printfn "%s Class = %d Fare = %g" row.Name row.``Passenger Class`` row.Fare

(*** include-fsi-merged-output ***)

(**

Alternatively, you can rename and override the type of any column by name instead of by position:

*)
type Titanic2 =
    CsvProvider<"../data/Titanic.csv", Schema="Fare=float,PClass->Passenger Class", ResolutionFolder=ResolutionFolder>

let titanic2 = Titanic2.GetSample()

for row in titanic2.Rows |> Seq.truncate 10 do
    printfn "%s Class = %d Fare = %g" row.Name row.``Passenger Class`` row.Fare

(*** include-fsi-merged-output ***)

(**

You can even mix and match the two syntaxes like this `Schema="int64,DidSurvive,PClass->Passenger Class=string"`

### DateOnly and TimeOnly (on .NET 6+)

On .NET 6 and later, when you set `PreferDateOnly = true`, date-only strings are inferred as `DateOnly`
and time-only strings as `TimeOnly`. For example, a column like `EventDate` containing values such as
`2023-06-01` will be given the type `DateOnly`. By default (`PreferDateOnly = false`), dates are
inferred as `DateTime` for backward compatibility.

You can also explicitly request a `DateOnly` or `TimeOnly` column using schema annotations:

    [lang=text]
    EventDate (dateonly),Duration (timeonly?)
    2023-06-01,08:30:00
    2023-06-02,

In the example above, `EventDate` is explicitly annotated as `dateonly` and `Duration` is explicitly
annotated as `timeonly?` (a nullable `TimeOnly`).

## Transforming CSV files

In addition to reading, `CsvProvider` also has support for transforming the row collection of CSV files. The operations
available are `Filter`, `Take`, `TakeWhile`, `Skip`, `SkipWhile`, and `Truncate`. All these operations
preserve the schema, so after transforming, you can save the results by using one of the overloads of
the `Save` method. You can also use the `SaveToString()` to get the output directly as a string.

*)

// Saving the first 10 rows that don't have missing values to a new csv file
airQuality
    .Filter(fun row -> not (Double.IsNaN row.Ozone) && not (Double.IsNaN row.``Solar.R``))
    .Truncate(10)
    .SaveToString()

(*** include-fsi-merged-output ***)

(**

It's also possible to transform the columns themselves by using `Map` and the constructor for the `Row` type.

*)

let doubleOzone =
    airQuality.Map(fun row -> AirQuality.Row(row.Ozone * 2.0, row.``Solar.R``, row.Wind, row.Temp, row.Month, row.Day))

(*** include-fsi-merged-output ***)

(**

You can also append new rows, either by creating them directly as in the previous example, or by parsing them from a string.

*)

let newRows =
    AirQuality.ParseRows(
        """41;190;7.4;67;5;1
        36;118;8;72;5;2"""
    )

let airQualityWithExtraRows = airQuality.Append newRows

(*** include-fsi-merged-output ***)

(**

It's even possible to create csv files without parsing at all:

*)

type MyCsvType = CsvProvider<Schema="A (int), B (string), C (date option)", HasHeaders=false>

let myRows =
    [ MyCsvType.Row(1, "a", None); MyCsvType.Row(2, "B", Some System.DateTime.Now) ]

let myCsv = new MyCsvType(myRows)
myCsv.SaveToString()

(*** include-fsi-merged-output ***)

(**

## Handling big datasets

By default, the rows are cached so you can iterate over the `Rows` property multiple times without worrying.
But if you will only iterate once, you can disable caching by setting the `CacheRows` static parameter of `CsvProvider`
to `false`. If the number of rows is very big, you have to do this otherwise you may exhaust the memory.
You can still cache the data at some point by using the `Cache` method, but only do that if you have already
transformed the dataset to be smaller.
*)

(**
## Related articles

 * [Using JSON provider in a library](JsonProvider.html#jsonlib) also applies to CSV type provider
 * [CSV Parser](CsvFile.html) - provides more information about
   working with CSV documents dynamically.
 * [API Reference: CsvProvider type provider](../reference/fsharp-data-csvprovider.html)

*)
