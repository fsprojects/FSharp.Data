module FSharp.Data.Core.Tests.XmlInference

open FsUnit
open NUnit.Framework
open System
open System.Xml.Linq
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference
open ProviderImplementation

// Test infrastructure similar to InferenceTests.fs
let internal culture = TextRuntime.GetCulture ""
let internal inferenceMode = InferenceMode'.ValuesOnly
let internal unitsOfMeasureProvider = 
    { new StructuralInference.IUnitsOfMeasureProvider with
        member x.SI(_) : System.Type = null
        member x.Product(_, _) = failwith "Not implemented yet"
        member x.Inverse(_) = failwith "Not implemented yet" }
let internal allowEmptyValues = true

// Helper function to create XElement from string
let createElement xmlString =
    XElement.Parse(xmlString)

// Helper function to create XElement array
let createElements xmlStrings =
    xmlStrings |> Array.map createElement

[<Test>]
let ``getInferedTypeFromValue handles simple string value`` () =
    let element = createElement """<name>John</name>"""
    let result = XmlInference.getInferedTypeFromValue unitsOfMeasureProvider inferenceMode culture element
    
    match result with
    | InferedType.Primitive(t, _, _, _) -> t |> should equal typeof<string>
    | _ -> failwith "Expected primitive string type"

[<Test>]
let ``getInferedTypeFromValue handles numeric value`` () =
    let element = createElement """<age>30</age>"""
    let result = XmlInference.getInferedTypeFromValue unitsOfMeasureProvider inferenceMode culture element
    
    match result with
    | InferedType.Primitive(t, _, _, _) -> t |> should equal typeof<int>
    | _ -> failwith "Expected primitive int type"

[<Test>]
let ``getInferedTypeFromValue handles boolean value`` () =
    let element = createElement """<active>true</active>"""
    let result = XmlInference.getInferedTypeFromValue unitsOfMeasureProvider inferenceMode culture element
    
    match result with
    | InferedType.Primitive(t, _, _, _) -> t |> should equal typeof<bool>
    | _ -> failwith "Expected primitive bool type"

[<Test>]
let ``getInferedTypeFromValue handles decimal value`` () =
    let element = createElement """<price>19.99</price>"""
    let result = XmlInference.getInferedTypeFromValue unitsOfMeasureProvider inferenceMode culture element
    
    match result with
    | InferedType.Primitive(t, _, _, _) -> t |> should equal typeof<decimal>
    | _ -> failwith "Expected primitive decimal type"

[<Test>]
let ``getInferedTypeFromValue handles embedded JSON object`` () =
    let element = createElement """<data>{"name": "John", "age": 30}</data>"""
    let result = XmlInference.getInferedTypeFromValue unitsOfMeasureProvider inferenceMode culture element
    
    match result with
    | InferedType.Json(_, _) -> () // Success - embedded JSON detected
    | _ -> failwith "Expected JSON type for embedded JSON content"

[<Test>]
let ``getInferedTypeFromValue handles embedded JSON array`` () =
    let element = createElement """<items>[1, 2, 3]</items>"""
    let result = XmlInference.getInferedTypeFromValue unitsOfMeasureProvider inferenceMode culture element
    
    match result with
    | InferedType.Json(_, _) -> () // Success - embedded JSON detected  
    | _ -> failwith "Expected JSON type for embedded JSON array"

[<Test>]
let ``getInferedTypeFromValue with NoInference mode skips JSON parsing`` () =
    let element = createElement """<data>{"name": "John"}</data>"""
    let result = XmlInference.getInferedTypeFromValue unitsOfMeasureProvider InferenceMode'.NoInference culture element
    
    match result with
    | InferedType.Primitive(t, _, _, _) -> t |> should equal typeof<string>
    | _ -> failwith "Expected string type with NoInference mode"

[<Test>]
let ``getInferedTypeFromValue handles malformed JSON as string`` () =
    let element = createElement """<data>{"name": invalid}</data>"""
    let result = XmlInference.getInferedTypeFromValue unitsOfMeasureProvider inferenceMode culture element
    
    match result with
    | InferedType.Primitive(t, _, _, _) -> t |> should equal typeof<string>
    | _ -> failwith "Expected string type for malformed JSON"

[<Test>]
let ``getInferedTypeFromValue handles empty element`` () =
    let element = createElement """<empty></empty>"""
    let result = XmlInference.getInferedTypeFromValue unitsOfMeasureProvider inferenceMode culture element
    
    // For empty elements, the inference returns Null type
    match result with
    | InferedType.Null -> () // Success - empty value gives Null type
    | _ -> failwithf "Expected Null type for empty value, got %A" result

