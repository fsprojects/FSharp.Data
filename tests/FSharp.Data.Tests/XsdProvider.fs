#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.XsdProvider
#endif

open NUnit.Framework
open FSharp.Data
open FsUnit
open System.Xml.Linq

type schema = XsdProvider<"""<schema xmlns="http://www.w3.org/2001/XMLSchema" targetNamespace="https://github.com/FSharp.Data/" xmlns:tns="https://github.com/FSharp.Data/" attributeFormDefault="unqualified" >
  <complexType name="root">
    <sequence>
      <element name="elem" type="string" >
        <annotation>
          <documentation>This is an identification of the preferred language</documentation>
        </annotation>
      </element>
      <element name="elem1" type="tns:foo" />
      <element name="choice" type="tns:bar" maxOccurs="2" />
      <element name="anonymousTyped">
        <complexType>
          <sequence>
            <element name="covert" type="boolean" />
          </sequence>
          <attribute name="attr" type="string" />
          <attribute name="windy">
            <simpleType>
              <restriction base="string">
                <maxLength value="10" />
              </restriction>
            </simpleType>
          </attribute>
        </complexType>
      </element>
    </sequence>
  </complexType>
  <complexType name="bar">
    <choice>
      <element name="language" type="string" >
        <annotation>
          <documentation>This is an identification of the preferred language</documentation>
        </annotation>
      </element>
      <element name="country" type="integer" />
      <element name="snur">
        <complexType>
          <sequence>
            <element name ="baz" type ="string"/>
          </sequence>
        </complexType>
      </element>
    </choice>
  </complexType>
  <complexType name="foo">
    <sequence>
      <element name="fooElem" type="boolean" />
      <element name="ISO639Code">
        <annotation>
          <documentation>This is an ISO 639-1 or 639-2 identifier</documentation>
        </annotation>
        <simpleType>
          <restriction base="string">
            <maxLength value="10" />
          </restriction>
        </simpleType>
      </element>
    </sequence>
  </complexType>
</schema>""">

[<Test>]
let ``Simple schema``() =
    let xml = 
         """<?xml version="1.0" encoding="utf-8"?>
            <schema>
              <root>
                <elem>it starts with a number</elem>
                <elem1>
                  <fooElem>false</fooElem>
                  <ISO639Code>dk-DA</ISO639Code>
                </elem1>
                <choice>
                  <language>Danish</language>
                </choice>
                <choice>
                  <country>1</country>
                </choice>
                <anonymousTyped attr="fish" windy="strong" >
                  <covert>True</covert>
                </anonymousTyped>
              </root>
            </schema>
         """ 
    let data = schema.Parse xml
    let root = data.Root
    let choices = root.Choices
    choices.[1].Country.Value     |> should equal 1
    choices.[0].Language.Value    |> should equal "Danish"
    root.AnonymousTyped.Covert    |> should equal true
    root.AnonymousTyped.Attr      |> should equal "fish"
    root.AnonymousTyped.Windy     |> should equal "strong"

[<Test>]
let ``Invalid xml for schema``() =
    let xml = 
         """<?xml version="1.0" encoding="utf-8"?>
            <schema>
              <root>
                <elemento>it starts with a number</elem>
              </root>
            </schema>
         """ 
    let failed = 
        try
          schema.Parse(xml) |> ignore
          false
        with e ->
           true
    failed |> should equal true

