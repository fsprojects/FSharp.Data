module FSharp.Data.Tests.HtmlCharRefs

open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.HtmlDocument
open FSharp.Data.HtmlNode
open FSharp.Data.JsonExtensions

let charRefsTestCases =
    JsonValue.Load(__SOURCE_DIRECTORY__ + "/../FSharp.Data.Tests/Data/charrefs.json")?items.AsArray()
    |> Array.map (fun x -> [| x?key.AsString(); x?characters.AsString() |])

[<Test>]
[<TestCaseSource "charRefsTestCases">]
let ``Should substitute char references``(ref:string, result:string) = 
    let html = sprintf """<html><body>%s</body></html>""" ref
    let parsed = HtmlDocument.Parse html
    let expected = HtmlDocument.New([HtmlNode.NewElement("html", [HtmlNode.NewElement("body", [HtmlNode.NewText result])])])
    parsed |> should equal expected

[<Test>]
let ``Should substitute char references in attribute``() = 
    let html = """<a href="/url?q=https://fsprojects.github.io/FSharp.Data/&amp;sa=U&amp;ei=sv1jU_3bMMmk0QX33YGQBw&amp;ved=0CB4QFjAA&amp;usg=AFQjCNF_2exXvCWzixA0Uj58KLThvXYUwA"><b>FSharp.Data</b>: Library for Data Access - F# Open Source Group @ GitHub</a>"""
    let parsed = HtmlDocument.Parse html
    let expected = 
        HtmlDocument.New(
         [
           HtmlNode.NewElement("a", ["href", "/url?q=https://fsprojects.github.io/FSharp.Data/&sa=U&ei=sv1jU_3bMMmk0QX33YGQBw&ved=0CB4QFjAA&usg=AFQjCNF_2exXvCWzixA0Uj58KLThvXYUwA"],
            [
               HtmlNode.NewElement("b", [HtmlNode.NewText "FSharp.Data"])
               HtmlNode.NewText ": Library for Data Access - F# Open Source Group @ GitHub"
            ])
         ])
    parsed |> should equal expected

[<Test>]
let ``Should handle indeterminate CharRefs``() =
    HtmlCharRefs.substitute "&#xD;" |> should equal "&#xD;"

[<Test>]
let ``Should handle Unicode characters above 655355``() =
    HtmlCharRefs.substitute "&#128516;" |> should equal "😄"
