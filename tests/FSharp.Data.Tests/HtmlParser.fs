#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.HtmlParser
#endif

open NUnit.Framework
open FsUnit
open FSharp.Data
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
                    HtmlElement("script", [HtmlAttribute("language","JavaScript")
                                           HtmlAttribute("src","/bwx_generic.js")], [])
                    HtmlElement("link", [HtmlAttribute("rel","stylesheet")
                                         HtmlAttribute("type","text/css")
                                         HtmlAttribute("href","/bwx_style.css")], [])
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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"
    tables.[0].Headers |> should equal ["Column 1"]
    (tables.[0].Rows.[0]) |> should equal ["1"]

[<Test>]
let ``Can parse tables from a simple html table but infer headers``() = 
    let html = """<html>
                    <body>
                        <table id="table">
                            <tr><td>Column 1</td></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"
    tables.[0].Headers |> should equal ["Column 1"]
    (tables.[0].Rows.[0]) |> should equal ["1"]

[<Test>]
let ``Can parse tables with no headers``() = 
    let html = """<html>
                    <body>
                        <table id="table">
                            <tr><td>2</td></tr>
                            <tr><td>1</td></tr>
                            <tr><td>3</td></tr>
                        </table>
                    </body>
                </html>"""
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"
    tables.[0].Headers |> should equal []
    (tables.[0].Rows) |> should equal [["2"]; ["1"]; ["3"]]

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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables

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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables

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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables

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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables

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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables
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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables
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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "Table_0"
    tables.[0].Headers |> should equal ["Column 1"]
    (tables.[0].Rows.[0]) |> should equal ["1"]

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

[<Test>]
let ``Can handle html with doctype and xml namespaces``() = 
    let html = """<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd"><html lang="en" xml:lang="en" xmlns="http://www.w3.org/1999/xhtml"><body>content</body></html>"""
    let htmlDoc = HtmlDocument.Parse html
    let expected = 
            HtmlDocument("DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\"", 
             [
               HtmlElement("html", [HtmlAttribute("lang","en"); HtmlAttribute("xml:lang","en"); HtmlAttribute("xmlns","http://www.w3.org/1999/xhtml")] ,
                [
                   HtmlElement("body", [],
                    [
                       HtmlText("content")
                    ])
                ])
            ])
    expected |> should equal htmlDoc
