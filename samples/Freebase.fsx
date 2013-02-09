(** 
# F# Data: Freebase Provider

The [Freebase graph database](http://www.worldbank.org) contains information on over 
23 million entities, with information on a wide variety of subjects ranging from books 
and movies to historical figures and events to chemical elements, as well as rich 
interconnections between the entities.

The Freebase type provider puts this information at your fingertips, giving you strongly-typed
access to a treasure trove of data. This article provides an introduction. The type provider
is also used on the [Try F#](http://www.tryfsharp.org) web site in the "Data Science" tutorial,
so you can find more examples there. The Visual Studio F# Team Blog also has a series of 4 blog
posts about it [here](http://blogs.msdn.com/b/fsharpteam/archive/2012/09/21/the-f-3-0-freebase-type-provider-sample-integrating-internet-scale-data-sources-into-a-strongly-typed-language.aspx)
and you can watch a recorded demo by Don Syme [here](http://skillsmatter.com/podcast/scala/an-informal-deep-dive-with-don-syme-the-freebase-type-provider)

## Introducing the provider

The following example loads the `FSharp.Data.dll` library (in F# Interactive), 
initializes a connection to Freebase using the `GetDataContext` method and then
retrieves the Chemical Elements, including Hydrogen in three lines of code!
*)

#r "../bin/FSharp.Data.dll"
open FSharp.Data

let data = FreebaseData.GetDataContext()
let elements = data.``Science and Technology``.Chemistry.``Chemical Elements`` |> Seq.toList
let hydrogen = data.``Science and Technology``.Chemistry.``Chemical Elements``.Individuals.Hydrogen

(**
You can now explore the Freebase data schema by using “.” after "data" or "hydrogen".

There is a lot of different data available on Freebase, and you can use it for all kinds of purposes.
Here is an example that generates realistic test data:
*)

let randomElement = 
    let random = new System.Random()
    fun (arr : string[]) -> arr.[random.Next(arr.Length)]

let surnames = 
    data.Society.People.``Family names``
    |> Seq.truncate 100
    |> Seq.map (fun name -> name.Name)
    |> Array.ofSeq

let firstnames = 
    data.Society.Celebrities.Celebrities
    |> Seq.truncate 100
    |> Seq.map (fun celeb -> celeb.Name.Split([|' '|]).[0])
    |> Array.ofSeq

let testNames = seq {
    while true do
        yield (randomElement firstnames) + " " + (randomElement surnames)
}

testNames |> Seq.take 10 |> Seq.toArray

(**
Some of the Freebase type provider features:

* Many queries are translated efficiently into the MQL language. Those that can't execute on the client side by default.
* A selection of sample individuals is given under the "Individuals" entry for each collection of objects. This allows you to program against stroingly named individual such as Hydrogen or ``Bob Dylan``
* Freebase features such as approximate counts are supported
* The implementation uses the latest Freebase API 
* Image URLs are provided with GetImages(), and the first image is provided using the MainImage property
* Snapshot dates for Freebase are supported
* Optional client-side caching of schema information makes type checking quick and efficient
* API keys are supported
* Units of measure are supported

## Freebase Queries

The Freebase provider includes some support for query translation from F# 3.0 LINQ queries to MQL.
This means you can write queries in F# 3.0 with auto-completion and strong typing, and still execute 
efficiently on the server, at least for the queries translated to MQL .

In addition to the standard F# 3.0 query operators, `FSharp.Data.FreebaseOperators` defines three more
Freebase specific operators: `ApproximatelyMatches`, `ApproximatelyOneOf` and `ApproximateCount`.
Here are some sample queries that are translated fully to MQL:
*)

open FSharp.Data.FreebaseOperators

// Name some sub-domains of data
let biology = data.``Science and Technology``.Biology
let computers = data.``Science and Technology``.Computers
let chemistry = data.``Science and Technology``.Chemistry
let astronomy = data.``Science and Technology``.Astronomy
let books = data.``Arts and Entertainment``.Books
 
/// Get the names of the US presidents
let presidents = 
    query { for e in data.Society.Government.``US Presidents`` do 
            select e.Name } 
    |> Seq.toList
 
/// Count the stars listed in the database
let numberOfStars = astronomy.Stars.Count()
 
/// The name and distances of stars which have a distance recorded.
let someStarDistances = 
    query { for e in astronomy.Stars do 
            where e.Distance.HasValue
            select (e.Name, e.Distance) } 
        |> Seq.toList
 
/// Get the stars in the database sorted by proximity to earth
let starsSortedByProximityToEarth = 
    query { for e in astronomy.Stars do 
            sortBy e.Distance.Value
            take 10
            select e } 
        |> Seq.toList
 
    /// Get some stars close to Earth
let getSomeCloseStars = 
    query { for e in astronomy.Stars do 
            where (e.Distance.Value < 4.011384e+18<_>)
            select e } 
        |> Seq.toList
 
    /// Get the first 10 books matching a user string. 
let topBooksWithNameContaining (s:string) = 
    query { for book in data.``Arts and Entertainment``.Books.Books do
            where (book.Name.ApproximatelyMatches s)
            take 10 
            select book.Name }
 
topBooksWithNameContaining "1984" |> Seq.toList

(**

## Providing an API key

The Freebase API is rate limited, and initially you are using some quota available for debugging purposes.
If you get the (403) Forbidden error, then this shows you are hitting rate limitations. 
Quickly you will need an API key with the Freebase service enabled. This gives you 100,000 requests/day. 
The F# Data Library also provides the `FreebaseDataProvider` type which allows you to specify several parameters,
including the API key:
*)

[<Literal>]
let API_KEY = "<enter your freebase-enabled google API key here, you will get errors before you do>"

type FreebaseDataWithKey = FreebaseDataProvider<Key=API_KEY>
let data = FreebaseDataWithKey.GetDataContext()

(**
## Related articles

 * [F# Data: Type Providers](FSharpData.html) - gives more information about other
   type providers in the `FSharp.Data` package.
 * [Try F#: Data Science](http://www.tryfsharp.org/Learn/data-science) - The Data Science
   tutorial on Try F# uses the Freebase type provider in numerous examples.
 * [Visual F# Team Blog: Integrating Internet-Scale Data Sources into a Strongly Typed Language](http://blogs.msdn.com/b/fsharpteam/archive/2012/09/21/the-f-3-0-freebase-type-provider-sample-integrating-internet-scale-data-sources-into-a-strongly-typed-language.aspx) - A series of 4 blog posts introducing the Freebase type provider
 * [Demo by Don Syme](http://skillsmatter.com/podcast/scala/an-informal-deep-dive-with-don-syme-the-freebase-type-provider) - An Informal Deep Dive With Don Syme: The Freebase Type Provider
*)
