#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.XmlProvider
#endif

open NUnit.Framework
open FSharp.Data
open FsUnit
open System.Xml.Linq

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

[<Test>]
let ``Type of attribute with empty value is string`` = 
  XmlProvider<"data/emptyValue.xml">.GetSample().A |> shouldEqual ""

[<Test>]
let ``Xml with namespaces``() = 
  let feed = XmlProvider<"Data/search.atom.xml">.GetSample()
  feed.Title |> should equal "Windows8 - Twitter Search"
  feed.Entries.[0].Metadata.ResultType |> should equal "recent"

[<Test>]
let ``Can read config with heterogeneous attribute types``() =
  let config = XmlProvider<"data/heterogeneous.xml">.GetSample()
  let opts = 
    [ for opt in config.Options -> 
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
let authors = philosophyType.GetSample().Authors

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
    let books = authors.[0].Books
    books.[0].Title |> should equal "Tractatus Logico-Philosophicus"
    books.[1].Title |> should equal "Philosophical Investigations"

[<Test>]
let ``Can get manuscripts in philosophy.xml``() = 
    authors.[0].Manuscript.Value.Title |> should equal "Notes on Logic"
    authors.[1].Manuscript |> should equal None
 
let topicDocument = XmlProvider<"""<topics><topic><title>My Topic Title</title></topic><topic><title>Another Topic Title</title></topic></topics>""">.GetSample()

[<Test>]
let ``Can get the title of the topics``() = 
    let topics = topicDocument.Topics
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
    let divWorks (div:Html.Div) = ()
    let spanWorks (span:Html.Span) = ()
    let ulWorks (ul:Html.Ul) = ()
    let liWorks (li:Html.Li) = ()
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

    let items = XmlProvider<"data/missingInnerValue.xml", SampleIsList=true>.GetSamples()
    
    items.Length |> should equal 2
    let child1 = items.[0]
    let child2 = items.[1]

    child1.A |> should equal (Some 1)
    child2.A |> should equal None
    
    child1.B |> should equal None
    child2.B |> should equal (Some "some")

    child1.Inner |> should notEqual None
    child1.Inner.Value.C |> should equal "foo"
    child2.Inner |> should equal None

[<Test>]
let ``Global inference with empty elements doesn't crash``() =

    let items = XmlProvider<"data/missingInnerValue.xml", SampleIsList=true, Global=true>.GetSamples()
    
    items.Length |> should equal 2
    let child1 = items.[0]
    let child2 = items.[1]

    child1.A |> should equal (Some 1)
    child2.A |> should equal None
    
    child1.B |> should equal None
    child2.B |> should equal (Some "some")

    child1.Inner |> should notEqual None
    child1.Inner.Value.C |> should equal "foo"
    child2.Inner |> should equal None

type OneLetterXML = XmlProvider<"<A><B></B></A>"> // see https://github.com/fsharp/FSharp.Data/issues/256

// Acronyms are renamed correctly; see https://github.com/fsharp/FSharp.Data/issues/309
let _ = XmlProvider<"<root><TVSeries /></root>">.GetSample().TvSeries

type ChoiceFeed = XmlProvider<"<s><a /><b /></s>", SampleIsList=true>

[<Test>]
let ``Infers type for sample list with different root elements`` () =
  ChoiceFeed.Parse("<a />").A.IsSome |> should equal true
  ChoiceFeed.Parse("<b />").A.IsSome |> should equal false
  ChoiceFeed.Parse("<a />").B.IsSome |> should equal false
  ChoiceFeed.Parse("<b />").B.IsSome |> should equal true

type AnyFeed = XmlProvider<"Data/AnyFeed.xml",SampleIsList=true>

[<Test>]
let ``Infers type and reads mixed RSS/Atom feed document`` () =
  let atomFeed = AnyFeed.GetSamples().[0]
  atomFeed.Feed.IsSome |> shouldEqual true
  atomFeed.Feed.Value.Title |> shouldEqual "Example Feed"

  let rssFeed = AnyFeed.GetSamples().[1]
  rssFeed.Rss.IsSome |> shouldEqual true
  rssFeed.Rss.Value.Channel.Title |> shouldEqual "W3Schools Home Page"
  

[<Test>]
let ``Optional value elements should work at runtime when attribute is missing`` () =
    let samples = XmlProvider<"data/optionals1.xml", SampleIsList=true>.GetSamples()
    samples.[0].Description |> should equal (Some "B")
    samples.[1].Description |> should equal None
    samples.[2].Description |> should equal None

[<Test>]
let ``Optional value elements should work at runtime when element is missing`` () =
    let samples = XmlProvider<"data/optionals2.xml", SampleIsList=true>.GetSamples()
    samples.[0].Channel.Items.[0].Description |> should equal None
    samples.[0].Channel.Items.[1].Description |> should equal (Some "A")
    samples.[1].Channel.Items.[0].Description |> should equal None

[<Test>]
let ``Optional value elements should work at runtime when element is missing 2`` () =
    let samples = XmlProvider<"data/optionals3.xml", SampleIsList=true>.GetSamples()
    samples.[0].Channel.Items.[0].Title |> should equal (Some "A")
    samples.[1].Channel.Items.[0].Title |> should equal None
    samples.[1].Channel.Items.[1].Title |> should equal (Some "B")

[<Test>]
let ``Collections are collapsed into just one element``() =
    let x = XmlProvider<"<Root><Persons><Person>John</Person><Person>Doe</Person></Persons></Root>">.GetSample()
    x.Persons.[0] |> should equal "John"
    x.Persons.[1] |> should equal "Doe"

[<Test>]
let ``Json inside Xml``() =
    let x = XmlProvider<"data/JsonInXml.xml", SampleIsList=true>.GetSamples()

    x.[0].BlahDataArray.BlahDataSomethingFoos.[0].SomethingSchema |> should equal "Something.Bar"
    x.[0].BlahDataArray.BlahDataSomethingFoos.[0].Results.Query |> should equal None
    x.[0].BlahDataArray.BlahDataSomethingFoos.[0].Results.SomethingSchema |> should equal "xpto.Foo"
    x.[0].BlahDataArray.BlahDataSomethingFoos.[1].SomethingSchema |> should equal "Something.Bar"
    x.[0].BlahDataArray.BlahDataSomethingFoos.[1].Results.Query |> should equal (Some "fsharp")
    x.[0].BlahDataArray.BlahDataSomethingFoos.[1].Results.SomethingSchema |> should equal "xpto.Foo"
    x.[0].BlahDataArray.BlahDataSomethingFoo2.Number |> should equal None
    x.[0].BlahDataArray.BlahDataSomethingFoo2.Json.Value.SomethingSchema |> should equal "Something.Bar"
    x.[0].BlahDataArray.BlahDataSomethingFoo2.Json.Value.Results.Query |> should equal "fsharp"
    x.[0].BlahDataArray.BlahDataSomethingFoo2.Json.Value.Results.SomethingSchema |> should equal "xpto.Foo"
    x.[0].BlahDataArray.BlahDataSomethingFoo3.Size |> should equal 5
    x.[0].BlahDataArray.BlahDataSomethingFoo3.Value.SomethingSchema |> should equal "Something.Bar"
    x.[0].BlahDataArray.BlahDataSomethingFoo3.Value.Results.Query |> should equal "fsharp"
    x.[0].BlahDataArray.BlahDataSomethingFoo3.Value.Results.SomethingSchema |> should equal "xpto.Foo"
    x.[0].BlahDataArray.BlahDataSomethingFoo4.IsSome |> should equal true
    x.[0].BlahDataArray.BlahDataSomethingFoo4.Value.SomethingSchema |> should equal "Something.Bar"
    x.[0].BlahDataArray.BlahDataSomethingFoo4.Value.Results.Query |> should equal "fsharp"
    x.[0].BlahDataArray.BlahDataSomethingFoo4.Value.Results.SomethingSchema |> should equal "xpto.Foo"

    x.[1].BlahDataArray.BlahDataSomethingFoos.[0].SomethingSchema |> should equal "Something.Bar"
    x.[1].BlahDataArray.BlahDataSomethingFoos.[0].Results.Query |> should equal (Some "fsharp")
    x.[1].BlahDataArray.BlahDataSomethingFoos.[0].Results.SomethingSchema |> should equal "xpto.Foo"
    x.[1].BlahDataArray.BlahDataSomethingFoos.[1].SomethingSchema |> should equal "Something.Bar"
    x.[1].BlahDataArray.BlahDataSomethingFoos.[1].Results.Query |> should equal (Some "fsharp")
    x.[1].BlahDataArray.BlahDataSomethingFoos.[1].Results.SomethingSchema |> should equal "xpto.Foo"
    x.[1].BlahDataArray.BlahDataSomethingFoo2.Number |> should equal (Some 2)
    x.[1].BlahDataArray.BlahDataSomethingFoo3.Size |> should equal 5
    x.[1].BlahDataArray.BlahDataSomethingFoo4.IsSome |> should equal false
