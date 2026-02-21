module FSharp.Data.Tests.BaseTypesHtmlGenerics

open NUnit.Framework
open FsUnit
open System
open System.Reflection
open System.IO
open FSharp.Data
open FSharp.Data.Runtime.BaseTypes

// ============================================
// BaseTypes.HtmlDocument Coverage Tests
// ============================================

// Use reflection to create HtmlDocument since Create is for generated code only
let private createHtmlDocumentFromString (html: string) =
    use reader = new StringReader(html)
    let createMethod = typeof<HtmlDocument>.GetMethod("Create", [| typeof<bool>; typeof<System.IO.TextReader> |])
    createMethod.Invoke(null, [| false; reader |]) :?> HtmlDocument

[<Test>]
let ``HtmlDocument Create should work using reflection`` () =
    let html = """<html><body><table id="test"><tr><td>Test</td></tr></table></body></html>"""
    let doc = createHtmlDocumentFromString html
    
    doc |> should not' (be null)
    doc.Html |> should not' (be null)

[<Test>]
let ``HtmlDocument GetTable should handle reflection calls`` () =
    let html = """<html><body><table id="testTable"><tr><td>Test</td></tr></table></body></html>"""
    let doc = createHtmlDocumentFromString html
    
    let getTableMethod = doc.GetType().GetMethod("GetTable")
    // Even if the table is not found, the method should exist and be callable
    getTableMethod |> should not' (be null)

[<Test>]
let ``HtmlDocument GetList should handle reflection calls`` () =
    let html = """<html><body><ul id="testList"><li>Item</li></ul></body></html>"""
    let doc = createHtmlDocumentFromString html
    
    let getListMethod = doc.GetType().GetMethod("GetList")
    // Even if the list is not found, the method should exist and be callable
    getListMethod |> should not' (be null)

[<Test>]
let ``HtmlDocument GetDefinitionList should handle reflection calls`` () =
    let html = """<html><body><dl id="testDefList"><dt>Term</dt><dd>Definition</dd></dl></body></html>"""
    let doc = createHtmlDocumentFromString html
    
    let getDefListMethod = doc.GetType().GetMethod("GetDefinitionList")
    // Even if the definition list is not found, the method should exist and be callable
    getDefListMethod |> should not' (be null)

// ============================================
// BaseTypes.HtmlTable<T> Coverage Tests
// ============================================

[<Test>]
let ``HtmlTable<T> Create with headers should work using reflection`` () =
    let html = """<html><body><table id="testTable"><tr><th>Name</th><th>Age</th></tr><tr><td>John</td><td>30</td></tr></table></body></html>"""
    let doc = createHtmlDocumentFromString html
    let converter = Func<string[], string>(fun row -> String.Join(",", row))
    let createMethod = typeof<HtmlTable<string>>.GetMethod("Create", [| typeof<Func<string[], string>>; typeof<HtmlDocument>; typeof<string>; typeof<bool> |])
    
    let table = createMethod.Invoke(null, [| converter; doc; "testTable"; true |]) :?> HtmlTable<string>
    
    table.Name |> should equal "testTable"
    table.Headers |> should not' (be null)
    table.Rows.Length |> should be (greaterThan 0)
    table.Html |> should not' (be null)

[<Test>]
let ``HtmlTable<T> Create without headers should work using reflection`` () =
    let html = """<html><body><table id="testTable"><tr><td>John</td><td>30</td></tr><tr><td>Jane</td><td>25</td></tr></table></body></html>"""
    let doc = createHtmlDocumentFromString html
    let converter = Func<string[], string>(fun row -> String.Join("|", row))
    let createMethod = typeof<HtmlTable<string>>.GetMethod("Create", [| typeof<Func<string[], string>>; typeof<HtmlDocument>; typeof<string>; typeof<bool> |])
    
    let table = createMethod.Invoke(null, [| converter; doc; "testTable"; false |]) :?> HtmlTable<string>
    
    table.Name |> should equal "testTable"
    table.Headers |> should be null
    table.Rows.Length |> should be (greaterThan 0)
    table.Html |> should not' (be null)

