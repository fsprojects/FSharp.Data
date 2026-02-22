module FSharp.Data.DesignTime.Tests.InferenceTests

open FsUnit
open System
open System.Globalization
open System.Xml
open System.Xml.Linq
open System.Xml.Schema
open NUnit.Framework
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.CsvInference
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference
open ProviderImplementation

/// A collection containing just one type
let internal SimpleCollection typ =
  InferedType.Collection([ typeTag typ], Map.ofSeq [typeTag typ, (InferedMultiplicity.Multiple, typ)])

let culture = TextRuntime.GetCulture ""
let internal inferenceMode = InferenceMode'.ValuesOnly
let internal unitsOfMeasureProvider = ProviderHelpers.unitsOfMeasureProvider

let internal inferType (csv:CsvFile) inferRows missingValues cultureInfo schema assumeMissingValues preferOptionals =
    let headerNamesAndUnits, schema = parseHeaders csv.Headers csv.NumberOfColumns schema unitsOfMeasureProvider
    inferType headerNamesAndUnits schema (csv.Rows |> Seq.map (fun x -> x.Columns)) inferRows missingValues inferenceMode cultureInfo assumeMissingValues preferOptionals unitsOfMeasureProvider

let internal toRecord fields = InferedType.Record(None, fields, false)

[<Test>]
let ``List.pairBy helper function works``() =
  let actual = List.pairBy fst [(2, "a"); (1, "b")] [(1, "A"); (3, "C")]
  let expected =
    [ (1, Some (1, "b"), Some (1, "A"))
      (2, Some (2, "a"), None)
      (3, None, Some (3, "C")) ]
  set actual |> should equal (set expected)

[<Test>]
let ``List.pairBy helper function preserves order``() =
  let actual = List.pairBy fst [("one", "a"); ("two", "b")] [("one", "A"); ("two", "B")]
  let expected =
    [ ("one", Some ("one", "a"), Some ("one", "A"))
      ("two", Some ("two", "b"), Some ("two", "B")) ]
  actual |> should equal expected

[<Test>]
let ``Finds common subtype of numeric types (decimal)``() =
  let source = JsonValue.Parse """[ 10, 10.23 ]"""
  let expected = SimpleCollection(InferedType.Primitive(typeof<decimal>, None, false, false))
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Finds common subtype of numeric types (int64)``() =
  let source = JsonValue.Parse """[ 10, 2147483648 ]"""
  let expected = SimpleCollection(InferedType.Primitive(typeof<int64>, None, false, false))
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Infers heterogeneous type of InferedType.Primitives``() =
  let source = JsonValue.Parse """[ 1,true ]"""
  let expected =
    InferedType.Collection
        ([ InferedTypeTag.Number; InferedTypeTag.Boolean ],
         [ InferedTypeTag.Number, (Single, InferedType.Primitive(typeof<Bit1>, None, false, false))
           InferedTypeTag.Boolean, (Single, InferedType.Primitive(typeof<bool>, None, false, false)) ] |> Map.ofList)
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Infers heterogeneous type of InferedType.Primitives and nulls``() =
  let source = JsonValue.Parse """[ 1,true,null ]"""
  let expected =
    InferedType.Collection
        ([ InferedTypeTag.Number; InferedTypeTag.Boolean; InferedTypeTag.Null ],
         [ InferedTypeTag.Null, (Single, InferedType.Null)
           InferedTypeTag.Number, (Single, InferedType.Primitive(typeof<Bit1>, None, false, false))
           InferedTypeTag.Boolean, (Single, InferedType.Primitive(typeof<bool>, None, false, false)) ] |> Map.ofList)
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Finds common subtype of numeric types (float)``() =
  let source = JsonValue.Parse """[ 10, 10.23, 79228162514264337593543950336 ]"""
  let expected = SimpleCollection(InferedType.Primitive(typeof<float>, None, false, false))
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Infers heterogeneous type of InferedType.Primitives and records``() =
  let source = JsonValue.Parse """[ {"a":0}, 1,2 ]"""
  let expected =
    InferedType.Collection
        ([ InferedTypeTag.Record None; InferedTypeTag.Number ],
         [ InferedTypeTag.Number, (Multiple, InferedType.Primitive(typeof<int>, None, false, false))
           InferedTypeTag.Record None,
             (Single, toRecord [ { Name="a"; Type=InferedType.Primitive(typeof<Bit0>, None, false, false) } ]) ] |> Map.ofList)
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Merges types in a collection of collections``() =
  let source = JsonValue.Parse """[ [{"a":true,"c":0},{"b":1,"c":0}], [{"b":1.1,"c":0}] ]"""
  let expected =
    [ { Name = "a"; Type = InferedType.Primitive(typeof<bool>, None, true, false) }
      { Name = "c"; Type = InferedType.Primitive(typeof<Bit0>, None, false, false) }
      { Name = "b"; Type = InferedType.Primitive(typeof<decimal>, None, true, false) } ]
    |> toRecord
    |> SimpleCollection
    |> SimpleCollection
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Unions properties of records in a collection``() =
  let source = JsonValue.Parse """[ {"a":1, "b":""}, {"a":1.2, "c":true} ]"""
  let expected =
    [ { Name = "a"; Type = InferedType.Primitive(typeof<decimal>, None, false, false) }
      { Name = "b"; Type = InferedType.Null }
      { Name = "c"; Type = InferedType.Primitive(typeof<bool>, None, true, false) } ]
    |> toRecord
    |> SimpleCollection
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Null should make string optional``() =
  let source = JsonValue.Parse """[ {"a":null}, {"a":"b"} ]"""
  let expected =
    [ { Name = "a"; Type = InferedType.Primitive(typeof<string>, None, true, false) } ]
    |> toRecord
    |> SimpleCollection
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Null is not a valid value of DateTime``() =
  let actual =
    subtypeInfered false InferedType.Null (InferedType.Primitive(typeof<DateTime>, None, false, false))
  let expected = InferedType.Primitive(typeof<DateTime>, None, true, false)
  actual |> should equal expected

