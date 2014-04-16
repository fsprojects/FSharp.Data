#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../bin/FSharp.Data.Experimental.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.HtmlOperations
#endif

open NUnit.Framework
open FsUnit
open System
open FSharp.Data
open FSharp.Data.Html

[<Test>]
let ``Can get the name of a HtmlAttribute``() = 
    let attr = HtmlAttribute("id", "table_1")
    HtmlAttribute.name attr |> should equal "id"

[<Test>]
let ``Can get the value of a HtmlAttribute``() = 
    let attr = HtmlAttribute("id", "table_1")
    HtmlAttribute.value attr |> should equal "table_1"

[<Test>]
let ``Can parse the value of a HtmlAttribute to the correct type``() =
    let attr = HtmlAttribute("cost", "59.99")
    HtmlAttribute.parseValue Decimal.Parse attr |> should equal 59.99M

[<Test>]
let ``Can tryParse the value of a HtmlAttribute to the correct type``() =
    let attr = HtmlAttribute("cost", "59.99")
    HtmlAttribute.tryParseValue 0M Decimal.TryParse attr |> should equal 59.99M

[<Test>]
let ``If tryParse HtmlAttribute failes it should return the defaultValue``() =
    let attr = HtmlAttribute("cost", "59.99")
    HtmlAttribute.tryParseValue 0M (fun _ -> false, 100M) attr |> should equal 0M

let htmlFragment = 
    element "div" ["id", "my_div"; "class", "my_class"] [
        text"Hello World!"
    ] (ref None)

[<Test>]
let ``Can get the name of a HtmlElement``() =
    HtmlNode.name htmlFragment |> should equal "div"

[<Test>]
let ``Name of a content element is an Empty string``() = 
    HtmlNode.name (text"Hello" (ref None)) |> should equal String.Empty

[<Test>]
let ``The children of a content node is an empty list``() =
    HtmlNode.children (text"Hello" (ref None)) |> should equal []

[<Test>]
let ``Can get the children of a node``() =
    HtmlNode.children htmlFragment |> should equal [text"Hello World!" (ref None)]

[<Test>]
let ``Can get the parent of a node``() =
    let child = HtmlNode.children htmlFragment |> List.head
    HtmlNode.parent child |> should equal (Some htmlFragment)

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
       |> HtmlDocument.elements (fun _ -> true)
       |> Seq.head

[<Test>]
let ``Can get descendants of a node that matches a predicate``() =
    let result = doc |> HtmlNode.descendants false (HtmlNode.name >> (=) "link")
    let expected = element "link" ["rel", "stylesheet"; "type", "text/css"; "href", "/bwx_style.css"] [] (ref None)
    result |> should equal [expected]

[<Test>]
let ``Can get all of the descendants that match the given set of names``() =
    let result = doc |> HtmlNode.descendantsNamed false ["link"]
    let expected = element "link" ["rel", "stylesheet"; "type", "text/css"; "href", "/bwx_style.css"] [] (ref None)
    result |> should equal [expected]

[<Test>]
let ``Can test to see if a node has a descendent``() =
    let result = doc |> HtmlNode.hasDescendants ["th"]
    result |> should equal true

[<Test>]
let ``Can get all elements of a node that matches a predicate``() = 
    let result = 
        """<body>
               <img src="myimg.jpg">
               <table title="table">
                   <tr><th>Column 1</th><th>Column 2</th></tr>
                   <tr><td>1</td><td>yes</td></tr>
               </table>
           </body>"""
        |> HtmlNode.Parse
        |> List.head
        |> HtmlNode.elements (HtmlNode.name >> (=) "img")
    let expected = element "img" ["src", "myimg.jpg"] [] (ref None)
    result |> should equal [expected]

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
            element "img" ["src", "myimg.jpg"] [] (ref None)
            element "div" [] [text "Hello World"] (ref None)
        ]
    result |> should equal expected

[<Test>]
let ``Can test to see if an element is a member of this node``() = 
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
      |> HtmlNode.hasElements ["img"; "div"]
      |> should equal true


[<Test>]
let ``Can extract the inner text from a node``() = 
    let result = doc.Descendants(["tr"]) |> List.map (HtmlNode.innerText)
    result |> should equal [
        "Column 1 Column 2"
        "1 yes"
    ]

