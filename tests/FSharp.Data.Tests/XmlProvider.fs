#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.XmlProvider
#endif

open System
open System.Xml.Linq
open NUnit.Framework
open FsUnit
open FSharp.Data

[<Test>]
let ``Can control type inference`` () =
  let inferred = XmlProvider<"Data/TypeInference.xml", InferTypesFromValues=true>.GetSample().Xs.[0]

  let intLike   : int       = inferred.IntLike
  let boolLike  : bool      = inferred.BoolLike
  let jsonValue : JsonValue = inferred.JsonLike.JsonValue

  intLike   |> should equal 123
  boolLike  |> should equal false
  jsonValue |> should equal (JsonValue.Record [|"a",(JsonValue.Number 1.0M)|])

  let notInferred = XmlProvider<"Data/TypeInference.xml", InferTypesFromValues=false>.GetSample().Xs.[0]

  let intLike   : string    = notInferred.IntLike
  let boolLike  : string    = notInferred.BoolLike
  let jsonValue : string    = notInferred.JsonLike

  intLike   |> should equal "123"
  boolLike  |> should equal "0"
  jsonValue |> should equal """{"a":1}"""

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
  XmlProvider<"Data/emptyValue.xml">.GetSample().A |> shouldEqual ""

[<Test>]
let ``Xml with namespaces``() = 
  let feed = XmlProvider<"Data/search.atom.xml">.GetSample()
  feed.Title |> should equal "Windows8 - Twitter Search"
  feed.Entries.[0].Metadata.ResultType |> should equal "recent"

[<Test>]
let ``Can read config with heterogeneous attribute types``() =
  let config = XmlProvider<"Data/heterogeneous.xml">.GetSample()
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
    let background = doc.Background
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
let ``Optionality inferred correctly for child elements``() =

    let items = XmlProvider<"Data/missingInnerValue.xml", SampleIsList=true>.GetSamples()
    
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

    let items = XmlProvider<"Data/missingInnerValue.xml", SampleIsList=true, Global=true>.GetSamples()
    
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

type Cars = XmlProvider<"Data/Cars.xml", SampleIsList=true, Global=true>

[<Test>]
let ``Global inference unifies element types across multiple samples``() =
  let readCars str = 
    let doc = Cars.Parse(str)
    match doc.Car, doc.ArrayOfCar with
    | Some car, _ -> [ car.Type ]
    | _, Some cars -> [ for c in cars.Cars -> c.Type ]
    | _ -> []
  
  readCars "<Car><Type>Audi</Type></Car>" 
  |> should equal ["Audi"]

  readCars "<ArrayOfCar><Car><Type>Audi</Type></Car><Car><Type>BMW</Type></Car></ArrayOfCar>" 
  |> should equal ["Audi"; "BMW"]

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
    let samples = XmlProvider<"Data/optionals1.xml", SampleIsList=true>.GetSamples()
    samples.[0].Description |> should equal (Some "B")
    samples.[1].Description |> should equal None
    samples.[2].Description |> should equal None

[<Test>]
let ``Optional value elements should work at runtime when element is missing`` () =
    let samples = XmlProvider<"Data/optionals2.xml", SampleIsList=true>.GetSamples()
    samples.[0].Channel.Items.[0].Description |> should equal None
    samples.[0].Channel.Items.[1].Description |> should equal (Some "A")
    samples.[1].Channel.Items.[0].Description |> should equal None

[<Test>]
let ``Optional value elements should work at runtime when element is missing 2`` () =
    let samples = XmlProvider<"Data/optionals3.xml", SampleIsList=true>.GetSamples()
    samples.[0].Channel.Items.[0].Title |> should equal (Some "A")
    samples.[1].Channel.Items.[0].Title |> should equal None
    samples.[1].Channel.Items.[1].Title |> should equal (Some "B")

type CollapsedCollections = XmlProvider<"<Root><Persons><Person>John</Person><Person>Doe</Person></Persons></Root>">

[<Test>]
let ``Collections are collapsed into just one element``() =
    let x = CollapsedCollections.GetSample()
    x.Persons.[0] |> should equal "John"
    x.Persons.[1] |> should equal "Doe"

