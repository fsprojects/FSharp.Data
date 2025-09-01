module FSharp.Data.Tests.HtmlTableCell

open NUnit.Framework
open FsUnit
open System
open FSharp.Data
open FSharp.Data.Runtime

[<Test>]
let ``HtmlTableCell.Cell creates cell with header flag and data``() =
    let cell = HtmlTableCell.Cell(true, "Header Text")
    cell.IsHeader |> should equal true
    cell.Data |> should equal "Header Text"

[<Test>]
let ``HtmlTableCell.Cell creates cell with non-header flag and data``() =
    let cell = HtmlTableCell.Cell(false, "Cell Data")
    cell.IsHeader |> should equal false
    cell.Data |> should equal "Cell Data"

[<Test>]
let ``HtmlTableCell.Empty creates empty cell``() =
    let cell = HtmlTableCell.Empty
    cell.IsHeader |> should equal true  // Empty cells are considered headers
    cell.Data |> should equal ""

[<Test>]
let ``HtmlTableCell IsHeader property works for various cell types``() =
    let headerCell = HtmlTableCell.Cell(true, "Header")
    let dataCell = HtmlTableCell.Cell(false, "Data")
    let emptyCell = HtmlTableCell.Empty
    
    headerCell.IsHeader |> should equal true
    dataCell.IsHeader |> should equal false
    emptyCell.IsHeader |> should equal true

[<Test>]
let ``HtmlTableCell Data property returns correct content``() =
    let headerCell = HtmlTableCell.Cell(true, "Column Title")
    let dataCell = HtmlTableCell.Cell(false, "Row Value")
    let emptyCell = HtmlTableCell.Empty
    
    headerCell.Data |> should equal "Column Title"
    dataCell.Data |> should equal "Row Value"
    emptyCell.Data |> should equal ""

[<Test>]
let ``HtmlTableCell handles empty string data``() =
    let cell = HtmlTableCell.Cell(false, "")
    cell.IsHeader |> should equal false
    cell.Data |> should equal ""

[<Test>]
let ``HtmlTableCell handles whitespace data``() =
    let cell = HtmlTableCell.Cell(true, "   \t\n   ")
    cell.IsHeader |> should equal true
    cell.Data |> should equal "   \t\n   "

[<Test>]
let ``HtmlTableCell handles special characters in data``() =
    let specialText = "Test with ñ, ü, and émojis 🎯"
    let cell = HtmlTableCell.Cell(false, specialText)
    cell.IsHeader |> should equal false
    cell.Data |> should equal specialText

[<Test>]
let ``HtmlTableCell equality comparison works``() =
    let cell1 = HtmlTableCell.Cell(true, "Test")
    let cell2 = HtmlTableCell.Cell(true, "Test")
    let cell3 = HtmlTableCell.Cell(false, "Test")
    let empty1 = HtmlTableCell.Empty
    let empty2 = HtmlTableCell.Empty
    
    (cell1 = cell2) |> should equal true
    (cell1 = cell3) |> should equal false
    (empty1 = empty2) |> should equal true

[<Test>]
let ``HtmlTableCell pattern matching works correctly``() =
    let headerCell = HtmlTableCell.Cell(true, "Header")
    let dataCell = HtmlTableCell.Cell(false, "Data")
    let emptyCell = HtmlTableCell.Empty
    
    match headerCell with
    | HtmlTableCell.Cell(isHeader, data) -> 
        isHeader |> should equal true
        data |> should equal "Header"
    | HtmlTableCell.Empty -> failwith "Should not match Empty"
    
    match dataCell with
    | HtmlTableCell.Cell(isHeader, data) -> 
        isHeader |> should equal false
        data |> should equal "Data"
    | HtmlTableCell.Empty -> failwith "Should not match Empty"
        
    match emptyCell with
    | HtmlTableCell.Empty -> () // Should match
    | HtmlTableCell.Cell(_, _) -> failwith "Should not match Cell"