[<Test>]
let ``Infers mixed fields of a a record as heterogeneous type with nulls (1.)``() =
  let source = JsonValue.Parse """[ {"a":null}, {"a":123} ]"""
  let expected =
    [ { Name = "a"; Type = InferedType.Primitive(typeof<int>, None, true, false) } ]
    |> toRecord
    |> SimpleCollection
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Null makes a record optional``() =
  let source = JsonValue.Parse """[ {"a":null}, {"a":{"b": 1}} ]"""
  let expected =
    [ { Name = "a"; Type = InferedType.Record(Some "a", [{ Name = "b"; Type = InferedType.Primitive(typeof<Bit1>, None, false, false) }], true) } ]
    |> toRecord
    |> SimpleCollection
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Infers mixed fields of a record as heterogeneous type``() =
  let source = JsonValue.Parse """[ {"a":"hi"}, {"a":2} , {"a":2147483648} ]"""
  let cases =
    Map.ofSeq [ InferedTypeTag.String, InferedType.Primitive(typeof<string>, None, false, false)
                InferedTypeTag.Number, InferedType.Primitive(typeof<int64>, None, false, false) ]
  let expected =
    [ { Name = "a"; Type = InferedType.Heterogeneous (cases, false) }]
    |> toRecord
    |> SimpleCollection
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Infers mixed fields of a record as heterogeneous type with nulls (2.)``() =
  let source = JsonValue.Parse """[ {"a":null}, {"a":2} , {"a":3} ]"""
  let expected =
    [ { Name = "a"; Type = InferedType.Primitive(typeof<int>, None, true, false) }]
    |> toRecord
    |> SimpleCollection
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Inference of multiple nulls works``() =
  let source = JsonValue.Parse """[0, [{"a": null}, {"a":null}]]"""
  let prop = { Name = "a"; Type = InferedType.Null }
  let expected =
    InferedType.Collection
        ([ InferedTypeTag.Number; InferedTypeTag.Collection ],
         [ InferedTypeTag.Collection, (Single, SimpleCollection(toRecord [prop]))
           InferedTypeTag.Number, (Single, InferedType.Primitive(typeof<Bit0>, None, false, false)) ] |> Map.ofList)
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Inference of DateTime``() =
  let source = CsvFile.Parse("date,int,float\n2012-12-19,2,3.0\n2012-12-12,4,5.0\n2012-12-1,6,10.0")
  let actual, _ = inferType source Int32.MaxValue [||] culture "" false false
  let propDate = { Name = "date"; Type = InferedType.Primitive(typeof<DateTime>, None, false, false) }
  let propInt = { Name = "int"; Type = InferedType.Primitive(typeof<int>, None, false, false) }
  let propFloat = { Name = "float"; Type = InferedType.Primitive(typeof<Decimal>, None, false, false) }
  let expected = toRecord [ propDate ; propInt ; propFloat ]
  actual |> should equal expected

