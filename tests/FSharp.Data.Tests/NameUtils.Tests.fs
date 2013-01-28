// --------------------------------------------------------------------------------------
// Tests for a utility that generates nice PascalCase and camelCase names for members
// --------------------------------------------------------------------------------------

module FSharp.Data.Tests.NameUtils.Tests

open FsUnit
open NUnit.Framework
open FSharp.Data.NameUtils

[<Test>]
let ``Formats empty string as PascalCase`` () = 
  Assert.AreEqual("", nicePascalName "")

[<Test>]
let ``Formats empty string as camelCase`` () = 
  Assert.AreEqual("", niceCamelName "")

[<Test>]
let ``Removes non-character symbols`` () = 
  Assert.AreEqual("Hello", nicePascalName "__hello__")
  Assert.AreEqual("hello", niceCamelName "__hello__")

[<Test>]
let ``Makes first letter uppercase`` () = 
  Assert.AreEqual("Abc", nicePascalName "abc")
  Assert.AreEqual("abc", niceCamelName "abc")

[<Test>]
let ``Detects word after underscore`` () = 
  Assert.AreEqual("HelloWorld", nicePascalName "hello_world")
  Assert.AreEqual("helloWorld", niceCamelName "hello_world")

[<Test>]
let ``Detects word after case change`` () = 
  Assert.AreEqual("HelloWorld", nicePascalName "helloWorld")
  Assert.AreEqual("helloWorld", niceCamelName "helloWorld")

[<Test>]
let ``No new word after numbers`` () = 
  Assert.AreEqual("Hello123world", nicePascalName "hello123world")
  Assert.AreEqual("hello123world", niceCamelName "hello123world")

[<Test>]
let ``Removes exclamation mark`` () = 
  Assert.AreEqual("Hello123", nicePascalName "hello!123") 
  Assert.AreEqual("hello123", niceCamelName "hello!123") 

[<Test>]
let ``Handles long and ugly names`` () = 
  Assert.AreEqual("HelloWorld123HelloOmg", nicePascalName "HelloWorld123_hello__@__omg")
  Assert.AreEqual("helloWorld123HelloOmg", niceCamelName "HelloWorld123_hello__@__omg")

[<Test>]
let ``Unique generator generates unique names`` () = 
  let gen = uniqueGenerator nicePascalName
  let names = [ for i in 0 .. 100 -> gen "test" ]
  Assert.AreEqual(Seq.length (set names), Seq.length names)

[<Test>]
let ``Trims HTML tags from string`` () = 
  Assert.AreEqual("hello world", trimHtml "<b>hello</b><em>world</em>")

[<Test>]
let ``Can pluralize names``() =
   let check a b = pluralize a |> should equal b
   check "Author" "Authors"
   check "Authors" "Authors"
   check "Item" "Items"
   check "Items" "Items"
   check "Entity" "Entities"
   check "goose" "geese"
   check "deer" "deer"
   check "sheep" "sheep"
   check "wolf" "wolves"
   check "volcano" "volcanoes"
   check "aircraft" "aircraft"
   check "alumna" "alumnae"
   check "alumnus" "alumni"
   check "house" "houses"
   check "fungus" "fungi"
   check "woman" "women"
   check "index" "indices"

[<Test>]
let ``Can singularize names``() =
   let check a b = singularize a |> should equal b
   check "Author" "Author"
   check "Authors" "Author"
   check "Item" "Item"
   check "Items" "Item"
   check "Entities" "Entity"
   check "geese" "goose" 
   check "deer" "deer"
   check "sheep" "sheep"
   check "wolves" "wolf"
   check "volcanoes" "volcano"
   check "aircraft" "aircraft"
   check "alumnae" "alumna"
   check "alumni" "alumnus"
   check "houses" "house"
   check "fungi" "fungus"
   check "funguses" "fungus"
   check "women" "woman"
   check "indices" "index"
   check "indexes" "index"
