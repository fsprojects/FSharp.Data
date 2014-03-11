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
open FSharp.Data
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
    let result = HtmlDocument.Parse simpleHtml
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
    let tables = HtmlRuntime.parseTables html

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
    let tables = HtmlRuntime.parseTables html

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
    let tables = HtmlRuntime.parseTables html |> Seq.toList

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
    let tables = HtmlRuntime.parseTables html |> Seq.toList

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
    let tables = HtmlRuntime.parseTables html

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
    let tables = HtmlRuntime.parseTables html

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
    let tables = HtmlRuntime.parseTables html
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
    let tables = HtmlRuntime.parseTables html
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "savings_table"
    tables.[0].Headers |> should equal ["Month";"Savings"]
    (tables.[0].Rows.[0]) |> should equal ["Sum"; "$180"]
    (tables.[0].Rows.[1]) |> should equal ["January"; "$100"]

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
    let tables = HtmlRuntime.parseTables html

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "Table_0"
    tables.[0].Headers |> should equal ["Column 1"]
    (tables.[0].Rows.[0]) |> should equal ["1"]

type CharRefs = FSharp.Data.JsonProvider<"data/charrefs-full.json">

open System
open System.Globalization
open System.Text.RegularExpressions

let charRefsTestCases =
    CharRefs.GetSample().Items
    |> Array.map (fun x -> [|x.Key; x.Characters|])
    |> Array.filter (fun [|_;c|] -> c <> "")


[<Test>]
[<TestCaseSource "charRefsTestCases">]
let ``Should substitute char references``(ref:string, result:string) = 
    let html = sprintf """<html><body>%s</body></html>""" ref
    let parsed = HtmlDocument.Parse html
    let expected = 
        HtmlDocument("", 
         [
           HtmlElement("html", [] ,
            [
               HtmlElement("body", [],
                [
                   HtmlText(result)
                ])
            ])
        ])
    parsed |> should equal expected