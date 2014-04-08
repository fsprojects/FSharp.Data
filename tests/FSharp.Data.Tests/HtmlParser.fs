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
open FSharp.Data.Html

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
        doc "" 
         [
           element "html" []
            [
               element "head" [] 
                [
                    element "script" [
                                      "language","JavaScript"
                                      "src","/bwx_generic.js"
                                     ] []
                    element "link" [
                                      "rel","stylesheet"
                                      "type","text/css"
                                      "href","/bwx_style.css"
                                   ][]
                ]
               element "body" []
                [
                    element "img" ["src", "myimg.jpg"] []
                    element "table" ["title", "table"]
                     [
                        element "tr" [] [element "th" [] [content Content "Column 1"]; element "th" [] [content Content "Column 2"]]
                        element "tr" [] [element "td" [] [content Content "1"]; element "td" [] [content Content "yes"]]
                     ]    
                ]
            ]
        ]
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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables false

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
                            <tr><td>2</td></tr>
                        </table>
                    </body>
                </html>"""
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables false

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"
    tables.[0].Headers |> should equal ["Column 1"]
    (tables.[0].Rows.[0]) |> should equal ["1"]

[<Test>]
let ``Ignores empty tables``() = 
    let html = """<html>
                    <body>
                        <table id="table">
                        </table>
                    </body>
                </html>"""
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables false

    tables.Length |> should equal 0

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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables false

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"
    tables.[0].Headers |> should equal ["Column0"]
    (tables.[0].Rows) |> should equal [["2"]; ["1"]; ["3"]]

[<Test>]
let ``Can parse tables with no headers and only 2 rows``() = 
    let html = """<html>
                    <body>
                        <table id="table">
                            <tr><td>1</td></tr>
                            <tr><td>3</td></tr>
                        </table>
                    </body>
                </html>"""
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables false

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"
    tables.[0].Headers |> should equal ["Column0"]
    (tables.[0].Rows) |> should equal [["1"]; ["3"]]

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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables false

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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables false

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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables false

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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables false

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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables false
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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables false
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
    let tables = html |> HtmlDocument.Parse |> HtmlRuntime.getTables false

    tables.Length |> should equal 1
    tables.[0].Name |> should equal "Table_0"
    tables.[0].Headers |> should equal ["Column 1"]
    (tables.[0].Rows.[0]) |> should equal ["1"]


[<Test>]
let ``Can handle html with doctype and xml namespaces``() = 
    let html = """<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd"><html lang="en" xml:lang="en" xmlns="http://www.w3.org/1999/xhtml"><body>content</body></html>"""
    let htmlDoc = HtmlDocument.Parse html
    let expected = 
            doc "DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\""
             [
               element "html" ["lang","en"; "xml:lang","en"; "xmlns","http://www.w3.org/1999/xhtml"]
                [
                   element "body" []
                    [
                       content Content "content"
                    ]
                ]
            ]
    expected |> should equal htmlDoc

[<Test>]
let ``Can find header when nested in a div``() = 
    let tables = 
        HtmlDocument.Load "data/wimbledon_wikipedia.html" 
        |> HtmlRuntime.getTables false
        |> List.map (fun t -> t.Name, t)
        |> Map.ofList
    
    Map.containsKey "Ranking points [ edit ]" tables |> should equal true
    Map.containsKey "Records [ edit ]" tables |> should equal true
    Map.containsKey "Current champions [ edit ]" tables |> should equal true

[<Test>]
let ``Can parse tables imdb chart``() = 
    let imdb = HtmlDocument.Load "data/imdb_chart.htm"
    let tables = imdb |> HtmlRuntime.getTables false
    tables.Length |> should equal 2
    tables.[0].Name |> should equal "Top 250"
    tables.[0].Rows.Length |> should equal 250