[<Test>]
let ``inferLocalType handles simple element with content`` () =
    let element = createElement """<name>John</name>"""
    let result = XmlInference.inferLocalType unitsOfMeasureProvider inferenceMode culture allowEmptyValues element
    
    match result with
    | InferedType.Record(Some name, properties, false) ->
        name |> should equal "name"
        properties.Length |> should equal 1
        properties.[0].Name |> should equal ""  // Body content
    | _ -> failwith "Expected record type with body content"

[<Test>]
let ``inferLocalType handles element with attributes`` () =
    let element = createElement """<person name="John" age="30">Developer</person>"""
    let result = XmlInference.inferLocalType unitsOfMeasureProvider inferenceMode culture allowEmptyValues element
    
    match result with
    | InferedType.Record(Some name, properties, false) ->
        name |> should equal "person"
        properties.Length |> should equal 3  // body content + 2 attributes
        properties |> List.exists (fun p -> p.Name = "name") |> should equal true
        properties |> List.exists (fun p -> p.Name = "age") |> should equal true
        properties |> List.exists (fun p -> p.Name = "") |> should equal true  // body
    | _ -> failwith "Expected record type with attributes and body"

[<Test>]
let ``inferLocalType handles element with child elements`` () =
    let element = createElement """<person><name>John</name><age>30</age></person>"""
    let result = XmlInference.inferLocalType unitsOfMeasureProvider inferenceMode culture allowEmptyValues element
    
    match result with
    | InferedType.Record(Some name, properties, false) ->
        name |> should equal "person"
        properties.Length |> should equal 1  // Collection of children
        properties.[0].Name |> should equal ""  // Body content (collection)
        match properties.[0].Type with
        | InferedType.Collection(_, _) -> () // Success - collection of children
        | _ -> failwith "Expected collection type for child elements"
    | _ -> failwith "Expected record type with child collection"

[<Test>]
let ``inferLocalType handles empty element`` () =
    let element = createElement """<empty />"""
    let result = XmlInference.inferLocalType unitsOfMeasureProvider inferenceMode culture allowEmptyValues element
    
    match result with
    | InferedType.Record(Some name, properties, false) ->
        name |> should equal "empty"
        properties.Length |> should equal 0  // No content or attributes
    | _ -> failwith "Expected empty record type"

[<Test>]
let ``inferLocalType handles element with only attributes`` () =
    let element = createElement """<config debug="true" timeout="30" />"""
    let result = XmlInference.inferLocalType unitsOfMeasureProvider inferenceMode culture allowEmptyValues element
    
    match result with
    | InferedType.Record(Some name, properties, false) ->
        name |> should equal "config"
        properties.Length |> should equal 2  // 2 attributes, no body
        properties |> List.exists (fun p -> p.Name = "debug") |> should equal true
        properties |> List.exists (fun p -> p.Name = "timeout") |> should equal true
    | _ -> failwith "Expected record type with attributes only"

[<Test>]
let ``inferLocalType handles nested structure`` () =
    let element = createElement """<root><item id="1"><value>test</value></item><item id="2"><value>test2</value></item></root>"""
    let result = XmlInference.inferLocalType unitsOfMeasureProvider inferenceMode culture allowEmptyValues element
    
    match result with
    | InferedType.Record(Some name, properties, false) ->
        name |> should equal "root"
        properties.Length |> should equal 1
        match properties.[0].Type with
        | InferedType.Collection(_, _) -> () // Collection of items
        | _ -> failwith "Expected collection of items"
    | _ -> failwith "Expected root record with item collection"

[<Test>]
let ``inferGlobalType handles single element`` () =
    let doc = XDocument.Parse("""<root><person name="John">Developer</person></root>""")
    let elements = [| doc.Root |]
    let result = XmlInference.inferGlobalType unitsOfMeasureProvider inferenceMode culture allowEmptyValues elements
    
    result.Length |> should equal 1
    match result.[0] with
    | InferedType.Record(Some name, _, false) ->
        name |> should equal "root"
    | _ -> failwith "Expected root record type"

[<Test>]
let ``inferGlobalType handles multiple elements of same type`` () =
    let xml = """<doc><person name="John" age="30" /><person name="Jane" city="NYC" /></doc>"""
    let doc = XDocument.Parse(xml)
    let elements = [| doc.Root |]
    let result = XmlInference.inferGlobalType unitsOfMeasureProvider inferenceMode culture allowEmptyValues elements
    
    result.Length |> should equal 1

