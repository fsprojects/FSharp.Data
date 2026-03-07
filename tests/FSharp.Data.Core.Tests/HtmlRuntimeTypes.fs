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
// ============================================
// Schema.org Microdata Tests
// ============================================

[<Test>]
let ``getSchemas returns empty list when no microdata present`` () =
    let doc = HtmlDocument.Parse "<html><body><p>No microdata here.</p></body></html>"
    let schemas = HtmlRuntime.getSchemas doc
    schemas |> List.length |> should equal 0

[<Test>]
let ``getSchemas finds itemscope items and groups by itemtype`` () =
    let html = """
<html><body>
  <div itemscope itemtype="http://schema.org/Person">
    <span itemprop="name">Jane Doe</span>
    <span itemprop="jobTitle">Engineer</span>
  </div>
</body></html>"""

    let doc = HtmlDocument.Parse html
    let schemas = HtmlRuntime.getSchemas doc
    schemas |> should haveLength 1
    let group = schemas.[0]
    group.TypeUrl |> should equal "http://schema.org/Person"
    group.Name |> should equal "Person"
    group.Items |> should haveLength 1
    group.Items.[0].Properties |> Map.find "name" |> should equal "Jane Doe"
    group.Items.[0].Properties |> Map.find "jobTitle" |> should equal "Engineer"

[<Test>]
let ``getSchemas groups multiple items of same type`` () =
    let html = """
<html><body>
  <div itemscope itemtype="http://schema.org/Product">
    <span itemprop="name">Widget A</span>
    <span itemprop="price">9.99</span>
  </div>
  <div itemscope itemtype="http://schema.org/Product">
    <span itemprop="name">Widget B</span>
  </div>
</body></html>"""

    let doc = HtmlDocument.Parse html
    let schemas = HtmlRuntime.getSchemas doc
    schemas |> should haveLength 1
    let group = schemas.[0]
    group.Items |> should haveLength 2
    group.Items.[0].Properties |> Map.find "name" |> should equal "Widget A"
    group.Items.[1].Properties |> Map.find "name" |> should equal "Widget B"
    group.Properties |> should contain "name"
    group.Properties |> should contain "price"

[<Test>]
let ``getSchemas creates separate groups for different schema types`` () =
    let html = """
<html><body>
  <div itemscope itemtype="http://schema.org/Person">
    <span itemprop="name">John</span>
  </div>
  <div itemscope itemtype="http://schema.org/Organization">
    <span itemprop="name">Acme Corp</span>
  </div>
</body></html>"""

    let doc = HtmlDocument.Parse html
    let schemas = HtmlRuntime.getSchemas doc
    schemas |> should haveLength 2

[<Test>]
let ``getSchemas uses content attribute for meta elements`` () =
    let html = """
<html><body>
  <div itemscope itemtype="http://schema.org/Event">
    <meta itemprop="startDate" content="2024-01-15">
  </div>
</body></html>"""

    let doc = HtmlDocument.Parse html
    let schemas = HtmlRuntime.getSchemas doc
    schemas.[0].Items.[0].Properties |> Map.find "startDate" |> should equal "2024-01-15"

[<Test>]
let ``HtmlSchemaGroup ToString formats correctly`` () =
    let node = HtmlNode.NewText "dummy"

    let item =
        { HtmlSchemaItem.Properties = Map.ofList [ "name", "Alice" ]
          Html = node }

    let group =
        { HtmlSchemaGroup.Name = "Person"
          TypeUrl = "http://schema.org/Person"
          Items = [| item |]
          Properties = [| "name" |] }

    let result = group.ToString()
    result |> should contain "Person"
    result |> should contain "name"
    result |> should contain "Alice"
