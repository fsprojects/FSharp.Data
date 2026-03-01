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

## JSON-LD Structured Data

Many modern websites — including **Wikipedia** — embed structured metadata as
[JSON-LD](https://json-ld.org/) inside `<script type="application/ld+json">` elements in
the page `<head>`.  `HtmlProvider` now parses these blocks automatically and exposes them
via a typed `.JsonLd` container, exactly like the `.Schemas` container for microdata.

### What Wikipedia exposes via JSON-LD

Wikipedia embeds a JSON-LD block on every article page that describes the article using
the [schema.org `Article` type](https://schema.org/Article).  The block includes:

| Property | Description |
|---|---|
| `name` | Article title |
| `headline` | Short headline |
| `description` | Brief description |
| `url` | Canonical URL |
| `datePublished` | First published date |
| `dateModified` | Last modified date |

Wikipedia also has pages about specific entities (people, places, organisations, events)
that carry a `mainEntity` link back to the corresponding [Wikidata](https://www.wikidata.org/)
item, and article categories encoded as `about` objects.

The sample HTML below mirrors the structure Wikipedia actually serves:
*)

[<Literal>]
let WikipediaArticleSample =
    """<html>
<head>
  <script type="application/ld+json">
  {
    "@context": "https://schema.org",
    "@type": "Article",
    "name": "F Sharp (programming language)",
    "headline": "Functional-first programming language for .NET",
    "description": "F# is a strongly typed, multi-paradigm programming language.",
    "url": "https://en.wikipedia.org/wiki/F_Sharp_(programming_language)",
    "datePublished": "2003-01-01",
    "dateModified": "2024-06-15",
    "license": "https://creativecommons.org/licenses/by-sa/4.0/",
    "inLanguage": "en"
  }
  </script>
</head>
<body>
  <table>
    <tr><th>Year</th><th>Event</th></tr>
    <tr><td>2002</td><td>F# created by Don Syme at Microsoft Research Cambridge</td></tr>
    <tr><td>2005</td><td>First public release of F#</td></tr>
    <tr><td>2010</td><td>F# ships with Visual Studio 2010</td></tr>
    <tr><td>2020</td><td>F# 5.0 released</td></tr>
  </table>
</body>
</html>"""

type WikipediaArticle = HtmlProvider<WikipediaArticleSample>

let wiki = WikipediaArticle.Parse(WikipediaArticleSample)

// Access the JSON-LD Article metadata
let article = wiki.JsonLd.Article |> Array.head
printfn "Title: %s" article.Name
printfn "Description: %s" article.Description
printfn "Published: %s  |  Modified: %s" article.DatePublished article.DateModified
printfn "URL: %s" article.Url

(*** include-fsi-merged-output ***)

(**
The `.JsonLd` container has one property per `@type` found in the JSON-LD blocks.  Each
property returns an array of items — so `wiki.JsonLd.Article` is an
`WikipediaArticle+Article[]`.  Each item has one `string` property per top-level scalar
field (strings, numbers, booleans), with names Pascal-cased for F# convention.  A `.Raw`
property gives the original JSON text if you need to access complex nested values.

### Wikipedia timeline article: tables + JSON-LD metadata

The next example shows combining the Wikipedia HTML table (a timeline of events) with the
JSON-LD article metadata, all via a single provider type:
*)

// Access the events table in the same page
for row in wiki.Tables.Table1.Rows do
    printfn "%d: %s" row.Year row.Event

(*** include-fsi-merged-output ***)

(**

### Multiple JSON-LD types on one page

Some pages include multiple JSON-LD blocks, e.g. a `WebPage` descriptor alongside the
`Article`.  The provider generates separate typed properties for each `@type`:
*)

[<Literal>]
let WikipediaWithWebPageSample =
    """<html>
<head>
  <script type="application/ld+json">
  {
    "@context": "https://schema.org",
    "@type": "WebPage",
    "name": "F Sharp — Wikipedia",
    "url": "https://en.wikipedia.org/wiki/F_Sharp_(programming_language)",
    "inLanguage": "en",
    "isPartOf": "https://en.wikipedia.org/"
  }
  </script>
  <script type="application/ld+json">
  {
    "@context": "https://schema.org",
    "@type": "Article",
    "name": "F Sharp (programming language)",
    "headline": "Functional-first programming language for .NET",
    "description": "F# is a strongly typed, multi-paradigm programming language.",
    "url": "https://en.wikipedia.org/wiki/F_Sharp_(programming_language)",
    "datePublished": "2003-01-01",
    "dateModified": "2024-06-15"
  }
  </script>
</head>
<body></body>
</html>"""

type WikipediaWithWebPage = HtmlProvider<WikipediaWithWebPageSample>

let wikiMulti = WikipediaWithWebPage.Parse(WikipediaWithWebPageSample)

// Both JsonLd types are available as separate typed properties
printfn "WebPage name: %s" wikiMulti.JsonLd.WebPage.[0].Name
printfn "Article name: %s" wikiMulti.JsonLd.Article.[0].Name
printfn "Article published: %s" wikiMulti.JsonLd.Article.[0].DatePublished

(*** include-fsi-merged-output ***)

(**

### Accessing raw JSON for complex properties

For properties with nested object values (such as `image`, `author`, or `publisher`
in a Wikipedia article), only scalar top-level fields are reflected as typed properties.
Use the `.Raw` property to access the full original JSON and parse it further with
`JsonProvider` or `JsonValue.Parse` if needed:
*)

[<Literal>]
let WikipediaPersonSample =
    """<html>
<head>
  <script type="application/ld+json">
  {
    "@context": "https://schema.org",
    "@type": "Article",
    "name": "Alan Turing",
    "headline": "British mathematician and computer scientist",
    "description": "Alan Mathison Turing was an English mathematician, computer scientist, logician, cryptanalyst, philosopher, and theoretical biologist.",
    "url": "https://en.wikipedia.org/wiki/Alan_Turing",
    "datePublished": "2001-10-13",
    "dateModified": "2024-09-10",
    "license": "https://creativecommons.org/licenses/by-sa/4.0/",
    "inLanguage": "en"
  }
  </script>
</head>
<body>
  <table>
    <tr><th>Year</th><th>Achievement</th></tr>
    <tr><td>1936</td><td>Turing machine concept published</td></tr>
    <tr><td>1939</td><td>Bombe code-breaking machine</td></tr>
    <tr><td>1950</td><td>Turing Test proposed</td></tr>
  </table>
</body>
</html>"""

type WikipediaPerson = HtmlProvider<WikipediaPersonSample>

let turingPage = WikipediaPerson.Parse(WikipediaPersonSample)

// JSON-LD article metadata
let turingArticle = turingPage.JsonLd.Article.[0]
printfn "Subject: %s" turingArticle.Name
printfn "Published: %s" turingArticle.DatePublished
printfn "License: %s" turingArticle.License

// Timeline table from the article body
for row in turingPage.Tables.Table1.Rows do
    printfn "%d — %s" row.Year row.Achievement

(*** include-fsi-merged-output ***)

(**

## Summary of structured data formats

| Format | HTML mechanism | Provider access | Typical use |
|---|---|---|---|
| Tables | `<table>` elements | `.Tables.TableName` | Tabular data, statistics |
| Microdata | `itemscope`/`itemprop` attributes | `.Schemas.TypeName` | Inline product/event/person markup |
| JSON-LD | `<script type="application/ld+json">` | `.JsonLd.TypeName` | Article/page metadata, SEO |

All three formats can coexist in the same `HtmlProvider` type.

## Related articles

 * [HTML Parser](HtmlParser.html) - provides more information about
   working with HTML documents dynamically.

*)
