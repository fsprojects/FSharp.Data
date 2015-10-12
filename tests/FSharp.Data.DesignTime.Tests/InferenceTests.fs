#if INTERACTIVE
#r "../../packages/NUnit/lib/nunit.framework.dll"
#r "../../bin/FSharp.Data.DesignTime.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.DesignTime.Tests.InferenceTests
#endif

open FsUnit
open System
open System.Globalization
open NUnit.Framework
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.CsvInference
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference
open ProviderImplementation

/// A collection containing just one type
let SimpleCollection typ = 
  InferedType.Collection([ typeTag typ], Map.ofSeq [typeTag typ, (InferedMultiplicity.Multiple, typ)])

let culture = TextRuntime.GetCulture ""

let inferType (csv:CsvFile) inferRows missingValues cultureInfo schema assumeMissingValues preferOptionals =
    let headerNamesAndUnits, schema = parseHeaders csv.Headers csv.NumberOfColumns schema ProviderHelpers.unitsOfMeasureProvider
    inferType headerNamesAndUnits schema (csv.Rows |> Seq.map (fun x -> x.Columns)) inferRows missingValues cultureInfo assumeMissingValues preferOptionals

let toRecord fields = InferedType.Record(None, fields, false)

let inferTypesFromValues = true

[<Test>]
let ``List.pairBy helper function works``() = 
  let actual = List.pairBy fst [(2, "a"); (1, "b")] [(1, "A"); (3, "C")]
  let expected = 
    [ (1, Some (1, "b"), Some (1, "A"))
      (2, Some (2, "a"), None)
      (3, None, Some (3, "C")) ]
  set actual |> shouldEqual (set expected)

[<Test>]
let ``List.pairBy helper function preserves order``() = 
  let actual = List.pairBy fst [("one", "a"); ("two", "b")] [("one", "A"); ("two", "B")]
  let expected = 
    [ ("one", Some ("one", "a"), Some ("one", "A"))
      ("two", Some ("two", "b"), Some ("two", "B")) ] 
  actual |> shouldEqual expected

[<Test>]
let ``Finds common subtype of numeric types (decimal)``() =
  let source = JsonValue.Parse """[ 10, 10.23 ]"""
  let expected = SimpleCollection(InferedType.Primitive(typeof<decimal>, None, false))
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Finds common subtype of numeric types (int64)``() =
  let source = JsonValue.Parse """[ 10, 2147483648 ]"""
  let expected = SimpleCollection(InferedType.Primitive(typeof<int64>, None, false))
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Infers heterogeneous type of InferedType.Primitives``() =
  let source = JsonValue.Parse """[ 1,true ]"""
  let expected = 
    InferedType.Collection
        ([ InferedTypeTag.Number; InferedTypeTag.Boolean ],
         [ InferedTypeTag.Number, (Single, InferedType.Primitive(typeof<Bit1>, None, false))
           InferedTypeTag.Boolean, (Single, InferedType.Primitive(typeof<bool>, None, false)) ] |> Map.ofList)
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Infers heterogeneous type of InferedType.Primitives and nulls``() =
  let source = JsonValue.Parse """[ 1,true,null ]"""
  let expected = 
    InferedType.Collection
        ([ InferedTypeTag.Number; InferedTypeTag.Boolean; InferedTypeTag.Null ],
         [ InferedTypeTag.Null, (Single, InferedType.Null)
           InferedTypeTag.Number, (Single, InferedType.Primitive(typeof<Bit1>, None, false))
           InferedTypeTag.Boolean, (Single, InferedType.Primitive(typeof<bool>, None, false)) ] |> Map.ofList)
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Finds common subtype of numeric types (float)``() =
  let source = JsonValue.Parse """[ 10, 10.23, 79228162514264337593543950336 ]"""
  let expected = SimpleCollection(InferedType.Primitive(typeof<float>, None, false))
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Infers heterogeneous type of InferedType.Primitives and records``() =
  let source = JsonValue.Parse """[ {"a":0}, 1,2 ]"""
  let expected = 
    InferedType.Collection
        ([ InferedTypeTag.Record None; InferedTypeTag.Number ],
         [ InferedTypeTag.Number, (Multiple, InferedType.Primitive(typeof<int>, None, false))
           InferedTypeTag.Record None, 
             (Single, toRecord [ { Name="a"; Type=InferedType.Primitive(typeof<Bit0>, None, false) } ]) ] |> Map.ofList)
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Merges types in a collection of collections``() =
  let source = JsonValue.Parse """[ [{"a":true,"c":0},{"b":1,"c":0}], [{"b":1.1,"c":0}] ]"""
  let expected = 
    [ { Name = "a"; Type = InferedType.Primitive(typeof<bool>, None, true) }
      { Name = "c"; Type = InferedType.Primitive(typeof<Bit0>, None, false) } 
      { Name = "b"; Type = InferedType.Primitive(typeof<decimal>, None, true) } ]
    |> toRecord
    |> SimpleCollection 
    |> SimpleCollection
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Unions properties of records in a collection``() =
  let source = JsonValue.Parse """[ {"a":1, "b":""}, {"a":1.2, "c":true} ]"""
  let expected =
    [ { Name = "a"; Type = InferedType.Primitive(typeof<decimal>, None, false) }
      { Name = "b"; Type = InferedType.Null }
      { Name = "c"; Type = InferedType.Primitive(typeof<bool>, None, true) } ]
    |> toRecord
    |> SimpleCollection
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Null should make string optional``() =
  let source = JsonValue.Parse """[ {"a":null}, {"a":"b"} ]"""
  let expected =
    [ { Name = "a"; Type = InferedType.Primitive(typeof<string>, None, true) } ]
    |> toRecord
    |> SimpleCollection
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Null is not a valid value of DateTime``() =
  let actual = 
    subtypeInfered false InferedType.Null (InferedType.Primitive(typeof<DateTime>, None, false))
  let expected = InferedType.Primitive(typeof<DateTime>, None, true)
  actual |> shouldEqual expected

