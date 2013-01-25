// --------------------------------------------------------------------------------------
// Tests for a utility that generates nice PascalCase and camelCase names for members
// --------------------------------------------------------------------------------------
namespace FSharp.Data.Tests

#if INTERACTIVE
#r "../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#r "../bin/FSharp.Data.dll"
#endif

open System
open System.IO
open FSharp.Data.Json
open NUnit.Framework
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.TypeInference
open ProviderImplementation
open ProviderImplementation.StructureInference

module ProviderInference = 

  /// A collection containing just one type
  let SimpleCollection typ = 
    Collection(Map.ofSeq [typeTag typ, (InferedMultiplicity.Multiple, typ)])

  [<Test>]
  let ``Seq.pairBy helper function works``() = 
    let actual = Seq.pairBy fst [(2, "a"); (1, "b")] [(1, "A"); (3, "C")]
    let expected = 
      [ (1, Some (1, "b"), Some (1, "A"))
        (2, Some (2, "a"), None)
        (3, None, Some (3, "C")) ]
    Assert.AreEqual(set expected, set actual)

  [<Test>]
  let ``Seq.pairBy helper function preserves order``() = 
    let actual = Seq.pairBy fst [("one", "a"); ("two", "b")] [("one", "A"); ("two", "B")]
    let expected = 
      [ ("one", Some ("one", "a"), Some ("one", "A"))
        ("two", Some ("two", "b"), Some ("two", "B")) ] 
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Finds common subtype of numeric types (decimal)``() =
    let source = JsonValue.Parse """[ 10, 10.23 ]"""
    let expected = SimpleCollection(Primitive(typeof<decimal>, None))
    let actual = JsonInference.inferType InferenceOptions.None  source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Finds common subtype of numeric types (int64)``() =
    let source = JsonValue.Parse """[ 10, 2147483648 ]"""
    let expected = SimpleCollection(Primitive(typeof<int64>, None))
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Infers heterogeneous type of primitives``() =
    let source = JsonValue.Parse """[ 1,true ]"""
    let expected = 
      [ InferedTypeTag.Number, (Single, Primitive(typeof<int>, None))
        InferedTypeTag.Boolean, (Single, Primitive(typeof<bool>, None)) ]
      |> Map.ofSeq |> Collection
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Infers heterogeneous type of primitives and nulls``() =
    let source = JsonValue.Parse """[ 1,true,null ]"""
    let expected = 
      [ InferedTypeTag.Null, (Single, Null)
        InferedTypeTag.Number, (Single, Primitive(typeof<int>, None))
        InferedTypeTag.Boolean, (Single, Primitive(typeof<bool>, None)) ]
      |> Map.ofSeq |> Collection
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Finds common subtype of numeric types (float)``() =
    let source = JsonValue.Parse """[ 10, 10.23, 79228162514264337593543950336 ]"""
    let expected = SimpleCollection(Primitive(typeof<float>, None))
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Infers heterogeneous type of primitives and records``() =
    let source = JsonValue.Parse """[ {"a":0}, 1,2 ]"""
    let expected = 
      [ InferedTypeTag.Number, (Multiple, Primitive(typeof<int>, None))
        InferedTypeTag.Record None, 
          (Single, Record(None, [ { Name="a"; Optional=false; Type=Primitive(typeof<int>, None) } ])) ]
      |> Map.ofSeq |> Collection
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Merges types in a collection of collections``() =
    let source = JsonValue.Parse """[ [{"a":true},{"b":1}], [{"b":1.1}] ]"""
    let expected = 
      Record(None, [ {Name = "a"; Optional = true; Type = Primitive(typeof<bool>, None) }
                     {Name = "b"; Optional = true; Type = Primitive(typeof<decimal>, None) } ])
      |> SimpleCollection |> SimpleCollection
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)
      
  [<Test>]
  let ``Unions properties of records in a collection``() =
    let source = JsonValue.Parse """[ {"a":1, "b":""}, {"a":1.2, "c":true} ]"""
    let expected =
      Record(None, [ {Name = "a"; Optional = false; Type = Primitive(typeof<decimal>, None) }
                     {Name = "b"; Optional = true; Type = Primitive(typeof<string>, None) }
                     {Name = "c"; Optional = true; Type = Primitive(typeof<bool>, None) }])
      |> SimpleCollection
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Null is a valid value of string``() =
    let source = JsonValue.Parse """[ {"a":null}, {"a":"b"} ]"""
    let expected =
      Record(None, [ {Name = "a"; Optional = false; Type = Primitive(typeof<string>, None) } ])
      |> SimpleCollection
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Null is not a valid value of DateTime``() =
    // None of the providers currently need to get a subtype of 'null' and
    // DateTime, so we call 'subtypeInfered' directly to test this property
    let actual = 
      ProviderImplementation.StructureInference.subtypeInfered InferenceOptions.None
        Null (Primitive(typeof<DateTime>, None))
    let expected = 
      [ InferedTypeTag.Null, Null
        InferedTypeTag.DateTime, Primitive(typeof<DateTime>, None) ]
      |> Map.ofSeq |> Heterogeneous
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Null is a valid value of int option``() =
    let source = JsonValue.Parse """[ {"a":null}, {"a":123} ]"""
    let expected =
      Record(None, [ {Name = "a"; Optional = true; Type = Primitive(typeof<int>, None) } ])
      |> SimpleCollection
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Null is a valid value of record``() =
    let source = JsonValue.Parse """[ {"a":null}, {"a":{"b": 1}} ]"""
    let nestedRecord = 
      Record(None, [{ Name = "b"; Optional = false; Type = Primitive(typeof<int>, None) }])
    let expected =
      Record(None, [ {Name = "a"; Optional = true; Type = nestedRecord } ])
      |> SimpleCollection
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Infers mixed fields of a record as heterogeneous type``() =
    let source = JsonValue.Parse """[ {"a":"hi"}, {"a":2} , {"a":2147483648} ]"""
    let cases = 
      Map.ofSeq [ InferedTypeTag.String, Primitive(typeof<string>, None) 
                  InferedTypeTag.Number, Primitive(typeof<int64>, None) ]
    let expected = 
      Record(None, [ { Name = "a"; Optional = false; Type = Heterogeneous cases }])
      |> SimpleCollection
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Infers mixed fields of a record with nulls as optional``() =
    let source = JsonValue.Parse """[ {"a":null}, {"a":2} , {"a":3} ]"""
    let cases = 
      Map.ofSeq [ InferedTypeTag.Null, Null
                  InferedTypeTag.Number, Primitive(typeof<int>, None) ]
    let expected = 
      Record(None, [ { Name = "a"; Optional = true; Type = Primitive(typeof<int>, None) }])
      |> SimpleCollection
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Inference of multiple nulls works``() = 
    let source = JsonValue.Parse """[0, [{"a": null}, {"a":null}]]"""
    let prop = { Name = "a"; Optional = false; Type = Null }
    let expected = 
      [ InferedTypeTag.Collection, (Single, SimpleCollection(Record(None, [prop])))
        InferedTypeTag.Number, (Single, Primitive(typeof<int>, None)) ]
      |> Map.ofSeq |> Collection
    let actual = JsonInference.inferType InferenceOptions.None source
    Assert.AreEqual(expected, actual)

  let culture = Operations.GetCulture ""

  [<Test>]
  let ``Inference of DateTime``() = 
      let source = new CsvFile(new StringReader("date,int,float\n2012-12-19,2,3.0\n2012-12-12,4,5.0\n2012-12-1,6,10.0"))
      let actual = CsvInference.inferType source Int32.MaxValue culture
      let propDate = { Name = "date"; Optional = false; Type = Primitive(typeof<DateTime>, None) }
      let propInt = { Name = "int"; Optional = false; Type = Primitive(typeof<int>, None) }
      let propFloat = { Name = "float"; Optional = false; Type = Primitive(typeof<Decimal>, None) }
      let expected = Record(None, [ propDate ; propInt ; propFloat ])
      Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Inference of DateTime with timestamp``() = 
      let source = new CsvFile(new StringReader("date,timestamp\n2012-12-19,2012-12-19 12:00\n2012-12-12,2012-12-12 00:00\n2012-12-1,2012-12-1 07:00"))
      let actual = CsvInference.inferType source Int32.MaxValue culture
      let propDate = { Name = "date"; Optional = false; Type = Primitive(typeof<DateTime>, None) }
      let propTimestamp = { Name = "timestamp"; Optional = false; Type = Primitive(typeof<DateTime>, None) }
      let expected = Record(None, [ propDate ; propTimestamp ])
      Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Inference of DateTime with timestamp non default separator``() = 
      let source = new CsvFile(new StringReader("date;timestamp\n2012-12-19;2012-12-19 12:00\n2012-12-12;2012-12-12 00:00\n2012-12-1;2012-12-1 07:00"), ";")
      let actual = CsvInference.inferType source Int32.MaxValue culture
      let propDate = { Name = "date"; Optional = false; Type = Primitive(typeof<DateTime>, None) }
      let propTimestamp = { Name = "timestamp"; Optional = false; Type = Primitive(typeof<DateTime>, None) }
      let expected = Record(None, [ propDate ; propTimestamp ])
      Assert.AreEqual(expected, actual)
  
  [<Test>]
  let ``Inference of float with #N/A values and non default separator``() = 
      let source = new CsvFile(new StringReader("float;integer\n2.0;2\n#N/A;3\n"), ";")
      let actual = CsvInference.inferType source Int32.MaxValue culture
      let propFloat = { Name = "float"; Optional = false; Type = Primitive(typeof<float>, None) }
      let propInteger = { Name = "integer"; Optional = false; Type = Primitive(typeof<int>, None) }
      let expected = Record(None, [ propFloat ; propInteger ])
      Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Inference of numbers with empty values``() = 
      let source = new CsvFile(new StringReader("float1,float2,float3,float4,int,float5,float6,date\n1,1,1,1,,,,\n2.0,#N/A,,1,1,1,,2010-01-10\n,,2.0,#N/A,1,#N/A,2.0,"))
      let actual = CsvInference.inferType source Int32.MaxValue culture
      let propFloat1 = { Name = "float1"; Optional = false; Type = Primitive(typeof<float>, None) }
      let propFloat2 = { Name = "float2"; Optional = false; Type = Primitive(typeof<float>, None) }
      let propFloat3 = { Name = "float3"; Optional = false; Type = Primitive(typeof<float>, None) }
      let propFloat4 = { Name = "float4"; Optional = false; Type = Primitive(typeof<float>, None) }
      let propInt = { Name = "int"; Optional = true; Type = Primitive(typeof<int>, None) }
      let propFloat5 = { Name = "float5"; Optional = false; Type = Primitive(typeof<float>, None) }
      let propFloat6 = { Name = "float6"; Optional = false; Type = Primitive(typeof<float>, None) }
      let date = { Name = "date"; Optional = true; Type = Primitive(typeof<DateTime>, None) }
      let expected = Record(None, [ propFloat1; propFloat2; propFloat3; propFloat4; propInt; propFloat5; propFloat6; date ])
      Assert.AreEqual(expected, actual)
