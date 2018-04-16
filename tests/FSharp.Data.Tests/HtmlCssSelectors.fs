#if INTERACTIVE
#r "../../bin/lib/net45/FSharp.Data.dll"
#r "../../bin/typeproviders/fsharp41/net45/FSharp.Data.Experimental.dll"
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#r "../../packages/test/FsUnit/lib/net46/FsUnit.NUnit.dll"
#else
module FSharp.Data.Tests.HtmlCssSelectors
#endif

open NUnit.Framework
open FsUnit
open System
open FSharp.Data
open FSharp.Data.HtmlNode
open FSharp.Data.HtmlAttribute
open FSharp.Data.CssSelectorExtensions

/// tests jQuery selector documented here: https://api.jquery.com/attribute-contains-prefix-selector/
[<Test>]
let ``Attribute Contains Prefix Selector``() = 
    let html = """<!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>attributeContainsPrefix demo</title>
          <style>
          a {
            display: inline-block;
          }
          </style>
          <script src="https://code.jquery.com/jquery-1.10.2.js"></script>
        </head>
        <body>
 
        <a href="example.html" hreflang="en">Some text</a>
        <a href="example.html" hreflang="en-UK">Some other text</a>
        <a href="example.html" hreflang="english">will not be outlined</a>
 
        <script>
        $( "a[hreflang|='en']" ).css( "border", "3px dotted green" );
        </script>
 
        </body>
        </html>""" |> HtmlDocument.Parse
    let selection = html.CssSelect "a[hreflang|=en]"
    selection |> should haveLength 2
    let values = selection |> List.map (fun n -> n.InnerText())
    values |> should equal ["Some text";"Some other text"]

/// tests jQuery selector documented here: https://api.jquery.com/attribute-contains-selector/
[<Test>]
let ``Attribute Contains Selector``() = 
    let html = """<!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>attributeContains demo</title>
          <script src="https://code.jquery.com/jquery-1.10.2.js"></script>
        </head>
        <body>
 
        <input name="man-news">
        <input name="milkman">
        <input name="letterman2">
        <input name="newmilk">
 
        <script>
        $( "input[name*='man']" ).val( "has man in it!" );
        </script>
 
        </body>
        </html>""" |> HtmlDocument.Parse
    let selection = html.CssSelect "input[name*='man']"
    let values = selection |> List.map (fun n -> n.AttributeValue("name"))
    values |> should equal ["man-news";"milkman";"letterman2"]

/// tests jQuery selector documented here: https://api.jquery.com/attribute-contains-word-selector/
[<Test>]
let ``Attribute Contains Word Selector``() = 
    let html = """<!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>attributeContainsWord demo</title>
          <script src="https://code.jquery.com/jquery-1.10.2.js"></script>
        </head>
        <body>
 
        <input name="man-news">
        <input name="milk man">
        <input name="letterman2">
        <input name="newmilk">
 
        <script>
        $( "input[name~='man']" ).val( "mr. man is in it!" );
        </script>
 
        </body>
        </html>""" |> HtmlDocument.Parse
    let selection = html.CssSelect "input[name~='man']"
    let values = selection |> List.map (fun n -> n.AttributeValue("name"))
    values |> should equal ["milk man"]


/// tests jQuery selector documented here: https://api.jquery.com/attribute-ends-with-selector/
[<Test>]
let ``Attribute Ends With Selector``() = 
    let html = """<!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>attributeEndsWith demo</title>
          <script src="https://code.jquery.com/jquery-1.10.2.js"></script>
        </head>
        <body>
 
        <input name="newsletter">
        <input name="milkman">
        <input name="jobletter">
 
        <script>
        $( "input[name$='letter']" ).val( "a letter" );
        </script>
 
        </body>
        </html>""" |> HtmlDocument.Parse
    let selection = html.CssSelect "input[name$='letter']"
    let values = selection |> List.map (fun n -> n.AttributeValue("name"))
    values |> should equal ["newsletter";"jobletter"]

/// tests jQuery selector documented here: https://api.jquery.com/attribute-equals-selector/
[<Test>]
let ``Attribute Equals Selector``() = 
    let html = """<!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>attributeEquals demo</title>
          <script src="https://code.jquery.com/jquery-1.10.2.js"></script>
        </head>
        <body>
        <div>
          <label>
            <input type="radio" name="newsletter" value="Hot Fuzz">
            <span>name?</span>
          </label>
        </div>
        <div>
          <label>
            <input type="radio" name="newsletter" value="Cold Fusion">
            <span>value?</span>
          </label>
        </div>
        <div>
          <label>
            <input type="radio" name="newsletter" value="Evil Plans">
            <span>value?</span>
          </label>
        </div>
         <script>
        $( "input[value='Hot Fuzz']" ).next().text( "Hot Fuzz" );
        </script>
        </body>
        </html>""" |> HtmlDocument.Parse
    let selection = html.CssSelect "input[value='Hot Fuzz']"
    let values = selection |> List.map (fun n -> n.AttributeValue("value"))
    values |> should equal ["Hot Fuzz"]

