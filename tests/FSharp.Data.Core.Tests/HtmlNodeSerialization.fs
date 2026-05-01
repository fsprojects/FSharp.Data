module FSharp.Data.Tests.HtmlNodeSerialization

open NUnit.Framework
open FsUnit
open FSharp.Data

// ─────────────────────────────────────────────────────────────────────────────
// HtmlNode.ToString() — leaf nodes
// ─────────────────────────────────────────────────────────────────────────────

[<Test>]
let ``HtmlNode.NewText serializes to its content`` () =
    HtmlNode.NewText "hello world" |> string |> should equal "hello world"

[<Test>]
let ``HtmlNode.NewText with empty string serializes to empty`` () =
    HtmlNode.NewText "" |> string |> should equal ""

[<Test>]
let ``HtmlNode.NewComment serializes to HTML comment`` () =
    HtmlNode.NewComment " a comment " |> string |> should equal "<!-- a comment -->"

[<Test>]
let ``HtmlNode.NewCData serializes to CDATA section`` () =
    HtmlNode.NewCData "some <raw> content" |> string |> should equal "<![CDATA[some <raw> content]]>"

// ─────────────────────────────────────────────────────────────────────────────
// HtmlNode.ToString() — void elements (should self-close with " />")
// ─────────────────────────────────────────────────────────────────────────────

[<Test>]
let ``Void element 'area' serializes as self-closing`` () =
    HtmlNode.NewElement "area" |> string |> should equal "<area />"

[<Test>]
let ``Void element 'br' serializes as self-closing`` () =
    HtmlNode.NewElement "br" |> string |> should equal "<br />"

[<Test>]
let ``Void element 'img' with attribute serializes as self-closing`` () =
    HtmlNode.NewElement("img", [ "src", "logo.png"; "alt", "Logo" ]) |> string
    |> should equal """<img src="logo.png" alt="Logo" />"""

[<Test>]
let ``All standard void elements serialize as self-closing`` () =
    let voidNames =
        [ "area"
          "base"
          "br"
          "col"
          "command"
          "embed"
          "hr"
          "img"
          "input"
          "keygen"
          "link"
          "meta"
          "param"
          "source"
          "track"
          "wbr" ]

    for name in voidNames do
        HtmlNode.NewElement name |> string |> should equal $"<{name} />"

// ─────────────────────────────────────────────────────────────────────────────
// HtmlNode.ToString() — non-void elements
// ─────────────────────────────────────────────────────────────────────────────

[<Test>]
let ``Non-void element with no children uses opening and closing tags`` () =
    HtmlNode.NewElement "div" |> string |> should equal "<div></div>"

[<Test>]
let ``Non-void element with text child serializes inline`` () =
    HtmlNode.NewElement("p", [], [ HtmlNode.NewText "Hello" ])
    |> string
    |> should equal "<p>Hello</p>"

[<Test>]
let ``Non-void element with multiple text children serializes inline`` () =
    HtmlNode.NewElement("p", [], [ HtmlNode.NewText "foo"; HtmlNode.NewText "bar" ])
    |> string
    |> should equal "<p>foobar</p>"

[<Test>]
let ``Non-void element with element children serializes with indentation`` () =
    let node =
        HtmlNode.NewElement("ul", [], [ HtmlNode.NewElement("li", [], [ HtmlNode.NewText "item" ]) ])

    let result = node |> string |> fun s -> s.Replace("\r\n", "\n").Replace("\r", "\n")
    result |> should equal "<ul>\n  <li>item</li>\n</ul>"

[<Test>]
let ``Nested element children are indented at each level`` () =
    let inner = HtmlNode.NewElement("span", [], [ HtmlNode.NewText "text" ])
    let middle = HtmlNode.NewElement("p", [], [ inner ])
    let outer = HtmlNode.NewElement("div", [], [ middle ])
    let result = outer |> string |> fun s -> s.Replace("\r\n", "\n").Replace("\r", "\n")
    result |> should equal "<div>\n  <p>\n    <span>text</span>\n  </p>\n</div>"

[<Test>]
let ``Element with sibling element children each get their own indented line when not text-only`` () =
    // Elements that themselves have element children (not text-only) get their own line.
    let node =
        HtmlNode.NewElement(
            "div",
            [],
            [ HtmlNode.NewElement("ul", [], [ HtmlNode.NewElement("li", [], [ HtmlNode.NewText "a" ]) ])
              HtmlNode.NewElement("ul", [], [ HtmlNode.NewElement("li", [], [ HtmlNode.NewText "b" ]) ]) ]
        )

    let result = node |> string |> fun s -> s.Replace("\r\n", "\n").Replace("\r", "\n")

    // Each <ul> has element children so each gets its own line with indentation.
    result |> should startWith "<div>\n  <ul>"
    result |> should contain "\n  <ul>"

