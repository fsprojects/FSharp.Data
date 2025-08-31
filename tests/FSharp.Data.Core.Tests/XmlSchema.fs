module FSharp.Data.Tests.XmlSchema

open FsUnit
open NUnit.Framework
open System
open System.IO
open FSharp.Data.Runtime.XmlSchema

[<Test>]
let ``ResolutionFolderResolver.ResolveUri handles absolute web URIs`` () =
    let resolver = ResolutionFolderResolver("")
    let baseUri = Uri("http://example.com/")
    let relativeUri = "schema.xsd"
    let result = resolver.ResolveUri(baseUri, relativeUri)
    result.ToString() |> should equal "http://example.com/schema.xsd"

[<Test>]
let ``ResolutionFolderResolver.ResolveUri uses resolution folder when base is null`` () =
    let resolver = ResolutionFolderResolver("http://example.com/schemas/")
    let result = resolver.ResolveUri(null, "schema.xsd")
    result.ToString() |> should equal "http://example.com/schemas/schema.xsd"

[<Test>]
let ``ResolutionFolderResolver.ResolveUri handles empty resolution folder`` () =
    let resolver = ResolutionFolderResolver("")
    let baseUri = Uri("http://example.com/")
    let result = resolver.ResolveUri(baseUri, "schema.xsd")
    result.ToString() |> should equal "http://example.com/schema.xsd"

[<Test>]
let ``ResolutionFolderResolver.ResolveUri adds trailing slash when needed`` () =
    let resolver = ResolutionFolderResolver("http://example.com/schemas")
    let result = resolver.ResolveUri(null, "schema.xsd")
    result.ToString() |> should equal "http://example.com/schemas/schema.xsd"

[<Test>]
let ``parseSchema creates XmlSchemaSet from XSD text`` () =
    let xsdText = """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="root" type="xs:string"/>
</xs:schema>"""
    let schemaSet = parseSchema "" xsdText
    schemaSet.Count |> should equal 1

[<Test>]
let ``parseSchema handles resolution folder parameter`` () =
    let xsdText = """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="test" type="xs:int"/>
</xs:schema>"""
    let schemaSet = parseSchema "http://example.com/schemas/" xsdText
    schemaSet.Count |> should equal 1

[<Test>]
let ``parseSchemaFromTextReader creates XmlSchemaSet from TextReader`` () =
    let xsdText = """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="sample" type="xs:boolean"/>
</xs:schema>"""
    use reader = new StringReader(xsdText)
    let schemaSet = parseSchemaFromTextReader "" reader
    schemaSet.Count |> should equal 1

[<Test>]
let ``parseSchemaFromTextReader handles complex schema`` () =
    let xsdText = """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="person">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="name" type="xs:string"/>
        <xs:element name="age" type="xs:int"/>
      </xs:sequence>
    </xs:complexType>
  </xs:element>
</xs:schema>"""
    use reader = new StringReader(xsdText)
    let schemaSet = parseSchemaFromTextReader "" reader
    schemaSet.Count |> should equal 1

[<Test>]
let ``parseSchema throws for invalid XSD`` () =
    let invalidXsd = "<invalid>not a schema</invalid>"
    (fun () -> parseSchema "" invalidXsd |> ignore) |> should throw typeof<System.Xml.Schema.XmlSchemaException>

[<Test>]
let ``ResolutionFolderResolver handles relative URIs correctly`` () =
    let resolver = ResolutionFolderResolver("./schemas/")
    let baseUri = Uri("http://example.com/base/")
    let result = resolver.ResolveUri(baseUri, "test.xsd")
    result.ToString() |> should equal "http://example.com/base/test.xsd"