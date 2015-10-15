#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../bin/FSharp.Data.Experimental.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#load "../Common/FsUnit.fs"
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

