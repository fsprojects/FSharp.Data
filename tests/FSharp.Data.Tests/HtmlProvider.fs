#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../bin/FSharp.Data.Experimental.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.HtmlProvider
#endif

open NUnit.Framework
open FsUnit
open System
open FSharp.Data

[<Literal>]
let simpleHtml = """<html>
                    <body>
                        <table title="table">
                            <tr><th>Date</th><th>Column 1</th><th>Column 2</th><th>Column 3</th><th>Column 4</th></tr>
                            <tr><td>01/01/2013 12:00</td><td>1</td><td>yes</td><td>2</td><td>2</td></tr>
                            <tr><td>01/02/2013 12:00</td><td>2</td><td>no</td><td>2.5</td><td>2</td></tr>
                            <tr><td>01/03/2013 12:00</td><td>3</td><td>true</td><td>3.456</td><td>2.3</td></tr>
                            <tr><td>01/04/2013 12:00</td><td>4</td><td>true</td><td>2.4</td><td>&nbsp;</td></tr>
                        </table>
                    </body>
                </html>"""

type SimpleHtml = HtmlProvider<simpleHtml, PreferOptionals=true>

[<Test>]
let ``SimpleHtml infers date type correctly ``() = 
    let table = SimpleHtml().Tables.Table
    table.Rows.[0].Date |> should equal (DateTime(2013, 01, 01, 12, 00, 00))

[<Test>]
let ``SimpleHtml infers int type correctly ``() = 
    let table = SimpleHtml().Tables.Table
    table.Rows.[0].``Column 1`` |> should equal 1

[<Test>]
let ``SimpleHtml infers bool type correctly ``() = 
    let table = SimpleHtml().Tables.Table
    table.Rows.[0].``Column 2`` |> should equal true

[<Test>]
let ``SimpleHtml infers decimal type correctly ``() = 
    let table = SimpleHtml().Tables.Table
    table.Rows.[0].``Column 3`` |> should equal 2M

[<Test>]
let ``SimpleHtml infers as optional fail through type correctly ``() = 
    let table = SimpleHtml().Tables.Table
    table.Rows.[0].``Column 4`` |> should equal (Some 2M)

[<Test>]
let ``Can create type for simple table``() = 
    let table = SimpleHtml().Tables.Table
    table.Rows.[0].``Column 1`` |> should equal 1

type MarketDepth = HtmlProvider<"data/marketdepth.htm">

[<Test>]
let ``Can infer tables out of the market depth file``() =
    let table = MarketDepth().Tables.Table3
    table.Rows.[0].``Settlement Day`` |> should equal (DateTime(2014, 1, 14, 0, 0,0))
    table.Rows.[0].Period |> should equal 1

[<Test>]
let ``NuGet table gets all rows``() =
    let table = HtmlProvider<"data/NuGet.html">.GetSample().Tables.Table0
    table.Rows.Length |> should equal 35