[<Test>]
let ``Inference of DateTime with timestamp``() =
  let source = CsvFile.Parse("date,timestamp\n2012-12-19,2012-12-19 12:00\n2012-12-12,2012-12-12 00:00\n2012-12-1,2012-12-1 07:00")
  let actual, _ = inferType source Int32.MaxValue [||] culture "" false false
  let propDate = { Name = "date"; Type = InferedType.Primitive(typeof<DateTime>, None, false, false) }
  let propTimestamp = { Name = "timestamp"; Type = InferedType.Primitive(typeof<DateTime>, None, false, false) }
  let expected = toRecord [ propDate ; propTimestamp ]
  actual |> should equal expected

[<Test>]
let ``Inference of DateTime with timestamp non default separator``() =
  let source = CsvFile.Parse("date;timestamp\n2012-12-19;2012-12-19 12:00\n2012-12-12;2012-12-12 00:00\n2012-12-1;2012-12-1 07:00", ";")
  let actual, _ = inferType source Int32.MaxValue [||] culture "" false false
  let propDate = { Name = "date"; Type = InferedType.Primitive(typeof<DateTime>, None, false, false) }
  let propTimestamp = { Name = "timestamp"; Type = InferedType.Primitive(typeof<DateTime>, None, false, false) }
  let expected = toRecord [ propDate ; propTimestamp ]
  actual |> should equal expected

[<Test>]
let ``Inference of float with #N/A values and non default separator``() =
  let source = CsvFile.Parse("float;integer\n2.0;2\n#N/A;3\n", ";")
  let actual, _ = inferType source Int32.MaxValue [|"#N/A"|] culture "" false false
  let propFloat = { Name = "float"; Type = InferedType.Primitive(typeof<float>, None, false, false) }
  let propInteger = { Name = "integer"; Type = InferedType.Primitive(typeof<int>, None, false, false) }
  let expected = toRecord [ propFloat ; propInteger ]
  actual |> should equal expected

[<Test>]
let ``Inference of numbers with empty values``() =
  let source = CsvFile.Parse("""float1,float2,float3,float4,int,float5,float6,date,bool,int64
                                1,1,1,1,,,,,,
                                2.0,#N/A,,1,1,1,,2010-01-10,yes,
                                ,,2.0,NA,1,foo,2.0,,,2147483648""")
  let actual, typeOverrides = inferType source Int32.MaxValue [|"#N/A"; "NA"; "foo"|] culture "" false false
  let propFloat1 = { Name = "float1"; Type = InferedType.Primitive(typeof<decimal>, None, true, false) }
  let propFloat2 = { Name = "float2"; Type = InferedType.Primitive(typeof<float>, None, false, false) }
  let propFloat3 = { Name = "float3"; Type = InferedType.Primitive(typeof<decimal>, None, true, false) }
  let propFloat4 = { Name = "float4"; Type = InferedType.Primitive(typeof<float>, None, false, false) }
  let propInt =    { Name = "int";    Type = InferedType.Primitive(typeof<Bit1>, None, true, false) }
  let propFloat5 = { Name = "float5"; Type = InferedType.Primitive(typeof<float>, None, false, false) }
  let propFloat6 = { Name = "float6"; Type = InferedType.Primitive(typeof<decimal>, None, true, false) }
  let propDate =   { Name = "date";   Type = InferedType.Primitive(typeof<DateTime>, None, true, false) }
  let propBool =   { Name = "bool";   Type = InferedType.Primitive(typeof<bool>, None, true, false) }
  let propInt64 =  { Name = "int64";  Type = InferedType.Primitive(typeof<int64>, None, true, false) }
  let expected = toRecord [ propFloat1; propFloat2; propFloat3; propFloat4; propInt; propFloat5; propFloat6; propDate; propBool; propInt64 ]
  actual |> should equal expected

  // Test second part of the csv inference
  let actual = CsvInference.getFields false actual typeOverrides
  let field name (wrapper:TypeWrapper) typ = PrimitiveInferedProperty.Create(name, typ, wrapper, None)
  let propFloat1 = field "float1" TypeWrapper.None     typeof<float>
  let propFloat2 = field "float2" TypeWrapper.None     typeof<float>
  let propFloat3 = field "float3" TypeWrapper.None     typeof<float>
  let propFloat4 = field "float4" TypeWrapper.None     typeof<float>
  let propInt =    field "int"    TypeWrapper.Nullable typeof<Bit1>
  let propFloat5 = field "float5" TypeWrapper.None     typeof<float>
  let propFloat6 = field "float6" TypeWrapper.None     typeof<float>
  let propDate =   field "date"   TypeWrapper.Option   typeof<DateTime>
  let propBool =   field "bool"   TypeWrapper.Option   typeof<bool>
  let propInt64 =  field "int64"  TypeWrapper.Nullable typeof<int64>
  let expected = [ propFloat1; propFloat2; propFloat3; propFloat4; propInt; propFloat5; propFloat6; propDate; propBool; propInt64 ]
  actual |> should equal expected

