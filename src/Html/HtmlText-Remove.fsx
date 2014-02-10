#load "../Net/Http.fs"
#load "../CommonRuntime/IO.fs"
#load "../CommonRuntime/TextConversions.fs"
#load "../CommonRuntime/TextRuntime.fs"
#load "../CommonRuntime/HtmlParser.fs"
#load "HtmlRuntime.fs"

open System.Text
open FSharp.Data.Runtime

let thead = Html.Table.parse @"file://D:\Appdev\FSharp.Data\tests\FSharp.Data.Tests\Data\SimpleHtmlTablesWithTHead.html"
let tr = Html.Table.parse @"file://D:\Appdev\FSharp.Data\tests\FSharp.Data.Tests\Data\SimpleHtmlTablesWithTr.html"
let w3cTables = Html.Table.parse @"file://D:\Appdev\FSharp.Data\tests\FSharp.Data.Tests\Data\w3c_html_tables.html" 
let wikiCounties = Html.Table.parse @"file://D:\Appdev\FSharp.Data\tests\FSharp.Data.Tests\Data\list_of_counties_wikipedia.html"

let selfClosingTag = HtmlParser.parse """<!DOCTYPE html><html><head><script>function foo() { return 0 < 1; }</script></head><body><br /><div id="foo" special>hello world</div></body></html>"""

let wikiParse = Html.Table.parse @"file://D:\Appdev\FSharp.Data\tests\FSharp.Data.Tests\Data\wimbledon_wikipedia.html"

let wikiTablePArse = Html.Table.parse @"file://D:\Appdev\FSharp.Data\tests\FSharp.Data.Tests\Data\wimbledon_wikipedia.html"

let write doc =
    use sw = new System.IO.StreamWriter(System.IO.File.OpenWrite("D:\wiki_wimbledon.html"))
    match doc with
    | Some(HtmlDocument(_, cts)) -> List.iter (Html.write sw) cts
    | None -> ()


