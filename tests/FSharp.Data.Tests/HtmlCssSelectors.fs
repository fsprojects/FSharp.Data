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

