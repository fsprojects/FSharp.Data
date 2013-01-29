module FSharp.Data.Tests.JsonValue.XmlConversionTests

open NUnit.Framework
open FsUnit
open System.Xml.Linq
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
(*
[<Test>]
let ``Can serialize Json to XML``() =
    let text = "{\"items\": [{\"id\": \"Open\"}, null, {\"id\": 25}]}"
    let json = JsonValue.Parse text
    let xml = json.ToXml() |> Seq.head 
    let expectedXml = XDocument.Parse("<items><item id=\"Open\" /><item /><item id=\"25\" /></items>")
    xml.ToString() |> should equal (expectedXml.ToString())

[<Test>]
let ``Can serialize single XML node to Json``() =
    let text = "<item name=\"Steffen\" />" 
    let xml = XDocument.Parse text
    let generatedJSON = JsonValue.fromXml xml
    let expectedJSON = "{\"name\":\"Steffen\"}"
    generatedJSON.ToString() |> should equal expectedJSON

[<Test>]
let ``Can serialize XML to Json``() =
    let text = "<items><item id=\"Open\" /><item /><item id=\"25\" /></items>"    
    let xml = XDocument.Parse text
    let generatedJSON = xml.ToJson()
    let expectedJSON = "{\"items\":[{\"id\":\"Open\"},{},{\"id\":\"25\"}]}"
    generatedJSON.ToString() |> should equal expectedJSON
*)