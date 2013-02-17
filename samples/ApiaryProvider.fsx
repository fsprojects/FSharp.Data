(** 
# F# Data: Apiary Type Provider (Experimental)

*)

#r "../bin/FSharp.Data.Experimental.dll"
open System.IO
open FSharp.Data

//
let db = new ApiaryProvider<"themoviedb">("http://api.themoviedb.org")
db.AddQueryParam("api_key", "6ce0ef5b176501f8c07c634dfa933cff")

let res = db.Search.Movie(query=["query","batman"])
for movie in res.Results do
  printfn "%s (%d)" movie.Title movie.Id

let res2 = db.Search.Person(query=["query","craig"])
for person in res2.Results do
  printfn "%d %s" person.Id person.Name

let person = db.Person.GetPerson("8784")
person.PlaceOfBirth

for cast in person.Credits().Cast do
  printfn "%s (as %s)" cast.Title.String.Value cast.Character

async { let! v = db.Search.AsyncMovie(query=["query","batman"])
        printfn "%A" v.Results }
|> Async.Start

let fs = new ApiaryProvider<"fssnip">("http://api.fssnip.net/")

let snips = fs.Snippet.List()
for snip in snips do 
  printfn "%s" snip.Title

let snip = fs.Snippet.GetSnippet("fj")
snip.Tags

(**

## Related articles

 * [F# Data: Type Providers](FSharpData.html) - gives more information about other
   type providers in the `FSharp.Data` package.
 * [F# Data: JSON Type Provider](JsonProvider.html) - describes simpler type provider
   for working with JSON documents, which may be useful as a stable alternative
   to the Apiary type provider.
*)