[<Test>]
let ``Collections are collapsed into just one element 2``() =
    let x = XmlProvider<"<Root><Persons><P>John</P><P>Doe</P></Persons></Root>">.GetSample()
    x.Persons.[0] |> should equal "John"
    x.Persons.[1] |> should equal "Doe"

[<Test>]
let ``Collections are collapsed into just one element 3``() =
    let x = XmlProvider<"<Root><PersonList><Person>John</Person><Person>Doe</Person></PersonList></Root>">.GetSample()
    x.PersonList.[0] |> should equal "John"
    x.PersonList.[1] |> should equal "Doe"

type SampleAzureServiceManagement = XmlProvider<"Data/SampleAzureServiceManagement.xml">

[<Test>]
let ``Collections are collapsed into just one element 4``() =
    let x = SampleAzureServiceManagement.GetSample()
    x.Locations.[0].AvailableServices |> should equal ["Compute"; "Storage"]
    x.Locations.[1].AvailableServices |> should equal ["Compute"; "Storage"; "PersistentVMRole"; "HighMemory"]

type JsonInXml = XmlProvider<"Data/JsonInXml.xml", SampleIsList=true>

[<Test>]
let ``Json inside Xml``() =
    let x = JsonInXml.GetSamples()

    x.Length |> should equal 2

    x.[0].BlahData.X |> should equal [||]
    x.[0].BlahData.BlahDataSomethingFoos.Length |> should equal 2
    x.[0].BlahData.BlahDataSomethingFoos.[0].SomethingSchema |> should equal "Something.Bar"
    x.[0].BlahData.BlahDataSomethingFoos.[0].Results.Query |> should equal None
    x.[0].BlahData.BlahDataSomethingFoos.[0].Results.SomethingSchema |> should equal "xpto.Foo"
    x.[0].BlahData.BlahDataSomethingFoos.[1].SomethingSchema |> should equal "Something.Bar"
    x.[0].BlahData.BlahDataSomethingFoos.[1].Results.Query |> should equal (Some "fsharp")
    x.[0].BlahData.BlahDataSomethingFoos.[1].Results.SomethingSchema |> should equal "xpto.Foo"
    x.[0].BlahData.BlahDataSomethingFoo2.Number |> should equal None
    x.[0].BlahData.BlahDataSomethingFoo2.Json.Value.SomethingSchema |> should equal "Something.Bar"
    x.[0].BlahData.BlahDataSomethingFoo2.Json.Value.Results.Query |> should equal "fsharp"
    x.[0].BlahData.BlahDataSomethingFoo2.Json.Value.Results.SomethingSchema |> should equal "xpto.Foo"
    x.[0].BlahData.BlahDataSomethingFoo3.Size |> should equal 5
    x.[0].BlahData.BlahDataSomethingFoo3.Value.SomethingSchema |> should equal "Something.Bar"
    x.[0].BlahData.BlahDataSomethingFoo3.Value.Results.Query |> should equal "fsharp"
    x.[0].BlahData.BlahDataSomethingFoo3.Value.Results.SomethingSchema |> should equal "xpto.Foo"
    x.[0].BlahData.BlahDataSomethingFoo4.IsSome |> should equal true
    x.[0].BlahData.BlahDataSomethingFoo4.Value.SomethingSchema |> should equal "Something.Bar"
    x.[0].BlahData.BlahDataSomethingFoo4.Value.Results.Query |> should equal "fsharp"
    x.[0].BlahData.BlahDataSomethingFoo4.Value.Results.SomethingSchema |> should equal "xpto.Foo"

    x.[1].BlahData.X.Length |> should equal 1
    x.[1].BlahData.X.[0].T |> should equal 2
    x.[1].BlahData.X.[0].Val |> should equal "foo"
    x.[1].BlahData.BlahDataSomethingFoos.Length |> should equal 2
    x.[1].BlahData.BlahDataSomethingFoos.[0].SomethingSchema |> should equal "Something.Bar"
    x.[1].BlahData.BlahDataSomethingFoos.[0].Results.Query |> should equal (Some "fsharp")
    x.[1].BlahData.BlahDataSomethingFoos.[0].Results.SomethingSchema |> should equal "xpto.Foo"
    x.[1].BlahData.BlahDataSomethingFoos.[1].SomethingSchema |> should equal "Something.Bar"
    x.[1].BlahData.BlahDataSomethingFoos.[1].Results.Query |> should equal (Some "fsharp")
    x.[1].BlahData.BlahDataSomethingFoos.[1].Results.SomethingSchema |> should equal "xpto.Foo"
    x.[1].BlahData.BlahDataSomethingFoo2.Number |> should equal (Some 2)
    x.[1].BlahData.BlahDataSomethingFoo3.Size |> should equal 5
    x.[1].BlahData.BlahDataSomethingFoo4.IsSome |> should equal false