[<Test>]
let ``HtmlTable<T> properties should be accessible`` () =
    let html = """<html><body><table id="testTable"><tr><th>Col1</th><th>Col2</th></tr><tr><td>A</td><td>B</td></tr></table></body></html>"""
    let doc = createHtmlDocumentFromString html
    let converter = Func<string[], int>(fun row -> row.Length)
    let createMethod = typeof<HtmlTable<int>>.GetMethod("Create", [| typeof<Func<string[], int>>; typeof<HtmlDocument>; typeof<string>; typeof<bool> |])
    
    let table = createMethod.Invoke(null, [| converter; doc; "testTable"; true |]) :?> HtmlTable<int>
    
    table.Name |> should equal "testTable"
    table.Headers.Value |> should haveLength 2
    table.Rows |> should haveLength 1
    table.Rows.[0] |> should equal 2
    table.Html |> should not' (be null)

// ============================================
// BaseTypes.HtmlList<T> Coverage Tests  
// ============================================

[<Test>]
let ``HtmlList<T> Create should work using reflection`` () =
    let html = """<html><body><ul id="testList"><li>Item 1</li><li>Item 2</li><li>Item 3</li></ul></body></html>"""
    let doc = createHtmlDocumentFromString html
    let converter = Func<string, int>(fun item -> item.Length)
    let createMethod = typeof<HtmlList<int>>.GetMethod("Create", [| typeof<Func<string, int>>; typeof<HtmlDocument>; typeof<string> |])
    
    let list = createMethod.Invoke(null, [| converter; doc; "testList" |]) :?> HtmlList<int>
    
    list.Name |> should equal "testList"
    list.Values.Length |> should be (greaterThan 0)
    list.Html |> should not' (be null)

[<Test>]
let ``HtmlList<T> CreateNested should work using reflection`` () =
    let html = """<html><body><dl id="testDefList"><dt>Term 1</dt><dd>Definition 1</dd><dd>Another definition</dd><dt>Term 2</dt><dd>Definition 2</dd></dl></body></html>"""
    let doc = createHtmlDocumentFromString html
    let converter = Func<string, string>(fun item -> item.ToUpper())
    let createMethod = typeof<HtmlList<string>>.GetMethod("CreateNested", [| typeof<Func<string, string>>; typeof<HtmlDocument>; typeof<string>; typeof<int> |])
    
    let list = createMethod.Invoke(null, [| converter; doc; "testDefList"; 0 |]) :?> HtmlList<string>
    
    list.Name |> should not' (be null)
    list.Values.Length |> should be (greaterThan 0)
    list.Html |> should not' (be null)

[<Test>]
let ``HtmlList<T> properties should be accessible`` () =
    let html = """<html><body><ul id="testList"><li>Item 1</li><li>Item 2</li></ul></body></html>"""
    let doc = createHtmlDocumentFromString html
    let converter = Func<string, string>(fun item -> item.Replace("Item", "Element"))
    let createMethod = typeof<HtmlList<string>>.GetMethod("Create", [| typeof<Func<string, string>>; typeof<HtmlDocument>; typeof<string> |])
    
    let list = createMethod.Invoke(null, [| converter; doc; "testList" |]) :?> HtmlList<string>
    
    list.Name |> should equal "testList"
    list.Values.Length |> should be (greaterThan 0)
    list.Html |> should not' (be null)

[<Test>]
let ``HtmlList<T> with complex converter should work using reflection`` () =
    let html = """<html><body><ul id="testList"><li>Item 1</li><li>Item 2</li></ul></body></html>"""
    let doc = createHtmlDocumentFromString html
    
    // Complex converter that creates a tuple
    let converter = Func<string, string * int>(fun item -> (item.Trim(), item.Length))
    let createMethod = typeof<HtmlList<string * int>>.GetMethod("Create", [| typeof<Func<string, string * int>>; typeof<HtmlDocument>; typeof<string> |])
    
    let list = createMethod.Invoke(null, [| converter; doc; "testList" |]) :?> HtmlList<string * int>
    
    list.Name |> should equal "testList"
    list.Values.Length |> should be (greaterThan 0)
    let (text, length) = list.Values.[0]
    text |> should not' (be null)
    length |> should be (greaterThan 0)