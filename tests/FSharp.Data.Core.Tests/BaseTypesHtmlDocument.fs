module FSharp.Data.Tests.BaseTypesHtmlDocument

#nowarn "10001" // Disable warning for methods intended for use in generated code only

open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.Runtime.BaseTypes
open System.IO

[<Test>]
let ``HtmlDocument Create method with simple HTML`` () =
    let html = "<html><body><h1>Test</h1></body></html>"
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(false, reader)
    
    doc |> should not' (be null)
    doc.Html |> should not' (be null)

[<Test>]
let ``HtmlDocument Html property returns original document`` () =
    let html = "<html><body><h1>Test Document</h1><p>Content</p></body></html>"
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(false, reader)
    let originalDoc = doc.Html
    
    originalDoc |> should not' (be null)
    // The Html property should return the parsed HtmlDocument
    originalDoc.ToString() |> should contain "Test Document"

[<Test>]
let ``HtmlDocument Create with includeLayoutTables false`` () =
    let html = """
    <html>
    <body>
        <table cellspacing="0" cellpadding="0">
            <tr><td>Layout Table</td></tr>
        </table>
        <table>
            <tr><td>Data Table</td></tr>
        </table>
    </body>
    </html>"""
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(false, reader)
    
    doc |> should not' (be null)
    doc.Html |> should not' (be null)

[<Test>]
let ``HtmlDocument Create with includeLayoutTables true`` () =
    let html = """
    <html>
    <body>
        <table cellspacing="0" cellpadding="0">
            <tr><td>Layout Table</td></tr>
        </table>
        <table>
            <tr><td>Data Table</td></tr>
        </table>
    </body>
    </html>"""
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(true, reader)
    
    doc |> should not' (be null)
    doc.Html |> should not' (be null)

[<Test>]
let ``HtmlDocument GetTable method can access existing table by id`` () =
    let html = """
    <html>
    <body>
        <table id="testTable">
            <thead><tr><th>Name</th><th>Age</th></tr></thead>
            <tbody>
                <tr><td>John</td><td>25</td></tr>
                <tr><td>Jane</td><td>30</td></tr>
            </tbody>
        </table>
    </body>
    </html>"""
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(false, reader)
    
    // Table should be accessible by its id
    let table = doc.GetTable("testTable")
    table |> should not' (be null)
    
    // Test that an exception is thrown for unknown names
    (fun () -> doc.GetTable("UnknownTableName") |> ignore) 
    |> should throw typeof<System.Collections.Generic.KeyNotFoundException>

[<Test>]
let ``HtmlDocument GetTable method throws when table not found`` () =
    let html = "<html><body><p>No tables here</p></body></html>"
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(false, reader)
    
    // Trying to get a non-existent table should throw
    (fun () -> doc.GetTable("NonExistentTable") |> ignore) 
    |> should throw typeof<System.Collections.Generic.KeyNotFoundException>

[<Test>]
let ``HtmlDocument GetList method throws when list not found regardless`` () =
    let html = """
    <html>
    <body>
        <ul>
            <li>Item 1</li>
            <li>Item 2</li>
            <li>Item 3</li>
        </ul>
    </body>
    </html>"""
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(false, reader)
    
    // Test that exception is thrown for unknown names
    (fun () -> doc.GetList("UnknownListName") |> ignore)
    |> should throw typeof<System.Collections.Generic.KeyNotFoundException>

[<Test>]
let ``HtmlDocument GetList method throws when list not found`` () =
    let html = "<html><body><p>No lists here</p></body></html>"
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(false, reader)
    
    // Trying to get a non-existent list should throw
    (fun () -> doc.GetList("NonExistentList") |> ignore)
    |> should throw typeof<System.Collections.Generic.KeyNotFoundException>

[<Test>]
let ``HtmlDocument GetDefinitionList method throws when definition list not found regardless`` () =
    let html = """
    <html>
    <body>
        <dl>
            <dt>Term 1</dt>
            <dd>Definition 1</dd>
            <dt>Term 2</dt>
            <dd>Definition 2</dd>
        </dl>
    </body>
    </html>"""
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(false, reader)
    
    // Test that exception is thrown for unknown names
    (fun () -> doc.GetDefinitionList("UnknownDefinitionListName") |> ignore)
    |> should throw typeof<System.Collections.Generic.KeyNotFoundException>

