module FSharp.Data.Tests.HtmlAttributeExtensions

open NUnit.Framework
open FsUnit
open FSharp.Data

[<Test>]
let ``HtmlAttributeExtensions.Name returns correct attribute name``() =
    let attr = HtmlAttribute.New("id", "test_element")
    attr.Name() |> should equal "id"

[<Test>]
let ``HtmlAttributeExtensions.Name works with different attribute names``() =
    let classAttr = HtmlAttribute.New("class", "test-class")
    let styleAttr = HtmlAttribute.New("style", "color: red;")
    let dataAttr = HtmlAttribute.New("data-value", "123")
    
    classAttr.Name() |> should equal "class"
    styleAttr.Name() |> should equal "style" 
    dataAttr.Name() |> should equal "data-value"

[<Test>]
let ``HtmlAttributeExtensions.Name handles normalized lowercase names``() =
    // HtmlAttribute names are normalized to lowercase according to documentation
    let attr = HtmlAttribute.New("ID", "test")
    attr.Name() |> should equal "id"

[<Test>]
let ``HtmlAttributeExtensions.Name handles empty attribute name``() =
    let attr = HtmlAttribute.New("", "value")
    attr.Name() |> should equal ""

[<Test>]
let ``HtmlAttributeExtensions.Value returns correct attribute value``() =
    let attr = HtmlAttribute.New("id", "test_element")
    attr.Value() |> should equal "test_element"

[<Test>]
let ``HtmlAttributeExtensions.Value works with different attribute values``() =
    let idAttr = HtmlAttribute.New("id", "main-content")
    let classAttr = HtmlAttribute.New("class", "btn btn-primary active")
    let styleAttr = HtmlAttribute.New("style", "display: block; margin: 10px;")
    let dataAttr = HtmlAttribute.New("data-value", "42")
    
    idAttr.Value() |> should equal "main-content"
    classAttr.Value() |> should equal "btn btn-primary active"
    styleAttr.Value() |> should equal "display: block; margin: 10px;"
    dataAttr.Value() |> should equal "42"

[<Test>]
let ``HtmlAttributeExtensions.Value handles empty attribute value``() =
    let attr = HtmlAttribute.New("disabled", "")
    attr.Value() |> should equal ""

[<Test>]
let ``HtmlAttributeExtensions.Value handles whitespace-only values``() =
    let attr = HtmlAttribute.New("class", "   ")
    attr.Value() |> should equal "   "

[<Test>]
let ``HtmlAttributeExtensions.Value preserves special characters``() =
    let attr = HtmlAttribute.New("onclick", "alert('Hello \"World\"!'); return false;")
    attr.Value() |> should equal "alert('Hello \"World\"!'); return false;"

[<Test>]
let ``HtmlAttributeExtensions.Value handles Unicode characters``() =
    let attr = HtmlAttribute.New("title", "Café naïve résumé 中文")
    attr.Value() |> should equal "Café naïve résumé 中文"

[<Test>]
let ``HtmlAttributeExtensions methods work with common HTML attributes``() =
    // Test various standard HTML attributes to ensure compatibility
    let attributes = [
        HtmlAttribute.New("href", "https://example.com")
        HtmlAttribute.New("src", "/images/logo.png")
        HtmlAttribute.New("alt", "Company logo")
        HtmlAttribute.New("type", "text/css")
        HtmlAttribute.New("rel", "stylesheet")
        HtmlAttribute.New("placeholder", "Enter your name")
        HtmlAttribute.New("maxlength", "100")
        HtmlAttribute.New("readonly", "readonly")
        HtmlAttribute.New("checked", "checked")
        HtmlAttribute.New("disabled", "disabled")
    ]
    
    let expectedNames = ["href"; "src"; "alt"; "type"; "rel"; "placeholder"; "maxlength"; "readonly"; "checked"; "disabled"]
    let expectedValues = ["https://example.com"; "/images/logo.png"; "Company logo"; "text/css"; "stylesheet"; "Enter your name"; "100"; "readonly"; "checked"; "disabled"]
    
    let actualNames = attributes |> List.map (fun attr -> attr.Name())
    let actualValues = attributes |> List.map (fun attr -> attr.Value())
    
    actualNames |> should equal expectedNames
    actualValues |> should equal expectedValues

[<Test>]
let ``HtmlAttributeExtensions methods are consistent with module functions``() =
    // Verify that extension methods return same results as module functions
    let attr = HtmlAttribute.New("data-test", "extension-consistency")
    
    // Extension methods should match module functions
    attr.Name() |> should equal (HtmlAttribute.name attr)
    attr.Value() |> should equal (HtmlAttribute.value attr)

[<Test>]
let ``HtmlAttributeExtensions handle edge case attributes``() =
    // Test edge cases that might occur in real HTML
    let edgeCases = [
        HtmlAttribute.New("data-json", "{\"key\": \"value\", \"number\": 42}")
        HtmlAttribute.New("data-list", "item1,item2,item3")
        HtmlAttribute.New("style", "background-image: url('image.png'); z-index: 999;")
        HtmlAttribute.New("onclick", "if(confirm('Are you sure?')){ doSomething(); }")
    ]
    
    let expectedNames = ["data-json"; "data-list"; "style"; "onclick"]
    let expectedValues = [
        "{\"key\": \"value\", \"number\": 42}"
        "item1,item2,item3" 
        "background-image: url('image.png'); z-index: 999;"
        "if(confirm('Are you sure?')){ doSomething(); }"
    ]
    
    let actualNames = edgeCases |> List.map (fun attr -> attr.Name())
    let actualValues = edgeCases |> List.map (fun attr -> attr.Value())
    
    actualNames |> should equal expectedNames
    actualValues |> should equal expectedValues