//to be able to test units of measures we have to compare the typenames with strings
let prettyTypeName (t:Type) =
  t.ToString()
   .Replace("Microsoft.FSharp.Data.UnitSystems.SI.", null)
   .Replace("UnitNames.", null)
   .Replace("UnitSymbols.", null)
   .Replace("System.", null)
   .Replace("[]", null)
   .Replace("[", "<")
   .Replace("]", ">")
   .Replace("String", "string")
   .Replace("Double", "float")
   .Replace("Decimal", "decimal")
   .Replace("Int32", "int")
   .Replace("Int64", "int64")
   .Replace("Boolean", "bool")
   .Replace("DateTime", "date")

[<Test>]
let ``Infers units of measure correctly``() =

    let source = CsvFile.Parse("String(metre), Float(meter),Date (second),Int\t( Second), Decimal  (watt),Bool(N), Long(N), Unknown (measure)\nxpto, #N/A,2010-01-10,4,3.7, yes,2147483648,2")
    let actual =
      inferType source Int32.MaxValue [|"#N/A"|] culture "" false false
      ||> CsvInference.getFields false
      |> List.map (fun field ->
          field.Name,
          field.Value.RuntimeType,
          prettyTypeName field.Value.TypeWithMeasure)

    let propString =  "String(metre)"      , typeof<string>  , "string"
    let propFloat =   "Float"              , typeof<float>   , "float<meter>"
    let propDate =    "Date (second)"      , typeof<DateTime>, "date"
    let propInt =     "Int"                , typeof<int>     , "int<second>"
    let propDecimal = "Decimal"            , typeof<decimal> , "decimal<watt>"
    let propBool =    "Bool(N)"            , typeof<bool>    , "bool"
    let propLong =    "Long"               , typeof<int64>   , "int64<N>"
    let propInt2 =    "Unknown (measure)"  , typeof<int>     , "int"
    let expected = [ propString; propFloat; propDate; propInt; propDecimal; propBool; propLong; propInt2 ]

    actual |> should equal expected

[<Test>]
let ``Inference schema override by column name``() =

  let source = CsvFile.Parse("A (second), B (decimal?), C (float<watt>), float, second, float<N>\n1,1,,1,1,1")
  let actual =
    inferType source Int32.MaxValue [||] culture "" false false
    ||> CsvInference.getFields false
    |> List.map (fun field ->
        field.Name,
        field.Value.RuntimeType,
        prettyTypeName field.Value.TypeWithMeasure,
        field.Value.TypeWrapper)

  let col1 = "A"       , typeof<int>    , "int<second>", TypeWrapper.None
  let col2 = "B"       , typeof<decimal>, "decimal"    , TypeWrapper.Nullable
  let col3 = "C"       , typeof<float>  , "float<watt>", TypeWrapper.None
  let col4 = "float"   , typeof<int>    , "int"        , TypeWrapper.None
  let col5 = "second"  , typeof<int>    , "int"        , TypeWrapper.None
  let col6 = "float<N>", typeof<int>    , "int"        , TypeWrapper.None
  let expected = [ col1; col2; col3; col4; col5; col6 ]

  actual |> should equal expected

[<Test>]
let ``Inference schema override by parameter``() =

  let source = CsvFile.Parse(",Foo,,,,\n1,1,1,1,1,")
  let actual =
    inferType source Int32.MaxValue [||] culture "float,,float?<second>,A(float option),foo,C(float<m>)" false false
    ||> CsvInference.getFields false
    |> List.map (fun field ->
        field.Name,
        field.Value.RuntimeType,
        prettyTypeName field.Value.TypeWithMeasure,
        field.Value.TypeWrapper)

  let col1 = "Column1" , typeof<float>, "float"        , TypeWrapper.None
  let col2 = "Foo"     , typeof<int>  , "int"          , TypeWrapper.None
  let col3 = "Column3" , typeof<float>, "float<second>", TypeWrapper.Nullable
  let col4 = "A"       , typeof<float>, "float"        , TypeWrapper.Option
  let col5 = "foo"     , typeof<int>  , "int"          , TypeWrapper.None
  let col6 = "C"       , typeof<float>, "float<m>"     , TypeWrapper.None
  let expected = [ col1; col2; col3; col4; col5; col6 ]

  actual |> should equal expected