[<Test>]
let ``HtmlDocument GetDefinitionList method throws when definition list not found`` () =
    let html = "<html><body><p>No definition lists here</p></body></html>"
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(false, reader)
    
    // Trying to get a non-existent definition list should throw
    (fun () -> doc.GetDefinitionList("NonExistentDefinitionList") |> ignore)
    |> should throw typeof<System.Collections.Generic.KeyNotFoundException>

[<Test>]
let ``HtmlDocument handles minimal valid HTML document`` () =
    let html = "<html><body></body></html>"
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(false, reader)
    
    doc |> should not' (be null)
    doc.Html |> should not' (be null)

[<Test>]
let ``HtmlDocument handles complex nested HTML`` () =
    let html = """
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="UTF-8">
        <title>Test Document</title>
    </head>
    <body>
        <header>
            <h1>Main Title</h1>
        </header>
        <main>
            <article>
                <h2>Article Title</h2>
                <p>Article content with <em>emphasis</em> and <strong>strong text</strong>.</p>
                <table>
                    <caption>Data Table</caption>
                    <thead>
                        <tr><th>Column 1</th><th>Column 2</th></tr>
                    </thead>
                    <tbody>
                        <tr><td>Data 1</td><td>Data 2</td></tr>
                    </tbody>
                </table>
                <ul>
                    <li>List item 1</li>
                    <li>List item 2 with <a href="http://example.com">link</a></li>
                </ul>
                <dl>
                    <dt>Definition term 1</dt>
                    <dd>Definition description 1</dd>
                    <dt>Definition term 2</dt>
                    <dd>Definition description 2</dd>
                </dl>
            </article>
        </main>
        <footer>
            <p>&copy; 2023 Test</p>
        </footer>
    </body>
    </html>"""
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(false, reader)
    
    doc |> should not' (be null)
    doc.Html |> should not' (be null)
    
    // All elements (table, list, definition list) are named after the preceding Article Title heading
    let table = doc.GetTable("Article Title")
    let list = doc.GetList("Article Title") 
    let definitionList = doc.GetDefinitionList("Article Title")
    
    table |> should not' (be null)
    list |> should not' (be null)
    definitionList |> should not' (be null)

[<Test>]
let ``HtmlDocument multiple tables with clear structure`` () =
    let html = """
    <html>
    <body>
        <h2>First Table</h2>
        <table><tr><td>Table 1 Data</td><td>More Data</td></tr><tr><td>Row 2</td><td>Row 2 Col 2</td></tr></table>
        <h2>Second Table</h2>  
        <table><tr><td>Table 2 Data</td><td>More Data</td></tr><tr><td>Row 2</td><td>Row 2 Col 2</td></tr></table>
        <h2>Lists Section</h2>
        <ul><li>List 1 Item</li><li>Second Item</li></ul>
        <ol><li>List 2 Item</li><li>Ordered Second</li></ol>
        <h2>Definition Lists Section</h2>
        <dl><dt>DL 1 Term</dt><dd>DL 1 Def</dd><dt>DL 1 Term 2</dt><dd>DL 1 Def 2</dd></dl>
        <dl><dt>DL 2 Term</dt><dd>DL 2 Def</dd><dt>DL 2 Term 2</dt><dd>DL 2 Def 2</dd></dl>
    </body>
    </html>"""
    use reader = new StringReader(html)
    
    let doc = HtmlDocument.Create(false, reader)
    
    // Should be able to access tables by their heading names
    let table1 = doc.GetTable("First Table")
    let table2 = doc.GetTable("Second Table")
    // Lists should be named after their preceding headings  
    let list1 = doc.GetList("Lists Section")  
    // Definition lists should be named after their preceding heading
    let dl1 = doc.GetDefinitionList("Definition Lists Section")
    
    table1 |> should not' (be null)
    table2 |> should not' (be null)
    list1 |> should not' (be null)
    dl1 |> should not' (be null)
    
    // Test that accessing non-existent elements throws appropriate exceptions
    (fun () -> doc.GetTable("NonExistent") |> ignore) |> should throw typeof<System.Collections.Generic.KeyNotFoundException>
    (fun () -> doc.GetList("NonExistent") |> ignore) |> should throw typeof<System.Collections.Generic.KeyNotFoundException>
    (fun () -> doc.GetDefinitionList("NonExistent") |> ignore) |> should throw typeof<System.Collections.Generic.KeyNotFoundException>