let normalize (str:string) =
  str.Replace("\r\n", "\n")
     .Replace("\r", "\n")

type Customer = XmlProvider<"""
  <Customer name="ACME">
    <Order Number="A012345">
      <OrderLine Item="widget">
          <Quantity>2</Quantity>
      </OrderLine>
    </Order>
    <Order>
      <OrderLine Item="5" />
    </Order>
    <Order />
    <x y="">foo</x>
    <z>1</z>
    <z>a</z>
    <z>b</z>
    <w>a</w>
    <w>b</w>
  </Customer>
""">

[<Test>]
let ``Can construct complex objects``() =
    let customer = 
        Customer.Customer(
            "ACME", 
            [| Customer.Order(Some "A012345", None)
               Customer.Order(None, Some (Customer.OrderLine(Customer.ItemChoice(2), None)))
               Customer.Order(None, Some (Customer.OrderLine(Customer.ItemChoice("xpto"), Some 2))) |],
            Customer.X("a", "b"),
            [| Customer.Z(2); Customer.Z("foo") |],
            [| "d"; "e" |])

    customer.ToString() |> normalize |> should equal (normalize """<Customer name="ACME">
  <Order Number="A012345" />
  <Order>
    <OrderLine Item="2" />
  </Order>
  <Order>
    <OrderLine Item="xpto">
      <Quantity>2</Quantity>
    </OrderLine>
  </Order>
  <x y="a">b</x>
  <z>2</z>
  <z>foo</z>
  <w>d</w>
  <w>e</w>
</Customer>""")

[<Test>]
let ``Can construct collapsed primitive collections``() =
    let c = CollapsedCollections.Root [| "John"; "Doe" |]
    c.ToString() |> normalize |> should equal (normalize """<Root>
  <Persons>
    <Person>John</Person>
    <Person>Doe</Person>
  </Persons>
</Root>""")

[<Test>]
let ``Can construct collapsed non-primitive collections and elements with json``() =
    let pb = 
        JsonInXml.PropertyBag(
            JsonInXml.BlahData(
                [| JsonInXml.X(2, "bar") |],
                [| JsonInXml.BlahDataSomethingFoo("schema", JsonInXml.Results("schema2", Some "query")) |], 
                Unchecked.defaultof<_>, 
                Unchecked.defaultof<_>, 
                None))
    pb.ToString() |> normalize |> should equal (normalize """<PropertyBag>
  <BlahDataArray>
    <BlahData>
      <X>{
  "T": 2,
  "Val": "bar"
}</X>
      <BlahDataSomethingFoo>{
  "Something.Schema": "schema",
  "results": {
    "Something.Schema": "schema2",
    "Query": "query"
  }
}</BlahDataSomethingFoo>
    </BlahData>
  </BlahDataArray>
</PropertyBag>""")

