#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../bin/FSharp.Data.Experimental.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.HtmlParser
#endif

open NUnit.Framework
open FSharp.Data.Experimental
open FsUnit
open System.Xml
open System.Xml.Linq
open System.Text
open System.IO
open FSharp.Data.Runtime

[<Test>]
let ``Can handle unclosed tags correctly``() = 
    let simpleHtml = """<html>
                         <head>
                            <script language="JavaScript" src="/bwx_generic.js"></script>
                            <link rel="stylesheet" type="text/css" href="/bwx_style.css">
                            </head>
                        <body>
                            <img src="myimg.jpg">
                            <table title="table">
                                <tr><th>Column 1</th><th>Column 2</th></tr>
                                <tr><td>1</td><td>yes</td></tr>
                            </table>
                        </body>
                    </html>"""
    let result = HtmlParser.parse simpleHtml |> Option.get
    let expected = 
        HtmlDocument("", 
         [
           HtmlElement("html", [] ,
            [
               HtmlElement("head", [], 
                [
                    HtmlElement("script", [HtmlAttribute("src","/bwx_generic.js");HtmlAttribute("language","JavaScript")], [])
                    HtmlElement("link", [HtmlAttribute("href","/bwx_style.css");HtmlAttribute("type","text/css");HtmlAttribute("rel","stylesheet")], [])
                ])
               HtmlElement("body", [],
                [
                    HtmlElement("img", [HtmlAttribute("src", "myimg.jpg")], [])
                    HtmlElement("table", [HtmlAttribute("title", "table")],
                     [
                        HtmlElement("tr", [], [HtmlElement("th", [],[HtmlText("Column 1")]); HtmlElement("th", [], [HtmlText("Column 2")])])
                        HtmlElement("tr", [], [HtmlElement("td", [],[HtmlText("1")]); HtmlElement("td", [], [HtmlText("yes")])])
                     ])    
                ])
            ])
        ])
    result |> should equal expected
    

[<Literal>]
let simpleHtml = """<html>
                    <body>
                        <table title="table">
                            <tr><th>Date</th><th>Column 1</th><th>Column 2</th><th>Column 3</th></tr>
                            <tr><td>01/01/2013 12:00</td><td>1</td><td>yes</td><td>2</td></tr>
                            <tr><td>01/02/2013 12:00</td><td>2</td><td>no</td><td>2.5</td></tr>
                            <tr><td>01/03/2013 12:00</td><td>3</td><td>true</td><td>3.456</td></tr>
                        </table>
                    </body>
                </html>"""

type SimpleHtml = HtmlProvider<simpleHtml>

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
let ``Can create type for simple table``() = 
    let html = SimpleHtml.Tables.table.Load(simpleHtml)
    html.Data.[0].``Column 1`` |> should equal 1


[<Test>]
let ``Can parse tables from a simple html``() = 
    let html = """<html>
                    <body>
                        <table id="table">
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    let tables = (Html.Table.parse html)

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"
    tables.[0].Headers |> should equal ["Column 1"]
    (tables.[0].Rows.[0]) |> should equal ["1"]

[<Test>]
let ``Can parse tables from a simple html table with no defined headers``() = 
    let html = """<html>
                    <body>
                        <table id="table">
                            <tr><td>Column 1</td></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    let tables = (Html.Table.parse html)

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"
    tables.[0].Headers |> should equal ["Column 1"]
    (tables.[0].Rows.[0]) |> should equal ["1"]

[<Test>]
let ``Extracts table when title attribute is set``() = 
    let html = """<html>
                    <body>
                        <table title="table">
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    let tables = (Html.Table.parse html) |> Seq.toList

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"

[<Test>]
let ``Extracts table when name attribute is set``() = 
    let html = """<html>
                    <body>
                        <table name="table">
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    let tables = (Html.Table.parse html) |> Seq.toList

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"

[<Test>]
let ``When mutiple identifying attributes are set the id attribute is selected``() = 
    let html = """<html>
                    <body>
                        <table id="table_id" name="table_name" title="table_title">
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    let tables = (Html.Table.parse html)

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table_id"

[<Test>]
let ``When mutiple identifying attributes are set but not the id attribute is then name attribute selected``() = 
    let html = """<html>
                    <body>
                        <table title="table_title" name="table_name">
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    let tables = (Html.Table.parse html)

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table_name"

[<Test>]
let ``Extracts tables without an id title or name attribute``() = 
    let html = """<html>
                    <body>
                        <table>
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    let tables = (Html.Table.parse html)
    tables.Length |> should equal 1

[<Test>]
let ``Extracts data and headers with thead and tbody``() = 
    let html = """<table id="savings_table">
                    <thead>
                      <tr>
                        <th>Month</th><th>Savings</th>
                      </tr>
                    </thead>
                    <tfoot>
                      <tr>
                        <td>Sum</td><td>$180</td>
                      </tr>
                    </tfoot>
                    <tbody>
                      <tr>
                        <td>January</td><td>$100</td>
                      </tr>
                      <tr>
                        <td>February</td><td>$80</td>
                      </tr>
                    </tbody>
                  </table>"""
    let tables = (Html.Table.parse html)
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "savings_table"
    tables.[0].Headers |> should equal ["Month";"Savings"]
    (tables.[0].Rows.[0]) |> should equal ["January"; "$100"]

[<Test>]
let ``Extracts tables in malformed html``() = 
    let html = """<html>
                    <body> >>
                    <br><br>All Tables
                        <table>
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    let tables = (Html.Table.parse html)

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "Table_0"
    tables.[0].Headers |> should equal ["Column 1"]
    (tables.[0].Rows.[0]) |> should equal ["1"]