﻿// --------------------------------------------------------------------------------------
// Tests for a utility that generates nice PascalCase and camelCase names for members
// --------------------------------------------------------------------------------------

module FSharp.Data.Tests.NameUtils.Tests

open FsUnit
open NUnit.Framework
open FSharp.Data.NameUtils

[<Test>]
let ``Formats empty string as PascalCase`` () = 
  nicePascalName "" |> should equal ""

[<Test>]
let ``Formats empty string as camelCase`` () = 
  niceCamelName "" |> should equal ""

[<Test>]
let ``Removes non-character symbols`` () = 
  nicePascalName "__hello__" |> should equal "Hello"
  niceCamelName "__hello__"  |> should equal "hello"

[<Test>]
let ``Makes first letter uppercase`` () = 
  nicePascalName "abc" |> should equal "Abc"
  niceCamelName "abc"  |> should equal "abc"

[<Test>]
let ``Detects word after underscore`` () = 
  nicePascalName "hello_world" |> should equal "HelloWorld"
  niceCamelName "hello_world"  |> should equal "helloWorld"

[<Test>]
let ``Detects word after case change`` () = 
  nicePascalName "helloWorld" |> should equal "HelloWorld"
  niceCamelName "helloWorld"  |> should equal "helloWorld"

[<Test>]
let ``No new word after numbers`` () = 
  nicePascalName "hello123world" |> should equal "Hello123world"
  niceCamelName "hello123world"  |> should equal "hello123world"

[<Test>]
let ``Removes exclamation mark`` () = 
  nicePascalName "hello!123" |> should equal "Hello123" 
  niceCamelName "hello!123"  |> should equal "hello123"

[<Test>]
let ``Handles long and ugly names`` () = 
  nicePascalName "HelloWorld123_hello__@__omg" |> should equal "HelloWorld123HelloOmg"
  niceCamelName "HelloWorld123_hello__@__omg"  |> should equal "helloWorld123HelloOmg"

[<Test>]
let ``Unique generator generates unique names`` () = 
  let gen = uniqueGenerator nicePascalName
  let names = [ for i in 0 .. 100 -> gen "test" ]
  Seq.length names  |> should equal (Seq.length (set names))

[<Test>]
let ``Trims HTML tags from string`` () = 
  trimHtml "<b>hello</b><em>world</em>" |> should equal "hello world"

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