[<Test>]
let ``Can construct elements with namespaces and heterogeneous records``() =
    let rss = AnyFeed.Choice(AnyFeed.Rss(1.0M, AnyFeed.Channel("title", "link", "description", [| |])))
    rss.ToString() |> normalize |> should equal (normalize """<rss version="1.0">
  <channel>
    <title>title</title>
    <link>link</link>
    <description>description</description>
  </channel>
</rss>""")

    let atom = 
        AnyFeed.Choice(
            AnyFeed.Feed("title", 
                         "subtitle", 
                         [| |], 
                         "id", 
                         DateTime(2014, 04, 27), 
                         AnyFeed.Entry("title2", 
                                       [| |],
                                       "id2",
                                       DateTime(2014, 04, 28),
                                       "summary",
                                       AnyFeed.Author("name", "email"))))
    atom.ToString() |> normalize |> should equal (normalize """<feed xmlns="http://www.w3.org/2005/Atom">
  <title>title</title>
  <subtitle>subtitle</subtitle>
  <id>id</id>
  <updated>2014-04-27T00:00:00.0000000</updated>
  <entry>
    <title>title2</title>
    <id>id2</id>
    <updated>2014-04-28T00:00:00.0000000</updated>
    <summary>summary</summary>
    <author>
      <name>name</name>
      <email>email</email>
    </author>
  </entry>
</feed>""")

type AtomSearch = XmlProvider<"Data/search.atom.xml", SampleIsList=true>

[<Test>]
let ``Can construct elements with heterogeneous records with primitives``() =
    let id = AtomSearch.Choice(id = "id")
    id.XElement.ToString() |> should equal """<id xmlns="http://www.w3.org/2005/Atom">id</id>"""
    let link = AtomSearch.Choice(AtomSearch.Link2("type", "href", "rel"))
    link.XElement.ToString() |> should equal """<link type="type" href="href" rel="rel" xmlns="http://www.w3.org/2005/Atom" />"""
    let title = AtomSearch.Choice(title = "title")
    title.XElement.ToString() |> should equal """<title xmlns="http://www.w3.org/2005/Atom">title</title>"""
    let updated = AtomSearch.Choice(updated = DateTime(2000, 1, 1))
    updated.XElement.ToString() |> should equal """<updated xmlns="http://www.w3.org/2005/Atom">2000-01-01T00:00:00.0000000</updated>"""
    let itemsPerPage = AtomSearch.Choice(2)
    itemsPerPage.XElement.ToString() |> should equal """<itemsPerPage xmlns="http://a9.com/-/spec/opensearch/1.1/">2</itemsPerPage>"""
    let entry = AtomSearch.Entry("id", 
                                 DateTime(2000, 2, 2), 
                                 [| |], 
                                 "title", 
                                 AtomSearch.Content("type", "value"),
                                 DateTime(2000, 3, 3),
                                 Unchecked.defaultof<_>,
                                 AtomSearch.Metadata("resultType"),
                                 "source",
                                 "lange",
                                 AtomSearch.Author("name", "uri"))
    entry.XElement.ToString() |> normalize |> should equal (normalize """<entry xmlns="http://www.w3.org/2005/Atom">
  <id>id</id>
  <published>2000-02-02T00:00:00.0000000</published>
  <title>title</title>
  <content type="type">value</content>
  <updated>2000-03-03T00:00:00.0000000</updated>
  <metadata xmlns="http://api.twitter.com/">
    <result_type>resultType</result_type>
  </metadata>
  <source xmlns="http://api.twitter.com/">source</source>
  <lang xmlns="http://api.twitter.com/">lange</lang>
  <author>
    <name>name</name>
    <uri>uri</uri>
  </author>
</entry>""")
    AtomSearch.Choice(entry).XElement.ToString() |> should equal (entry.XElement.ToString())

[<Test>]
let ``Serializing nested arrays do not introduce multiple outer tags``() =
    let t1 = Runtime.XmlRuntime.CreateRecord("translation", [| "language", "nl" :> obj |], [| "", "some text" :> obj |], "")
    let t2 = Runtime.XmlRuntime.CreateRecord("translation", [| "language", "en" :> obj |], [| "", "more text" :> obj |], "")
    let root = Runtime.XmlRuntime.CreateRecord("root", [| |], [| "translations|translation", [|t1; t2|] :> obj |], "")
    root.XElement.ToString(SaveOptions.DisableFormatting) |> should equal "<root><translations><translation language=\"nl\">some text</translation><translation language=\"en\">more text</translation></translations></root>"