[<Test>]
let ``Column name with parentheses is parsed correctly in schema`` () =
  // Regression test for https://github.com/fsprojects/FSharp.Data/issues/946
  // A column name like "Na(  )me" must not be split at the first '(' when the
  // schema type annotation "(int)" appears at the end.
  let source = CsvFile.Parse("Na(  )me,other\n1,2")
  let actual =
    inferType source Int32.MaxValue [||] culture "Na(  )me (int),other (string)" false false
    ||> CsvInference.getFields false
    |> List.map (fun field -> field.Name, field.Value.RuntimeType)

  let expected = [ "Na(  )me", typeof<int>; "other", typeof<string> ]
  actual |> should equal expected

[<Test>]
let ``Doesn't infer 12-002 as a date``() =
  // a previous version inferred a IntOrStringOrDateTime
  let source = JsonValue.Parse """[ "12-002", "001", "2012-selfservice" ]"""
  let expected =
    InferedType.Collection
        ([ InferedTypeTag.String; InferedTypeTag.Number],
         [ InferedTypeTag.String, (Multiple, InferedType.Primitive(typeof<string>, None, false, false))
           InferedTypeTag.Number, (Single, InferedType.Primitive(typeof<Bit1>, None, false, false)) ] |> Map.ofList)
  let actual = JsonInference.inferType unitsOfMeasureProvider inferenceMode culture "" source
  actual |> should equal expected

[<Test>]
let ``Doesn't infer ad3mar as a date``() =
  StructuralInference.inferPrimitiveType unitsOfMeasureProvider inferenceMode CultureInfo.InvariantCulture "ad3mar" None
  |> should equal (InferedType.Primitive(typeof<string>, None, false, false))

[<Test>]
let ``Inference with % suffix``() =
  let source = CsvFile.Parse("float,integer\n2.0%,2%\n4.0%,3%\n")
  let actual, _ = inferType source Int32.MaxValue [||] culture "" false false
  let propFloat = { Name = "float"; Type = InferedType.Primitive(typeof<Decimal>, None, false, false) }
  let propInteger = { Name = "integer"; Type = InferedType.Primitive(typeof<int>, None, false, false) }
  let expected = toRecord [ propFloat ; propInteger ]
  actual |> should equal expected

[<Test>]
let ``Inference with $ prefix``() =
  let source = CsvFile.Parse("float,integer\n$2.0,$2\n$4.0,$3\n")
  let actual, _ = inferType source Int32.MaxValue [||] culture "" false false
  let propFloat = { Name = "float"; Type = InferedType.Primitive(typeof<Decimal>, None, false, false) }
  let propInteger = { Name = "integer"; Type = InferedType.Primitive(typeof<int>, None, false, false) }
  let expected = toRecord [ propFloat ; propInteger ]
  actual |> should equal expected

let internal getInferedTypeFromSamples samples =
    let culture = System.Globalization.CultureInfo.InvariantCulture
    samples
    |> Array.map XElement.Parse
    |> XmlInference.inferType unitsOfMeasureProvider inferenceMode culture false false
    |> Seq.fold (subtypeInfered false) InferedType.Top

let internal getInferedTypeFromSchema xsd =
    xsd
    |> XmlSchema.parseSchema ""
    |> XsdParsing.getElements
    |> List.ofSeq
    |> XsdInference.inferElements

let internal isValid xsd =
    let xmlSchemaSet = XmlSchema.parseSchema "" xsd
    fun xml ->
        let settings = XmlReaderSettings(ValidationType = ValidationType.Schema)
        settings.Schemas <- xmlSchemaSet
        use reader = XmlReader.Create(new System.IO.StringReader(xml), settings)
        try
            while reader.Read() do ()
            true
        with :? XmlSchemaException as e ->
            printfn "%s/n%s" e.Message xml
            false

let internal getInferedTypes xsd xmlSamples =
    //printfn "%s/n---------------------------------------------" xsd
    let isValid = isValid xsd
    for xml in xmlSamples do
        //printfn "%A/n---------------------------------------------" xml
        xml.ToString() |> isValid |> should equal true

    let inferedTypeFromSchema = getInferedTypeFromSchema xsd
    //printfn "%A" inferedTypeFromSchema
    let inferedTypeFromSamples = getInferedTypeFromSamples xmlSamples
    //printfn "%A" inferedTypeFromSamples
    inferedTypeFromSchema, inferedTypeFromSamples

let internal check xsd xmlSamples =
    //printfn "checking schema and samples"
    let inferedTypeFromSchema, inferedTypeFromSamples = getInferedTypes xsd xmlSamples
    inferedTypeFromSchema |> should equal inferedTypeFromSamples

[<Test>]
let ``at least one global complex element is needed``() =
    let xsd = """
      <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
        elementFormDefault="qualified" attributeFormDefault="unqualified">
          <xs:element name="foo" type="xs:string" />
      </xs:schema>
    """
    let msg = "No suitable element definition found in the schema."
    (fun () -> getInferedTypeFromSchema xsd |> ignore)
    |> should (throwWithMessage msg) typeof<System.Exception>

