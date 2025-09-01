module FSharp.Data.Tests.HtmlOperations

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
    let expected: HtmlNode list = []
    HtmlNode.elements (HtmlNode.NewText "Hello") |> should equal expected

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
let ``Can get direct inner text``() =
    let html = "<div>21 minutes ago<span> LIVE</span> x</div>" |> HtmlNode.Parse |> Seq.exactlyOne
    html.InnerText() |> should equal "21 minutes ago LIVE x"
    html.DirectInnerText() |> should equal "21 minutes ago x"

[<Test>]
let ``Inner text on a comment should be String.Empty``() =
    let comment = HtmlNode.NewComment "Hello World"
    HtmlNode.innerText comment |> should equal String.Empty

// --------------------------------------------------------------------------------------
// Tests for Utils module functions (tested indirectly through public API)

[<Test>]
let ``Case-insensitive element name matching works via getNameSet``() =
    let html = "<div><P>Para 1</P><span>Span</span><p>Para 2</p></div>" 
               |> HtmlNode.Parse |> Seq.head
    let result = html |> HtmlNode.elementsNamed ["p"]
    result.Length |> should equal 2
    result |> List.map HtmlNode.innerText |> should equal ["Para 1"; "Para 2"]

[<Test>]
let ``Case-insensitive descendant name matching works with mixed case input``() =
    let html = "<div><DIV><P>Test</P></DIV><p>Another</p></div>" 
               |> HtmlNode.Parse |> Seq.head
    let result = html |> HtmlNode.descendantsNamed false ["P"; "div"] |> List.ofSeq
    result.Length |> should equal 2

[<Test>]
let ``Case-insensitive attribute matching works via toLower``() =
    let html = "<div ID='Test' Class='highlight'>Content</div>" 
               |> HtmlNode.Parse |> Seq.head
    html |> HtmlNode.hasAttribute "id" "test" |> should equal true
    html |> HtmlNode.hasAttribute "ID" "TEST" |> should equal true 
    html |> HtmlNode.hasAttribute "class" "HIGHLIGHT" |> should equal true

[<Test>]
let ``getNameSet handles empty name collections``() =
    let html = "<div><p>Test</p></div>" |> HtmlNode.Parse |> Seq.head
    let result = html |> HtmlNode.elementsNamed []
    result.Length |> should equal 0

[<Test>]
let ``getNameSet handles duplicate names (case variations)``() =
    let html = "<div><P>Para 1</P><span>Span</span><p>Para 2</p></div>" 
               |> HtmlNode.Parse |> Seq.head
    // Test with duplicate names in different cases
    let result = html |> HtmlNode.elementsNamed ["p"; "P"; "p"]
    result.Length |> should equal 2
    
[<Test>]
let ``toLower handles special characters in attribute values``() =
    let html = "<div title='Ñoño Café'>Content</div>" 
               |> HtmlNode.Parse |> Seq.head
    html |> HtmlNode.hasAttribute "title" "ñoño café" |> should equal true

[<Test>]
let ``Case-insensitive matching works in descendantsNamedWithPath``() =
    let html = "<html><head><Title>Test</Title></head></html>" 
               |> HtmlNode.Parse |> Seq.head
    let result = html |> HtmlNode.descendantsNamedWithPath false ["title"]
    result |> Seq.length |> should equal 1
    result |> Seq.head |> fst |> HtmlNode.innerText |> should equal "Test"
