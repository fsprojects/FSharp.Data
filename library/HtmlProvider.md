[![Binder](../img/badge-binder.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Data/gh-pages?filepath=library/HtmlProvider.ipynb)&emsp;
[![Script](../img/badge-script.svg)](https://fsprojects.github.io/FSharp.Data//library/HtmlProvider.fsx)&emsp;
[![Notebook](../img/badge-notebook.svg)](https://fsprojects.github.io/FSharp.Data//library/HtmlProvider.ipynb)

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

```fsharp
open FSharp.Data
```

### Formula 1 Race Calendar (Wikipedia)

This example extracts the 2017 Formula 1 race calendar from Wikipedia. The page has
multiple tables; we access the one whose nearest preceding heading is "Calendar".

The provider infers that `Round` is an integer and `Date` is a `DateTime` based on the
sample data.

```fsharp
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
```

```
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
Race, round "Source: [65]" is hosted at "Source: [65]" on "Source: [65]"
[<Literal>]
val F1_2017_URL: string
  = "https://en.wikipedia.org/wiki/2017_FIA_Formula_One_World_Championship"
type F1_2017 = HtmlProvider<...>
val f1Calendar: HtmlProvider<...>.Calendar
val firstRow: HtmlProvider<...>.Calendar.Row =
  ("1", "Australian Grand Prix", "Albert Park Circuit, Melbourne", "26 March")
val round: string = "1"
val grandPrix: string = "Australian Grand Prix"
val date: string = "26 March"
val it: unit = ()
```

The generated type has a property `Rows` that returns the data from the HTML page as a
collection of rows. We iterate over the rows using a `for` loop. The row type has
properties such as `Grand Prix`, `Circuit`, `Round` and `Date` that correspond to the
columns in the HTML table.

The `Load` method accepts any URL, file path, or HTML string — we could load a different
season's page at runtime while still using the 2017 page for type inference at compile time.

### Wikipedia S&amp;P 500 Components

This example queries the Wikipedia list of S&amp;P 500 companies, which exposes symbol,
sector, and founding year for each constituent. Note that the table has no heading or
caption, so the provider names it `Table1` by position.

```fsharp
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
```

```

```

We can also use standard F# collection functions to analyse the data — for example,
grouping companies by sector to see how many are in each:

```fsharp
let bySector =
    companies.Rows
    |> Seq.groupBy (fun r -> r.``GICS Sector``)
    |> Seq.map (fun (sector, rows) -> sector, Seq.length rows)
    |> Seq.sortByDescending snd
    |> Seq.toArray
```

```

```

### NuGet Package Statistics

This example uses the HTML Type Provider to scrape download statistics for the
`FSharp.Data` package from NuGet. Because we pass the live URL as the type parameter,
the default constructor loads the same live page at runtime.

```fsharp
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
```

```
type NugetStats = HtmlProvider<...>
val rawStats: HtmlProvider<...>.VersionHistoryOfFSharpData
val getMinorVersion: v: string -> string
val downloadsByMinorVersion: (string * decimal) array =
  [|("8.1", 22142M); ("8.0", 8286M); ("7.0", 3099M); ("6.7", 1777M);
    ("6.6", 475099M); ("6.5", 4425M); ("6.4", 815260M); ("6.3", 457505M);
    ("6.2", 175661M); ("6.1", 2063M)|]
```

### Getting Statistics on Doctor Who (Multiple Tables)

Wikipedia pages often contain many tables. This example loads the Doctor Who episode
guide and aggregates viewing figures by director across the first series.

```fsharp
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
```

```
[<Literal>]
val DrWho: string
  =
  "https://en.wikipedia.org/wiki/List_of_Doctor_Who_episodes_(1963%E2%80%931989)"
val doctorWho: HtmlProvider<...>
val viewersByDirector: (string * float) array =
  [|("Waris Hussein", 8.0); ("", nan); ("Christopher Barry", 8.275);
    ("Richard Martin", 10.025); ("Frank Cox", 7.9); ("John Crockett", 8.0);
    ("John Gorrie", 9.066666667); ("Mervyn Pinfield", 6.925);
    ("Henric Hirsch", 6.733333333)|]
```

## Schema.org Microdata

In addition to HTML tables, the provider also understands
[HTML microdata](https://html.spec.whatwg.org/multipage/microdata.html) — the
`itemscope` / `itemtype` / `itemprop` attributes defined by the
[schema.org](https://schema.org/) vocabulary.  When the sample document contains
`itemscope` elements, the provider generates a `.Schemas` container with one typed
property per schema type found.

The example below uses an inline HTML string as the type parameter sample.  At runtime
you can call `.Load(url)` to parse any live page that uses the same schema types.

```fsharp
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
```

```
FSharp.Data — F# Foundation (SKU: FSHARP-DATA)
Newtonsoft.Json — James Newton-King (SKU: NEWTONSOFT-JSON)
[<Literal>]
val ProductCatalogSample: string
  =
  "<html><body>
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
</body></html>"
type ProductCatalog = HtmlProvider<...>
val catalog: HtmlProvider<...>
val it: unit = ()
```

Each item in `Schemas.Product` has one string property per `itemprop` name discovered in
the sample, with names Pascal-cased for F# convention.  The `.Html` property gives
access to the underlying `HtmlNode` for cases where you need to traverse sub-elements.

You can mix microdata and table extraction in the same type:

```fsharp
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
```

```
Name: Grace Hopper, Title: Rear Admiral
1944: Debugged the Harvard Mark II
1952: First compiler (A-0 System)
1959: Co-designed COBOL
[<Literal>]
val MixedPageSample: string
  =
  "<html><body>
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
</body></html>"
type PersonPage = HtmlProvider<...>
val page: HtmlProvider<...>
val person: HtmlProvider<...>.Person =
  { Properties =
     map
       [("alumniOf", "Yale University"); ("jobTitle", "Rear Admiral");
        ("name", "Grace Hopper")]
    Html =
     <div itemscope="" itemtype="http://schema.org/Person">
  <span itemprop="name">Grace Hopper</span> <span itemprop="jobTitle">Rear Admiral</span> <span itemprop="alumniOf">Yale University</span>
</div>}
val it: unit = ()
```

## JSON-LD Structured Data

Many modern websites — including **Wikipedia** — embed structured metadata as
[JSON-LD](https://json-ld.org/) inside `<script type="application/ld+json">` elements in
the page `<head>`.  `HtmlProvider` now parses these blocks automatically and exposes them
via a typed `.JsonLd` container, exactly like the `.Schemas` container for microdata.

### What Wikipedia exposes via JSON-LD

Wikipedia embeds a JSON-LD block on every article page that describes the article using
the [schema.org `Article` type](https://schema.org/Article).  The block includes:

Property | Description
--- | ---
`name` | Article title
`headline` | Short headline
`description` | Brief description
`url` | Canonical URL
`datePublished` | First published date
`dateModified` | Last modified date


Wikipedia also has pages about specific entities (people, places, organisations, events)
that carry a `mainEntity` link back to the corresponding [Wikidata](https://www.wikidata.org/)
item, and article categories encoded as `about` objects.

The sample HTML below mirrors the structure Wikipedia actually serves:

```fsharp
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
```

```
Title: F Sharp (programming language)
Description: F# is a strongly typed, multi-paradigm programming language.
Published: 2003-01-01  |  Modified: 2024-06-15
URL: https://en.wikipedia.org/wiki/F_Sharp_(programming_language)
[<Literal>]
val WikipediaArticleSample: string
  =
  "<html>
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
</html>"
type WikipediaArticle = HtmlProvider<...>
val wiki: HtmlProvider<...>
val article: HtmlProvider<...>.Article =
  { Properties =
     map
       [("dateModified", "2024-06-15"); ("datePublished", "2003-01-01");
        ("description",
         "F# is a strongly typed, multi-paradigm programming language.");
        ("headline", "Functional-first programming language for .NET");
        ("inLanguage", "en");
        ("license", "https://creativecommons.org/licenses/by-sa/4.0/");
        ("name", "F Sharp (programming language)");
        ("url", "https://en.wikipedia.org/wiki/F_Sharp_(programming_language)")]
    Raw =
     "{
  "@context": "https://schema.org",
  "@type": "Article","+[423 chars] }
val it: unit = ()
```

The `.JsonLd` container has one property per `@type` found in the JSON-LD blocks.  Each
property returns an array of items — so `wiki.JsonLd.Article` is an
`WikipediaArticle+Article[]`.  Each item has one `string` property per top-level scalar
field (strings, numbers, booleans), with names Pascal-cased for F# convention.  A `.Raw`
property gives the original JSON text if you need to access complex nested values.

### Wikipedia timeline article: tables + JSON-LD metadata

The next example shows combining the Wikipedia HTML table (a timeline of events) with the
JSON-LD article metadata, all via a single provider type:

```fsharp
// Access the events table in the same page
for row in wiki.Tables.Table1.Rows do
    printfn "%d: %s" row.Year row.Event
```

```
2002: F# created by Don Syme at Microsoft Research Cambridge
2005: First public release of F#
2010: F# ships with Visual Studio 2010
2020: F# 5.0 released
val it: unit = ()
```

### Multiple JSON-LD types on one page

Some pages include multiple JSON-LD blocks, e.g. a `WebPage` descriptor alongside the
`Article`.  The provider generates separate typed properties for each `@type`:

```fsharp
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
```

```
WebPage name: F Sharp — Wikipedia
Article name: F Sharp (programming language)
Article published: 2003-01-01
[<Literal>]
val WikipediaWithWebPageSample: string
  =
  "<html>
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
</html>"
type WikipediaWithWebPage = HtmlProvider<...>
val wikiMulti: HtmlProvider<...>
val it: unit = ()
```

### Accessing raw JSON for complex properties

For properties with nested object values (such as `image`, `author`, or `publisher`
in a Wikipedia article), only scalar top-level fields are reflected as typed properties.
Use the `.Raw` property to access the full original JSON and parse it further with
`JsonProvider` or `JsonValue.Parse` if needed:

```fsharp
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
```

```
Subject: Alan Turing
Published: 2001-10-13
License: https://creativecommons.org/licenses/by-sa/4.0/
1936 — Turing machine concept published
1939 — Bombe code-breaking machine
1950 — Turing Test proposed
[<Literal>]
val WikipediaPersonSample: string
  =
  "<html>
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
</html>"
type WikipediaPerson = HtmlProvider<...>
val turingPage: HtmlProvider<...>
val turingArticle: HtmlProvider<...>.Article =
  { Properties =
     map
       [("dateModified", "2024-09-10"); ("datePublished", "2001-10-13");
        ("description",
         "Alan Mathison Turing was an English mathematician, computer s"+[73 chars]);
        ("headline", "British mathematician and computer scientist");
        ("inLanguage", "en");
        ("license", "https://creativecommons.org/licenses/by-sa/4.0/");
        ("name", "Alan Turing");
        ("url", "https://en.wikipedia.org/wiki/Alan_Turing")]
    Raw =
     "{
  "@context": "https://schema.org",
  "@type": "Article","+[457 chars] }
val it: unit = ()
```

## Summary of structured data formats

Format | HTML mechanism | Provider access | Typical use
--- | --- | --- | ---
Tables | `<table>` elements | `.Tables.TableName` | Tabular data, statistics
Microdata | `itemscope`/`itemprop` attributes | `.Schemas.TypeName` | Inline product/event/person markup
JSON-LD | `<script type="application/ld+json">` | `.JsonLd.TypeName` | Article/page metadata, SEO


All three formats can coexist in the same `HtmlProvider` type.

## Related articles

* [HTML Parser](HtmlParser.html) - provides more information about
working with HTML documents dynamically.