[<Test>]
let ``untyped elements have no properties``() =
    let xsd = """
      <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
        elementFormDefault="qualified" attributeFormDefault="unqualified">
          <xs:element name="foo" />
      </xs:schema>
    """
    let sample1 = "<foo><a/></foo>"
    let sample2 = "<foo><b/></foo>"
    let ty, _ = getInferedTypes xsd [| sample1; sample2 |]
    ty |> should equal (InferedType.Record (Some "foo", [], false))


[<Test>]
let ``names can be qualified``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      targetNamespace="http://test.001"
      elementFormDefault="qualified" attributeFormDefault="qualified">
      <xs:element name="foo">
        <xs:complexType>
          <xs:attribute name="bar" type="xs:string" use="required" form="qualified" />
          <xs:attribute name="baz" type="xs:int" use="required" form="unqualified" />
        </xs:complexType>
      </xs:element>
    </xs:schema>"""
    let sample = """<foo xmlns='http://test.001' xmlns:t='http://test.001' t:bar='aa' baz='2' />"""
    check xsd [| sample |]

[<Test>]
let ``recursive schemas don't cause loops``() =
    let xsd = """ <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
	    <xs:complexType name="TextType" mixed="true">
		    <xs:choice minOccurs="0" maxOccurs="unbounded">
			    <xs:element ref="bold"/>
			    <xs:element ref="italic"/>
			    <xs:element ref="underline"/>
		    </xs:choice>
	    </xs:complexType>
	    <xs:element name="bold" type="TextType"/>
	    <xs:element name="italic" type="TextType"/>
	    <xs:element name="underline" type="TextType"/>
    </xs:schema>"""
    let inferedTypeFromSchema = getInferedTypeFromSchema xsd
    inferedTypeFromSchema |> ignore
    //printfn "%A" inferedTypeFromSchema

    let xsd = """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
        <xs:element name="Section" type="SectionType" />
        <xs:complexType name="SectionType">
          <xs:sequence>
            <xs:element name="Title" type="xs:string" />
            <xs:element name="Section" type="SectionType" minOccurs="0"/>
          </xs:sequence>
        </xs:complexType>
        </xs:schema>"""
    let inferedTypeFromSchema = getInferedTypeFromSchema xsd
    //printfn "%A" inferedTypeFromSchema
    inferedTypeFromSchema |> ignore


[<Test>]
let ``attributes become properties``() =
    let xsd = """
      <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
        elementFormDefault="qualified" attributeFormDefault="unqualified">
        <xs:element name="foo">
	       <xs:complexType>
		     <xs:attribute name="bar" type="xs:string" use="required" />
             <xs:attribute name="baz" type="xs:int" />
	      </xs:complexType>
	    </xs:element>
      </xs:schema>    """
    let sample1 = "<foo bar='aa' />"
    let sample2 = "<foo bar='aa' baz='2' />"
    check xsd [| sample1; sample2 |]

[<Test>]
let ``multiple root elements are allowed``() =
    let xsd = """
      <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
        elementFormDefault="qualified" attributeFormDefault="unqualified">
        <xs:element name="root1">
	      <xs:complexType>
		     <xs:attribute name="foo" type="xs:string" use="required" />
             <xs:attribute name="fow" type="xs:int" use="required" />
	      </xs:complexType>
	    </xs:element>
        <xs:element name="root2">
           <xs:complexType>
		     <xs:attribute name="bar" type="xs:string" use="required" />
             <xs:attribute name="baz" type="xs:date" use="required" />
	       </xs:complexType>
        </xs:element>
      </xs:schema>
      """
    let sample1 = "<root1 foo='aa' fow='34' />"
    let sample2 = "<root2 bar='aa' baz='2017-12-22' />"
    check xsd [| sample1; sample2 |]


[<Test>]
let ``sequences can have elements``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo">
        <xs:complexType>
		  <xs:sequence>
			<xs:element name="bar" type="xs:int"/>
			<xs:element name="baz" type="xs:int"/>
		  </xs:sequence>
		</xs:complexType>
	  </xs:element>
    </xs:schema>    """
    let sample = "<foo><bar>2</bar><baz>5</baz></foo>"
    check xsd [| sample |]