[<Test>]
let ``Infers mixed fields of a a record as heterogeneous type with nulls (1.)``() =
  let source = JsonValue.Parse """[ {"a":null}, {"a":123} ]"""
  let expected =
    [ { Name = "a"; Type = InferedType.Primitive(typeof<int>, None, true) } ]
    |> toRecord
    |> SimpleCollection
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Null makes a record optional``() =
  let source = JsonValue.Parse """[ {"a":null}, {"a":{"b": 1}} ]"""
  let expected =
    [ { Name = "a"; Type = InferedType.Record(Some "a", [{ Name = "b"; Type = InferedType.Primitive(typeof<Bit1>, None, false) }], true) } ]
    |> toRecord
    |> SimpleCollection
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Infers mixed fields of a record as heterogeneous type``() =
  let source = JsonValue.Parse """[ {"a":"hi"}, {"a":2} , {"a":2147483648} ]"""
  let cases = 
    Map.ofSeq [ InferedTypeTag.String, InferedType.Primitive(typeof<string>, None, false) 
                InferedTypeTag.Number, InferedType.Primitive(typeof<int64>, None, false) ]
  let expected = 
    [ { Name = "a"; Type = InferedType.Heterogeneous cases }]
    |> toRecord
    |> SimpleCollection
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Infers mixed fields of a record as heterogeneous type with nulls (2.)``() =
  let source = JsonValue.Parse """[ {"a":null}, {"a":2} , {"a":3} ]"""
  let expected = 
    [ { Name = "a"; Type = InferedType.Primitive(typeof<int>, None, true) }]
    |> toRecord
    |> SimpleCollection
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Inference of multiple nulls works``() = 
  let source = JsonValue.Parse """[0, [{"a": null}, {"a":null}]]"""
  let prop = { Name = "a"; Type = InferedType.Null }
  let expected = 
    InferedType.Collection
        ([ InferedTypeTag.Number; InferedTypeTag.Collection ],
         [ InferedTypeTag.Collection, (Single, SimpleCollection(toRecord [prop]))
           InferedTypeTag.Number, (Single, InferedType.Primitive(typeof<Bit0>, None, false)) ] |> Map.ofList)
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Inference of DateTime``() = 
  let source = CsvFile.Parse("date,int,float\n2012-12-19,2,3.0\n2012-12-12,4,5.0\n2012-12-1,6,10.0")
  let actual, _ = inferType source Int32.MaxValue [||] culture "" false false
  let propDate = { Name = "date"; Type = InferedType.Primitive(typeof<DateTime>, None, false) }
  let propInt = { Name = "int"; Type = InferedType.Primitive(typeof<int>, None, false) }
  let propFloat = { Name = "float"; Type = InferedType.Primitive(typeof<Decimal>, None, false) }
  let expected = toRecord [ propDate ; propInt ; propFloat ]
  actual |> shouldEqual expected