[<Test>]
let ``inferType with globalInference=true uses global inference`` () =
    let doc = XDocument.Parse("""<root><person name="John">Developer</person></root>""")
    let elements = [| doc.Root |]
    let result = XmlInference.inferType unitsOfMeasureProvider inferenceMode culture allowEmptyValues true elements
    
    result.Length |> should equal 1
    match result.[0] with
    | InferedType.Record(Some name, _, false) ->
        name |> should equal "root"
    | _ -> failwith "Expected root record type from global inference"

[<Test>]
let ``inferType with globalInference=false uses local inference`` () =
    let elements = [| createElement """<person name="John">Developer</person>""" |]
    let result = XmlInference.inferType unitsOfMeasureProvider inferenceMode culture allowEmptyValues false elements
    
    result.Length |> should equal 1
    match result.[0] with
    | InferedType.Record(Some name, _, false) ->
        name |> should equal "person"
    | _ -> failwith "Expected person record type from local inference"

[<Test>]
let ``inferType handles multiple root elements`` () =
    let elements = [| 
        createElement """<person name="John">Developer</person>"""
        createElement """<person name="Jane">Designer</person>"""
    |]
    let result = XmlInference.inferType unitsOfMeasureProvider inferenceMode culture allowEmptyValues false elements
    
    result.Length |> should equal 2
    // Both should be person records
    result |> Array.forall (function 
        | InferedType.Record(Some "person", _, false) -> true 
        | _ -> false) |> should equal true

[<Test>]
let ``XML with complex nested structure infers correctly`` () =
    let element = createElement """
        <library name="City Library">
            <book id="1" category="fiction">
                <title>The Great Gatsby</title>
                <author>F. Scott Fitzgerald</author>
                <year>1925</year>
                <available>true</available>
            </book>
            <book id="2" category="science">
                <title>A Brief History of Time</title>
                <author>Stephen Hawking</author>
                <year>1988</year>
                <available>false</available>
            </book>
        </library>
    """
    
    let result = XmlInference.inferLocalType unitsOfMeasureProvider inferenceMode culture allowEmptyValues element
    
    match result with
    | InferedType.Record(Some name, properties, false) ->
        name |> should equal "library"
        properties |> List.exists (fun p -> p.Name = "name") |> should equal true
        properties |> List.exists (fun p -> p.Name = "") |> should equal true // Collection of books
    | _ -> failwith "Expected library record with books collection"

[<Test>]
let ``XML with mixed content types infers correctly`` () =
    let element = createElement """
        <data>
            <number>42</number>
            <text>Hello</text>
            <flag>true</flag>
            <price>19.99</price>
        </data>
    """
    
    let result = XmlInference.inferLocalType unitsOfMeasureProvider inferenceMode culture allowEmptyValues element
    
    match result with
    | InferedType.Record(Some name, properties, false) ->
        name |> should equal "data"
        properties.Length |> should equal 1  // Collection of mixed elements
        match properties.[0].Type with
        | InferedType.Collection(_, _) -> () // Success
        | _ -> failwith "Expected collection of mixed elements"
    | _ -> failwith "Expected data record with mixed content"

[<Test>]
let ``XML with namespaced elements handles correctly`` () =
    let element = createElement """<root xmlns:ns="http://example.com"><ns:item ns:value="test">content</ns:item></root>"""
    
    let result = XmlInference.inferLocalType unitsOfMeasureProvider inferenceMode culture allowEmptyValues element
    
    match result with
    | InferedType.Record(Some name, _, false) ->
        name |> should equal "root"
    | _ -> failwith "Expected root record type for namespaced XML"

[<Test>]
let ``XML inference handles large values correctly`` () =
    let element = createElement """<data>9223372036854775807</data>"""  // Int64.MaxValue
    let result = XmlInference.getInferedTypeFromValue unitsOfMeasureProvider inferenceMode culture element
    
    match result with
    | InferedType.Primitive(t, _, _, _) -> t |> should equal typeof<int64>
    | _ -> failwith "Expected int64 type for large integer"

[<Test>]
let ``XML inference handles floating point values`` () =
    let element = createElement """<value>123.456789</value>"""
    let result = XmlInference.getInferedTypeFromValue unitsOfMeasureProvider inferenceMode culture element
    
    match result with
    | InferedType.Primitive(t, _, _, _) -> t |> should equal typeof<decimal>
    | _ -> failwith "Expected decimal type for floating point"