[<Test>]
let ``sequences can have multiple elements``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo">
        <xs:complexType>
		  <xs:sequence>
			<xs:element name="bar" type="xs:int" maxOccurs='unbounded' />
			<xs:element name="baz" type="xs:int" maxOccurs='3' />
		  </xs:sequence>
		</xs:complexType>
	  </xs:element>
    </xs:schema>    """
    let sample = """
    <foo>
      <bar>2</bar>
      <bar>3</bar>
      <baz>5</baz>
      <baz>5</baz>
    </foo>"""
    check xsd [| sample |]


[<Test>]
let ``sequences may occur multiple times``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo">
        <xs:complexType>
		  <xs:sequence maxOccurs='unbounded'>
			<xs:element name="bar" type="xs:int"/>
			<xs:element name="baz" type="xs:int"/>
		  </xs:sequence>
		</xs:complexType>
	  </xs:element>
    </xs:schema>    """
    let sample = """
    <foo>
        <bar>2</bar><baz>5</baz>
        <bar>3</bar><baz>6</baz>
    </foo>"""
    check xsd [| sample |]

[<Test>]
let ``sequences can be nested``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo">
        <xs:complexType>
		  <xs:sequence maxOccurs='unbounded'>
            <xs:sequence maxOccurs='1'>
			  <xs:element name="bar" type="xs:int"/>
			  <xs:element name="baz" type="xs:int"/>
            </xs:sequence>
		  </xs:sequence>
		</xs:complexType>
	  </xs:element>
    </xs:schema>    """
    let sample = """
    <foo>
        <bar>2</bar><baz>5</baz>
        <bar>3</bar><baz>6</baz>
    </foo>"""
    check xsd [| sample |]

[<Test>]
let ``sequences can have multiple nested sequences``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo">
        <xs:complexType>
		  <xs:sequence maxOccurs='1'>
            <xs:sequence maxOccurs='unbounded'>
			  <xs:element name="bar" type="xs:int"/>
			  <xs:element name="baz" type="xs:int"/>
            </xs:sequence>
		  </xs:sequence>
		</xs:complexType>
	  </xs:element>
    </xs:schema>    """
    let sample = """
    <foo>
        <bar>2</bar><baz>5</baz>
        <bar>3</bar><baz>6</baz>
    </foo>"""
    check xsd [| sample |]


[<Test>]
let ``simple content can be extended with attributes``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo">
		<xs:complexType>
		  <xs:simpleContent>
			  <xs:extension base="xs:date">
				  <xs:attribute name="bar" type="xs:string" />
				  <xs:attribute name="baz" type="xs:int" />
			  </xs:extension>
		  </xs:simpleContent>
		</xs:complexType>
	  </xs:element>
    </xs:schema>"""
    let sample1 = """<foo>1957-08-13</foo>"""
    let sample2 = """<foo bar="hello" baz="2">1957-08-13</foo>"""
    check xsd [| sample1; sample2 |]

[<Test>]
let ``elements in a choice become optional``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
      <xs:element name="foo">
		<xs:complexType>
		  <xs:choice>
            <xs:element name="bar" type="xs:int" />
            <xs:element name="baz" type="xs:date" />
		  </xs:choice>
		</xs:complexType>
	  </xs:element>
    </xs:schema>"""
    let sample1 = """<foo><bar>5</bar></foo>"""
    let sample2 = """<foo><baz>1957-08-13</baz></foo>"""
    check xsd [| sample1; sample2 |]

[<Test>]
let ``elements can reference attribute groups``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
	    <xs:attributeGroup name="myAttributes">
		    <xs:attribute name="myNr" type="xs:int" use="required" />
		    <xs:attribute name="available" type="xs:boolean" use="required" />
	    </xs:attributeGroup>
	    <xs:element name="foo">
            <xs:complexType>
                <xs:attributeGroup ref="myAttributes"/>
                <xs:attribute name="lang" type="xs:language" use="required" />
            </xs:complexType>
	    </xs:element>
    </xs:schema>"""
    let sample1 = """
    <foo myNr="42" available="false" lang="en-US" />"""
    check xsd [| sample1 |]


[<Test; Ignore("test failing, System.Xml.Schema.XmlSchemaException : The 'http://www.w3.org/XML/1998/namespace:base' attribute is not declared") >]
let ``can import namespaces``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
	    <xs:import namespace="http://www.w3.org/XML/1998/namespace" schemaLocation="http://www.w3.org/2001/03/xml.xsd"/>
        <xs:element name="test">
		    <xs:complexType>
			    <xs:attribute ref="xml:base"/>
		    </xs:complexType>
	    </xs:element>
    </xs:schema>"""
    let inferedTypeFromSchema = getInferedTypeFromSchema xsd
    //printfn "%A" inferedTypeFromSchema
    inferedTypeFromSchema |> ignore