[<Test>]
let ``Inference of DateTime with timestamp``() = 
  let source = CsvFile.Parse("date,timestamp\n2012-12-19,2012-12-19 12:00\n2012-12-12,2012-12-12 00:00\n2012-12-1,2012-12-1 07:00")
  let actual, _ = inferType source Int32.MaxValue [||] culture "" false false
  let propDate = { Name = "date"; Type = InferedType.Primitive(typeof<DateTime>, None, false) }
  let propTimestamp = { Name = "timestamp"; Type = InferedType.Primitive(typeof<DateTime>, None, false) }
  let expected = toRecord [ propDate ; propTimestamp ]
  actual |> shouldEqual expected

[<Test>]
let ``Inference of DateTime with timestamp non default separator``() = 
  let source = CsvFile.Parse("date;timestamp\n2012-12-19;2012-12-19 12:00\n2012-12-12;2012-12-12 00:00\n2012-12-1;2012-12-1 07:00", ";")
  let actual, _ = inferType source Int32.MaxValue [||] culture "" false false
  let propDate = { Name = "date"; Type = InferedType.Primitive(typeof<DateTime>, None, false) }
  let propTimestamp = { Name = "timestamp"; Type = InferedType.Primitive(typeof<DateTime>, None, false) }
  let expected = toRecord [ propDate ; propTimestamp ]
  actual |> shouldEqual expected

[<Test>]
let ``Inference of float with #N/A values and non default separator``() = 
  let source = CsvFile.Parse("float;integer\n2.0;2\n#N/A;3\n", ";")
  let actual, _ = inferType source Int32.MaxValue [|"#N/A"|] culture "" false false
  let propFloat = { Name = "float"; Type = InferedType.Primitive(typeof<float>, None, false) }
  let propInteger = { Name = "integer"; Type = InferedType.Primitive(typeof<int>, None, false) }
  let expected = toRecord [ propFloat ; propInteger ]
  actual |> shouldEqual expected

[<Test>]
let ``Inference of numbers with empty values``() = 
  let source = CsvFile.Parse("""float1,float2,float3,float4,int,float5,float6,date,bool,int64
                                1,1,1,1,,,,,,
                                2.0,#N/A,,1,1,1,,2010-01-10,yes,
                                ,,2.0,NA,1,foo,2.0,,,2147483648""")
  let actual, typeOverrides = inferType source Int32.MaxValue [|"#N/A"; "NA"; "foo"|] culture "" false false
  let propFloat1 = { Name = "float1"; Type = InferedType.Primitive(typeof<decimal>, None, true) }
  let propFloat2 = { Name = "float2"; Type = InferedType.Primitive(typeof<float>, None, false) }
  let propFloat3 = { Name = "float3"; Type = InferedType.Primitive(typeof<decimal>, None, true) }
  let propFloat4 = { Name = "float4"; Type = InferedType.Primitive(typeof<float>, None, false) }
  let propInt =    { Name = "int";    Type = InferedType.Primitive(typeof<Bit1>, None, true) }
  let propFloat5 = { Name = "float5"; Type = InferedType.Primitive(typeof<float>, None, false) }
  let propFloat6 = { Name = "float6"; Type = InferedType.Primitive(typeof<decimal>, None, true) }
  let propDate =   { Name = "date";   Type = InferedType.Primitive(typeof<DateTime>, None, true) }
  let propBool =   { Name = "bool";   Type = InferedType.Primitive(typeof<bool>, None, true) }
  let propInt64 =  { Name = "int64";  Type = InferedType.Primitive(typeof<int64>, None, true) }
  let expected = toRecord [ propFloat1; propFloat2; propFloat3; propFloat4; propInt; propFloat5; propFloat6; propDate; propBool; propInt64 ]
  actual |> shouldEqual expected

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
  actual |> shouldEqual expected

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
          field.RuntimeType, 
          prettyTypeName field.TypeWithMeasure)

    let propString =  "String(metre)"      , typeof<string>  , "string"
    let propFloat =   "Float"              , typeof<float>   , "float<meter>"
    let propDate =    "Date (second)"      , typeof<DateTime>, "date"
    let propInt =     "Int"                , typeof<int>     , "int<second>"
    let propDecimal = "Decimal"            , typeof<decimal> , "decimal<watt>"
    let propBool =    "Bool(N)"            , typeof<bool>    , "bool"
    let propLong =    "Long"               , typeof<int64>   , "int64<N>"
    let propInt2 =    "Unknown (measure)"  , typeof<int>     , "int"
    let expected = [ propString; propFloat; propDate; propInt; propDecimal; propBool; propLong; propInt2 ]

    actual |> shouldEqual expected

