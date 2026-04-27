module FSharp.Data.Tests.XmlRuntime

open FsUnit
open NUnit.Framework
open System
open System.IO
open System.Xml.Linq
open System.Reflection
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime
open FSharp.Data

// These tests use reflection to test the XmlElement methods that are marked as "generated code only"
// This approach allows us to test the functionality while respecting the compiler constraints

[<Test>]
let ``XmlElement.Create via reflection creates proper element from XElement`` () =
    let xelem = XElement(XName.Get("test"), "content")
    let createMethod = typeof<XmlElement>.GetMethod("Create", [| typeof<XElement> |])
    let xmlElement = createMethod.Invoke(null, [| xelem |]) :?> XmlElement
    xmlElement.XElement.Name.LocalName |> should equal "test"
    xmlElement.XElement.Value |> should equal "content"

[<Test>]
let ``XmlElement.Create via reflection from TextReader parses XML correctly`` () =
    let xml = "<root><child>value</child></root>"
    use reader = new StringReader(xml)
    let createMethod = typeof<XmlElement>.GetMethod("Create", [| typeof<TextReader> |])
    let xmlElement = createMethod.Invoke(null, [| reader |]) :?> XmlElement
    xmlElement.XElement.Name.LocalName |> should equal "root"
    xmlElement.XElement.Element(XName.Get("child")).Value |> should equal "value"

[<Test>]
let ``XmlElement.CreateList via reflection parses multiple elements correctly`` () =
    let xml = "<item>1</item><item>2</item>"
    use reader = new StringReader(xml)
    let createListMethod = typeof<XmlElement>.GetMethod("CreateList", [| typeof<TextReader> |])
    let elements = createListMethod.Invoke(null, [| reader |]) :?> XmlElement[]
    elements.Length |> should equal 2
    elements.[0].XElement.Value |> should equal "1"
    elements.[1].XElement.Value |> should equal "2"

[<Test>]
let ``XmlElement ToString returns XElement string representation`` () =
    let xelem = XElement(XName.Get("test"), "content")
    let createMethod = typeof<XmlElement>.GetMethod("Create", [| typeof<XElement> |])
    let xmlElement = createMethod.Invoke(null, [| xelem |]) :?> XmlElement
    xmlElement.ToString() |> should contain "<test>content</test>"

[<Test>]
let ``XmlElement _Print property truncates long strings`` () =
    let longContent = String.replicate 600 "a"
    let xelem = XElement(XName.Get("test"), longContent)
    let createMethod = typeof<XmlElement>.GetMethod("Create", [| typeof<XElement> |])
    let xmlElement = createMethod.Invoke(null, [| xelem |]) :?> XmlElement
    let printProperty = typeof<XmlElement>.GetProperty("_Print")
    let printed = printProperty.GetValue(xmlElement) :?> string
    printed.Length |> should equal 512
    printed |> should endWith "..."

[<Test>]
let ``XmlElement _Print property handles short strings`` () =
    let xelem = XElement(XName.Get("test"), "short")
    let createMethod = typeof<XmlElement>.GetMethod("Create", [| typeof<XElement> |])
    let xmlElement = createMethod.Invoke(null, [| xelem |]) :?> XmlElement
    let printProperty = typeof<XmlElement>.GetProperty("_Print")
    let printed = printProperty.GetValue(xmlElement) :?> string
    printed |> should not' (endWith "...")
    printed |> should contain "short"

// Helper: wrap an XElement in XmlElement
let private mkXml (xelem: XElement) : XmlElement = { XElement = xelem }

// ─── XmlRuntime.TryGetValue ────────────────────────────────────────────────

[<Test>]
let ``TryGetValue returns None for element with no text content`` () =
    let xml = mkXml (XElement(XName.Get("empty")))
    XmlRuntime.TryGetValue(xml) |> should equal None

[<Test>]
let ``TryGetValue returns Some for element with text content`` () =
    let xml = mkXml (XElement(XName.Get("item"), "hello"))
    XmlRuntime.TryGetValue(xml) |> should equal (Some "hello")