[<Test>]
let ``simple elements can be nillable``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
	    <xs:element name="author">
            <xs:complexType>
                <xs:sequence>
                    <xs:element name="name" type="xs:string" nillable="true" />
                    <xs:element name="surname" type="xs:string" />
                </xs:sequence>
            </xs:complexType>
        </xs:element>
    </xs:schema>"""
    let sample1 = """
    <author>
        <name>Stefano</name>
        <surname>Benny</surname>
    </author>
    """
    let sample2 = """
    <author>
        <name xsi:nil="true"
            xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"/>
        <surname>Benny</surname>
    </author>
    """
    check xsd [| sample1; sample2 |]



[<Test>]
let ``complex elements can be nillable``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
	    <xs:element name="person">
            <xs:complexType>
                <xs:sequence>
	                <xs:element name="address" nillable="true">
                        <xs:complexType>
                            <xs:sequence>
                                <xs:element name="city" type="xs:string" />
                            </xs:sequence>
                        </xs:complexType>
                    </xs:element>
                </xs:sequence>
            </xs:complexType>
        </xs:element>
    </xs:schema>"""
    let sample1 = """
    <person>
        <address xsi:nil="true" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"/>
    </person>
    """
    let sample2 = """
    <person>
        <address>
            <city>Bologna</city>
        </address>
    </person>
    """
    check xsd [| sample1; sample2 |]

[<Test>]
let ``substitution groups are supported``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
        <xs:element name="name" type="xs:string"/>
        <xs:element name="nome" substitutionGroup="name" type="xs:string" />
        <xs:element name="person">
          <xs:complexType>
            <xs:sequence>
              <xs:element ref="name"/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
        <xs:element name="persona" substitutionGroup="person"/>
    </xs:schema>"""
    let sample1 = """<person><name>Jim</name></person>"""
    let sample2 = """<persona><name>Jim</name></persona>"""
    let sample3 = """<person><nome>Jim</nome></person>"""
    let sample4 = """<persona><nome>Jim</nome></persona>"""

    check xsd [| sample1; sample2; sample3; sample4 |]

[<Test>]
let ``list fallback to string``() =
    let xsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
      elementFormDefault="qualified" attributeFormDefault="unqualified">
        <xs:simpleType name="listOfInts">
          <xs:list itemType="xs:int"/>
        </xs:simpleType>
        <xs:element name="nums">
          <xs:complexType>
		    <xs:simpleContent>
			  <xs:extension base="listOfInts" />
		    </xs:simpleContent>
		  </xs:complexType>
        </xs:element>
    </xs:schema>"""
    let sample = """<nums>40 50 60</nums>"""
    check xsd [| sample |]


open System.Xml.Schema

[<Test>]
let ``abstract elements can be recursive``() =

    // sample xsd with a recursive abstract element and substitution groups
    let formulaXsd = """
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
        elementFormDefault="qualified" attributeFormDefault="unqualified">
	    <xs:element name="Formula" abstract="true"/>
	    <xs:element name="Prop" type="xs:string" substitutionGroup="Formula"/>
	    <xs:element name="And" substitutionGroup="Formula">
		    <xs:complexType>
			    <xs:sequence>
				    <xs:element ref="Formula" minOccurs="2" maxOccurs="2"/>
			    </xs:sequence>
		    </xs:complexType>
	    </xs:element>
    </xs:schema>
    """

    let xsd = XmlSchema.parseSchema "" formulaXsd
    let elms = xsd.GlobalElements.Values |> XsdParsing.ofType<XmlSchemaElement>
    let andElm = elms |> Seq.filter (fun x -> x.Name = "And") |> Seq.exactlyOne
    let formElm = elms |> Seq.filter (fun x -> x.Name = "Formula") |> Seq.exactlyOne
    let formRefElm = // 'And' is a sequence whose only item is a reference to 'Formula'
        let formRefType = andElm.ElementSchemaType :?> XmlSchemaComplexType
        (formRefType.ContentTypeParticle :?> XmlSchemaSequence).Items
        |> XsdParsing.ofType<XmlSchemaElement>
        |> Seq.exactlyOne

    formRefElm.QualifiedName |> should equal formElm.QualifiedName
    formRefElm.QualifiedName |> should equal formRefElm.RefName
    formElm.ElementSchemaType |> should equal formRefElm.ElementSchemaType
    // this may be a bit surprising:
    formElm.IsAbstract |> should equal true
    formRefElm.IsAbstract |> should equal false

    let sample1 = """<Prop>p1</Prop>"""
    let sample2 = """
    <And>
        <Prop>p1</Prop>
        <And>
            <Prop>p2</Prop>
            <Prop>p3</Prop>
        </And>
    </And>
    """
    getInferedTypes formulaXsd [| sample1; sample2 |]
    |> ignore

