module FSharp.Data.Tests.BaseTypesHtmlDocument

open System.IO
open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.Runtime.BaseTypes

#nowarn "10001" // Suppress "intended for use in generated code only" warnings

[<Test>]
let ``HtmlDocument.Create successfully parses HTML with tables`` () =
    let htmlContent = """
    <html>
        <body>
            <table id="test-table">
                <tr><th>Name</th><th>Age</th></tr>
                <tr><td>John</td><td>30</td></tr>
            </table>
        </body>
    </html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    
    htmlDoc.Html.ToString() |> should contain "test-table"

[<Test>]
let ``HtmlDocument.Create with includeLayoutTables true`` () =
    let htmlContent = """
    <html>
        <body>
            <table id="layout-table">
                <tr><td>Layout cell</td></tr>
            </table>
        </body>
    </html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(true, reader)
    
    htmlDoc.Html.ToString() |> should contain "layout-table"

[<Test>]
let ``HtmlDocument.Create with includeLayoutTables false`` () =
    let htmlContent = """
    <html>
        <body>
            <table id="data-table">
                <tr><td>Data cell</td></tr>
            </table>
        </body>
    </html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    
    htmlDoc.Html.ToString() |> should contain "data-table"

[<Test>]
let ``HtmlDocument.Html property returns the parsed document`` () =
    let htmlContent = """<html><body><h1>Test Title</h1></body></html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    let doc = htmlDoc.Html
    
    doc.ToString() |> should contain "Test Title"

[<Test>]
let ``HtmlDocument.GetTable retrieves table by id`` () =
    let htmlContent = """
    <html>
        <body>
            <table id="data-table">
                <tr><th>Column1</th><th>Column2</th></tr>
                <tr><td>Value1</td><td>Value2</td></tr>
            </table>
        </body>
    </html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    let table = htmlDoc.GetTable("data-table")
    
    table.Name |> should equal "data-table"

[<Test>]
let ``HtmlDocument.GetTable throws when table not found`` () =
    let htmlContent = """<html><body><p>No tables here</p></body></html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    
    (fun () -> htmlDoc.GetTable("nonexistent") |> ignore) |> should throw typeof<System.Collections.Generic.KeyNotFoundException>

[<Test>]
let ``HtmlDocument.GetList retrieves list by id`` () =
    let htmlContent = """
    <html>
        <body>
            <ul id="item-list">
                <li>Item 1</li>
                <li>Item 2</li>
            </ul>
        </body>
    </html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    let list = htmlDoc.GetList("item-list")
    
    list.Name |> should equal "item-list"

[<Test>]
let ``HtmlDocument.GetList works with ordered lists`` () =
    let htmlContent = """
    <html>
        <body>
            <ol id="numbered-list">
                <li>First</li>
                <li>Second</li>
            </ol>
        </body>
    </html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    let list = htmlDoc.GetList("numbered-list")
    
    list.Name |> should equal "numbered-list"

[<Test>]
let ``HtmlDocument.GetList throws when list not found`` () =
    let htmlContent = """<html><body><p>No lists here</p></body></html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    
    (fun () -> htmlDoc.GetList("nonexistent") |> ignore) |> should throw typeof<System.Collections.Generic.KeyNotFoundException>

[<Test>]
let ``HtmlDocument.GetDefinitionList retrieves definition list by id`` () =
    let htmlContent = """
    <html>
        <body>
            <dl id="def-list">
                <dt>Term1</dt>
                <dd>Definition1</dd>
                <dt>Term2</dt>
                <dd>Definition2</dd>
            </dl>
        </body>
    </html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    let defList = htmlDoc.GetDefinitionList("def-list")
    
    defList.Name |> should equal "def-list"

[<Test>]
let ``HtmlDocument.GetDefinitionList throws when definition list not found`` () =
    let htmlContent = """<html><body><p>No definition lists here</p></body></html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    
    (fun () -> htmlDoc.GetDefinitionList("nonexistent") |> ignore) |> should throw typeof<System.Collections.Generic.KeyNotFoundException>

[<Test>]
let ``HtmlDocument.Create handles empty HTML`` () =
    let htmlContent = "<html><body></body></html>"
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    
    htmlDoc.Html.ToString() |> should not' (equal "")

[<Test>]
let ``HtmlDocument.Create handles malformed HTML gracefully`` () =
    let htmlContent = "<html><body><h1>Test Content</h1><p>Valid paragraph</p><div>Unclosed div"
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    
    // Parser handles malformed HTML by auto-closing tags and preserving content
    let htmlString = htmlDoc.Html.ToString()
    htmlString |> should contain "Test Content"
    htmlString |> should contain "Valid paragraph"
    // The parser should preserve at least some structure even with malformed HTML

[<Test>]
let ``HtmlDocument.Create processes multiple tables correctly`` () =
    let htmlContent = """
    <html>
        <body>
            <table id="table1">
                <tr><td>Table 1 Content</td></tr>
            </table>
            <table id="table2">
                <tr><td>Table 2 Content</td></tr>
            </table>
        </body>
    </html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    
    // Use the actual generated names since HTML parsing creates unique names
    htmlDoc.Html.ToString() |> should contain "Table 1 Content"
    htmlDoc.Html.ToString() |> should contain "Table 2 Content"

[<Test>]
let ``HtmlDocument.Create processes multiple lists correctly`` () =
    let htmlContent = """
    <html>
        <body>
            <ul id="list1">
                <li>List 1 Item</li>
            </ul>
            <ol id="list2">
                <li>List 2 Item</li>
            </ol>
        </body>
    </html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    
    // Verify the content is parsed correctly
    htmlDoc.Html.ToString() |> should contain "List 1 Item"
    htmlDoc.Html.ToString() |> should contain "List 2 Item"

[<Test>]
let ``HtmlDocument.Create processes multiple definition lists correctly`` () =
    let htmlContent = """
    <html>
        <body>
            <dl id="def1">
                <dt>Term A</dt>
                <dd>Definition A</dd>
            </dl>
            <dl id="def2">
                <dt>Term B</dt>
                <dd>Definition B</dd>
            </dl>
        </body>
    </html>"""
    
    use reader = new StringReader(htmlContent)
    let htmlDoc = HtmlDocument.Create(false, reader)
    
    // Verify the definition lists are parsed correctly
    htmlDoc.Html.ToString() |> should contain "Term A"
    htmlDoc.Html.ToString() |> should contain "Definition A"
    htmlDoc.Html.ToString() |> should contain "Term B"
    htmlDoc.Html.ToString() |> should contain "Definition B"