[<Test>]
let ``TryGetValue returns None for element whose value is empty string`` () =
    let xelem = XElement(XName.Get("item"))
    xelem.Value <- ""
    let xml = mkXml xelem
    XmlRuntime.TryGetValue(xml) |> should equal None

// ─── XmlRuntime.TryGetAttribute ───────────────────────────────────────────

[<Test>]
let ``TryGetAttribute returns None when attribute is absent`` () =
    let xml = mkXml (XElement(XName.Get("item")))
    XmlRuntime.TryGetAttribute(xml, "id") |> should equal None

[<Test>]
let ``TryGetAttribute returns Some with attribute value when present`` () =
    let xelem = XElement(XName.Get("item"), XAttribute(XName.Get("id"), "42"))
    let xml = mkXml xelem
    XmlRuntime.TryGetAttribute(xml, "id") |> should equal (Some "42")

[<Test>]
let ``TryGetAttribute handles namespace-qualified attribute name`` () =
    let ns = XNamespace.Get("http://example.com")
    let xelem = XElement(XName.Get("item"), XAttribute(ns + "type", "foo"))
    let xml = mkXml xelem
    XmlRuntime.TryGetAttribute(xml, "{http://example.com}type") |> should equal (Some "foo")

// ─── XmlRuntime.GetChild ──────────────────────────────────────────────────

[<Test>]
let ``GetChild returns the single matching child element`` () =
    let parent = XElement(XName.Get("root"), XElement(XName.Get("child"), "val"))
    let xml = mkXml parent
    let child = XmlRuntime.GetChild(xml, "child")
    child.XElement.Value |> should equal "val"

[<Test>]
let ``GetChild throws when no matching child exists`` () =
    let xml = mkXml (XElement(XName.Get("root")))
    (fun () -> XmlRuntime.GetChild(xml, "missing") |> ignore)
    |> should throw typeof<Exception>

[<Test>]
let ``GetChild throws when multiple matching children exist`` () =
    let parent =
        XElement(
            XName.Get("root"),
            XElement(XName.Get("child"), "1"),
            XElement(XName.Get("child"), "2")
        )
    let xml = mkXml parent
    (fun () -> XmlRuntime.GetChild(xml, "child") |> ignore)
    |> should throw typeof<Exception>

// ─── XmlRuntime.ConvertArray ──────────────────────────────────────────────

[<Test>]
let ``ConvertArray returns empty array when no matching children`` () =
    let xml = mkXml (XElement(XName.Get("root")))
    let result = XmlRuntime.ConvertArray(xml, "item", System.Func<XmlElement, string>(fun e -> e.XElement.Value))
    result |> should equal [||]

[<Test>]
let ``ConvertArray returns converted values for all matching children`` () =
    let parent =
        XElement(
            XName.Get("root"),
            XElement(XName.Get("item"), "1"),
            XElement(XName.Get("item"), "2"),
            XElement(XName.Get("item"), "3")
        )
    let xml = mkXml parent
    let result = XmlRuntime.ConvertArray(xml, "item", System.Func<XmlElement, int>(fun e -> int e.XElement.Value))
    result |> should equal [| 1; 2; 3 |]

[<Test>]
let ``ConvertArray ignores children with different names`` () =
    let parent =
        XElement(
            XName.Get("root"),
            XElement(XName.Get("item"), "a"),
            XElement(XName.Get("other"), "b")
        )
    let xml = mkXml parent
    let result = XmlRuntime.ConvertArray(xml, "item", System.Func<XmlElement, string>(fun e -> e.XElement.Value))
    result |> should equal [| "a" |]

// ─── XmlRuntime.ConvertOptional ───────────────────────────────────────────

[<Test>]
let ``ConvertOptional returns None when no matching child exists`` () =
    let xml = mkXml (XElement(XName.Get("root")))
    let result = XmlRuntime.ConvertOptional(xml, "opt", System.Func<XmlElement, string>(fun e -> e.XElement.Value))
    result |> should equal None

[<Test>]
let ``ConvertOptional returns Some when exactly one matching child exists`` () =
    let parent = XElement(XName.Get("root"), XElement(XName.Get("opt"), "42"))
    let xml = mkXml parent
    let result = XmlRuntime.ConvertOptional(xml, "opt", System.Func<XmlElement, int>(fun e -> int e.XElement.Value))
    result |> should equal (Some 42)

