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
open FSharp.Data.Html
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
        doc "" 
         [
           element "html" []
            [
               element "body" []
                [
                   content Content result
                ]
            ]
         ]
    parsed |> should equal expected

