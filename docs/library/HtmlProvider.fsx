(**
---
category: Type Providers
categoryindex: 1
index: 2
---
*)
(*** condition: prepare ***)
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Runtime.Utilities.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Csv.Core.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Html.Core.dll"
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

# HTML Type Provider

This article demonstrates how to use the HTML type provider to read HTML pages in a
statically typed way. The provider is useful for extracting data from pages that expose
information as HTML tables or via [schema.org microdata](https://schema.org/) markup.

The HTML Type Provider takes a sample HTML document as input — either a URL, a local file
path, or an inline HTML string — and generates F# types for the tables and microdata found
in that document. You can then load live data using the same or a different URL at runtime.

## Parsing HTML Tables

HTML tables (`<table>` elements) are the primary target of the HTML Type Provider.
Each discovered table becomes a typed property on the generated `.Tables` container.

**Table naming**: Table properties are named after the nearest preceding heading element
(`<h2>`, `<h3>`, etc.), the `<caption>` element, or the table's `id`, `name`, `title`,
`summary`, or `aria-label` attribute — whichever is found first. If none are found the
table is named `Table1`, `Table2`, etc. by position.

**Column types**: The provider infers column types (`int`, `float`, `DateTime`, `string`,
etc.) from the values in the sample document. Columns that cannot be fully inferred
default to `string`.

*)

open FSharp.Data

(**

### Formula 1 Race Calendar (Wikipedia)

This example extracts the 2017 Formula 1 race calendar from Wikipedia. The page has
multiple tables; we access the one whose nearest preceding heading is "Calendar".

The provider infers that `Round` is an integer and `Date` is a `DateTime` based on the
sample data.
*)

[<Literal>]
let F1_2017_URL =
    "https://en.wikipedia.org/wiki/2017_FIA_Formula_One_World_Championship"

type F1_2017 = HtmlProvider<F1_2017_URL>

// Download the table for the 2017 F1 calendar from Wikipedia
let f1Calendar = F1_2017.Load(F1_2017_URL).Tables.Calendar

// Look at the top row, being the first race of the calendar
let firstRow = f1Calendar.Rows |> Seq.head
let round = firstRow.Round
let grandPrix = firstRow.``Grand Prix``
let date = firstRow.Date

// Print the round, location and date for each race
for row in f1Calendar.Rows do
    printfn "Race, round %A is hosted at %A on %A" row.Round row.``Grand Prix`` row.Date

(*** include-fsi-merged-output ***)

(**
The generated type has a property `Rows` that returns the data from the HTML page as a
collection of rows. We iterate over the rows using a `for` loop. The row type has
properties such as `Grand Prix`, `Circuit`, `Round` and `Date` that correspond to the
columns in the HTML table.

The `Load` method accepts any URL, file path, or HTML string — we could load a different
season's page at runtime while still using the 2017 page for type inference at compile time.

### Wikipedia S&P 500 Components

This example queries the Wikipedia list of S&P 500 companies, which exposes symbol,
sector, and founding year for each constituent. Note that the table has no heading or
caption, so the provider names it `Table1` by position.
*)

[<Literal>]
let SP500_URL =
    "https://en.wikipedia.org/wiki/List_of_S%26P_500_companies"

type SP500 = HtmlProvider<SP500_URL>

let sp500 = SP500.Load(SP500_URL)

// Table1 is the main constituents table (no caption on the Wikipedia page)
let companies = sp500.Tables.Table1

// Show the first five companies with their sector
for row in companies.Rows |> Seq.truncate 5 do
    printfn "%s (%s) — %s, founded %d" row.Symbol row.Security row.``GICS Sector`` row.Founded

(*** include-fsi-merged-output ***)

(**
We can also use standard F# collection functions to analyse the data — for example,
grouping companies by sector to see how many are in each:
*)

let bySector =
    companies.Rows
    |> Seq.groupBy (fun r -> r.``GICS Sector``)
    |> Seq.map (fun (sector, rows) -> sector, Seq.length rows)
    |> Seq.sortByDescending snd
    |> Seq.toArray

(*** include-fsi-merged-output ***)

(**

### NuGet Package Statistics

This example uses the HTML Type Provider to scrape download statistics for the
`FSharp.Data` package from NuGet. Because we pass the live URL as the type parameter,
the default constructor loads the same live page at runtime.
*)

type NugetStats = HtmlProvider<"https://www.nuget.org/packages/FSharp.Data">

// Load the live package stats for FSharp.Data
let rawStats = NugetStats().Tables.``Version History of FSharp.Data``

// Helper to extract the minor version string (e.g. "6.4" from "6.4.0")
let getMinorVersion (v: string) =
    System.Text.RegularExpressions.Regex(@"\d+\.\d+").Match(v).Value