[<Test>]
let ``Inference schema override by column name``() = 

  let source = CsvFile.Parse("A (second), B (decimal?), C (float<watt>), float, second, float<N>\n1,1,,1,1,1")
  let actual = 
    inferType source Int32.MaxValue [||] culture "" false false
    ||> CsvInference.getFields false
    |> List.map (fun field -> 
        field.Name, 
        field.RuntimeType, 
        prettyTypeName field.TypeWithMeasure,
        field.TypeWrapper)

  let col1 = "A"       , typeof<int>    , "int<second>", TypeWrapper.None
  let col2 = "B"       , typeof<decimal>, "decimal"    , TypeWrapper.Nullable
  let col3 = "C"       , typeof<float>  , "float<watt>", TypeWrapper.None
  let col4 = "float"   , typeof<int>    , "int"        , TypeWrapper.None
  let col5 = "second"  , typeof<int>    , "int"        , TypeWrapper.None
  let col6 = "float<N>", typeof<int>    , "int"        , TypeWrapper.None
  let expected = [ col1; col2; col3; col4; col5; col6 ]

  actual |> shouldEqual expected

[<Test>]
let ``Inference schema override by parameter``() = 

  let source = CsvFile.Parse(",Foo,,,,\n1,1,1,1,1,")
  let actual = 
    inferType source Int32.MaxValue [||] culture "float,,float?<second>,A(float option),foo,C(float<m>)" false false
    ||> CsvInference.getFields false
    |> List.map (fun field -> 
        field.Name, 
        field.RuntimeType, 
        prettyTypeName field.TypeWithMeasure,
        field.TypeWrapper)

  let col1 = "Column1" , typeof<float>, "float"        , TypeWrapper.None
  let col2 = "Foo"     , typeof<int>  , "int"          , TypeWrapper.None
  let col3 = "Column3" , typeof<float>, "float<second>", TypeWrapper.Nullable
  let col4 = "A"       , typeof<float>, "float"        , TypeWrapper.Option
  let col5 = "foo"     , typeof<int>  , "int"          , TypeWrapper.None
  let col6 = "C"       , typeof<float>, "float<m>"     , TypeWrapper.None
  let expected = [ col1; col2; col3; col4; col5; col6 ]

  actual |> shouldEqual expected

[<Test>]
let ``Doesn't infer 12-002 as a date``() =
  // a previous version inferred a IntOrStringOrDateTime
  let source = JsonValue.Parse """[ "12-002", "001", "2012-selfservice" ]"""
  let expected = 
    InferedType.Collection
        ([ InferedTypeTag.String; InferedTypeTag.Number],
         [ InferedTypeTag.String, (Multiple, InferedType.Primitive(typeof<string>, None, false))
           InferedTypeTag.Number, (Single, InferedType.Primitive(typeof<Bit1>, None, false)) ] |> Map.ofList)
  let actual = JsonInference.inferType inferTypesFromValues culture "" source
  actual |> shouldEqual expected

[<Test>]
let ``Doesn't infer ad3mar as a date``() =
  StructuralInference.inferPrimitiveType CultureInfo.InvariantCulture "ad3mar"
  |> shouldEqual typeof<string>

[<Test>]
let ``Inference with % suffix``() = 
  let source = CsvFile.Parse("float,integer\n2.0%,2%\n4.0%,3%\n")
  let actual, _ = inferType source Int32.MaxValue [||] culture "" false false
  let propFloat = { Name = "float"; Type = InferedType.Primitive(typeof<Decimal>, None, false) }
  let propInteger = { Name = "integer"; Type = InferedType.Primitive(typeof<int>, None, false) }
  let expected = toRecord [ propFloat ; propInteger ]
  actual |> shouldEqual expected


[<Test>]
let ``Inference with $ prefix``() = 
  let source = CsvFile.Parse("float,integer\n$2.0,$2\n$4.0,$3\n")
  let actual, _ = inferType source Int32.MaxValue [||] culture "" false false
  let propFloat = { Name = "float"; Type = InferedType.Primitive(typeof<Decimal>, None, false) }
  let propInteger = { Name = "integer"; Type = InferedType.Primitive(typeof<int>, None, false) }
  let expected = toRecord [ propFloat ; propInteger ]
  actual |> shouldEqual expected
