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

## Practice 1: Search something on Google

We will parse links of a Google to search for `FSharp.Data` like in `HTML Parser` article.
*)
let doc = HtmlDocument.Load "http://www.google.co.uk/search?q=FSharp.Data"

(**
To be sure we get search results, we will parse links in the div with id `search`.
Then, for example, we could ensure we the HTML's structure is really compliant with the parser
using the direct descendants selector.
*)

let links = 
    doc.CssSelect "div#search > div#ires li.g > div.s div.kv cite"
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

(**

## Practice 2: Search FSharp books on Youscribe

We will parse links of a Youscribe to search result for `F#`.
*)
let doc2 = HtmlDocument.Load "http://en.youscribe.com/o-reilly-media/?quick_search=f%23"

(**
We simply ensure to match good links with their CSS's styles and DOM's hierachy
*)

let books = 
    doc2.CssSelect "div.document-infos a.doc-explore-title"
    |> List.map(fun a -> a.InnerText().Trim(), a.AttributeValue "href")
    |> List.filter(fun (t,h) -> t.Contains "F#")

(*** include-value:books ***)

(**
## JQuery selectors

### Attribute Contains Prefix Selector

Finds all links with an english hreflang attribute.
*)

let englishLinks = 
    """<!doctype html>
        <html lang="en">
        <body>
        <a href="example.html" hreflang="en">Some text</a>
        <a href="example.html" hreflang="en-UK">Some other text</a>
        <a href="example.html" hreflang="english">will not be outlined</a>
        </body>
        </html>""" 
        |> HtmlDocument.Parse
        |> fun html -> html.CssSelect "a[hreflang|=en]"

(*** include-value:searchResults ***)

(**
### Attribute Contains Selector

Finds all inputs with a name containing "man".
*)

let case1 = 
    """<!doctype html>
        <html lang="en">
        <body>
        <input name="man-news">
        <input name="milkman">
        <input name="letterman2">
        <input name="newmilk">
        </body>
        </html>""" 
        |> HtmlDocument.Parse
        |> fun html -> html.CssSelect "input[name*='man']"

(*** include-value:case1 ***)

(**
### Attribute Contains Word Selector

Finds all inputs with a name containing the word "man".
*)

let case2 = 
    """<!doctype html>
        <html lang="en">
        <body>
        <input name="man-news">
        <input name="milkman">
        <input name="milk man">
        <input name="letterman2">
        <input name="newmilk">
        </body>
        </html>""" 
        |> HtmlDocument.Parse
        |> fun html -> html.CssSelect "input[name~='man']"

(*** include-value:case2 ***)

(**
### Attribute Ends With Selector

Finds all inputs with a name ending with "man".
*)

let case3 = 
    """<!doctype html>
        <html lang="en">
        <body>
        <input name="newsletter">
        <input name="milkman">
        <input name="jobletter">
        </body>
        </html>""" 
        |> HtmlDocument.Parse
        |> fun html -> html.CssSelect "input[name$='man']"

(*** include-value:case3 ***)

(**
### Attribute Equals Selector

Finds all inputs with a name equal to "man".
*)

let case4 = 
    """<!doctype html>
        <html lang="en">
        <body>
        <input name="newsletter">
        <input name="milkman">
        <input name="man">
        <input name="jobletter">
        </body>
        </html>""" 
        |> HtmlDocument.Parse
        |> fun html -> html.CssSelect "input[name='man']"

(*** include-value:case4 ***)

(**
### Attribute Not Equal Selector

Finds all inputs with a name different to "man".
*)

let case5 = 
    """<!doctype html>
        <html lang="en">
        <body>
        <input name="newsletter">
        <input name="milkman">
        <input name="man">
        <input name="jobletter">
        </body>
        </html>""" 
        |> HtmlDocument.Parse
        |> fun html -> html.CssSelect "input[name!='man']"

(*** include-value:case5 ***)

(**
### Attribute Starts With Selector

Finds all inputs with a name starting with "man".
*)

let case6 = 
    """<!doctype html>
        <html lang="en">
        <body>
        <input name="newsletter">
        <input name="milkman">
        <input name="manual">
        <input name="jobletter">
        </body>
        </html>""" 
        |> HtmlDocument.Parse
        |> fun html -> html.CssSelect "input[name^='man']"

(*** include-value:case6 ***)

(**
### Forms helpers

There are some syntax shorcuts to find forms controls.
*)

let htmlForm = 
    """<!doctype html>
        <html>
        <body>
        <form>
          <fieldset>
            <input type="button" value="Input Button">
            <input type="checkbox" id="check1">
            <input type="hidden" id="hidden1">
            <input type="password" id="pass1">
            <input name="email" disabled="disabled">
            <input type="radio" id="radio1">
            <input type="checkbox" id="check2" checked="checked">
            <input type="file" id="uploader1">
            <input type="reset">
            <input type="submit">
            <input type="text">
            <select><option>Option</option></select>
            <textarea class="comment box1">Type a comment here</textarea>
            <button>Go !</button>
          </fieldset>
        </form>
        </body>
        </html>"""
    |> HtmlDocument.Parse

(**
Find all buttons.
*)
let buttons = htmlForm.CssSelect ":button"
(*** include-value:buttons ***)

(**
Find all checkboxes.
*)
let checkboxes = htmlForm.CssSelect ":checkbox"
(*** include-value:checkboxes ***)

(**
Find all checked checkboxs or radio.
*)
let ``checked`` = htmlForm.CssSelect ":checked"
(*** include-value:checked ***)

(**
Find all disabled controls.
*)
let disabled = htmlForm.CssSelect ":disabled"
(*** include-value:disabled ***)

(**
Find all inputs with type hidden.
*)
let hidden = htmlForm.CssSelect ":hidden"
(*** include-value:hidden ***)

(**
Find all inputs with type radio.
*)
let radio = htmlForm.CssSelect ":radio"
(*** include-value:radio ***)

(**
Find all inputs with type password.
*)
let password = htmlForm.CssSelect ":password"
(*** include-value:password ***)

(**
Find all files uploaders.
*)
let file = htmlForm.CssSelect ":file"
(*** include-value:file ***)