[<Test>]
let ``ConvertOptional throws when more than one matching child exists`` () =
    let parent =
        XElement(
            XName.Get("root"),
            XElement(XName.Get("opt"), "1"),
            XElement(XName.Get("opt"), "2")
        )
    let xml = mkXml parent
    (fun () -> XmlRuntime.ConvertOptional(xml, "opt", System.Func<XmlElement, string>(fun e -> e.XElement.Value)) |> ignore)
    |> should throw typeof<Exception>

// ─── XmlRuntime.ConvertOptional2 ──────────────────────────────────────────

[<Test>]
let ``ConvertOptional2 returns None when no matching child`` () =
    let xml = mkXml (XElement(XName.Get("root")))
    let result =
        XmlRuntime.ConvertOptional2(xml, "opt", System.Func<XmlElement, string option>(fun e -> Some e.XElement.Value))
    result |> should equal None

[<Test>]
let ``ConvertOptional2 returns None when child exists but inner function returns None`` () =
    let parent = XElement(XName.Get("root"), XElement(XName.Get("opt")))
    let xml = mkXml parent
    let result =
        XmlRuntime.ConvertOptional2(xml, "opt", System.Func<XmlElement, string option>(fun _ -> None))
    result |> should equal None

[<Test>]
let ``ConvertOptional2 returns Some when child exists and inner function returns Some`` () =
    let parent = XElement(XName.Get("root"), XElement(XName.Get("opt"), "hello"))
    let xml = mkXml parent
    let result =
        XmlRuntime.ConvertOptional2(xml, "opt", System.Func<XmlElement, string option>(fun e -> Some e.XElement.Value))
    result |> should equal (Some "hello")

// ─── XmlRuntime.ConvertAsName ─────────────────────────────────────────────

[<Test>]
let ``ConvertAsName returns None when element name does not match`` () =
    let xml = mkXml (XElement(XName.Get("foo")))
    let result =
        XmlRuntime.ConvertAsName(xml, "bar", System.Func<XmlElement, string>(fun e -> e.XElement.Name.LocalName))
    result |> should equal None

[<Test>]
let ``ConvertAsName returns Some when element name matches`` () =
    let xml = mkXml (XElement(XName.Get("foo"), "data"))
    let result =
        XmlRuntime.ConvertAsName(xml, "foo", System.Func<XmlElement, string>(fun e -> e.XElement.Value))
    result |> should equal (Some "data")

// ─── XmlRuntime.GetJsonValue / TryGetJsonValue ────────────────────────────

[<Test>]
let ``GetJsonValue parses embedded JSON string as JsonDocument`` () =
    let xml = mkXml (XElement(XName.Get("item"), """{"x":1}"""))
    let doc = XmlRuntime.GetJsonValue(xml)
    doc.JsonValue.["x"].AsInteger() |> should equal 1

[<Test>]
let ``GetJsonValue throws when element has no text content`` () =
    let xml = mkXml (XElement(XName.Get("item")))
    (fun () -> XmlRuntime.GetJsonValue(xml) |> ignore)
    |> should throw typeof<Exception>

[<Test>]
let ``TryGetJsonValue returns None when element has no text content`` () =
    let xml = mkXml (XElement(XName.Get("item")))
    XmlRuntime.TryGetJsonValue(xml) |> should equal None

[<Test>]
let ``TryGetJsonValue returns None when element text is not valid JSON`` () =
    let xml = mkXml (XElement(XName.Get("item"), "not-json!"))
    XmlRuntime.TryGetJsonValue(xml) |> should equal None

[<Test>]
let ``TryGetJsonValue returns Some for valid JSON content`` () =
    let xml = mkXml (XElement(XName.Get("item"), "[1,2,3]"))
    let result = XmlRuntime.TryGetJsonValue(xml)
    result |> should not' (equal None)
    result.Value.JsonValue.AsArray().Length |> should equal 3

// ─── XmlRuntime.CreateValue ───────────────────────────────────────────────