/// tests jQuery selector documented here: https://api.jquery.com/attribute-not-equal-selector/
[<Test>]
let ``Attribute Not Equal Selector``() = 
    let html = """<!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>attributeNotEqual demo</title>
          <script src="https://code.jquery.com/jquery-1.10.2.js"></script>
        </head>
        <body>
 
        <div>
          <input type="radio" name="newsletter" value="Hot Fuzz">
          <span>name is newsletter</span>
        </div>
        <div>
          <input type="radio" value="Cold Fusion">
          <span>no name</span>
        </div>
        <div>
          <input type="radio" name="accept" value="Evil Plans">
          <span>name is accept</span>
        </div>
 
        <script>
        $( "input[name!='newsletter']" ).next().append( "<b>; not newsletter</b>" );
        </script>
 
        </body>
        </html>""" |> HtmlDocument.Parse
    let selection = html.CssSelect "input[name!='newsletter']"
    let values = selection |> List.map (fun n -> n.AttributeValue("value"))
    values |> should equal ["Cold Fusion";"Evil Plans"]

/// tests jQuery selector documented here: https://api.jquery.com/attribute-starts-with-selector/
[<Test>]
let ``Attribute Starts With Selector``() = 
    let html = """<!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>attributeStartsWith demo</title>
          <script src="https://code.jquery.com/jquery-1.10.2.js"></script>
        </head>
        <body>
        <input name="newsletter">
        <input name="milkman">
        <input name="newsboy">
        </body>
        </html>""" |> HtmlDocument.Parse
    let selection = html.CssSelect "input[name^='news']"
    let values = selection |> List.map (fun n -> n.AttributeValue("name"))
    values |> should equal ["newsletter";"newsboy"]
    
let htmlForms = """<!doctype html>
        <html>
        <body>
        <form>
          <fieldset>
            <input type="button" value="Input Button">
            <input type="checkbox" id="check1" data="test">
            <input type="hidden" id="hidden1">
            <input type="password" id="pass1">
            <input name="email" disabled="disabled">
            <input type="radio" id="radio1">
            <input type="checkbox" id="check2" checked="checked">
            <input type="file" id="uploader1">
            <input type="image" id="img1">
            <input type="reset">
            <input type="submit" id="submit1">
            <input type="text" id="login">
            <select><option>Option</option></select>
            <textarea class="comment box1">Type a comment here</textarea>
            <button>Go !</button>
          </fieldset>
        </form>
        </body>
        </html>""" |> HtmlDocument.Parse

/// tests jQuery selector documented here: https://api.jquery.com/button-selector/
[<Test>]
let ``:button Selector``() = 
    let selection = htmlForms.CssSelect ":button"
    let values = selection |> List.map (fun n -> n.InnerText())
    values |> should haveLength 2
    values |> List.filter(fun v -> String.IsNullOrWhiteSpace v |> not) |> should equal ["Go !"]

/// tests jQuery selector documented here: https://api.jquery.com/checkbox-selector/
[<Test>]
let ``:checkbox Selector``() = 
    htmlForms.CssSelect ":checkbox"
    |> List.map (fun n -> n.AttributeValue "id")
    |> should equal ["check1";"check2"]

/// tests jQuery selector documented here: https://api.jquery.com/checked-selector/
[<Test>]
let ``:checked Selector``() = 
    htmlForms.CssSelect ":checked"
    |> List.map (fun n -> n.AttributeValue "id")
    |> should equal ["check2"]

/// tests jQuery selector documented here: https://api.jquery.com/child-selector/
[<Test>]
let ``Child Selector``() = 
    let html = """<!doctype html>
        <html>
        <body>
        <ul class="topnav">
          <li name="li1">Item 1</li>
          <li name="li2">Item 2
            <ul>
            <li>Nested item 1</li>
            <li>Nested item 2</li>
            <li>Nested item 3</li>
            </ul>
          </li>
          <li name="li3">Item 3</li>
        </ul>
        </body>
        </html>""" |> HtmlDocument.Parse
    let selection = html.CssSelect "ul.topnav > li"
    let values = selection |> List.map (fun n -> n.AttributeValue("name"))
    values |> should equal ["li1";"li2";"li3"]

[<Test>]
let ``class Selector``() = 
    htmlForms.CssSelect ".comment"
    |> List.map (fun n -> n.InnerText())
    |> should equal ["Type a comment here"]

[<Test>]
let ``id Selector``() = 
    htmlForms.CssSelect "#check1"
    |> List.map (fun n -> n.AttributeValue "type")
    |> should equal ["checkbox"]

/// tests jQuery selector documented here: https://api.jquery.com/disabled-selector/
[<Test>]
let ``:disabled Selector``() = 
    htmlForms.CssSelect ":disabled"
    |> List.map (fun n -> n.AttributeValue("name"))
    |> should equal ["email"]

