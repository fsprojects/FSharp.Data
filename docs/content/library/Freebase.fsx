(** 
# F# Data: Freebase Provider

The [Freebase graph database](http://www.freebase.com) contains information on over 
23 million entities, with information on a wide variety of subjects ranging from books 
and movies to historical figures and events to chemical elements, as well as rich 
interconnections between the entities.

The Freebase type provider puts this information at your fingertips, giving you strongly-typed
access to a treasure trove of data. This article provides a brief introduction showing
some of the features. 

This type provider is also used on the [Try F#](http://www.tryfsharp.org) web site in the 
"Data Science" tutorial, so you can find more examples there. The Visual Studio F# Team 
Blog also has a series of 4 blog posts about it [here](http://blogs.msdn.com/b/fsharpteam/archive/2012/09/21/the-f-3-0-freebase-type-provider-sample-integrating-internet-scale-data-sources-into-a-strongly-typed-language.aspx)
and you can watch a recorded demo by Don Syme [here](http://skillsmatter.com/podcast/scala/an-informal-deep-dive-with-don-syme-the-freebase-type-provider).

## Introducing the provider

The following example loads the `FSharp.Data.dll` library (in F# Interactive), 
initializes a connection to Freebase using the `GetDataContext` method:
*)

#r "../../../bin/FSharp.Data.dll"
open FSharp.Data

let data = FreebaseData.GetDataContext()

(**

### Exploring Freebase data

Now you can explore the Freebase data schema by typing `data.` and exploring the
available data sources using autocomplete. For example, the following snippet
retrieves the Chemical Elements and then looks at the details of Hydrogen:
*)

let elements = data.``Science and Technology``.Chemistry.``Chemical Elements``

let all = elements |> Seq.toList
printfn "Elements found: %d" (Seq.length all)

let hydrogen = elements.Individuals.Hydrogen
printfn "Atominc number: %A" hydrogen.``Atomic number``

(**

### Generating test cases

There is a lot of different data available on Freebase, and you can use it for all 
kinds of purposes. The following snippet uses the database of celebrities to generate
realistic names for testing purposes. First, we obtain two arrays - one containing
100 first names (based on names of celebrities) and another obtaining 100 surnames
(from a Freebase list of last names):
*)

let firstnames = 
    data.Society.Celebrities.Celebrities
    |> Seq.truncate 100
    |> Seq.map (fun celeb -> celeb.Name.Split(' ').[0])
    |> Array.ofSeq

let surnames = 
    data.Society.People.``Family names``
    |> Seq.truncate 100
    |> Seq.map (fun name -> name.Name)
    |> Array.ofSeq

(**
To generate realistic test case data, we now write a helper function that picks a 
random element from the array and then concatenate a random first name with a 
random surname:
*)

let randomElement = 
    let random = new System.Random()
    fun (arr : string[]) -> arr.[random.Next(arr.Length)]

for i in 0 .. 10 do
  let name = 
    (randomElement firstnames) + " " +
    (randomElement surnames)
  printfn "%s" name

(**
## Querying Freebase data

In the previous examples, we used `Seq` functions to work with the collections returned by 
Freebase type provider. This works in simple cases, but it is inefficient if we need to 
filter the data or perform other querying tasks.

However, the Freebase provider includes support for querying. Queries written using the
F# 3.0 LINQ syntax are translated to MQL (a querying language used by Freebase). This means 
you can write queries in F# 3.0 with auto-completion and strong typing, and still execute 
efficiently on the server, at least for the queries translated to MQL.

The following example returns stars, together with their distance from Earth (stars
without known distance are skipped):
*)

let astronomy = data.``Science and Technology``.Astronomy

query { for e in astronomy.Stars do 
        where e.Distance.HasValue
        select (e.Name, e.Distance) } 
      |> Seq.toList

(**
To make the example shorter, we first defined a variable `astronomy` that represents the
domain of astronomical data. We also need to add `Seq.toList` to the end to actually 
execute the query and get results back in a list.

The following query returns stars that have a known distance and are close to Earth:
*)

query { for e in astronomy.Stars do 
        where (e.Distance.Value < 4.011384e+18<_>)
        select e } 
      |> Seq.toList

(**
The query language supports a number of advanced operators in addition to simple 
`where` and `select`. For example, we can sort the stars by distance from Earth and
then select 10 closest stars:        
*)

query { for e in astronomy.Stars do 
        sortBy e.Distance.Value
        take 10
        select e } 
      |> Seq.toList

(**

### Freebase query operators

In addition to the standard F# 3.0 query operators, the namespace `FSharp.Data.FreebaseOperators` 
defines a couple more Freebase specific operators such as `ApproximatelyMatches`, `ApproximatelyOneOf`,
`ApproximateCount` and `Count`. These are translated to specific MQL operations.

For example, the following snippet uses `Count` and `ApproximateCount` to count the number
of US presidents (in this case, `ApproximateCount` is not very useful, because counting the
exact number is efficient enough):
*)

open FSharp.Data.FreebaseOperators

data.Society.Government.``US Presidents``.Count()
data.Society.Government.``US Presidents``.ApproximateCount()

(**
The `ApproximatelyMatches` operator can be used, for example, when working with strings.
The following snippet searches for books that have a name _approximately matching_ the 
specified string:
*)

let topBooksWithNameContaining (s:string) = 
    query { for book in data.``Arts and Entertainment``.Books.Books do
            where (book.Name.ApproximatelyMatches s)
            take 10 
            select book.Name }
 
topBooksWithNameContaining "1984" |> Seq.toList

(**

### Units of Measure

Units of measure are supported. For example, the `Atomic mass` property of chemical elements
is automatically converted to SI units and exposed in Kilograms. This is statically
tracked in the F# type system using units of measure. 

Here is an example from data about cyclones and hurricanes:
*)

open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols

let cyclones = data.``Science and Technology``.Meteorology.``Tropical Cyclones``

// The type here is float<metre/second>, since the Freebase project uses normalized SI units
let topWind = cyclones.Individuals10.``Hurricane Sandy``.``Highest winds``

(**
We can convert this figure into 185 km/h like this:
*)

let distanceTravelledByWindInAnHour : float = topWind * 3600.0<second> / 1000.0<meter>

(**

## Advanced provider features

The Freebase type provider has a number of features and it is beyond the scope of this 
introduction to discuss all of them. Some of the aspects were already demonstrated and more
documentation can be found in the articles linked in the introduction. To give a brief 
summary, here is a list of features:

* Many queries are translated efficiently into the MQL language. Those that can't are
  executed on the client side by default.
* A selection of sample individuals is given under the `Individuals` entry for each collection 
  of objects. This allows you to program against strongly named individual such as 
  `Hydrogen` or `Bob Dylan`.
* Custom Freebase operators such as approximate counts and approximate string matching are supported.
* Image URLs are provided via the `GetImages()` method, and the first image is provided using the 
  `MainImage` property
* Snapshot dates for Freebase are supported. This means that you can view the state of the
  Freebase database on a specific date (also meaning that your application will not break
  when the schema changes).
* Optional client-side caching of schema information makes type checking quick and efficient.
* If you want to query larger amount of Freebase data, you can register at Google and
  obtain a custom API key. The key can be passed as a static parameter to the type provider.

### Providing an API key

The Freebase API is rate limited, and initially you are using some quota available for debugging purposes.
If you get the (403) Forbidden error, then this shows you are hitting rate limitations. 
You will need an API key with the Freebase service enabled. This gives you 100,000 requests/day. 
The F# Data Library also provides the `FreebaseDataProvider` type which allows you to specify several 
static parameters, including the API key:
*)

[<Literal>]
let FreebaseApiKey = "<enter your freebase-enabled google API key here>"

//type FreebaseDataWithKey = FreebaseDataProvider<Key=FreebaseApiKey>
//let dataWithKey = FreebaseDataWithKey.GetDataContext()

(** In alternative, you can also set the `FREEBASE_API_KEY` environment variable, which will be used if you don't specify the Key parameter. *)

(**
### Further Individuals

As you saw above, individual entities can be addressed through the ``Individuals`` property.
By default the first 1,000 individuals are returned by Freebase. Three other versions of individuals exist - 
``Individuals10`` (containing 10,000 individuals), ``Individuals100`` (containing 100,000 individuals) and
``IndividualsAZ`` (containing individuals bucketed by first letter of their name, with each bucket containing 
up to 10,000 individuals). Together these help provide alternative, more stable ways of scaling to larger tables, 
but where navigation may be slower. *)

data.``Science and Technology``.Astronomy.Stars.Individuals10.``Alpha Centauri A``

data.``Science and Technology``.Astronomy.Stars.IndividualsAZ.A.``Alpha Centauri A``

(** 
For example, there are at least 3,921,979 books in Freebase:
*)

data.``Arts and Entertainment``.Books.Books.ApproximateCount()

(** 
Listing the first 100,000 reveals the Bible but is very, very slow:
*)
// data.``Arts and Entertainment``.Books.Books.Individuals100.``The Bible``

(** 
 This provides a stable but more efficient way of address that specific book:
*)

data.``Arts and Entertainment``.Books.Books.IndividualsAZ.T.``The Bible``

(**
### Debugging MQL queries

If you want to understand how the Freebase type provider works, or if you want to debug a 
performance issue, it might be useful to see the requests that the provider sends to 
Freebase. This can be done by subscribing to the `SendingQuery` and `SendingRequest` events.
The former triggers for overall Freebase MQL queries and can be run in the [Freebase query editor](http://www.freebase.com/query).
The latter triggers for individual REST requests including cursor-advancing requests and documentation requests.
*)

data.DataContext.SendingQuery.Add (fun e -> 
  printfn "query: %A" e.QueryText)

data.DataContext.SendingRequest.Add (fun e -> 
  printfn "request: %A" e.RequestUri)

data.``Science and Technology``.Chemistry.
     ``Chemical Elements``.Individuals.Hydrogen.``Atomic mass``.Mass

(**
## Related articles

 * [Try F#: Data Science](http://www.tryfsharp.org/Learn/data-science) - The Data Science
   tutorial on Try F# uses the Freebase type provider in numerous examples.
 * [Visual F# Team Blog: Integrating Internet-Scale Data Sources into a Strongly Typed Language](http://blogs.msdn.com/b/fsharpteam/archive/2012/09/21/the-f-3-0-freebase-type-provider-sample-integrating-internet-scale-data-sources-into-a-strongly-typed-language.aspx) - A series of 4 blog posts introducing the Freebase type provider
 * [Demo by Don Syme](http://skillsmatter.com/podcast/scala/an-informal-deep-dive-with-don-syme-the-freebase-type-provider) - An Informal Deep Dive With Don Syme: The Freebase Type Provider
 * [API Reference: FreebaseDataProvider type provider](../reference/fsharp-data-freebasedataprovider.html)
 * [API Reference: FreebaseOperators module](../reference/fsharp-data-freebaseoperators.html)

*)
