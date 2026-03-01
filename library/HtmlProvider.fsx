(**

*)
#r "nuget: FSharp.Data,8.1.0-beta"
#endif
(**
[![Binder](../img/badge-binder.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Data/gh-pages?filepath=library/HtmlProvider.ipynb)&emsp;
[![Script](../img/badge-script.svg)](https://fsprojects.github.io/FSharp.Data//library/HtmlProvider.fsx)&emsp;
[![Notebook](../img/badge-notebook.svg)](https://fsprojects.github.io/FSharp.Data//library/HtmlProvider.ipynb)

# HTML Type Provider

This article demonstrates how to use the HTML type provider to read HTML tables files
in a statically typed way.

The HTML Type Provider takes a sample HTML document as input and generates a type based on the data
present in the columns of that sample. The column names are obtained from the first (header) row.

## Introducing the provider

The type provider is located in the `FSharp.Data.dll` assembly. Assuming the assembly
is located in the `../../../bin` directory, we can load it in F# Interactive as follows:

*)
open FSharp.Data
(**
### Parsing F1 Calendar Data

This example shows an example of using the HTML Type Provider to extract each row from a table on a Wikipedia page.

Usually with HTML files headers are demarked by using the `<th>` tag, however this is not true in general, so the provider assumes that the
first row is headers. (This behaviour is likely to get smarter in later releases). But it highlights a general problem about HTML's strictness.

*)
[<Literal>]
let F1_2017_URL =
    "https://en.wikipedia.org/wiki/2017_FIA_Formula_One_World_Championship"

type F1_2017 = HtmlProvider<F1_2017_URL>
(**
The generated type provides a type space of tables that it has managed to parse out of the given HTML Document.
Each type's name is derived from either the id, title, name, summary or caption attributes/tags provided. If none of these
entities exist then the table will simply be named `Tablexx` where xx is the position in the HTML document if all of the tables were flattened out into a list.
The `Load` method allows reading the data from a file or web resource. We could also have used a web URL instead of a local file in the sample parameter of the type provider.
The following sample calls the `Load` method with an URL that points to a live version of the same page on Wikipedia.

*)
// Download the table for the 2017 F1 calendar from Wikipedia
let f1Calendar = F1_2017.Load(F1_2017_URL).Tables.Calendar

// Look at the top row, being the first race of the calendar
let firstRow = f1Calendar.Rows |> Seq.head
let round = firstRow.Round
let grandPrix = firstRow.``Grand Prix``
let date = firstRow.Date

// Print the round, location and date for each race, corresponding to a row
for row in f1Calendar.Rows do
    printfn "Race, round %A is hosted at %A on %A" row.Round row.``Grand Prix`` row.Date(* output: 
Race, round "1" is hosted at "Australian Grand Prix" on "26 March"
Race, round "2" is hosted at "Chinese Grand Prix" on "9 April"
Race, round "3" is hosted at "Bahrain Grand Prix" on "16 April"
Race, round "4" is hosted at "Russian Grand Prix" on "30 April"
Race, round "5" is hosted at "Spanish Grand Prix" on "14 May"
Race, round "6" is hosted at "Monaco Grand Prix" on "28 May"
Race, round "7" is hosted at "Canadian Grand Prix" on "11 June"
Race, round "8" is hosted at "Azerbaijan Grand Prix" on "25 June"
Race, round "9" is hosted at "Austrian Grand Prix" on "9 July"
Race, round "10" is hosted at "British Grand Prix" on "16 July"
Race, round "11" is hosted at "Hungarian Grand Prix" on "30 July"
Race, round "12" is hosted at "Belgian Grand Prix" on "27 August"
Race, round "13" is hosted at "Italian Grand Prix" on "3 September"
Race, round "14" is hosted at "Singapore Grand Prix" on "17 September"
Race, round "15" is hosted at "Malaysian Grand Prix" on "1 October"
Race, round "16" is hosted at "Japanese Grand Prix" on "8 October"
Race, round "17" is hosted at "United States Grand Prix" on "22 October"
Race, round "18" is hosted at "Mexican Grand Prix" on "29 October"
Race, round "19" is hosted at "Brazilian Grand Prix" on "12 November"
Race, round "20" is hosted at "Abu Dhabi Grand Prix" on "26 November"
Race, round "Source: [63]" is hosted at "Source: [63]" on "Source: [63]"
val f1Calendar: HtmlProvider<...>.Calendar
val firstRow: HtmlProvider<...>.Calendar.Row =
  ("1", "Australian Grand Prix", "Albert Park Circuit, Melbourne", "26 March")
val round: string = "1"
val grandPrix: string = "Australian Grand Prix"
val date: string = "26 March"
val it: unit = ()*)
(**
The generated type has a property `Rows` that returns the data from the HTML file as a
collection of rows. We iterate over the rows using a `for` loop. As you can see the
(generated) type for rows has properties such as `Grand Prix`, `Circuit`, `Round` and `Date` that correspond
to the columns in the selected HTML table file.

As you can see, the type provider also infers types of individual rows. The `Date`
property is inferred to be a `DateTime` (because the values in the sample file can all
be parsed as dates) while other columns are inferred as the correct type where possible.

### Parsing Nuget package stats

This small sample shows how the HTML Type Provider can be used to scrape data from a website. In this example, we analyze the download counts of the FSharp.Data package on NuGet.
Note that we're using the live URL as the sample, so we can just use the default constructor as the runtime data will be the same as the compile time data.

*)
// Configure the type provider
type NugetStats = HtmlProvider<"https://www.nuget.org/packages/FSharp.Data">

// load the live package stats for FSharp.Data
let rawStats = NugetStats().Tables.``Version History of FSharp.Data``

// helper function to analyze version numbers from Nuget
let getMinorVersion (v: string) =
    System.Text.RegularExpressions.Regex(@"\d.\d").Match(v).Value

// group by minor version and calculate the download count
let stats =
    rawStats.Rows
    |> Seq.groupBy (fun r -> getMinorVersion r.Version)
    |> Seq.map (fun (k, xs) -> k, xs |> Seq.sumBy (fun x -> x.Downloads))
    |> Seq.toArray(* output: 
type NugetStats = HtmlProvider<...>
val rawStats: HtmlProvider<...>.VersionHistoryOfFSharpData
val getMinorVersion: v: string -> string
val stats: (string * decimal) array =
  [|("8.1", 70M); ("8.0", 1550M); ("7.0", 2481M); ("6.7", 472M);
    ("6.6", 414284M); ("6.5", 4331M); ("6.4", 771781M); ("6.3", 448164M);
    ("6.2", 173043M); ("6.1", 3404M); ("6.0", 19559M); ("5.0", 541257M);
    ("4.2", 556643M)|]*)
(**
### Getting statistics on Doctor Who

This sample shows some more screen scraping from Wikipedia:

*)
[<Literal>]
let DrWho =
    "https://en.wikipedia.org/wiki/List_of_Doctor_Who_episodes_(1963%E2%80%931989)"

let doctorWho = new HtmlProvider<DrWho>()

// Get the average number of viewers for each doctor's series run
let viewersByDoctor =
    doctorWho.Tables.``Season 1 (1963-1964)``.Rows
    |> Seq.groupBy (fun season -> season.``Directed by``)
    |> Seq.map (fun (doctor, seasons) ->
        let averaged =
            seasons |> Seq.averageBy (fun season -> season.``UK viewers (millions)``)

        doctor, averaged)
    |> Seq.toArray(* output: 
[<Literal>]
val DrWho: string
  =
  "https://en.wikipedia.org/wiki/List_of_Doctor_Who_episodes_(1963%E2%80%931989)"
val doctorWho: HtmlProvider<...>
val viewersByDoctor: (string * float) array =
  [|("Waris Hussein", 8.0); ("", nan); ("Christopher Barry", 8.275);
    ("Richard Martin", 10.025); ("Frank Cox", 7.9); ("John Crockett", 8.0);
    ("John Gorrie", 9.066666667); ("Mervyn Pinfield", 6.925);
    ("Henric Hirsch", 6.733333333)|]*)
(**
## Related articles

* [HTML Parser](HtmlParser.html) - provides more information about
working with HTML documents dynamically.

*)

