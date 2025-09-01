module FSharp.Data.Tests.XmlRuntime

open FsUnit
open NUnit.Framework
open System
open System.IO
open System.Xml.Linq
open System.Reflection
open FSharp.Data.Runtime.BaseTypes

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