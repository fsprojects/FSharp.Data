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

(**

## Implemented and missing features

Basic CSS selectors are implemented, but some JQuery selectors are missing

This table lists all JQuery selectors and their status

Selector name|Status|specification
|-|:-:|-:
*All Selector *|` TODO `|[specification](http://api.jquery.com/all-selector/)|
*:animated Selector*|` not possible `|[specification](http://api.jquery.com/animated-selector/)|
*Attribute Contains Prefix Selector*|` implemented `|[specification](http://api.jquery.com/attribute-contains-prefix-selector/)|
*Attribute Contains Selector *|` implemented `|[specification](http://api.jquery.com/attribute-contains-selector/)|
*Attribute Contains Word Selector*|` implemented `|[specification](http://api.jquery.com/attribute-contains-word-selector/)|
*Attribute Ends With Selector *|` implemented `|[specification](http://api.jquery.com/attribute-ends-with-selector/)|
*Attribute Equals Selector *| ` implemented `|[specification](http://api.jquery.com/attribute-equals-selector/)|
*Attribute Not Equal Selector*|` implemented `|[specification](http://api.jquery.com/attribute-not-equal-selector/)|
*Attribute Starts With Selector *|` implemented `|[specification](http://api.jquery.com/attribute-starts-with-selector/)|
*:button Selector *|` implemented `|[specification](http://api.jquery.com/button-selector/)|
*:checkbox Selector*|` implemented `|[specification](http://api.jquery.com/checkbox-selector/)|
*:checked Selector*|` implemented `|[specification](http://api.jquery.com/checked-selector/)|
*Child Selector (“parent > child”)*|` implemented `|[specification](http://api.jquery.com/child-selector/)|
*Class Selector (“.class”)*|` implemented `|[specification](http://api.jquery.com/class-selector/)|
*:contains() Selector*|` TODO `|[specification](http://api.jquery.com/contains-selector/)|
*Descendant Selector (“ancestor descendant”)*|` implemented `|[specification](http://api.jquery.com/descendant-selector/)|
*:disabled Selector*|` implemented `|[specification](http://api.jquery.com/disabled-selector/)|
*Element Selector (“element”)*|` implemented `|[specification](http://api.jquery.com/element-selector/)|
*:empty Selector*|` implemented `|[specification](http://api.jquery.com/empty-selector/)|
*:enabled Selector*|` implemented `|[specification](http://api.jquery.com/enabled-selector/)|
*:eq() Selector*|` TODO `|[specification](http://api.jquery.com/eq-selector/)|
*:even Selector*|` implemented `|[specification](http://api.jquery.com/even-selector/)|
*:file Selector*|` implemented `|[specification](http://api.jquery.com/file-selector/)|
*:first-child Selector*|` TODO `|[specification](http://api.jquery.com/first-child-selector/)|
*:first-of-type Selector*|` TODO  `|[specification](http://api.jquery.com/first-of-type-selector/)|
*:first Selector*|` TODO `|[specification](http://api.jquery.com/first-selector/)|
*:focus Selector*|` not possible `|[specification](http://api.jquery.com/focus-selector/)|
*:gt() Selector*|` TODO `|[specification](http://api.jquery.com/gt-selector/)|
*Has Attribute Selector [name]*|` implemented `|[specification](http://api.jquery.com/has-attribute-selector/)|
*:has() Selector*|` TODO `|[specification](http://api.jquery.com/has-selector/)|
*:header Selector*|` TODO `|[specification](http://api.jquery.com/header-selector/)|
*:hidden Selector*|` implemented `|[specification](http://api.jquery.com/hidden-selector/)|
*ID Selector (“#id”)*|` implemented `|[specification](http://api.jquery.com/id-selector/)|
*:image Selector*|` implemented `|[specification](http://api.jquery.com/image-selector/)|
*:input Selector*|` implemented `|[specification](http://api.jquery.com/input-selector/)|
*:lang() Selector*|` TODO `|[specification](http://api.jquery.com/lang-selector/)|
*:last-child Selector*|` TODO `|[specification](http://api.jquery.com/last-child-selector/)|
*:last-of-type Selector*|` TODO `|[specification](http://api.jquery.com/last-of-type-selector/)|
*:last Selector*|` TODO `|[specification](http://api.jquery.com/last-selector/)|
*:lt() Selector*|` TODO `|[specification](http://api.jquery.com/lt-selector/)|
*Multiple Attribute Selector [name=”value”][name2=”value2″]*|` implemented `|[specification](http://api.jquery.com/multiple-attribute-selector/)|
*Multiple Selector (“selector1, selector2, selectorN”)*|` TODO `|[specification](http://api.jquery.com/multiple-selector/)|
*Next Adjacent Selector (“prev + next”)*|` TODO `|[specification](http://api.jquery.com/next-adjacent-selector/)|
*Next Siblings Selector (“prev ~ siblings”)*|` TODO `|[specification](http://api.jquery.com/next-siblings-selector/)|
*:not() Selector*|` TODO `|[specification](http://api.jquery.com/not-selector/)|
*:nth-child() Selector*|` TODO `|[specification](http://api.jquery.com/nth-child-selector/)|
*:nth-last-child() Selector*|` TODO `|[specification](http://api.jquery.com/nth-last-child-selector/)|
*:nth-last-of-type() Selector*|` TODO `|[specification](http://api.jquery.com/nth-last-of-type-selector/)|
*:nth-of-type() Selector*|` TODO `|[specification](http://api.jquery.com/nth-of-type-selector/)|
*:odd Selector*|` implemented `|[specification](http://api.jquery.com/odd-selector/)|
*:only-child Selector*|` TODO `|[specification](http://api.jquery.com/only-child-selector/)|
*:only-of-type Selector*|` TODO `|[specification](http://api.jquery.com/only-of-type-selector/)|
*:parent Selector*|` TODO `|[specification](http://api.jquery.com/parent-selector/)|
*:password Selector*|` implemented `|[specification](http://api.jquery.com/password-selector/)|
*:radio Selector*|` implemented `|[specification](http://api.jquery.com/radio-selector/)|
*:reset Selector*|` not possible `|[specification](http://api.jquery.com/reset-selector/)|
*:root Selector*|` useless[1] `|[specification](http://api.jquery.com/root-selector/)|
*:selected Selector*|` implemented `|[specification](http://api.jquery.com/selected-selector/)|
*:submit Selector*|` implemented `|[specification](http://api.jquery.com/submit-selector/)|
*:target Selector*|` not possible `|[specification](http://api.jquery.com/target-selector/)|
*:text Selector*|` implemented `|[specification](http://api.jquery.com/text-selector/)|
*:visible Selector*|` not possible `|[specification](http://api.jquery.com/visible-selector/)|

[1] :root Selector seems to be useless in our case because with the HTML parser the root is always the html node.

*)