// Group downloads by minor version
let downloadsByMinorVersion =
    rawStats.Rows
    |> Seq.groupBy (fun r -> getMinorVersion r.Version)
    |> Seq.map (fun (k, xs) -> k, xs |> Seq.sumBy (fun x -> x.Downloads))
    |> Seq.toArray

(*** include-fsi-merged-output ***)

(**

### Getting Statistics on Doctor Who (Multiple Tables)

Wikipedia pages often contain many tables. This example loads the Doctor Who episode
guide and aggregates viewing figures by director across the first series.
*)

[<Literal>]
let DrWho =
    "https://en.wikipedia.org/wiki/List_of_Doctor_Who_episodes_(1963%E2%80%931989)"

let doctorWho = new HtmlProvider<DrWho>()

// Get the average number of viewers for each director in Series 1
let viewersByDirector =
    doctorWho.Tables.``Season 1 (1963-1964)``.Rows
    |> Seq.groupBy (fun episode -> episode.``Directed by``)
    |> Seq.map (fun (director, episodes) ->
        let avgViewers =
            episodes |> Seq.averageBy (fun e -> e.``UK viewers (millions)``)

        director, avgViewers)
    |> Seq.toArray

(*** include-fsi-merged-output ***)

(**

## Schema.org Microdata

In addition to HTML tables, the provider also understands
[HTML microdata](https://html.spec.whatwg.org/multipage/microdata.html) — the
`itemscope` / `itemtype` / `itemprop` attributes defined by the
[schema.org](https://schema.org/) vocabulary.  When the sample document contains
`itemscope` elements, the provider generates a `.Schemas` container with one typed
property per schema type found.

The example below uses an inline HTML string as the type parameter sample.  At runtime
you can call `.Load(url)` to parse any live page that uses the same schema types.
*)

[<Literal>]
let ProductCatalogSample =
    """<html><body>
  <div itemscope itemtype="http://schema.org/Product">
    <span itemprop="name">FSharp.Data</span>
    <span itemprop="description">F# data-access library</span>
    <span itemprop="brand">F# Foundation</span>
    <span itemprop="sku">FSHARP-DATA</span>
  </div>
  <div itemscope itemtype="http://schema.org/Product">
    <span itemprop="name">Newtonsoft.Json</span>
    <span itemprop="description">Popular high-performance JSON framework for .NET</span>
    <span itemprop="brand">James Newton-King</span>
    <span itemprop="sku">NEWTONSOFT-JSON</span>
  </div>
</body></html>"""

type ProductCatalog = HtmlProvider<ProductCatalogSample>

let catalog = ProductCatalog.Parse(ProductCatalogSample)

// Schemas.Product returns an array of typed Product items
for product in catalog.Schemas.Product do
    printfn "%s — %s (SKU: %s)" product.Name product.Brand product.Sku

(*** include-fsi-merged-output ***)

(**
Each item in `Schemas.Product` has one string property per `itemprop` name discovered in
the sample, with names Pascal-cased for F# convention.  The `.Html` property gives
access to the underlying `HtmlNode` for cases where you need to traverse sub-elements.

You can mix microdata and table extraction in the same type:
*)

[<Literal>]
let MixedPageSample =
    """<html><body>
  <div itemscope itemtype="http://schema.org/Person">
    <span itemprop="name">Grace Hopper</span>
    <span itemprop="jobTitle">Rear Admiral</span>
    <span itemprop="alumniOf">Yale University</span>
  </div>
  <table>
    <tr><th>Year</th><th>Achievement</th></tr>
    <tr><td>1944</td><td>Debugged the Harvard Mark II</td></tr>
    <tr><td>1952</td><td>First compiler (A-0 System)</td></tr>
    <tr><td>1959</td><td>Co-designed COBOL</td></tr>
  </table>
</body></html>"""

type PersonPage = HtmlProvider<MixedPageSample>

let page = PersonPage.Parse(MixedPageSample)

// Access the microdata
let person = page.Schemas.Person |> Array.head
printfn "Name: %s, Title: %s" person.Name person.JobTitle

// Access the table
for row in page.Tables.Table1.Rows do
    printfn "%d: %s" row.Year row.Achievement

(*** include-fsi-merged-output ***)

(**

> **Note on modern websites**: Many websites today (including IMDB and eBay) use
> client-side JavaScript frameworks (React, Next.js, etc.) that render content
> dynamically — meaning the HTML served to the browser contains little or no table
> data or microdata. These sites often embed structured data as
> [JSON-LD](https://json-ld.org/) inside a `<script type="application/ld+json">` tag
> instead. For such sites, use `HtmlDocument.Load` to fetch the page and then
> `HtmlDocumentExtensions.Descendants` to locate the JSON-LD script block, then parse
> its text with `JsonProvider`.  See the [HTML Parser](HtmlParser.html) article for
> examples of working with the underlying document model.

## Related articles

 * [HTML Parser](HtmlParser.html) - provides more information about
   working with HTML documents dynamically, including extracting JSON-LD structured data.

*)
