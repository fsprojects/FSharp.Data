(**

*)
#r "nuget: FSharp.Data,6.4.0"
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
The following example uses Google to search for `FSharp.Data` then parses the first set of
search results from the page, extracting the URL and Title of the link.
We use the [HtmlDocument](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-htmldocument.html) type.

To achieve this we must first parse the webpage into our DOM. We can do this using
the [HtmlDocument.Load](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-htmldocument.html) method. This method will take a URL and make a synchronous web call
to extract the data from the page. Note: an asynchronous variant [HtmlDocument.AsyncLoad](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-htmldocument.html) is also available

*)
let results = HtmlDocument.Load("http://www.google.co.uk/search?q=FSharp.Data")(* output: 
val results: HtmlDocument =
  <!-- html>--><html lang="en">
  <head>
    <meta charset="UTF-8" /><meta content="/images/branding/googleg/1x/googleg_standard_color_128dp.png" itemprop="image" /><title>FSharp.Data - Google Search</title><script nonce="YIvrPJ9ZGny4apcywwRFTg">(function(){
document.documentElement.addEventListener("submit",function(b){var a;if(a=b.target){var c=a.getAttribute("data-submitfalse");a="1"===c||"q"===c&&!a.elements.q.value?!0:!1}else a=!1;a&&(b.preventDefault(),b.stopPropagation())},!0);document.documentEle...*)
(**
Now that we have a loaded HTML document we can begin to extract data from it.
Firstly we want to extract all of the anchor tags `a` out of the document, then
inspect the links to see if it has a `href` attribute, using [HtmlDocumentExtensions.Descendants](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-htmldocumentextensions.html#Descendants). If it does, extract the value,
which in this case is the url that the search result is pointing to, and additionally the
`InnerText` of the anchor tag to provide the name of the web page for the search result
we are looking at.

*)
let links =
    results.Descendants [ "a" ]
    |> Seq.choose (fun x ->
        x.TryGetAttribute("href")
        |> Option.map (fun a -> x.InnerText(), a.Value()))
    |> Seq.truncate 10
    |> Seq.toList(* output: 
val links: (string * string) list =
  [("Google", "/?sa=X&ved=0ahUKEwi6vJqO8tOFAxUGGTQIHerqCQwQOwgC");
   ("here",
    "/search?q=FSharp.Data&sca_esv=2386da0e1e038e73&ie=UTF-8&gbv=1"+[27 chars]);
   ("Videos",
    "/search?q=FSharp.Data&sca_esv=2386da0e1e038e73&ie=UTF-8&tbm=v"+[65 chars]);
   ("Images",
    "/search?q=FSharp.Data&sca_esv=2386da0e1e038e73&ie=UTF-8&tbm=i"+[66 chars]);
   ("News",
    "/search?q=FSharp.Data&sca_esv=2386da0e1e038e73&ie=UTF-8&tbm=n"+[65 chars]);
   ("Maps",
    "/url?q=http://maps.google.co.uk/maps%3Fq%3DFSharp.Data%26um%3"+[145 chars]);
   ("Shopping",
    "/url?q=/search%3Fq%3DFSharp.Data%26sca_esv%3D2386da0e1e038e73"+[172 chars]);
   ("Books",
    "/search?q=FSharp.Data&sca_esv=2386da0e1e038e73&ie=UTF-8&tbm=b"+[65 chars]);
   ("Search tools", "/advanced_search")]*)
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
        name <> "Cached"
        && name <> "Similar"
        && url.StartsWith("/url?"))
    |> List.map (fun (name, url) ->
        name,
        url
            .Substring(0, url.IndexOf("&sa="))
            .Replace("/url?q=", ""))(* output: 
val searchResults: (string * string) list =
  [("Maps",
    "http://maps.google.co.uk/maps%3Fq%3DFSharp.Data%26um%3D1%26ie"+[52 chars]);
   ("Shopping",
    "/search%3Fq%3DFSharp.Data%26sca_esv%3D2386da0e1e038e73%26ie%3"+[79 chars])]*)

