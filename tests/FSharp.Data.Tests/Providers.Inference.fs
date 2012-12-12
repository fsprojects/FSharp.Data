// --------------------------------------------------------------------------------------
// Tests for a utility that generates nice PascalCase and camelCase names for members
// --------------------------------------------------------------------------------------
namespace FSharp.Data.Tests

#if INTERACTIVE
#r "../../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#r "../../bin/FSharp.Data.dll"
#endif

open FSharp.Web
open NUnit.Framework
open ProviderImplementation
open ProviderImplementation.StructureInference

module ProviderInfernece = 

  /// A collection containing just one type
  let SimpleCollection typ = 
    Collection(Map.ofSeq [typeTag typ, (InferedMultiplicity.Multiple, typ)])

  [<Test>]
  let ``Seq.pairBy helper function works``() = 
    let actual = Seq.pairBy fst [(1, "a"); (2, "b")] [(1, "A"); (3, "C")]
    let expected = 
      [ (1, Some (1, "a"), Some (1, "A"))
        (2, Some (2, "b"), None)
        (3, None, Some (3, "C")) ]
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Finds common subtype of numeric types (decimal)``() =
    let source = JsonValue.Parse """[ 10, 10.23 ]"""
    let expected = SimpleCollection(Primitive typeof<decimal>)
    let actual = JsonInference.inferType source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Finds common subtype of numeric types (int64)``() =
    let source = JsonValue.Parse """[ 10, 2147483648 ]"""
    let expected = SimpleCollection(Primitive typeof<int64>)
    let actual = JsonInference.inferType source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Infers heterogeneous type of primitives``() =
    let source = JsonValue.Parse """[ 1,true ]"""
    let expected = 
      [ InferedTypeTag.Number, (Single, Primitive typeof<int>)
        InferedTypeTag.Boolean, (Single, Primitive typeof<bool>) ]
      |> Map.ofSeq |> Collection
    let actual = JsonInference.inferType source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Infers heterogeneous type of primitives and nulls``() =
    let source = JsonValue.Parse """[ 1,true,null ]"""
    let expected = 
      [ InferedTypeTag.Null, (Single, Null)
        InferedTypeTag.Number, (Single, Primitive typeof<int>)
        InferedTypeTag.Boolean, (Single, Primitive typeof<bool>) ]
      |> Map.ofSeq |> Collection
    let actual = JsonInference.inferType source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Finds common subtype of numeric types (float)``() =
    let source = JsonValue.Parse """[ 10, 10.23, 79228162514264337593543950336 ]"""
    let expected = SimpleCollection(Primitive typeof<float>)
    let actual = JsonInference.inferType source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Infers heterogeneous type of primitives and records``() =
    let source = JsonValue.Parse """[ {"a":0}, 1,2 ]"""
    let expected = 
      [ InferedTypeTag.Number, (Multiple, Primitive typeof<int>)
        InferedTypeTag.Record None, 
          (Single, Record(None, [ { Name="a"; Optional=false; Type=Primitive typeof<int> } ])) ]
      |> Map.ofSeq |> Collection
    let actual = JsonInference.inferType source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Merges types in a collection of collections``() =
    let source = JsonValue.Parse """[ [{"a":true},{"b":1}], [{"b":1.1}] ]"""
    let expected = 
      Record(None, [ {Name = "a"; Optional = true; Type = Primitive typeof<bool> }
                     {Name = "b"; Optional = true; Type = Primitive typeof<decimal> } ])
      |> SimpleCollection |> SimpleCollection
    let actual = JsonInference.inferType source
    Assert.AreEqual(expected, actual)
      
  [<Test>]
  let ``Unions properties of records in a collection``() =
    let source = JsonValue.Parse """[ {"a":1, "b":""}, {"a":1.2, "c":true} ]"""
    let expected =
      Record(None, [ {Name = "a"; Optional = false; Type = Primitive typeof<decimal> }
                     {Name = "b"; Optional = true; Type = Primitive typeof<string> }
                     {Name = "c"; Optional = true; Type = Primitive typeof<bool> }])
      |> SimpleCollection
    let actual = JsonInference.inferType source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Null is a valid value of string``() =
    let source = JsonValue.Parse """[ {"a":null}, {"a":"b"} ]"""
    let expected =
      Record(None, [ {Name = "a"; Optional = false; Type = Primitive typeof<string> } ])
      |> SimpleCollection
    let actual = JsonInference.inferType source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Infers mixed fields of a record as heterogeneous type``() =
    let source = JsonValue.Parse """[ {"a":"hi"}, {"a":2} , {"a":2147483648} ]"""
    let cases = 
      Map.ofSeq [ InferedTypeTag.String, Primitive typeof<string> 
                  InferedTypeTag.Number, Primitive typeof<int64> ]
    let expected = 
      Record(None, [ { Name = "a"; Optional = false; Type = Heterogeneous cases }])
      |> SimpleCollection
    let actual = JsonInference.inferType source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Infers mixed fields of a record as heterogeneous type with nulls``() =
    let source = JsonValue.Parse """[ {"a":null}, {"a":2} , {"a":3} ]"""
    let cases = 
      Map.ofSeq [ InferedTypeTag.Null, Null
                  InferedTypeTag.Number, Primitive typeof<int> ]
    let expected = 
      Record(None, [ { Name = "a"; Optional = false; Type = Heterogeneous cases }])
      |> SimpleCollection
    let actual = JsonInference.inferType source
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Inference of multiple nulls works``() = 
    let source = JsonValue.Parse """[0, [{"a": null}, {"a":null}]]"""
    let prop = { Name = "a"; Optional = false; Type = Null }
    let expected = 
      [ InferedTypeTag.Collection, (Single, SimpleCollection(Record(None, [prop])))
        InferedTypeTag.Number, (Single, Primitive typeof<int>) ]
      |> Map.ofSeq |> Collection
    let actual = JsonInference.inferType source
    Assert.AreEqual(expected, actual)
