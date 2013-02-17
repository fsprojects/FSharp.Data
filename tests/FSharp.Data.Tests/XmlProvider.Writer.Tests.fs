module FSharp.Data.Tests.XmlProvider.Writer.Tests

open NUnit.Framework
open FsUnit
open System.Xml.Linq
open FSharp.Data
open FSharp.Data.Json.Extensions

type AuthorsXml = XmlProvider<"""<authors><author name="Ludwig" surname="Wittgenstein" age="29" isPhilosopher="True" size="30.3" /></authors>""">

//[<Test>]
//let ``Can set properties in inlined xml``() =
//    let inlined = AuthorsXml.GetSample()
//    let author = inlined.Author
//
//    author.Name <- "John"
//    author.Name |> should equal "John"
//
//    author.Age <- 30
//    author.Age |> should equal 30
//
//    author.IsPhilosopher <- false
//    author.IsPhilosopher |> should equal false
//
//    author.Size <- 42.42
//    author.Size |> should equal 42.42
//
//[<Test>]
//let ``Can add author in inlined xml``() =
//    let inlined = AuthorsXml.GetSample()
//
//    let author = inlined.NewAuthor()
//    author.Name <- "John"
//    author.Age <- 31
//    author.IsPhilosopher <- false
//    author.Size <- 22.2
//
//    inlined.AddAuthor author
//
//    let authors = inlined.GetAuthors() |> Seq.toList
//    authors.Length |> should equal 2

//[<Test>]
//let ``Can use named parameters in author constructor``() =
//    let inlined = AuthorsXml.GetSample()
//
//    let author = inlined.NewAuthor(Name="John", Age=31)
//    author.Name |> should equal "John"    
//    author.Age |> should equal 31

//[<Test>]
//let ``Can export modified xml``() = 
//    let inlined = AuthorsXml.GetSample()
//    let author = inlined.Author
//
//    author.Name <- "John"
//    author.Age <- 31
//    author.IsPhilosopher <- false
//    author.Size <- 22.2
//
//    inlined.XElement.ToString(SaveOptions.DisableFormatting)
//      .Replace("22,2","22.2")  // TODO: Use  InvariantCulture 
//    |> should equal """<authors><author name="John" surname="Wittgenstein" age="31" isPhilosopher="False" size="22.2" /></authors>"""

[<Test>]
let ``Can serialize the xml``() =
    let inlined = AuthorsXml.GetSample()
    let xml = inlined.ToString()
    xml |> should equal "<authors>\r\n  <author name=\"Ludwig\" surname=\"Wittgenstein\" age=\"29\" isPhilosopher=\"True\" size=\"30.3\" />\r\n</authors>"
