(** 
# F# Data: WorldBank Provider

The [World Bank](http://www.worldbank.org) is an international organization that provides
financial and technical assistance to developing countries around the world. As one of the
activities, the World Bank also collects development indicators and other data about
countries in the world. The [data catalog](http://data.worldbank.org/) contains over
8,000 indicators that can be programmatically accessed.

The WorldBank Type Provider makes the WorldBank data easily accessible to F# programs and
scripts in a type-safe manner. This article provides an introduction. The type provider
is also used on the [Try F#](http://www.tryfsharp.org) web site in the "Data Science" tutorial,
so you can find more examples there.

## Introducing the provider

The following example loads the `FSharp.Data.dll` library (in F# Interactive), 
initializes a connection to the WorldBank using the `GetDataContext` method and then
retrieves the percentage of population who attend universities in the UK:
*)

#r "../../../bin/FSharp.Data.dll"
open FSharp.Data

let data = WorldBankData.GetDataContext()

data
  .Countries.``United Kingdom``
  .Indicators.``School enrollment, tertiary (% gross)``
|> Seq.maxBy fst

(**
When generating the data context, the WorldBank Type Provider retrieves the list of all
countries known to the WorldBank and the list of all supported indicators. Both of these
dimensions are provided as properties, so you can use autocomplete to easily discover
various data sources. Most of the indicators use longer names, so we need to wrap the name
in `\`\``.

The result of the `School enrollment, tertiary (% gross)` property is a sequence with 
values for different years. Using `Seq.maxBy fst` we get the most recent available value.

### Charting World Bank data

We can easily see how the university enrollment changes over time by using the
[FSharp.Charting](http://fsharp.github.io/FSharp.Charting/) library and plotting the data:

*)

#load "../../../packages/FSharp.Charting/FSharp.Charting.fsx"
open FSharp.Charting

(*** define-output:chart1 ***)

data.Countries.``United Kingdom``
    .Indicators.``School enrollment, tertiary (% gross)``
|> Chart.Line

(**
The `Chart.Line` function takes a sequence of pairs containing X and Y values, so we
can call it directly with the World Bank data set using the year as the X value and the
value as a Y value.

*)

(*** include-it:chart1 ***)

(**
## Using World Bank data asynchronously

If you need to download large amounts of data or run the operation without
blocking the caller, then you probably want to use F# asynchronous workflows to perform
the operation. The F# Data Library also provides the `WorldBankDataProvider` type which takes
a number of static parameters. If the `Asynchronous` parameter is set to `true` then the
type provider generates all operations as asynchronous:
*)

type WorldBank = WorldBankDataProvider<"World Development Indicators", Asynchronous=true>
WorldBank.GetDataContext()

(**
The above snippet specified "World Development Indicators" as the name of the data 
source (a collection of commonly available indicators) and it set the optional argument
`Asynchronous` to `true`. As a result, properties such as 
`School enrollment, tertiary (% gross)` will now have a type `Async<(int * int)[]>` meaning
that they represent an asynchronous computation that can be started and will eventually
produce the data.

### Downloading data in parallel

To demonstrate the asynchronous version of the type provider, let's write code that
downloads the university enrollment data about a number of countries in parallel.
We first create a data context and then define an array with some countries we want to 
process:
*)

let wb = WorldBank.GetDataContext()

// Create a list of countries to process
let countries = 
 [| wb.Countries.``Arab World``
    wb.Countries.``European Union``
    wb.Countries.Australia
    wb.Countries.Brazil
    wb.Countries.Canada
    wb.Countries.Chile
    wb.Countries.``Czech Republic``
    wb.Countries.Denmark
    wb.Countries.France
    wb.Countries.Greece
    wb.Countries.``Low income``
    wb.Countries.``High income``
    wb.Countries.``United Kingdom``
    wb.Countries.``United States`` |]

(**
To download the information in parallel, we can create a list of asynchronous 
computations, compose them using `Async.Parallel` and then run the (single) obtained 
computation to perform all the downloads:
*)

(*** define-output:chart2 ***)

[ for c in countries ->
    c.Indicators.``School enrollment, tertiary (% gross)`` ]
|> Async.Parallel
|> Async.RunSynchronously
|> Array.map Chart.Line
|> Chart.Combine

(**
The above snippet does not just download the data using `Async.RunSynchronously`, but
it also turns every single downloaded data set into a line chart (using `Chart.Line`) 
and then creates a single composed chart using `Chart.Combine`.

*)

(*** include-it:chart2 ***)

(**
## Related articles

 * [Try F#: Data Science](http://www.tryfsharp.org/Learn/data-science) - The Data Science
   tutorial on Try F# uses the WorldBank type provider in numerous examples.
 * [API Reference: WorldBankDataProvider type provider](../reference/fsharp-data-worldbankdataprovider.html)

*)
