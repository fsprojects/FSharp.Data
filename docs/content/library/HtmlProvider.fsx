(** 
# F# Data: Html Type Provider

This article demonstrates how to use the Html type provider to read Html tables files
in a statically typed way. 

The Html type provider takes a sample Html document as input and generates a type based on the data
present on the columns of that sample. The column names are obtained from the first (header) row.

## Introducing the provider

The type provider is located in the `FSharp.Data.dll` assembly. Assuming the assembly 
is located in the `../../../bin` directory, we can load it in F# Interactive as follows:
*)

#r "../../../bin/FSharp.Data.dll"
open FSharp.Data

(**

### Parsing Power Market Data

The Elexon - BM Reports website provides market data about the U.K's current power system. For simplicty an example of this data below is shown in a CSV format,
 (you can see an example of the raw html document this data was extracted from in [`data/MarketDepth.htm`](../data/MarketDepth.htm)):

    [lang=text]
    Settlement Day,Period,IMBALNGC,Offer Volume Bid Volume,Accepted Offer Vol,Accepted Bid Vol,UAOV,UABV,PAOV,PABV
    2014-01-14,1,877.000,52378.500,-53779.500,348.200,-654.374,0.000,0.000,348.200,-654.374 
    2014-01-14,2,196.000,52598.000,-53559.500,349.601,-310.862,0.000,0.000,316.701,-310.862 
    2014-01-14,3,-190.000,52575.000,-53283.500,186.183,-2.426,0.000,0.000,162.767,-1.917 
    2014-01-14,4,-61.000,52576.000,-53454.500,18.000,-24.158,0.000,0.000,18.000,-24.158 


Usually with HTML files headers are demarked by using the <th> tag, however in this file this is not the case, so the provider assumes that the
first row is headers. (This behaviour is likely to get smarer in later releases). But it highlights a general problem about HTML's strictness. 
*)

type MarketDepth = HtmlProvider<"../data/MarketDepth.htm">

(**
The generated type provides a type space of tables that it has managed to parse out of the given HTML Document.
Each types name is derived from either the id, title, name, summary or caption attributes/tags provided. If none of these 
entities exist then the table will simply be named `Table_xx` where xx is the position in the HTML doc if all of the tables were flatterned out into a list.
The `Load` method allows reading the data from a file or from a web resource. We could also have used a web url instead of a local file in the sample parameter of the type provider.
The following sample calls the `Load` method with an URL that points to a live market depth servlet on the BM Reports website.
*)
 
// Download the latest market depth information
let mrktDepth = MarketDepth.Load("http://www.bmreports.com/servlet/com.logica.neta.bwp_MarketDepthServlet").Tables.Table3

// Look at the most recent row. Note the 'Date' property
// is of type 'DateTime' and 'Open' has a type 'decimal'
let firstRow = mrktDepth.Rows |> Seq.head
let settlementDate = firstRow.``Settlement Day``
let acceptedBid = firstRow.``Accepted Bid Vol``
let acceptedOffer = firstRow.``Accepted Offer Vol``

// Print the bid / offer volumes for each row
for row in mrktDepth.Rows do
  printfn "Bid/Offer: (%A, %A, %A)" row.``Settlement Day`` row.``Bid Volume`` row.``Offer Volume``

(**
The generated type has a property `Rows` that returns the data from the HTML file as a
collection of rows. We iterate over the rows using a `for` loop. As you can see the
(generated) type for rows has properties such as `Settlement Day`, `Bid Volume` and `Offer Volume` that correspond
to the columns in the selected HTML table file.

As you can see, the type provider also infers types of individual rows. The `Date`
property is inferred to be a `DateTime` (because the values in the sample file can all
be parsed as dates) while other columns are inferred as `decimal` or `float`.
*)

(**

### Parsing Nuget package stats

This small sample shows how the HTML type provider can be used to scrape data from a website. In this case we analyze the download counts of the FSharp.Data package on nuget.

*)

open System

// helper function to analyze version numbers from nuget
let getMinorVersion (v:string) =
  try
      let parts' = v.Replace("(this version)","").Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
      let parts = parts' |> Seq.last |> fun x -> x.Split([|'.'|])
      parts.[0] + "." + parts.[1]
  with 
  | exn -> v

// Load the FSharp.Charting library
#load "../../../packages/FSharp.Charting.0.90.5/FSharp.Charting.fsx"
open FSharp.Charting

// Configure the type provider
type NugetStats = HtmlProvider<"https://www.nuget.org/packages/FSharp.Data">

// load the live package stats for FSharp.Data
let rawStats = NugetStats().Tables.Table0

// group by minor version and calculate download count
let stats = 
    rawStats.Rows
    |> Seq.groupBy (fun r -> getMinorVersion r.Version)
    |> Seq.map (fun (k,xs) -> k,xs |> Seq.sumBy (fun x -> x.Downloads))

// Visualize the package stats
Chart.Bar stats

(**

The output looks like:

![alt text](../images/downloads.png "Nuget package stats for FSharp.Data")

*)
(**
## Related articles

 * [F# Data: Type Providers](../fsharpdata.html) - gives more information about other
   type providers in the `FSharp.Data` package.
 * [F# Data: CSV Parser and Reader](CsvFile.html) - provides more information about 
   working with CSV documents dynamically.

*)
