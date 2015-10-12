#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.HtmlCharRefs
#endif

open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.HtmlDocument
open FSharp.Data.HtmlNode
open FSharp.Data.JsonExtensions

let charRefsTestCases =
    JsonValue.Load(__SOURCE_DIRECTORY__ + "/data/charrefs.json")?items.AsArray()
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
    let html = """<a href="/url?q=http://fsharp.github.io/FSharp.Data/&amp;sa=U&amp;ei=sv1jU_3bMMmk0QX33YGQBw&amp;ved=0CB4QFjAA&amp;usg=AFQjCNF_2exXvCWzixA0Uj58KLThvXYUwA"><b>F# Data</b>: Library for Data Access - F# Open Source Group @ GitHub</a>"""
    let parsed = HtmlDocument.Parse html
    let expected = 
        HtmlDocument.New(
         [
           HtmlNode.NewElement("a", ["href", "/url?q=http://fsharp.github.io/FSharp.Data/&sa=U&ei=sv1jU_3bMMmk0QX33YGQBw&ved=0CB4QFjAA&usg=AFQjCNF_2exXvCWzixA0Uj58KLThvXYUwA"],
            [
               HtmlNode.NewElement("b", [HtmlNode.NewText "F# Data"])
               HtmlNode.NewText ": Library for Data Access - F# Open Source Group @ GitHub"
            ])
         ])
    parsed |> should equal expected

[<Test>]
let ``Should handle indeterminate CharRefs``() =
    HtmlCharRefs.substitute "&#xD;" |> should equal "&#xD;"