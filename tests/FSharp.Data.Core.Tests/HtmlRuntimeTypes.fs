module FSharp.Data.Tests.HtmlRuntimeTypes

open NUnit.Framework
open FsUnit
open System
open System.Reflection
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes

// ============================================
// HtmlList (Record Type) Coverage Tests
// ============================================

[<Test>]
let ``HtmlList record should have correct properties`` () =
    let name = "Test List"
    let values = [| "item1"; "item2"; "item3" |]
    let html = HtmlNode.NewText("dummy")
    
    let list = { HtmlList.Name = name; Values = values; Html = html }
    
    list.Name |> should equal name
    list.Values |> should equal values
    list.Html |> should equal html

[<Test>]
let ``HtmlList record ToString should format correctly`` () =
    let name = "Test List"
    let values = [| "item1"; "item2" |]
    let html = HtmlNode.NewText("dummy")
    
    let list = { HtmlList.Name = name; Values = values; Html = html }
    let result = list.ToString()
    
    result |> should contain name
    result |> should contain "item1"
    result |> should contain "item2"

[<Test>]
let ``HtmlList record ToString should handle empty values`` () =
    let name = "Empty List"
    let values = [||]
    let html = HtmlNode.NewText("dummy")
    
    let list = { HtmlList.Name = name; Values = values; Html = html }
    let result = list.ToString()
    
    result |> should contain name

// ============================================
// HtmlDefinitionList (Record Type) Coverage Tests
// ============================================

[<Test>]
let ``HtmlDefinitionList record should have correct properties`` () =
    let name = "Test Definition List"
    let html = HtmlNode.NewText("dummy")
    let definitions = [
        { HtmlList.Name = "def1"; Values = [| "val1"; "val2" |]; Html = html }
        { HtmlList.Name = "def2"; Values = [| "val3" |]; Html = html }
    ]
    
    let defList = { HtmlDefinitionList.Name = name; Definitions = definitions; Html = html }
    
    defList.Name |> should equal name
    defList.Definitions |> should equal definitions
    defList.Html |> should equal html

[<Test>]
let ``HtmlDefinitionList record ToString should format correctly`` () =
    let name = "Test Definition List"
    let html = HtmlNode.NewText("dummy")
    let definitions = [
        { HtmlList.Name = "def1"; Values = [| "val1"; "val2" |]; Html = html }
        { HtmlList.Name = "def2"; Values = [| "val3" |]; Html = html }
    ]
    
    let defList = { HtmlDefinitionList.Name = name; Definitions = definitions; Html = html }
    let result = defList.ToString()
    
    result |> should contain name
    result |> should contain "def1"
    result |> should contain "def2"
    result |> should contain "val1"
    result |> should contain "val2"
    result |> should contain "val3"

[<Test>]
let ``HtmlDefinitionList record ToString should handle empty definitions`` () =
    let name = "Empty Definition List"
    let html = HtmlNode.NewText("dummy")
    let definitions = []
    
    let defList = { HtmlDefinitionList.Name = name; Definitions = definitions; Html = html }
    let result = defList.ToString()
    
    result |> should contain name

// Note: Generic HtmlList<T> and HtmlTable<T> tests are complex due to internal type usage
// The record types (HtmlList and HtmlDefinitionList) above provide good coverage for the 0% areas