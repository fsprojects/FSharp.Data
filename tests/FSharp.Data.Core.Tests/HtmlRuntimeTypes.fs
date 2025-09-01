module FSharp.Data.Tests.HtmlRuntimeTypes

open System
open System.Text
open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.Runtime

#nowarn "10001" // Suppress "intended for use in generated code only" warnings

// --------------------------------------------------------------------------------------
// HtmlTableCell tests
// --------------------------------------------------------------------------------------

[<Test>]
let ``HtmlTableCell Empty IsHeader returns true`` () =
    let cell = HtmlTableCell.Empty
    cell.IsHeader |> should equal true

[<Test>]
let ``HtmlTableCell Empty Data returns empty string`` () =
    let cell = HtmlTableCell.Empty
    cell.Data |> should equal ""

[<Test>]
let ``HtmlTableCell Cell with header flag true has IsHeader true`` () =
    let cell = HtmlTableCell.Cell(true, "Test Header")
    cell.IsHeader |> should equal true

[<Test>]
let ``HtmlTableCell Cell with header flag false has IsHeader false`` () =
    let cell = HtmlTableCell.Cell(false, "Test Data")
    cell.IsHeader |> should equal false

[<Test>]
let ``HtmlTableCell Cell Data returns correct data`` () =
    let testData = "Test Cell Data"
    let cell = HtmlTableCell.Cell(false, testData)
    cell.Data |> should equal testData

[<Test>]
let ``HtmlTableCell Cell with header data returns correct data`` () =
    let headerData = "Column Header"
    let cell = HtmlTableCell.Cell(true, headerData)
    cell.Data |> should equal headerData

// --------------------------------------------------------------------------------------
// HtmlList tests
// --------------------------------------------------------------------------------------

[<Test>]
let ``HtmlList has correct Name property`` () =
    let testName = "Test List"
    let testValues = [| "Item 1"; "Item 2"; "Item 3" |]
    let htmlNode = HtmlNode.NewText("dummy")
    let htmlList = { Name = testName; Values = testValues; Html = htmlNode }
    
    htmlList.Name |> should equal testName

[<Test>]
let ``HtmlList has correct Values property`` () =
    let testName = "Test List"
    let testValues = [| "Item 1"; "Item 2"; "Item 3" |]
    let htmlNode = HtmlNode.NewText("dummy")
    let htmlList = { Name = testName; Values = testValues; Html = htmlNode }
    
    htmlList.Values |> should equal testValues

[<Test>]
let ``HtmlList ToString generates correct formatted output`` () =
    let testName = "Shopping List"
    let testValues = [| "Apples"; "Oranges"; "Bananas" |]
    let htmlNode = HtmlNode.NewText("dummy")
    let htmlList = { Name = testName; Values = testValues; Html = htmlNode }
    
    let result = htmlList.ToString()
    result |> should contain "Shopping List"
    result |> should contain "Apples"
    result |> should contain "Oranges"
    result |> should contain "Bananas"

[<Test>]
let ``HtmlList ToString with empty values`` () =
    let testName = "Empty List"
    let testValues = [||]
    let htmlNode = HtmlNode.NewText("dummy")
    let htmlList = { Name = testName; Values = testValues; Html = htmlNode }
    
    let result = htmlList.ToString()
    result |> should contain "Empty List"
    // Should not throw exception with empty values

[<Test>]
let ``HtmlList ToString with single value`` () =
    let testName = "Single Item List"
    let testValues = [| "Only Item" |]
    let htmlNode = HtmlNode.NewText("dummy")
    let htmlList = { Name = testName; Values = testValues; Html = htmlNode }
    
    let result = htmlList.ToString()
    result |> should contain "Single Item List"
    result |> should contain "Only Item"

// --------------------------------------------------------------------------------------
// HtmlDefinitionList tests
// --------------------------------------------------------------------------------------

[<Test>]
let ``HtmlDefinitionList has correct Name property`` () =
    let testName = "Definition List"
    let htmlNode = HtmlNode.NewText("dummy")
    let innerList = { Name = "Term 1"; Values = [| "Definition 1" |]; Html = htmlNode }
    let htmlDefList = { Name = testName; Definitions = [ innerList ]; Html = htmlNode }
    
    htmlDefList.Name |> should equal testName

[<Test>]
let ``HtmlDefinitionList has correct Definitions property`` () =
    let testName = "Definition List"
    let htmlNode = HtmlNode.NewText("dummy")
    let innerList1 = { Name = "Term 1"; Values = [| "Definition 1" |]; Html = htmlNode }
    let innerList2 = { Name = "Term 2"; Values = [| "Definition 2a"; "Definition 2b" |]; Html = htmlNode }
    let definitions = [ innerList1; innerList2 ]
    let htmlDefList = { Name = testName; Definitions = definitions; Html = htmlNode }
    
    htmlDefList.Definitions |> should equal definitions