type schemaWithExtension = XsdProvider<"""<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="items" type="ItemsType"/>
  <xs:complexType name="ItemsType">
    <xs:choice minOccurs="0" maxOccurs="unbounded">
      <xs:element name="hat" type="ProductType"/>
      <xs:element name="umbrella" type="RestrictedProductType"/>
      <xs:element name="shirt" type="ShirtType"/>
    </xs:choice>
  </xs:complexType>
  <!--Empty Content Type-->
  <xs:complexType name="ItemType" abstract="true">

  </xs:complexType>
  <!--Empty Content Extension (with Attribute Extension)-->
  <xs:complexType name="ProductType">
    <xs:sequence>
      <xs:element name="number" type="xs:integer"/>
      <xs:element name="name" type="xs:string"/>
      <xs:element name="description"
                   type="xs:string" minOccurs="0"/>
    </xs:sequence>
    <xs:anyAttribute />
  </xs:complexType>
  <!--Complex Content Restriction-->
  <xs:complexType name="RestrictedProductType">
    <xs:complexContent>
      <xs:restriction base="ProductType">
        <xs:sequence>
          <xs:element name="number" type="xs:integer"/>
          <xs:element name="name" type="xs:token"/>
        </xs:sequence>
      </xs:restriction>
    </xs:complexContent>
  </xs:complexType>
  <!--Complex Content Extension-->
  <xs:complexType name="ShirtType">
    <xs:complexContent>
      <xs:extension base="ProductType">
        <xs:choice maxOccurs="unbounded">
          <xs:element name="size" type="SmallSizeType"/>
          <xs:element name="color" type="ColorType"/>
        </xs:choice>
        <xs:attribute name="sleeve" type="xs:integer"/>
      </xs:extension>
    </xs:complexContent>
  </xs:complexType>
  <!--Simple Content Extension-->
  <xs:complexType name="SizeType">
    <xs:simpleContent>
      <xs:extension base="xs:integer">
        <xs:attribute name="system" type="xs:token"/>
      </xs:extension>
    </xs:simpleContent>
  </xs:complexType>
  <!--Simple Content Restriction-->
  <xs:complexType name="SmallSizeType">
    <xs:simpleContent>
      <xs:restriction base="SizeType">
        <xs:minInclusive value="2"/>
        <xs:maxInclusive value="6"/>
        <xs:attribute  name="system" type="xs:token"
                        use="required"/>
      </xs:restriction>
    </xs:simpleContent>
  </xs:complexType>
  <xs:complexType name="ColorType">
    <xs:attribute name="value" type="xs:string"/>
  </xs:complexType>
</xs:schema>""">
//setting FailOnUnsupported = true should create a compile error
//because restrictions are not supported


[<Test>]
let ``Extension on complex types``() =
    let xml = 
        """<?xml version="1.0"?>
           <items>
             <!--You have a CHOICE of the next 3 items at this level-->
             <hat routingNum="100" effDate="2008-09-29" lang="string">
               <number>100</number>
               <name>string</name>
               <!--Optional:-->
               <description>string</description>
             </hat>
             <umbrella routingNum="1" effDate="1900-01-01">
               <number>100</number>
               <name>token</name>
             </umbrella>
             <shirt routingNum="1" effDate="1900-01-01" sleeve="100">
               <number>100</number>
               <name>token</name>
               <!--You have a CHOICE of the next 2 items at this level-->
               <size system="token">6</size>
               <color value="string"/>
             </shirt>
           </items>"""

    let items = schemaWithExtension.ParseItems xml
    items.Hat.IsSome             |> should equal true
    items.Hat.Value.Number       |> should equal 100
    items.Hat.Value.Name         |> should equal "string"
    items.Shirt.Value.Sleeve     |> should equal 100

type anonymousTypes = XsdProvider<"""<schema xmlns="http://www.w3.org/2001/XMLSchema" targetNamespace="https://github.com/FSharp.Data/" xmlns:tns="https://github.com/FSharp.Data/" attributeFormDefault="unqualified" >
  <element name="elem1" type="tns:foo" />
  <complexType name="root">
    <sequence>
      <element name="elem" type="string" >
        <annotation>
          <documentation>This is an identification of the preferred language</documentation>
        </annotation>
      </element>
      <element name="anonymousTyped">
        <complexType>
          <sequence>
            <element name="covert">
            <complexType>
                <choice>
                   <element name="truth" type="string" />
                   <element name="lie"   type="string" />
                   <element ref="tns:elem1" />
                </choice>
            </complexType>
            </element>
          </sequence>
          <attribute name="attr" type="string" />
          <attribute name="windy">
            <simpleType>
              <restriction base="string">
                <maxLength value="10" />
              </restriction>
            </simpleType>
          </attribute>
        </complexType>
      </element>
    </sequence>
  </complexType>
  <complexType name="foo">
    <sequence>
      <element name="fooElem" type="boolean" />
      <element name="ISO639Code">
        <annotation>
          <documentation>This is an ISO 639-1 or 639-2 identifier</documentation>
        </annotation>
        <simpleType>
          <restriction base="string">
            <maxLength value="10" />
          </restriction>
        </simpleType>
      </element>
    </sequence>
  </complexType>
</schema>""">


[<Test>]
let ``nested anonymous types and ref elements``() =
    let xml = 
         """<?xml version="1.0" encoding="utf-8"?>
            <schema>
              <root>
                <elem>it starts with a number</elem>
                <elem1>
                  <fooElem>false</fooElem>
                  <ISO639Code>dk-DA</ISO639Code>
                </elem1>
                <anonymousTyped attr="fish" windy="strong" >
                  <covert><truth>true</truth></covert>
                </anonymousTyped>
              </root>
            </schema>
         """ 
    let data = anonymousTypes.Parse xml
    let root = data.Root

    root.AnonymousTyped.Covert.Elem1 |> should equal ""