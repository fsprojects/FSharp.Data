(** 
# F# Data: Html Parser

This article demonstrates how to use the HTML Parser to parse HTML files.
*)

#r "../../../bin/FSharp.Data.dll"
open FSharp.Data

(**
The HTML parser takes any fragment of HTML, uri or a stream and trys to parse it into a DOM. 
The parser is based on the [HTML Living Standard](http://www.whatwg.org/specs/web-apps/current-work/multipage/index.html#contents)
Once a document/fragment has been parsed the `FSharp.Data.Html` module provides a set of extension methods over the HTML Dom elements
this then allows you to extract information from a web page independantly of the actual HTML Type provider. 
*)

open FSharp.Data.Html

(**
The following example uses google to search for `FSharp.Data` then parses the first set of
search results from the page, extracting the URL and the Title of the link.

To acheive this we must first parse the webpage into our DOM. We can do this using
the `HtmlDocument.Load` method. This method will take a URL and make a synchronous web call
to extract the data from the page. Note: an asynchronous variant `HtmlDocument.AsyncLoad` is also available  
*)
let results = HtmlDocument.Load("http://www.google.co.uk/search?q=FSharp.Data")

(**
Now we have a loaded HTML document we can begin to extract data from it. 
Firstly we want to extract all of the anchor tags `a` out of the document then
inspect the links to see if it has a `href` attribute, if it does extract the value
which in this case is the url that the search result is pointing to and additionally the 
`InnerText` of the anchor tag to provide the name of the web page for the search result
we are looking at. 
*)
let links = 
    results.Elements 
    |> Seq.collect(fun (x:HtmlElement) -> x.Descendants(fun x -> x.Name = "a"))
    |> Seq.choose (fun x -> 
           x.TryGetAttribute("href")
           |> Option.map (fun a -> x.InnerText, a.Value)
    )

(**
Now we have extracted our search results you will notice that there lots of
other links to various google services and cached/similar results ideally we would 
like to filter these results as we are probably not interested in them.
At this point we simply have a sequence of Tuples so F# makes this trivial, using `Seq.filter`
and `Seq.map` 
*)

let searchResults =
    links
    |> Seq.filter (fun (name, url) -> 
                    name <> "Cached" && name <> "Similar" && url.StartsWith("/url?"))
    |> Seq.map (fun (name, url) -> name, url.Replace("/url?q=", ""))
    |> Seq.toArray

(**
Putting this all together yields the following
`   [lang=text]
    [|("F# Data : Library for Data Access - F# Open Source Group @ GitHub",
     "http://fsharp.github.io/FSharp.Data/&amp;sa=U&amp;ei=plkhU9H7"+[75 chars]);
    ("Freebase Provider",
     "http://fsharp.github.io/FSharp.Data/library/Freebase.html&amp"+[100 chars]);
    ("Contributing to F# Data",
     "http://fsharp.github.io/FSharp.Data/contributing.html&amp;sa="+[96 chars]);
    ("JSON Type Provider",
     "http://fsharp.github.io/FSharp.Data/library/JsonProvider.html"+[104 chars]);...|]
*)