/// tests jQuery selector documented here: https://api.jquery.com/empty-selector/
[<Test>]
let ``empty Selector``() = 
    let html = """<!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>empty demo</title>
        </head>
        <body>
        <table border="1">
          <tr><td>TD #0</td><td id="td1"></td></tr>
          <tr><td>TD #2</td><td id="td2"></td></tr>
          <tr><td id="td3"></td><td>TD#5</td></tr>
        </table>
        </body>
        </html>""" |> HtmlDocument.Parse
    let selection = html.CssSelect "td:empty"
    let values = selection |> List.map (fun n -> n.AttributeValue("id"))
    values |> should equal ["td1";"td2";"td3"]

/// tests jQuery selector documented here: https://api.jquery.com/enabled-selector/
[<Test>]
let ``:enabled Selector``() = 
    let html = """<!doctype html><html>
        <body>
        <form>
          <input name="email" disabled="disabled">
          <input name="id">
        </form>
        </body>
        </html>""" |> HtmlDocument.Parse
    let selection = html.CssSelect "input:enabled"
    let values = selection |> List.map (fun n -> n.AttributeValue("name"))
    values |> should equal ["id"]

/// tests jQuery selector documented here: https://api.jquery.com/file-selector/
[<Test>]
let ``:file Selector``() = 
    let selection = htmlForms.CssSelect ":file"
    selection 
    |> List.map (fun n -> n.AttributeValue("id"))
    |> should equal ["uploader1"]

/// tests jQuery selector documented here: https://api.jquery.com/hidden-selector/
[<Test>]
let ``:hidden Selector``() = 
    let selection = htmlForms.CssSelect ":hidden"
    selection 
    |> List.map (fun n -> n.AttributeValue("id"))
    |> should equal ["hidden1"]

/// tests jQuery selector documented here: https://api.jquery.com/radio-selector/
[<Test>]
let ``:radio Selector``() = 
    let selection = htmlForms.CssSelect ":radio"
    selection 
    |> List.map (fun n -> n.AttributeValue("id"))
    |> should equal ["radio1"]

/// tests jQuery selector documented here: https://api.jquery.com/password-selector/
[<Test>]
let ``:password Selector``() = 
    let selection = htmlForms.CssSelect ":password"
    selection 
    |> List.map (fun n -> n.AttributeValue("id"))
    |> should equal ["pass1"]

/// tests jQuery selector documented here: http://api.jquery.com/image-selector/
[<Test>]
let ``:image Selector``() = 
    let selection = htmlForms.CssSelect ":image"
    selection 
    |> List.map (fun n -> n.AttributeValue("id"))
    |> should equal ["img1"]

/// tests jQuery selector documented here: http://api.jquery.com/submit-selector/
[<Test>]
let ``:submit Selector``() = 
    let selection = htmlForms.CssSelect ":submit"
    selection 
    |> List.map (fun n -> n.AttributeValue("id"))
    |> should equal ["submit1"]

let evenOddHtml = 
    """
        <!doctype html>
        <html lang="en">
        <body>
        <table border="1">
          <tr><td>Row with Index #0</td></tr>
          <tr><td>Row with Index #1</td></tr>
          <tr><td>Row with Index #2</td></tr>
          <tr><td>Row with Index #3</td></tr>
        </table>
        </body>
        </html>
    """
    |> HtmlDocument.Parse

/// tests jQuery selector documented here: http://api.jquery.com/even-selector/
[<Test>]
let ``:even Selector``() = 
    let selection = evenOddHtml.CssSelect "tr:even"
    let values = selection |> List.map (fun n -> n.InnerText())
    values |> should equal ["Row with Index #0";"Row with Index #2"]

/// tests jQuery selector documented here: http://api.jquery.com/odd-selector/
[<Test>]
let ``:odd Selector``() = 
    let selection = evenOddHtml.CssSelect "tr:odd"
    let values = selection |> List.map (fun n -> n.InnerText())
    values |> should equal ["Row with Index #1";"Row with Index #3"]


/// tests jQuery selector documented here: http://api.jquery.com/has-attribute-selector/
[<Test>]
let ``has attribute Selector``() = 
    let selection = htmlForms.CssSelect "input[data]"
    let values = selection |> List.map (fun n -> n.AttributeValue("data"))
    values |> should equal ["test"]

[<Test>]
let ``special characters can be escaped``() =
    let html =
        """
            <!doctype html>
            <html lang="en">
            <body>
            <p id="has-id-with:column-and.dot" class="has-class">Matched, has id and class</p>
            <p id="has-id-with:column-and.dot">Not matched, has id only</p>
            <p id="ignore">Ignore</p>
            </body>
            </html>
        """ |> HtmlDocument.Parse
    let selection = html.CssSelect ".has-class#has-id-with\\:column-and\\.dot"
    selection |> should haveLength 1
    let values = selection |> Seq.exactlyOne |> HtmlNode.innerText
    values |> should equal "Matched, has id and class"


[<Test>]
let ``selector outside body tag``() = 
    let html = """<!doctype html>
        <html lang="en">
        <head>
          <title>Page Title</title>
        </head>
        <body>
        </body>
        </html>""" |> HtmlDocument.Parse
    let selection = html.CssSelect "title"
    selection |> should haveLength 1
    let values = selection |> List.map (fun n -> n.InnerText())
    values |> should equal ["Page Title"]
