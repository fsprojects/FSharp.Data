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
open FSharp.Data
open FsUnit
open System.Xml
open System.Xml.Linq
open System.Text
open System.IO
open FSharp.Data.Runtime

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

type SimpleHtml = HtmlTableProvider<simpleHtml, PreferOptionals=true>

[<Test>]
let ``SimpleHtml infers date type correctly ``() = 
    let html = SimpleHtml.Tables.table.Load(simpleHtml)
    html.Data.[0].Date |> should equal (System.DateTime(2013, 01, 01, 12, 00, 00))

[<Test>]
let ``SimpleHtml infers int type correctly ``() = 
    let html = SimpleHtml.Tables.table.Load(simpleHtml)
    html.Data.[0].``Column 1`` |> should equal 1

[<Test>]
let ``SimpleHtml infers bool type correctly ``() = 
    let html = SimpleHtml.Tables.table.Load(simpleHtml)
    html.Data.[0].``Column 2`` |> should equal true

[<Test>]
let ``SimpleHtml infers decimal type correctly ``() = 
    let html = SimpleHtml.Tables.table.Load(simpleHtml)
    html.Data.[0].``Column 3`` |> should equal 2M

[<Test>]
let ``SimpleHtml infers as optional fail through type correctly ``() = 
    let html = SimpleHtml.Tables.table.Load(simpleHtml)
    html.Data.[0].``Column 4`` |> should equal (Some 2M)

[<Test>]
let ``Can create type for simple table``() = 
    let html = SimpleHtml.Tables.table.Load(simpleHtml)
    html.Data.[0].``Column 1`` |> should equal 1

type MarketDepth = HtmlTableProvider<"data/marketdepth.htm">

[<Test>]
let ``Can infer tables out of the market depth file``() =
    let table = MarketDepth.Tables.Table_2.Load("file://data/marketdepth.htm")
    table.Data.[0].``Settlement Day`` |> should equal (System.DateTime(2014, 1, 14, 0, 0,0))
    table.Data.[0].Period |> should equal 1
