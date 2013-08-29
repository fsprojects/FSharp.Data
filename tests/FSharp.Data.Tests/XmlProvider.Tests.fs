module FSharp.Data.Tests.XmlProvider.Tests

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
  ()
