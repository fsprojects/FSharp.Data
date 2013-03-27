(** 
# F# Data: Apiary Type Provider (Experimental)

In this article, we look at an experimental type provider that makes it possible to
easily call REST services that are documented using the [apiary.io](http://www.apiary.io/)
service. The Apiary.io service lets you document your REST API by writing an API
_blueprint_ in a simple Markdown-like language. 

The key part of the documentation specifies the structure of REST requests and the 
expected response. For example, a [documentation for F# Snippets API](http://docs.fssnip.apiary.io/)
specifies that `GET` request to a URL of a form `/1/snippet/{id}` (with an appropriate
content-type header) returns JSON data as follows:

    GET /1/snippet/{id}
    < 200
    < Content-Type: application/json
    { 
      "title":"All subsets of a set", "author": "Tomas Petricek",
      "description": "A function that returns all subsets of a specified set.", 
      "formatted":"<pre class=\"fssnip\">(...)</pre>",
      "published": "1 years ago",
      "tags": ["set","sequences","sequence expressions","subset"]
    }
    
The `ApiaryProvider` uses the above information to infer the structure of the REST API.
It also re-uses the JSON inference from [JSON Type Provider)(JsonProvider.html) to infer 
the result type from the sample responses.

## Introducing the provider

The type provider is experimental and will not work for all APIs hosted on Apiary.io and
so it is located in `FSharp.Data.Experimental.dll`:

*)

#r "../bin/FSharp.Data.Experimental.dll"
open System.IO
open FSharp.Data

(**
The following two sections demonstrate how to use the type provider to work with the
data available from [themoviedb.org](http://themoviedb.org). The API documentation
can be [found here](http://docs.themoviedb.apiary.io/).

### Searching for actors

To obtain a type representing the API, you need to provide the name of the API as a
static parameter to `ApiaryProvider`. In this case, the API name is "themoviedb".
In the following snippet, we immediately create an instance of the type and give it
the URL where the (live) REST API is located:
*)

let db = new ApiaryProvider<"themoviedb">("http://api.themoviedb.org")
db.AddQueryParam("api_key", "6ce0ef5b176501f8c07c634dfa933cff")

(**
To call the `ApiaryProvider`, you need to register with [themoviedb.org](http://www.themoviedb.org/),
obtain your own API key and insert it in the `AddQueryParam` method above. The method
allows specifying additional parameters that will be appended to all requests at 
runtime. (At compile-time, the API key is not needed, because all information is
obtained from Apiary.io.)

Now, you can type `db.` and explore the types that were generated for the API.
For example, the `db.Search` module contains a number of functions for searching
movies, actors and other entities:
*)

let res = db.Search.Person(query=["query","craig"])
printfn "Showing page %d out of %d." res.Page res.TotalPages

for person in res.Results do
  printfn "%d %s" person.Id person.Name

(**
Some aspects of the API cannot be fully infered from the documentation. In that case,
we can specify additional request properties using optional parameters
`query` and `headers`. In the snippet above, the `query` parameter is used to add
`&query=craig` to the request URL.

The result is a record containing information about the returned list (with paging
information) and a field `res.Results` which is a collection of results. As you 
can see, all the results can be easily accessed in a statically-typed way.

### Obtaining entities

The `ApiaryProvider` distinguishes between simple REST methods and REST entities.
An entity is a group of related API calls that retrieve data about some object,
such as an actor. It is formed by a group of REST methods such as `/3/person/{id}`
(to obtain information about a person) and `/3/person/{id}/credists` (to obtain
a list of movies where the actor played).

An entity is exposed as an object that can be obtained with a `GetXyz` method.
The returned object than has other methods to obtain more information (such as
credits).

The following snippet prints some information about Daniel Craig:
*)

let person = db.Person.GetPerson("8784")
printfn "Born: %s" person.PlaceOfBirth

let credits = person.Credits()
for cast in credits.Cast do
  printfn "%s (as %s)" cast.Title.String.Value cast.Character

(**
## Making asynchronous requests

The `ApiaryProvider` exposes both synchronous (blocking) and asynchronous 
(non-blocking) version of all methods. If you're writing an application, then
you should always use the asynchronous version to avoid blocking threads. (The
synchronous version is good for interactive scripting.)

The asynchronous versions have the same name as synchronous with a prefix
`Async` and they return F# `Async<'T>` type as a result. The following snippet
implements simple asynchronous search for Batman movies:

*)
let printBatman () = async { 
  let! res = db.Search.AsyncMovie(query=["query","batman"])
  for movie in res.Results do
    printfn " - %s" movie.Title }

printBatman() |> Async.Start

(**
## Using different REST APIs

As discussed earlier, the `ApiaryProvider` takes a static parameter which determines
the name of the API (as hosted on Apiary.io). If you document your own REST API using the
Apiary service, you will be able to call it just by using the name of your API as
a static parameter (provided that your documentation is well-formed and matches the
expectations of `ApiaryProvider`).

The following snippet shows another basic example - this time, we're calling the 
[F# Snippets](http://www.fssnip.net) REST API to get a list of recent snippets shared
on the site:
*)

// Get recently posted snippets 
let fs = new ApiaryProvider<"fssnip">("http://api.fssnip.net/")
let snips = fs.Snippet.List()
for snip in snips do 
  printfn "%s" snip.Title

// Print details about specific snippet
let snip = fs.Snippet.GetSnippet("fj")
snip.Tags

(**

## Summary

This article demonstrated the `ApiaryProvider` type from the experimental extensions to 
F# Data. The provider infers structure of a REST API from a documentation obtained from 
[apiary.io](http://apiary.io) and exposes it in a nice typed way to F# programmers.

At the moment, the type provider is only experimental - it may work for your API or it
may not. If you're interested in making the provider better, or in supporting other 
REST API documentation formats, then please visit [Contributing to F# Data](contributing.html)
page!

## Related articles

 * [F# Data: Type Providers](FSharpData.html) - gives more information about other
   type providers in the `FSharp.Data` package.
 * [F# Data: JSON Type Provider](JsonProvider.html) - describes simpler type provider
   for working with JSON documents, which may be useful as a stable alternative
   (when you perform HTTP requests explicitly).
 * [F# Data: HTTP Utilities](Http.html) - if you wish to perform HTTP requests explicitly,
   the `Http` type makes that easily possible.
*)