[<Test>]
let ``CreateValue creates element with string value`` () =
    let xml = XmlRuntime.CreateValue("greeting", box "hello", "")
    xml.XElement.Name.LocalName |> should equal "greeting"
    xml.XElement.Value |> should equal "hello"

[<Test>]
let ``CreateValue creates element with integer value`` () =
    let xml = XmlRuntime.CreateValue("count", box 42, "")
    xml.XElement.Name.LocalName |> should equal "count"
    xml.XElement.Value |> should equal "42"

[<Test>]
let ``CreateValue creates element with boolean true`` () =
    let xml = XmlRuntime.CreateValue("flag", box true, "")
    xml.XElement.Value |> should equal "true"

[<Test>]
let ``CreateValue creates element with boolean false`` () =
    let xml = XmlRuntime.CreateValue("flag", box false, "")
    xml.XElement.Value |> should equal "false"

// ─── XmlRuntime.CreateRecord ──────────────────────────────────────────────

[<Test>]
let ``CreateRecord creates element with no attributes or children`` () =
    let xml = XmlRuntime.CreateRecord("record", [||], [||], "")
    xml.XElement.Name.LocalName |> should equal "record"
    xml.XElement.HasAttributes |> should equal false
    xml.XElement.HasElements |> should equal false

[<Test>]
let ``CreateRecord creates element with attributes`` () =
    let xml = XmlRuntime.CreateRecord("record", [| "id", box "99" |], [||], "")
    xml.XElement.Attribute(XName.Get("id")).Value |> should equal "99"

[<Test>]
let ``CreateRecord creates element with text content via empty-string name`` () =
    let xml = XmlRuntime.CreateRecord("record", [||], [| "", box "body text" |], "")
    xml.XElement.Value |> should equal "body text"

[<Test>]
let ``CreateRecord creates element with named child element`` () =
    let xml = XmlRuntime.CreateRecord("root", [||], [| "child", box "42" |], "")
    let child = xml.XElement.Element(XName.Get("child"))
    child |> should not' (equal null)
    child.Value |> should equal "42"

[<Test>]
let ``CreateRecord serialises DateTime value as ISO date string`` () =
    let dt = DateTime(2024, 3, 15)
    let xml = XmlRuntime.CreateValue("date", box dt, "")
    xml.XElement.Value |> should equal "2024-03-15"

[<Test>]
let ``CreateRecord serialises DateTime with time as ISO-8601 string`` () =
    let dt = DateTime(2024, 3, 15, 10, 30, 0)
    let xml = XmlRuntime.CreateValue("ts", box dt, "")
    xml.XElement.Value |> should startWith "2024-03-15T"

[<Test>]
let ``CreateRecord handles optional string element — Some value`` () =
    let xml = XmlRuntime.CreateRecord("root", [||], [| "opt", box (Some "present") |], "")
    xml.XElement.Element(XName.Get("opt")).Value |> should equal "present"

[<Test>]
let ``CreateRecord handles optional string element — None omits child`` () =
    let xml = XmlRuntime.CreateRecord("root", [||], [| "opt", box (None: string option) |], "")
    xml.XElement.Element(XName.Get("opt")) |> should equal null

// ─── XmlRuntime — path navigation via pipe-separated names ────────────────

[<Test>]
let ``ConvertArray navigates path with pipe-separated names`` () =
    // <root><parent><child>A</child><child>B</child></parent></root>
    let grandchild1 = XElement(XName.Get("child"), "A")
    let grandchild2 = XElement(XName.Get("child"), "B")
    let parentElem = XElement(XName.Get("parent"), grandchild1, grandchild2)
    let root = XElement(XName.Get("root"), parentElem)
    let xml = mkXml root
    let result = XmlRuntime.ConvertArray(xml, "parent|child", System.Func<XmlElement, string>(fun e -> e.XElement.Value))
    result |> should equal [| "A"; "B" |]

[<Test>]
let ``ConvertArray returns empty array when intermediate path element is missing`` () =
    let xml = mkXml (XElement(XName.Get("root")))
    let result = XmlRuntime.ConvertArray(xml, "missing|child", System.Func<XmlElement, string>(fun e -> e.XElement.Value))
    result |> should equal [||]