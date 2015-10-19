(** 
# F# Data: HTML CSS selectors

This article demonstrates how to use HTML CSS selectors to browse the DOM of parsed HTML files.

Usage of CSS selectors is a very natural way to parse HTML when we come from Web developments.
The HTML CSS selectors are based on the [JQuery selectors](https://api.jquery.com/category/selectors/).
*)

#r "../../../bin/FSharp.Data.dll"
open FSharp.Data

(** 
Openning FSharp.Data.CssSelectorExtensions module will enable CSS selectors.
*)
open FSharp.Data.CssSelectorExtensions

(**
We will parse links of a Google to search for `FSharp.Data` like in `HTML Parser` article.
*)
let doc = HtmlDocument.Load "http://www.google.co.uk/search?q=FSharp.Data"

(**
To be sure we get search results, we will parse links in the div with id `search`.
Then, for example, we could ensure we the HTML's structure is really compliant with the parser
using the direct descendants selector.
*)

let links = doc.CssSelect "div#search > div#ires li.g > div.s div.kv cite"
            |> List.map (
                    fun n -> 
                        match n.InnerText() with
                        | t when (t.StartsWith("https://") || t.StartsWith("http://"))-> t
                        | t -> "http://" + t
            )

(**
"li.g > div.s" skips the 4 sub results targeting github pages.
*)

(*** include-value:links ***)

(**
Now we could want the pages titles associated with their urls with a `List.zip`
*)

let searchResults = 
    doc.CssSelect "div#search > div#ires li.g > h3"
    |> List.map (fun n -> n.InnerText())
    |> List.zip (links)

(*** include-value:searchResults ***)

