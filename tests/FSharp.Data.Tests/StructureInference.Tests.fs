// --------------------------------------------------------------------------------------
// Tests for a utility that generates nice PascalCase and camelCase names for members
// --------------------------------------------------------------------------------------
namespace FSharp.Data.Tests

#if INTERACTIVE
#r "../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#r "../bin/FSharp.Data.dll"
#endif

open NUnit.Framework
open FSharp.Data.StructureInference

module StructureInference = 

  /// A collection containing just one type
  let SimpleCollection typ = 
    Collection(Map.ofSeq [typeTag typ, (InferedMultiplicity.Multiple, typ)])

  [<Test>]
  let ``pairBy helper function works``() = 
    let actual = pairBy fst [(2, "a"); (1, "b")] [(1, "A"); (3, "C")]
    let expected = 
      [ (1, Some (1, "b"), Some (1, "A"))
        (2, Some (2, "a"), None)
        (3, None, Some (3, "C")) ]
    Assert.AreEqual(set expected, set actual)

  [<Test>]
  let ``pairBy helper function preserves order``() = 
    let actual = pairBy fst [("one", "a"); ("two", "b")] [("one", "A"); ("two", "B")]
    let expected = 
      [ ("one", Some ("one", "a"), Some ("one", "A"))
        ("two", Some ("two", "b"), Some ("two", "B")) ] 
    Assert.AreEqual(expected, actual)
