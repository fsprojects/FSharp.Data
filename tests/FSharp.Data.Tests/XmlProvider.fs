#if INTERACTIVE
#r "../../bin/lib/net45/FSharp.Data.dll"
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#r "../../packages/test/FsUnit/lib/net46/FsUnit.NUnit.dll"
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
  XmlProvider<"Data/emptyValue.xml">.GetSample().A |> should equal ""

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

    child1.Inner |> should not' (equal None)
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

    child1.Inner |> should not' (equal None)
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
  atomFeed.Feed.IsSome |> should equal true
  atomFeed.Feed.Value.Title |> should equal "Example Feed"

  let rssFeed = AnyFeed.GetSamples().[1]
  rssFeed.Rss.IsSome |> should equal true
  rssFeed.Rss.Value.Channel.Title |> should equal "W3Schools Home Page"


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
                         DateTimeOffset(2014, 04, 27, 0, 0, 0, TimeSpan.Zero), 
                         AnyFeed.Entry("title2", 
                                       [| |],
                                       "id2",
                                       DateTimeOffset(2014, 04, 28, 0, 0, 0,TimeSpan.Zero),
                                       "summary",
                                       AnyFeed.Author("name", "email"))))
    atom.ToString() |> normalize |> should equal (normalize """<feed xmlns="http://www.w3.org/2005/Atom">
  <title>title</title>
  <subtitle>subtitle</subtitle>
  <id>id</id>
  <updated>2014-04-27T00:00:00.0000000+00:00</updated>
  <entry>
    <title>title2</title>
    <id>id2</id>
    <updated>2014-04-28T00:00:00.0000000+00:00</updated>
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
    let updated = AtomSearch.Choice(updated = DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero))
    updated.XElement.ToString() |> should equal """<updated xmlns="http://www.w3.org/2005/Atom">2000-01-01T00:00:00.0000000+00:00</updated>"""
    let itemsPerPage = AtomSearch.Choice(2)
    itemsPerPage.XElement.ToString() |> should equal """<itemsPerPage xmlns="http://a9.com/-/spec/opensearch/1.1/">2</itemsPerPage>"""
    let entry = AtomSearch.Entry("id", 
                                 DateTimeOffset(2000, 2, 2, 0, 0, 0, TimeSpan.Zero), 
                                 [| |], 
                                 "title", 
                                 AtomSearch.Content("type", "value"),
                                 DateTimeOffset(2000, 3, 3, 0, 0, 0, TimeSpan.Zero),
                                 Unchecked.defaultof<_>,
                                 AtomSearch.Metadata("resultType"),
                                 "source",
                                 "lange",
                                 AtomSearch.Author("name", "uri"))
    entry.XElement.ToString() |> normalize |> should equal (normalize """<entry xmlns="http://www.w3.org/2005/Atom">
  <id>id</id>
  <published>2000-02-02T00:00:00.0000000+00:00</published>
  <title>title</title>
  <content type="type">value</content>
  <updated>2000-03-03T00:00:00.0000000+00:00</updated>
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

type RoundtripXmlDocument = XmlProvider<"""<?xml version="1.0"?>
<doc>
    <assembly><name>lala</name></assembly>
    <members>
        <member name="">
            <summary>ala</summary>
            <param name="">lala</param>
            <param name="">lala</param>
            <returns>lala</returns>
        </member>
        <member name=""></member>
    </members>
</doc>
""">

[<Test>]
let ``Roundtripping works correctly``() =
    let original = RoundtripXmlDocument.GetSample()
    let afterRoundtrip = new RoundtripXmlDocument.Doc(original.Assembly, original.Members)
    XDocument.Parse(original.XElement.ToString()).ToString() |> should equal <| XDocument.Parse(afterRoundtrip.XElement.ToString()).ToString()

type DrugsXml = XmlProvider<"""<drugs>
  <drug>
    <name>Paracetamol</name>
    <dose>
      <div>
        <p>In children</p> <p>every six hours</p>
      </div>
    </dose>
  </drug>
</drugs>
""">

[<Test>]
let ``Should preserve whitespace between elements``() =
    let drugs = DrugsXml.GetSample().Drug
    let elements = drugs.Dose.Div.XElement.Value
    elements |> should equal "\n        In children every six hours\n      "

type ElemWithAttrs = XmlProvider<Schema = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo">
        <xs:complexType>
          <xs:attribute name="bar" type="xs:string" use="required" />
          <xs:attribute name="baz" type="xs:int" />
        </xs:complexType>
      </xs:element>
    </xs:schema>""">

[<Test>]
let ``attributes are parsed``() =
    let elm = ElemWithAttrs.Parse "<foo bar='aa' baz='2' />"
    elm.Bar |> should equal "aa"
    elm.Baz |> should equal (Some 2)
    let elm = ElemWithAttrs.Parse "<foo bar='aa' />"
    elm.Bar |> should equal "aa"
    elm.Baz |> should equal None

type ElemWithQualifiedAttrs = XmlProvider<Schema = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      targetNamespace="http://test.001"
      elementFormDefault="qualified" attributeFormDefault="qualified">
      <xs:element name="foo">
        <xs:complexType>
          <xs:attribute name="bar" type="xs:string" use="required" form="qualified" />
          <xs:attribute name="baz" type="xs:int" use="required" form="unqualified" />
        </xs:complexType>
      </xs:element>
    </xs:schema>""">


[<Test>]
let ``qualified attributes are parsed``() =
    let xml = """<foo xmlns="http://test.001" xmlns:t="http://test.001" t:bar="aa" baz="2" />"""
    let elm = ElemWithQualifiedAttrs.Parse xml
    elm.Baz |> should equal 2
    elm.Bar |> should equal "aa"


type TwoElems = XmlProvider<Schema = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo">
        <xs:complexType>
          <xs:attribute name="bar" type="xs:string" use="required" />
          <xs:attribute name="baz" type="xs:int" />
        </xs:complexType>
      </xs:element>
      <xs:element name = 'azz'>
        <xs:complexType>
          <xs:attribute name="foffolo" type="xs:string" use="required" />
          <xs:attribute name="fuffola" type="xs:date" />
        </xs:complexType>
      </xs:element>
    </xs:schema>
""">

[<Test>]
let ``multiple root elements are handled``() =
    let elm = TwoElems.Parse "<foo bar='aa' baz='2' />"
    match elm.Foo, elm.Azz with
    | Some x, None ->
        x.Bar |> should equal "aa"
        x.Baz |> should equal (Some 2)
    | _ -> failwith "Invalid"
    let elm = TwoElems.Parse "<azz foffolo='aa' fuffola='2017-12-22' />"
    match elm.Foo, elm.Azz with
    | None, Some x ->
        x.Foffolo |> should equal "aa"
        x.Fuffola |> should equal (Some <| System.DateTime(2017, 12, 22))
    | _ -> failwith "Invalid"




type AttrsAndSimpleContent = XmlProvider<Schema = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo">
        <xs:complexType>
          <xs:simpleContent>
            <xs:extension base="xs:date">
              <xs:attribute name="bar" type="xs:string" use="required"/>
              <xs:attribute name="baz" type="xs:int"/>
            </xs:extension>
          </xs:simpleContent>
        </xs:complexType>
      </xs:element>
    </xs:schema>""">

[<Test>]
let ``element with attributes can have simple content``() =
    let date = System.DateTime(1957, 8, 13)
    let foo = AttrsAndSimpleContent.Parse("""<foo bar="hello">1957-08-13</foo>""")
    foo.Value |> should equal date
    foo.Bar |> should equal "hello"
    foo.Baz |> should equal None
    let foo = AttrsAndSimpleContent.Parse("""<foo bar="hello" baz="2">1957-08-13</foo>""")
    foo.Value |> should equal date
    foo.Bar |> should equal "hello"
    foo.Baz |> should equal (Some 2)



type UntypedElement = XmlProvider<Schema = """
  <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
    elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo" />
  </xs:schema>""">

[<Test>]
let ``untyped elements have only the XElement property``() =
  let foo = UntypedElement.Parse """
  <foo>
    <anything />
    <greetings>hi</greetings>
  </foo>"""
  //printfn "%A" foo.XElement
  foo.XElement.Element(XName.Get "greetings").Value
  |> should equal "hi"


type Wildcard = XmlProvider<Schema = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo">
        <xs:complexType>
          <xs:sequence>
            <xs:element name="id" type="xs:string"/>
            <xs:any minOccurs="0"/>
          </xs:sequence>
        </xs:complexType>
      </xs:element>
    </xs:schema>
    """>

[<Test>]
let ``wildcard elements have only the XElement property``() =
  let foo = Wildcard.Parse """
  <foo>
    <id>XYZ</id>
    <anything name='abc' />
  </foo>"""
  //printfn "%A" foo.XElement
  foo.Id |> should equal "XYZ"
  foo.XElement.Element(XName.Get "anything").FirstAttribute.Value
  |> should equal "abc"

type RecursiveElements = XmlProvider<Schema = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:complexType name="TextType" mixed="true">
        <xs:choice minOccurs="0" maxOccurs="unbounded">
          <xs:element ref="bold"/>
          <xs:element ref="italic"/>
          <xs:element ref="underline"/>
        </xs:choice>
      </xs:complexType>
      <xs:element name="bold" type="TextType"/>
      <xs:element name="italic" type="TextType"/>
      <xs:element name="underline" type="TextType"/>
    </xs:schema>
    """>

[<Test>]
let ``recursive elements are supported``() =
  let doc = RecursiveElements.Parse """
    <italic>
      <bold></bold>
      <underline></underline>
      <bold>
        <italic />
        <bold />
      </bold>
    </italic>
    """
  //printfn "%A" doc.XElement
  match doc.Bold, doc.Italic, doc.Underline with
  | None, Some x, None ->
    x.Bolds.Length |> should equal 2
    x.Italics.Length |>should equal 0
    x.Underlines.Length |> should equal 1
    x.Bolds.[1].Bolds.Length |> should equal 1

  | _ -> failwith "unexpected"




type ElmWithChildChoice = XmlProvider<Schema = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo">
        <xs:complexType>
          <xs:choice>
            <xs:element name="bar" type="xs:int"/>
            <xs:element name="baz" type="xs:date"/>
          </xs:choice>
        </xs:complexType>
      </xs:element>
    </xs:schema>""">

[<Test>]
let ``choice makes properties optional``() =
    let foo = ElmWithChildChoice.Parse "<foo><baz>1957-08-13</baz></foo>"
    foo.Bar |> should equal None
    foo.Baz |> should equal (Some <| System.DateTime(1957, 8, 13))
    let foo = ElmWithChildChoice.Parse "<foo><bar>5</bar></foo>"
    foo.Bar |> should equal (Some 5)
    foo.Baz |> should equal None


type ElmWithMultipleChildElements = XmlProvider<Schema = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
        <xs:element name="foo">
          <xs:complexType>
            <xs:sequence minOccurs='0' >
              <xs:element name="bar" type="xs:int" maxOccurs='unbounded' />
              <xs:element name="baz" type="xs:date"/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
    </xs:schema>""">

[<Test>]
let ``multiple child elements become an array``() =
    let foo = ElmWithMultipleChildElements.Parse """
    <foo>
        <bar>42</bar>
        <bar>43</bar>
        <baz>1957-08-13</baz>
    </foo>"""
    foo.Bars.Length |> should equal 2
    foo.Bars.[0] |> should equal 42
    foo.Bars.[1] |> should equal 43
    foo.Baz |> should equal (Some(System.DateTime(1957, 08, 13)))


type SubstGroup = XmlProvider<Schema = """
  <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
    elementFormDefault="qualified" attributeFormDefault="unqualified">
        <xs:element name="name" type="xs:string"/>
        <xs:element name="navn" substitutionGroup="name"/>
        <xs:complexType name="custinfo">
          <xs:sequence>
            <xs:element ref="name"/>
          </xs:sequence>
        </xs:complexType>
        <xs:element name="customer" type="custinfo"/>
        <xs:element name="kunde" substitutionGroup="customer"/>
  </xs:schema>""">

[<Test>]
let ``substitution groups are like choices``() =
  let doc = SubstGroup.Parse "<kunde><name>hello</name></kunde>"
  match doc.Customer, doc.Kunde with
  | None, Some x ->
    x.Name |> should equal (Some "hello")
    x.Navn |> should equal None
  | _ -> failwith "unexpected"

  let doc = SubstGroup.Parse "<kunde><navn>hello2</navn></kunde>"
  match doc.Customer, doc.Kunde with
  | None, Some x ->
    x.Navn |> should equal (Some "hello2")
    x.Name |> should equal None
  | _ -> failwith "unexpected"



type SimpleSchema = XmlProvider<Schema = """
<schema xmlns="http://www.w3.org/2001/XMLSchema" targetNamespace="https://github.com/FSharp.Data/"
  xmlns:tns="https://github.com/FSharp.Data/" attributeFormDefault="unqualified" >
  <complexType name="root">
    <sequence>
      <element name="elem" type="string" >
        <annotation>
          <documentation>This is an identification of the preferred language</documentation>
        </annotation>
      </element>
      <element name="elem1" type="tns:foo" />
      <element name="choice" type="tns:bar" maxOccurs="2" />
      <element name="anonymousTyped">
        <complexType>
          <sequence>
            <element name="covert" type="boolean" />
          </sequence>
          <attribute name="attr" type="string" />
          <attribute name="windy">
            <simpleType>
              <restriction base="string">
                <maxLength value="10" />
              </restriction>
            </simpleType>
          </attribute>
        </complexType>
      </element>
    </sequence>
  </complexType>
  <complexType name="bar">
    <choice>
      <element name="language" type="string" >
        <annotation>
          <documentation>This is an identification of the preferred language</documentation>
        </annotation>
      </element>
      <element name="country" type="integer" />
      <element name="snur">
        <complexType>
          <sequence>
            <element name ="baz" type ="string"/>
          </sequence>
        </complexType>
      </element>
    </choice>
  </complexType>
  <complexType name="foo">
    <sequence>
      <element name="fooElem" type="boolean" />
      <element name="ISO639Code">
        <annotation>
          <documentation>This is an ISO 639-1 or 639-2 identifier</documentation>
        </annotation>
        <simpleType>
          <restriction base="string">
            <maxLength value="10" />
          </restriction>
        </simpleType>
      </element>
    </sequence>
  </complexType>
  <element name="rootElm" type="tns:root" />
</schema>""">

[<Test>]
let ``simple schema is parsed``() =
    let xml =
         """<?xml version="1.0" encoding="utf-8"?>
              <tns:rootElm xmlns:tns="https://github.com/FSharp.Data/">
                <elem>it starts with a number</elem>
                <elem1>
                  <fooElem>false</fooElem>
                  <ISO639Code>dk-DA</ISO639Code>
                </elem1>
                <choice>
                  <language>Danish</language>
                </choice>
                <choice>
                  <country>1</country>
                </choice>
                <anonymousTyped attr="fish" windy="strong" >
                  <covert>True</covert>
                </anonymousTyped>
              </tns:rootElm>
         """
    let root = SimpleSchema.Parse xml
    let choices = root.Choices
    choices.[1].Country.Value     |> should equal "1" // an integer is mapped to string (BigInteger would be better)
    choices.[0].Language.Value    |> should equal "Danish"
    root.AnonymousTyped.Covert    |> should equal true
    root.AnonymousTyped.Attr      |> should equal (Some "fish")
    root.AnonymousTyped.Windy     |> should equal (Some "strong")

type Nums = XmlProvider<Schema = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
        elementFormDefault="qualified" >
        <xs:element name='A'>
            <xs:complexType>
                <xs:attribute name='integer' type='xs:integer' />
                <xs:attribute name='int' type='xs:int' />
                <xs:attribute name='long' type='xs:long' />
                <xs:attribute name='decimal' type='xs:decimal' />
                <xs:attribute name='float' type='xs:float' />
                <xs:attribute name='double' type='xs:double' />
            </xs:complexType>
        </xs:element>
    </xs:schema>""">

[<Test>]
let ``numeric types are partially supported``() =
    Nums.Parse("<A integer='2' />").Integer |> should equal (Some "2")
    Nums.Parse("<A int='2' />").Int |> should equal (Some 2) // int32
    Nums.Parse("<A long='2' />").Long |> should equal (Some 2L) // int64
    Nums.Parse("<A decimal='2' />").Decimal |> should equal (Some 2M) // decimal
    Nums.Parse("<A float='2' />").Float |> should equal (Some "2") // should be float32
    Nums.Parse("<A double='2' />").Double |> should equal (Some 2.0) // float

type SameNames = XmlProvider<Schema="""
<xs:schema elementFormDefault="qualified" xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="X">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="E1" type="xs:string"/>
        <xs:element name="X" type="T"/>
      </xs:sequence>
    </xs:complexType>
   </xs:element>
  <xs:complexType name="T">
    <xs:sequence>
      <xs:element name="E2" type="xs:string"/>
      <xs:element name="X" type="xs:string"/>
    </xs:sequence>
  </xs:complexType>
</xs:schema>""">

[<Test>]
let ``different types with the same name are supported``() =
    let xml = """
    <X>
      <E1>a</E1>
      <X>
        <E2>b</E2>
        <X>c</X>
      </X>
    </X>"""
    let x = SameNames.Parse xml
    x.E1   |> should equal "a"
    x.X.E2 |> should equal "b"
    x.X.X  |> should equal "c"

type SchemaWithExtension = XmlProvider<Schema="""<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="items" type="ItemsType"/>
  <xs:complexType name="ItemsType">
    <xs:choice minOccurs="0" maxOccurs="unbounded">
      <xs:element name="hat" type="ProductType"/>
      <xs:element name="umbrella" type="RestrictedProductType"/>
      <xs:element name="shirt" type="ShirtType"/>
    </xs:choice>
  </xs:complexType>
  <!--Empty Content Type-->
  <xs:complexType name="ItemType" abstract="true">
  </xs:complexType>
  <!--Empty Content Extension (with Attribute Extension)-->
  <xs:complexType name="ProductType">
    <xs:sequence>
      <xs:element name="number" type="xs:integer"/>
      <xs:element name="name" type="xs:string"/>
      <xs:element name="description"
                   type="xs:string" minOccurs="0"/>
    </xs:sequence>
    <xs:anyAttribute />
  </xs:complexType>
  <!--Complex Content Restriction-->
  <xs:complexType name="RestrictedProductType">
    <xs:complexContent>
      <xs:restriction base="ProductType">
        <xs:sequence>
          <xs:element name="number" type="xs:integer"/>
          <xs:element name="name" type="xs:token"/>
        </xs:sequence>
      </xs:restriction>
    </xs:complexContent>
  </xs:complexType>
  <!--Complex Content Extension-->
  <xs:complexType name="ShirtType">
    <xs:complexContent>
      <xs:extension base="ProductType">
        <xs:choice maxOccurs="unbounded">
          <xs:element name="size" type="SmallSizeType"/>
          <xs:element name="color" type="ColorType"/>
        </xs:choice>
        <xs:attribute name="sleeve" type="xs:integer"/>
      </xs:extension>
    </xs:complexContent>
  </xs:complexType>
  <!--Simple Content Extension-->
  <xs:complexType name="SizeType">
    <xs:simpleContent>
      <xs:extension base="xs:integer">
        <xs:attribute name="system" type="xs:token"/>
      </xs:extension>
    </xs:simpleContent>
  </xs:complexType>
  <!--Simple Content Restriction-->
  <xs:complexType name="SmallSizeType">
    <xs:simpleContent>
      <xs:restriction base="SizeType">
        <xs:minInclusive value="2"/>
        <xs:maxInclusive value="6"/>
        <xs:attribute  name="system" type="xs:token"
                        use="required"/>
      </xs:restriction>
    </xs:simpleContent>
  </xs:complexType>
  <xs:complexType name="ColorType">
    <xs:attribute name="value" type="xs:string"/>
  </xs:complexType>
</xs:schema>""">

[<Test>]
let ``Extension on complex types``() =
    let xml =
        """<?xml version="1.0"?>
           <items>
             <!--You have a CHOICE of the next 3 items at this level-->
             <hat routingNum="100" effDate="2008-09-29" lang="string">
               <number>100</number>
               <name>string</name>
               <!--Optional:-->
               <description>string</description>
             </hat>
             <umbrella routingNum="1" effDate="1900-01-01">
               <number>100</number>
               <name>token</name>
             </umbrella>
             <shirt routingNum="1" effDate="1900-01-01" sleeve="100">
               <number>100</number>
               <name>token</name>
               <!--You have a CHOICE of the next 2 items at this level-->
               <size system="token">6</size>
               <color value="string"/>
             </shirt>
           </items>"""

    let items = SchemaWithExtension.Parse xml
    items.Hats.Length |> should equal 1
    items.Hats.[0].Number |> should equal "100"
    items.Hats.[0].Name |> should equal "string"
    items.Hats.[0].Description |> should equal (Some "string")
    items.Umbrellas.Length |> should equal 1
    items.Umbrellas.[0].Number |> should equal "100"
    items.Umbrellas.[0].Name |> should equal "token"
    items.Shirts.Length |> should equal 1
    items.Shirts.[0].Sleeve |> should equal (Some "100")

type NillableElements = XmlProvider<Schema= """
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
  elementFormDefault="qualified" attributeFormDefault="unqualified">
    <xs:element name="passport" type="passportType" />
    <xs:complexType name="passportType">
        <xs:sequence>
          <xs:element nillable="true" name="PassportCountry" type="xs:string"/>
          <xs:element nillable="true" name="PassportNumber" type="xs:string"/>
        </xs:sequence>
    </xs:complexType>
</xs:schema>""">

[<Test>]
let ``nillable elements are supported``() =
    let xml = """
    <passport>
      <PassportCountry>XY</PassportCountry>
      <PassportNumber xsi:nil="true"
        xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"/>
    </passport>"""

    let x = NillableElements.Parse xml
    x.PassportCountry.Nil |> should equal None
    x.PassportCountry.Value |> should equal (Some "XY")
    x.PassportNumber.Nil |> should equal (Some true)
    x.PassportNumber.Value |> should equal None

[<Literal>]
let SimpleTypesXsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
        elementFormDefault="qualified" >
        <xs:element name='A'>
            <xs:complexType>
                <xs:attribute name='int'      type='xs:int'      use="required" />
                <xs:attribute name='long'     type='xs:long'     use="required" />
                <xs:attribute name='date'     type='xs:date'     use="required" />
                <xs:attribute name='dateTime' type='xs:dateTime' use="required" />
                <xs:attribute name='boolean'  type='xs:boolean'  use="required" />
                <xs:attribute name='decimal'  type='xs:decimal'  use="required" />
                <xs:attribute name='double'   type='xs:double'   use="required" />
            </xs:complexType>
        </xs:element>
    </xs:schema>"""


type SimpleTypes = XmlProvider<Schema = SimpleTypesXsd>

open System.Xml
open System.Xml.Schema

let parseSchema xsdText =    
    let schemaSet = XmlSchemaSet() 
    use reader = XmlReader.Create(new System.IO.StringReader(xsdText))
    schemaSet.Add(null, reader) |> ignore
    schemaSet.Compile()
    schemaSet

let isValid xsd =
    let xmlSchemaSet = parseSchema xsd
    fun xml ->
        try
            (XDocument.Parse xml).Validate(xmlSchemaSet, validationEventHandler = null)
            true
        with :? XmlSchemaException as e -> 
            printfn "%s/n%s" e.Message xml
            false


[<Test>]
let ``simple types are formatted properly``() =
    let simpleValues =
      SimpleTypes.A(
        int = 0,
        long = 0L,
        date = System.DateTime.Today,
        dateTime = System.DateTimeOffset.Now,
        boolean = false,
        decimal = 0M,
        double = System.Double.NaN)
        .ToString()
    
    let minValues =
      SimpleTypes.A(
        int = System.Int32.MinValue,
        long = System.Int64.MinValue,
        date = System.DateTime.MinValue.Date,
        dateTime = System.DateTimeOffset.MinValue,
        boolean = false,
        decimal = System.Decimal.MinValue,
        double = System.Double.NegativeInfinity)
        .ToString()

    let maxValues = 
      SimpleTypes.A(
        int = System.Int32.MaxValue,
        long = System.Int64.MaxValue,
        date = System.DateTime.MaxValue.Date,
        dateTime = System.DateTimeOffset.MaxValue,
        boolean = true,
        decimal = System.Decimal.MaxValue,
        double = System.Double.PositiveInfinity)
        .ToString()

    let isValid = isValid SimpleTypesXsd
    isValid simpleValues |> should equal true
    isValid minValues |> should equal true
    isValid maxValues |> should equal true

[<Test>]
let ``time is invalid for xs:date``() =
    let simpleValues =
      SimpleTypes.A(
        int = 0,
        long = 0L,
        date = System.DateTime.Today.AddHours(1.),
        dateTime = System.DateTimeOffset.Now,
        boolean = false,
        decimal = 0M,
        double = System.Double.NaN)
        .ToString()
    let isValid = isValid SimpleTypesXsd
    isValid simpleValues |> should equal false
