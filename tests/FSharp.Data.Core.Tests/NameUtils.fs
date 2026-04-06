// --------------------------------------------------------------------------------------
// Tests for a utility that generates nice PascalCase and camelCase names for members
// --------------------------------------------------------------------------------------

module FSharp.Data.Tests.NameUtils

open FsUnit
open NUnit.Framework
open FSharp.Data.Runtime.NameUtils

[<Test>]
let ``Formats empty string as PascalCase`` () =
  nicePascalName "" |> should equal ""

[<Test>]
let ``Formats empty string as camelCase`` () =
  niceCamelName "" |> should equal ""

[<Test>]
let ``Formats one letter string as PascalCase`` () =
  nicePascalName "b" |> should equal "B"

[<Test>]
let ``Formats one letter string as camelCase`` () =
  niceCamelName "a" |> should equal "a"

[<Test>]
let ``Removes non-character symbols`` () =
  nicePascalName "__hello__" |> should equal "Hello"
  niceCamelName "__hello__"  |> should equal "hello"

[<Test>]
let ``Makes first letter uppercase`` () =
  nicePascalName "abc" |> should equal "Abc"
  niceCamelName "abc"  |> should equal "abc"

[<Test>]
let ``One letter words at the end of names are not removed`` () =
  nicePascalName "IntOrBooleanOrArrayOrB" |> should equal "IntOrBooleanOrArrayOrB"
  niceCamelName "IntOrBooleanOrArrayOrB" |> should equal "intOrBooleanOrArrayOrB"

[<Test>]
let ``Handles acronyms`` () =
  nicePascalName "ABC" |> should equal "Abc"
  niceCamelName "ABC"  |> should equal "abc"

  nicePascalName "TVSeries" |> should equal "TvSeries"
  niceCamelName "TVSeries"  |> should equal "tvSeries"

  nicePascalName "ABCWord" |> should equal "AbcWord"
  niceCamelName "ABCWord"  |> should equal "abcWord"

  nicePascalName "abcABCWord" |> should equal "AbcAbcWord"
  niceCamelName "abcABCWord"  |> should equal "abcAbcWord"

[<Test>]
let ``Detects word after underscore`` () =
  nicePascalName "hello_world" |> should equal "HelloWorld"
  niceCamelName "hello_world"  |> should equal "helloWorld"

[<Test>]
let ``Works with numbers`` () =
  nicePascalName "A21_SERVICE" |> should equal "A21Service"
  niceCamelName "A21_SERVICE"  |> should equal "a21Service"


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
  let names = [ for _i in 0 .. 100 -> gen "test" ]
  Seq.length names  |> should equal (Seq.length (set names))

[<Test>]
let ``Unique generator works on single letter names`` () =
  let gen = uniqueGenerator nicePascalName
  gen "a" |> should equal "A"
  gen "a" |> should equal "A2"
  gen "a" |> should equal "A3"

[<Test>]
let ``Trims HTML tags from string`` () =
    trimHtml "<b>hello</b><em>world</em>" |> should equal "hello world"

[<Test>]
let ``trimHtml empty string returns empty string`` () =
    trimHtml "" |> should equal ""

[<Test>]
let ``trimHtml plain text with no tags is returned as-is (trailing spaces trimmed)`` () =
    trimHtml "hello world" |> should equal "hello world"
    trimHtml "hello world   " |> should equal "hello world"

[<Test>]
let ``trimHtml tag with attributes strips all tag content including attributes`` () =
    trimHtml """<a href="http://example.com">link text</a>""" |> should equal "link text"

[<Test>]
let ``trimHtml multiple consecutive tags collapse to a single space`` () =
    trimHtml "<b></b><i></i>text" |> should equal "text"
    trimHtml "before<br/><br/>after" |> should equal "before after"

[<Test>]
let ``trimHtml stray closing angle bracket is passed through`` () =
    // A '>' without a matching '<' is not inside a tag, so it appears in output
    trimHtml "a > b" |> should equal "a > b"

[<Test>]
let ``trimHtml content surrounding tags is preserved`` () =
    // Each tag boundary where emitSpace=true inserts one space, including closing tags.
    // "a<b>B</b>c" → space before <b> and space before </b>
    trimHtml "a<b>B</b>c" |> should equal "a B c"
    // Two consecutive tags: only the first triggers a space (second tag sees emitSpace=false)
    trimHtml "before<br/><br/>after" |> should equal "before after"

[<Test>]
let ``capitalizeFirstLetter empty string returns empty string`` () =
    capitalizeFirstLetter "" |> should equal ""

[<Test>]
let ``capitalizeFirstLetter single lowercase letter is uppercased`` () =
    capitalizeFirstLetter "a" |> should equal "A"

[<Test>]
let ``capitalizeFirstLetter single uppercase letter is unchanged`` () =
    capitalizeFirstLetter "A" |> should equal "A"

[<Test>]
let ``capitalizeFirstLetter multi-char lowercase string has first char uppercased`` () =
    capitalizeFirstLetter "hello" |> should equal "Hello"
    capitalizeFirstLetter "world" |> should equal "World"

[<Test>]
let ``capitalizeFirstLetter already-PascalCase string is unchanged`` () =
    capitalizeFirstLetter "Hello" |> should equal "Hello"
    capitalizeFirstLetter "FSharp" |> should equal "FSharp"

[<Test>]
let ``capitalizeFirstLetter preserves non-letter first character`` () =
    // Digits and non-letter characters: the function applies ToUpperInvariant which is a no-op for non-letters
    capitalizeFirstLetter "123abc" |> should equal "123abc"

[<Test>]
let ``uniqueGenerator deduplicates names containing spaces`` () =
    let gen = uniqueGenerator nicePascalName
    // The nicePascalName of "hello world" strips spaces; deduplication appends "2" without space
    let name1 = gen "hello world"
    let name2 = gen "hello world"
    name1 |> should not' (equal name2)
    name2 |> should not' (be null)

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
   check "status" "statuses"
   check "release" "releases"
   check "choice" "choices"
   check "purchase" "purchases"
   check "purchases" "purchases"

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
   check "statuses" "status"
   check "releases" "release"
   check "choices" "choice"
   check "purchase" "purchase"
   check "purchases" "purchase"
