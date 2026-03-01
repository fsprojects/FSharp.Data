(**

*)
#r "nuget: FSharp.Data,8.1.0-beta"
#endif
(**
[![Binder](../img/badge-binder.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Data/gh-pages?filepath=library/HtmlParser.ipynb)&emsp;
[![Script](../img/badge-script.svg)](https://fsprojects.github.io/FSharp.Data//library/HtmlParser.fsx)&emsp;
[![Notebook](../img/badge-notebook.svg)](https://fsprojects.github.io/FSharp.Data//library/HtmlParser.ipynb)

# HTML Parser

This article demonstrates how to use the HTML Parser to parse HTML files.

The HTML parser takes any fragment of HTML, uri or a stream and trys to parse it into a DOM.
The parser is based on the [HTML Living Standard](http://www.whatwg.org/specs/web-apps/current-work/multipage/index.html#contents)
Once a document/fragment has been parsed, a set of extension methods over the HTML DOM elements allow you to extract information from a web page
independently of the actual HTML Type provider.

*)
open FSharp.Data
(**
The following example uses Google to search for `FSharp.Data` and then parses the first set of
search results from the page, extracting the URL and Title of the link.
We use the [HtmlDocument](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-htmldocument.html) type.

To achieve this we must first parse the webpage into our DOM. We can do this using
the [HtmlDocument.Load](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-htmldocument.html) method. This method will take a URL and make a synchronous web call
to extract the data from the page. Note: an asynchronous variant [HtmlDocument.AsyncLoad](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-htmldocument.html) is also available

*)
let results = HtmlDocument.Load("http://www.google.co.uk/search?q=FSharp.Data")(* output: 
val results: HtmlDocument =
  <!DOCTYPE html>
<html lang="en">
  <head>
    <title>Google Search</title><style>body{background-color:#fff}</style><script nonce="Xslg8MP6Y4NGxHh-moB-7w">window.google = window.google || {};window.google.c = window.google.c || {cap:0};</script>
  </head>
  <body>
    <noscript>
      <style>table,div,span,p{display:none}</style><meta content="0;url=/httpservice/retry/enablejs?sei=uUKkabj0KKbKkPIPsYvY6Ak" http-equiv="refresh" />
      <div style="display:block">
        Please click <a href="/ht...*)
(**
Now that we have a loaded HTML document we can begin to extract data from it.
Firstly, we want to extract all of the anchor tags `a` out of the document, then
inspect the links to see if it has a `href` attribute, using [HtmlDocumentExtensions.Descendants](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-htmldocumentextensions.html#Descendants). If it does, extract the value,
which in this case is the url that the search result is pointing to, and additionally the
`InnerText` of the anchor tag to provide the name of the web page for the search result
we are looking at.

*)
let links =
    results.Descendants [ "a" ]
    |> Seq.choose (fun x -> x.TryGetAttribute("href") |> Option.map (fun a -> x.InnerText(), a.Value()))
    |> Seq.truncate 10
    |> Seq.toList(* output: 
val links: (string * string) list =
  [("here", "/httpservice/retry/enablejs?sei=uUKkabj0KKbKkPIPsYvY6Ak");
   ("click here",
    "/search?q=FSharp.Data&sca_esv=d2de273313dd764e&ie=UTF-8&emsg="+[34 chars]);
   ("feedback", "https://support.google.com/websearch")]*)
(**
Now that we have extracted our search results you will notice that there are lots of
other links to various Google services and cached/similar results. Ideally, we would
like to filter these results as we are probably not interested in them.
At this point we simply have a sequence of Tuples, so F# makes this trivial using `Seq.filter`
and `Seq.map`.

*)
let searchResults =
    links
    |> List.filter (fun (name, url) -> name <> "Cached" && name <> "Similar" && url.StartsWith("/url?"))
    |> List.map (fun (name, url) -> name, url.Substring(0, url.IndexOf("&sa=")).Replace("/url?q=", ""))(* output: 
val searchResults: (string * string) list = []*)