[<Test>]
let ``HtmlDefinitionList ToString generates correct formatted output`` () =
    let testName = "Technical Terms"
    let htmlNode = HtmlNode.NewText("dummy")
    let term1 = { Name = "API"; Values = [| "Application Programming Interface" |]; Html = htmlNode }
    let term2 = { Name = "REST"; Values = [| "Representational State Transfer" |]; Html = htmlNode }
    let htmlDefList = { Name = testName; Definitions = [ term1; term2 ]; Html = htmlNode }
    
    let result = htmlDefList.ToString()
    result |> should contain "Technical Terms"
    result |> should contain "API"
    result |> should contain "Application Programming Interface"
    result |> should contain "REST"
    result |> should contain "Representational State Transfer"

[<Test>]
let ``HtmlDefinitionList ToString with empty definitions`` () =
    let testName = "Empty Definition List"
    let htmlNode = HtmlNode.NewText("dummy")
    let htmlDefList = { Name = testName; Definitions = []; Html = htmlNode }
    
    let result = htmlDefList.ToString()
    result |> should contain "Empty Definition List"
    // Should not throw exception with empty definitions

[<Test>]
let ``HtmlDefinitionList ToString with multiple values per term`` () =
    let testName = "Synonyms"
    let htmlNode = HtmlNode.NewText("dummy")
    let term = { Name = "Big"; Values = [| "Large"; "Huge"; "Enormous" |]; Html = htmlNode }
    let htmlDefList = { Name = testName; Definitions = [ term ]; Html = htmlNode }
    
    let result = htmlDefList.ToString()
    result |> should contain "Synonyms"
    result |> should contain "Big"
    result |> should contain "Large"
    result |> should contain "Huge"
    result |> should contain "Enormous"

// --------------------------------------------------------------------------------------
// HtmlObjectDescription tests
// --------------------------------------------------------------------------------------

[<Test>]
let ``HtmlObjectDescription Table variant has correct Name`` () =
    let tableName = "Test Table"
    let htmlNode = HtmlNode.NewText("dummy")
    let table = HtmlTable(tableName, None, None, None, [||], htmlNode)
    let objDesc = HtmlObjectDescription.Table table
    
    objDesc.Name |> should equal tableName

[<Test>]
let ``HtmlObjectDescription List variant has correct Name`` () =
    let listName = "Test List"
    let htmlNode = HtmlNode.NewText("dummy")
    let list = { Name = listName; Values = [| "Item1"; "Item2" |]; Html = htmlNode }
    let objDesc = HtmlObjectDescription.List list
    
    objDesc.Name |> should equal listName

[<Test>]
let ``HtmlObjectDescription DefinitionList variant has correct Name`` () =
    let defListName = "Test Definition List"
    let htmlNode = HtmlNode.NewText("dummy")
    let innerList = { Name = "Term"; Values = [| "Definition" |]; Html = htmlNode }
    let defList = { Name = defListName; Definitions = [ innerList ]; Html = htmlNode }
    let objDesc = HtmlObjectDescription.DefinitionList defList
    
    objDesc.Name |> should equal defListName

// --------------------------------------------------------------------------------------
// Additional tests to improve coverage for internal implementation paths
// --------------------------------------------------------------------------------------

[<Test>]
let ``HtmlTable constructor works with internal access`` () =
    let tableName = "Test Table"
    let htmlNode = HtmlNode.NewText("dummy")
    let table = HtmlTable(tableName, None, None, None, [| [| "data1"; "data2" |] |], htmlNode)
    
    table.Name |> should equal tableName
    table.Rows.Length |> should equal 1
    table.Rows.[0].Length |> should equal 2

[<Test>]
let ``HtmlTable with headers works`` () =
    let tableName = "Table With Headers"
    let htmlNode = HtmlNode.NewText("dummy")
    let rows = [| [| "John"; "30" |]; [| "Jane"; "25" |] |]
    let table = HtmlTable(tableName, None, None, Some true, rows, htmlNode)
    
    table.Name |> should equal tableName
    table.Rows |> should equal rows
    table.HasHeaders |> should equal (Some true)

[<Test>]
let ``HtmlTable ToString generates formatted output`` () =
    let tableName = "Sample Table"
    let htmlNode = HtmlNode.NewText("dummy")
    let rows = [| [| "Name"; "Age" |]; [| "John"; "30" |] |]
    let table = HtmlTable(tableName, None, None, None, rows, htmlNode)
    
    let result = table.ToString()
    result |> should contain "Sample Table"
    result |> should contain "Name"
    result |> should contain "Age"
    result |> should contain "John"
    result |> should contain "30"