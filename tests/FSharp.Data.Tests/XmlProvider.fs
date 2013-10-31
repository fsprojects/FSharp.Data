module FSharp.Data.Tests.XmlProvider

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

open NUnit.Framework
open FSharp.Data
open FsUnit
open System.Xml

type PersonXml = XmlProvider<"""<authors><author name="Ludwig" surname="Wittgenstein" age="29" /></authors>""">

let newXml = """<authors><author name="Jane" surname="Doe" age="23" /></authors>"""
let newXml2 = """<authors><author name="Jim" surname="Smith" age="24" /></authors>"""

let firstPerson = PersonXml.Parse(newXml).Author
let nextPerson = PersonXml.Parse(newXml2).Author

[<Test>]
let ``Jane should have first name of Jane``() = 
    firstPerson.Name |> should equal "Jane"

[<Test>]
let ``Jane should have a last name of Doe``() = 
    firstPerson.Surname |> should equal "Doe"

[<Test>]
let ``Jane should have an age of 23``() = 
    firstPerson.Age |> should equal 23

[<Test>]
let ``Jim should have a first name of Jim``() = 
    nextPerson.Name |> should equal "Jim"

[<Test>]
let ``Jim should have a last name of Smith``() = 
    nextPerson.Surname |> should equal "Smith"

[<Test>]
let ``Jim should have an age of 24``() = 
    nextPerson.Age |> should equal 24

let testXml = XmlProvider<""" <foo a="" /> """>.GetSample()

[<Test>]
let ``Type of attribute with empty value is string`` = 
  testXml.A |> shouldEqual ""

[<Test>]
let ``Xml with namespaces``() = 
  let feed = XmlProvider<"Data/search.atom.xml">.GetSample()
  feed.Title |> should equal "Windows8 - Twitter Search"
  feed.GetEntries().[0].Metadata.ResultType |> should equal "recent"

type Config = FSharp.Data.XmlProvider<"""
  <test>
    <options><node set="wales.css" /></options>
    <options><node set="true" /></options>
    <options><node set="42" /></options>
    <options><node /></options>
  </test>""">

[<Test>]
let ``Can read config with heterogeneous attribute types``() =
  let config = Config.GetSample()
  let opts = 
    [ for opt in config.GetOptions() -> 
        let set = opt.Node.Set in set.Boolean, set.Number, set.String ]
  opts |> should equal [ (None, None, Some "wales.css");
                         (Some true, None, Some "true"); 
                         (None, Some 42, Some "42")
                         (None, None, None) ]

let inlined = XmlProvider<"""<authors><author name="Ludwig" surname="Wittgenstein" /></authors>""">.GetSample()

[<Test>]
let ``Can get author name in inlined xml``() = 
    let author = inlined.Author
    author.Name |> should equal "Ludwig"
    author.Surname |> should equal "Wittgenstein"

type philosophyType = XmlProvider<"Data/Philosophy.xml">
let authors = philosophyType.GetSample().GetAuthors()

[<Test>]
let ``Can get author names in philosophy.xml``() = 
    authors.[0].Name |> should equal "Ludwig"
    authors.[1].Name |> should equal "Rene"

[<Test>]
let ``Can get author surnames in philosophy.xml``() = 
    authors.[0].Surname |> should equal "Wittgenstein"
    authors.[1].Surname |> should equal "Descartes"

[<Test>]
let ``Can get the optional author birthday in philosophy.xml``() = 
    authors.[0].Birth |> should equal None
    authors.[1].Birth |> should equal (Some 1596)

[<Test>]
let ``Can get Descartes books in philosophy.xml``() = 
    let books = authors.[0].GetBooks()
    books.[0].Title |> should equal "Tractatus Logico-Philosophicus"
    books.[1].Title |> should equal "Philosophical Investigations"

[<Test>]
let ``Can get manuscripts in philosophy.xml``() = 
    authors.[0].Manuscript.Value.Title |> should equal "Notes on Logic"
    authors.[1].Manuscript |> should equal None
 
let topicDocument = XmlProvider<"""<topics><topic><title>My Topic Title</title></topic><topic><title>Another Topic Title</title></topic></topics>""">.GetSample()

[<Test>]
let ``Can get the title of the topics``() = 
    let topics = topicDocument.GetTopics()
    topics.[0].Title |> should equal "My Topic Title"
    topics.[1].Title |> should equal "Another Topic Title"

type Project = XmlProvider<"Data/projects.xml">

[<Test>]
let ``Can access the background title``() =
    let doc = Project.GetSample()
    let background = doc.Backgrounds.Background
    background.Title |> should equal "purple stars"

[<Test>]
let ``Can access the project title``() =
    let doc = Project.GetSample()
    let project = doc.Project
    project.Title |> should equal "Avery"

type Html = XmlProvider<"""
<div>
    <span>
        <ul> 
            <li/>          
        </ul>
    </span>  
</div>""">
 
[<Test>]
let ``Nested xml types compile when only used in type annotations``() =
    let divWorks (div:Html.DomainTypes.Div) = ()
    let spanWorks (span:Html.DomainTypes.Span) = ()
    let ulWorks (ul:Html.DomainTypes.Ul) = ()
    let liWorks (li:Html.DomainTypes.Li) = ()
    ()

[<Literal>]
let sameNameDifferentNamespace = """<?xml version="1.0" encoding="UTF-8" ?>
<a>
    <b xmlns="urn:x" />
    <b xmlns="urn:y" />
</a>
"""

[<Test>]
let ``XML elements with same name in different namespaces``() =
    let xml = XmlProvider<sameNameDifferentNamespace>.GetSample()
    let b1 = xml.B
    let b2 = xml.B2
    ()

[<Test>]
let ``Optionality infered correctly for child elements``() =

    let items = XmlProvider<"""
        <root>
            <child a="1">
                <inner />
            </child>
            <child b="some"></child>
        </root>""", SampleIsList=true>.GetSamples()
    
    items.Length |> should equal 2
    let child1 = items.[0]
    let child2 = items.[1]

    child1.A |> should equal (Some 1)
    child2.A |> should equal None
    
    child1.B |> should equal None
    child2.B |> should equal (Some "some")

    child1.Inner |> should notEqual None
    child2.Inner |> should equal None

[<Test>]
let ``Global inference with empty elements doesn't crash``() =

    let items = XmlProvider<"""
        <root>
            <child a="1">
                <inner />
            </child>
            <child b="some"></child>
        </root>""", SampleIsList=true, Global=true>.GetSamples()
    
    items.Length |> should equal 2
    let child1 = items.[0]
    let child2 = items.[1]

    child1.A |> should equal (Some 1)
    child2.A |> should equal None
    
    child1.B |> should equal None
    child2.B |> should equal (Some "some")

    child1.Inner |> should notEqual None
    //not working correctly:
    //child2.Inner |> should equal None
