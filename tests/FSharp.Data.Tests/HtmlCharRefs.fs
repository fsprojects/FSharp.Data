#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.HtmlCharRefs
#endif

open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.HtmlDocument
open FSharp.Data.HtmlNode
open FSharp.Data.Runtime

type CharRefs = FSharp.Data.JsonProvider<"data/charrefs.json">

let charRefsTestCases =
    CharRefs.GetSample().Items
    |> Array.filter (fun x -> x.Characters <> "")
    |> Array.map (fun x -> [| x.Key; x.Characters |])

///When using `data\charrefs-full.json` there seems to be some encoding problems
///and equality issues on these characters however this gives a resonable 
///cross-section of the named char refs in the HTML standard. 
[<Test>]
[<TestCaseSource "charRefsTestCases">]
let ``Should substitute char references``(ref:string, result:string) = 
    let html = sprintf """<html><body>%s</body></html>""" ref
    let parsed = HtmlDocument.Parse html
    let expected = 
        createDoc "" 
         [
           createElement "html" []
            [
               createElement "body" []
                [
                   createText result
                ]
            ]
         ]
    parsed |> should equal expected

[<Test>]
let ``Should substitute char references in attribute``() = 
    let html = """<a href="/url?q=http://fsharp.github.io/FSharp.Data/&amp;sa=U&amp;ei=sv1jU_3bMMmk0QX33YGQBw&amp;ved=0CB4QFjAA&amp;usg=AFQjCNF_2exXvCWzixA0Uj58KLThvXYUwA"><b>F# Data</b>: Library for Data Access - F# Open Source Group @ GitHub</a>"""
    let parsed = HtmlDocument.Parse html
    let expected = 
        createDoc "" 
         [
           createElement "a" ["href", "/url?q=http://fsharp.github.io/FSharp.Data/&sa=U&ei=sv1jU_3bMMmk0QX33YGQBw&ved=0CB4QFjAA&usg=AFQjCNF_2exXvCWzixA0Uj58KLThvXYUwA"]
            [
               createElement "b" []
                [
                   createText "F# Data"
                ]
               createText ": Library for Data Access - F# Open Source Group @ GitHub"
            ]
         ]
    parsed |> should equal expected

[<Test>]
let ``Should handle interderimanent CharRefs``() =
    HtmlCharRefs.substitute "&#xD;" |> should equal "&#xD;"