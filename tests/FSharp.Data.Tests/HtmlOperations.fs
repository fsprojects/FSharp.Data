#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../bin/FSharp.Data.Experimental.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.HtmlOperations
#endif

open NUnit.Framework
open FsUnit
open System
open FSharp.Data
open FSharp.Data.HtmlNode
open FSharp.Data.HtmlAttribute

[<Test>]
let ``Can get the name of a HtmlAttribute``() = 
    let attr = HtmlAttribute.New("id", "table_1")
    HtmlAttribute.name attr |> should equal "id"

[<Test>]
let ``Can get the value of a HtmlAttribute``() = 
    let attr = HtmlAttribute.New("id", "table_1")
    HtmlAttribute.value attr |> should equal "table_1"

let htmlFragment = 
    HtmlNode.NewElement("div", ["id", "my_div"; "class", "my_class highlighted"], [HtmlNode.NewText "Hello World!"])

[<Test>]
let ``Can get the name of a HtmlElement``() =
    HtmlNode.name htmlFragment |> should equal "div"

[<Test>]
let ``Name of a content element is an Empty string``() = 
    HtmlNode.name (HtmlNode.NewText "Hello") |> should equal String.Empty

[<Test>]
let ``Getting the value of an attribute works``() =
    HtmlNode.attribute "class" htmlFragment |> should equal (HtmlAttribute.New("class", "my_class highlighted"))

[<Test>]
let ``Getting a missing attribute returns None``() =
    HtmlNode.tryGetAttribute "test" htmlFragment |> should equal None

[<Test>]
let ``Getting the value of a missing attribute returns empty string``() =
    HtmlNode.attributeValue "test" htmlFragment |> should equal ""

[<Test>]
let ``Checking for id works``() =
    HtmlNode.hasId "my_div" htmlFragment |> should equal true

[<Test>]
let ``Checking for class works``() =
    HtmlNode.hasClass "my_class" htmlFragment |> should equal true
    HtmlNode.hasClass "highlighted" htmlFragment |> should equal true
    HtmlNode.hasClass "my_class highlighted" htmlFragment |> should equal true
    HtmlNode.hasClass "highlighted my_class" htmlFragment |> should equal true

[<Test>]
let ``The children of a content node is an empty list``() =
    HtmlNode.elements (HtmlNode.NewText "Hello") |> should equal []

[<Test>]
let ``Can get the children of a node``() =
    HtmlNode.elements htmlFragment |> should equal [HtmlNode.NewText "Hello World!"]

let doc = 
    """<html>
            <head>
               <script language="JavaScript" src="/bwx_generic.js"></script>
               <link rel="stylesheet" type="text/css" href="/bwx_style.css">
               </head>
           <body>
               <img src="myimg.jpg">
               <table title="table">
                   <tr><th>Column 1</th><th>Column 2</th></tr>
                   <tr><td>1</td><td>yes</td></tr>
               </table>
           </body>
       </html>""" 
       |> HtmlDocument.Parse
       |> HtmlDocument.elements
       |> Seq.head

[<Test>]
let ``Can get descendants of a node that matches a predicate``() =
    let result = doc |> HtmlNode.descendants false (HtmlNode.name >> (=) "link")
    let expected = HtmlNode.NewElement("link", ["rel", "stylesheet"; "type", "text/css"; "href", "/bwx_style.css"])
    result |> should equal [expected]

[<Test>]
let ``Can get all of the descendants that match the given set of names``() =
    let result = doc |> HtmlNode.descendantsNamed false ["link"]
    let expected = HtmlNode.NewElement("link", ["rel", "stylesheet"; "type", "text/css"; "href", "/bwx_style.css"])
    result |> should equal [expected]

[<Test>]
let ``Can get descendants with path of a node that matches a predicate``() =
    let result = doc |> HtmlNode.descendantsWithPath false (HtmlNode.name >> (=) "link")
    let expected = HtmlNode.NewElement("link", ["rel", "stylesheet"; "type", "text/css"; "href", "/bwx_style.css"])
    result |> Seq.map fst |> Seq.toList |> should equal [expected]
    result |> Seq.map (snd >> List.map HtmlNode.name) |> Seq.toList |> should equal [["head"; "html"]]

[<Test>]
let ``Can get all of the descendants with path that match the given set of names``() =
    let result = doc |> HtmlNode.descendantsNamedWithPath false ["link"]
    let expected = HtmlNode.NewElement("link", ["rel", "stylesheet"; "type", "text/css"; "href", "/bwx_style.css"])
    result |> Seq.map fst |> Seq.toList |> should equal [expected]
    result |> Seq.map (snd >> List.map HtmlNode.name) |> Seq.toList |> should equal [["head"; "html"]]

[<Test>]
let ``Can get all elements of a node that matches a set of names``() = 
    let result = 
        """<body>
               <img src="myimg.jpg">
               <div>Hello World</div>
               <table title="table">
                   <tr><th>Column 1</th><th>Column 2</th></tr>
                   <tr><td>1</td><td>yes</td></tr>
               </table>
           </body>"""
        |> HtmlNode.Parse
        |> List.head
        |> HtmlNode.elementsNamed ["img"; "div"]
    let expected = [
            HtmlNode.NewElement("img", ["src", "myimg.jpg"])
            HtmlNode.NewElement("div", [HtmlNode.NewText "Hello World"])
        ]
    result |> should equal expected

[<Test>]
let ``Can extract the inner text from a node``() = 
    let result = doc.Descendants("tr") |> Seq.map (HtmlNode.innerText) |> Seq.toList
    result |> should equal [
        "Column 1Column 2"
        "1yes"
    ]

[<Test>]
let ``Inner text on a comment should be String.Empty``() = 
    let comment = HtmlNode.NewComment "Hello World"
    HtmlNode.innerText comment |> should equal String.Empty

[<Test>]
let ``Inner text on a style should be String.Empty``() = 
    let comment = HtmlNode.NewElement("style", [HtmlNode.NewText "Hello World"])
    HtmlNode.innerText comment |> should equal String.Empty

[<Test>]
let ``Inner text on a script should be String.Empty``() = 
    let comment = HtmlNode.NewElement("script", [HtmlNode.NewText "Hello World"])
    HtmlNode.innerText comment |> should equal String.Empty
