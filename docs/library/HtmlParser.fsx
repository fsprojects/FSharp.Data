(*** condition: prepare ***)
#r "../../bin/lib/netstandard2.0/FSharp.Data.dll"
(*** condition: fsx ***)
#if FSX
#r "nuget: FSharp.Data,{{fsdocs-package-version}}"
#endif // FSX
(*** condition: ipynb ***)
#if IPYNB
#r "nuget: FSharp.Data,{{fsdocs-package-version}}"

Formatter.SetPreferredMimeTypeFor(typeof<obj>, "text/plain")
Formatter.Register(fun (x:obj) (writer: TextWriter) -> fprintfn writer "%120A" x )
#endif // IPYNB
(** 
# F# Data: HTML Parser

[View this documentation in a .NET Interactive notebook ![Binder](https://mybinder.org/badge_logo.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Data/gh-pages?filepath=HtmlParser.ipynb)

This article demonstrates how to use the HTML Parser to parse HTML files.

The HTML parser takes any fragment of HTML, uri or a stream and trys to parse it into a DOM. 
The parser is based on the [HTML Living Standard](http://www.whatwg.org/specs/web-apps/current-work/multipage/index.html#contents)
Once a document/fragment has been parsed, a set of extension methods over the HTML DOM elements allow you to extract information from a web page
independently of the actual HTML Type provider.
*)

open FSharp.Data

(**
The following example uses Google to search for `FSharp.Data` then parses the first set of
search results from the page, extracting the URL and Title of the link.

To achieve this we must first parse the webpage into our DOM. We can do this using
the `HtmlDocument.Load` method. This method will take a URL and make a synchronous web call
to extract the data from the page. Note: an asynchronous variant `HtmlDocument.AsyncLoad` is also available  
*)
let results = HtmlDocument.Load("http://www.google.co.uk/search?q=FSharp.Data")

(**
Now that we have a loaded HTML document we can begin to extract data from it. 
Firstly we want to extract all of the anchor tags `a` out of the document, then
inspect the links to see if it has a `href` attribute. If it does, extract the value,
which in this case is the url that the search result is pointing to, and additionally the 
`InnerText` of the anchor tag to provide the name of the web page for the search result
we are looking at. 
*)
let links = 
    results.Descendants ["a"]
    |> Seq.choose (fun x -> 
           x.TryGetAttribute("href")
           |> Option.map (fun a -> x.InnerText(), a.Value())
    )
    |> Seq.toList

(**
Now that we have extracted our search results you will notice that there are lots of
other links to various Google services and cached/similar results. Ideally we would 
like to filter these results as we are probably not interested in them.
At this point we simply have a sequence of Tuples, so F# makes this trivial using `Seq.filter`
and `Seq.map`.
*)

let searchResults =
    links
    |> List.filter (fun (name, url) -> 
                    name <> "Cached" && name <> "Similar" && url.StartsWith("/url?"))
    |> List.map (fun (name, url) -> name, url.Substring(0, url.IndexOf("&sa=")).Replace("/url?q=", ""))

(** 
Putting this all together yields the following:
*)

(*** include-value:searchResults ***)