[<Test>]
let ``Text-only sibling elements are serialized inline without separating newlines`` () =
    // <p> elements whose children are all text nodes are considered inline by the serializer.
    let node =
        HtmlNode.NewElement(
            "div",
            [],
            [ HtmlNode.NewElement("p", [], [ HtmlNode.NewText "first" ])
              HtmlNode.NewElement("p", [], [ HtmlNode.NewText "second" ]) ]
        )

    let result = node |> string |> fun s -> s.Replace("\r\n", "\n").Replace("\r", "\n")
    // Text-only siblings appear on the same line (no newline inserted between them).
    result |> should equal "<div>\n  <p>first</p><p>second</p>\n</div>"

// ─────────────────────────────────────────────────────────────────────────────
// HtmlNode.ToString() — attributes
// ─────────────────────────────────────────────────────────────────────────────

[<Test>]
let ``Element with a single attribute serializes the attribute`` () =
    HtmlNode.NewElement("a", [ "href", "https://example.com" ])
    |> string
    |> should equal """<a href="https://example.com"></a>"""

[<Test>]
let ``Element with multiple attributes serializes all attributes in order`` () =
    HtmlNode.NewElement("input", [ "type", "text"; "name", "q"; "value", "" ])
    |> string
    |> should equal """<input type="text" name="q" value="" />"""

[<Test>]
let ``Attribute name is lowercased when created via HtmlAttribute.New`` () =
    HtmlNode.NewElement("div", [ "Class", "main" ])
    |> string
    |> should equal """<div class="main"></div>"""

[<Test>]
let ``Element name is lowercased when created via NewElement`` () =
    HtmlNode.NewElement("DIV") |> string |> should equal "<div></div>"

// ─────────────────────────────────────────────────────────────────────────────
// HtmlDocument.ToString()
// ─────────────────────────────────────────────────────────────────────────────

[<Test>]
let ``HtmlDocument with empty doctype serializes without DOCTYPE declaration`` () =
    let doc = HtmlDocument.New([ HtmlNode.NewElement "html" ])
    let result = doc.ToString()
    result |> should equal "<html></html>"

[<Test>]
let ``HtmlDocument with doctype includes DOCTYPE declaration and newline`` () =
    let doc =
        HtmlDocument.New("html", [ HtmlNode.NewElement("html", [], [ HtmlNode.NewText "content" ]) ])

    let result = doc.ToString() |> fun s -> s.Replace("\r\n", "\n").Replace("\r", "\n")
    result |> should startWith "<!DOCTYPE html>\n"
    result |> should contain "<html>content</html>"

[<Test>]
let ``HtmlDocument with multiple root elements serializes all of them`` () =
    let doc =
        HtmlDocument.New([ HtmlNode.NewElement "head"; HtmlNode.NewElement "body" ])

    let result = doc.ToString()
    result |> should contain "<head></head>"
    result |> should contain "<body></body>"

[<Test>]
let ``HtmlDocument with empty element list produces empty string`` () =
    let doc = HtmlDocument.New([])
    doc.ToString() |> should equal ""

[<Test>]
let ``HtmlDocument with void element inside body serializes self-closing`` () =
    let doc =
        HtmlDocument.New([ HtmlNode.NewElement("body", [], [ HtmlNode.NewElement "br" ]) ])

    let result = doc.ToString() |> fun s -> s.Replace("\r\n", "\n").Replace("\r", "\n")
    result |> should contain "<br />"

// ─────────────────────────────────────────────────────────────────────────────
// Round-trip: HtmlNode constructed manually then parsed back
// ─────────────────────────────────────────────────────────────────────────────

[<Test>]
let ``HtmlNode constructed with NewElement round-trips to same string`` () =
    let node =
        HtmlNode.NewElement(
            "ul",
            [],
            [ HtmlNode.NewElement("li", [], [ HtmlNode.NewText "alpha" ])
              HtmlNode.NewElement("li", [], [ HtmlNode.NewText "beta" ]) ]
        )

    let serialized = node.ToString()
    let reparsed = HtmlDocument.Parse("<body>" + serialized + "</body>")

    let uls =
        reparsed.Descendants() |> Seq.filter (fun n -> n.Name() = "ul") |> Seq.toList

    uls.Length |> should equal 1
    let lis = uls.[0].Descendants() |> Seq.filter (fun n -> n.Name() = "li") |> Seq.toList
    lis.Length |> should equal 2

