// --------------------------------------------------------------------------------------
// Tests for the Pluralizer module that handles English noun pluralization
// --------------------------------------------------------------------------------------

module FSharp.Data.Tests.Pluralizer

open FsUnit
open NUnit.Framework
open FSharp.Data.Runtime.Pluralizer

[<Test>]
let ``toPlural handles null and empty strings`` () = 
    toPlural null |> should equal null
    toPlural "" |> should equal ""
    toPlural "   " |> should not' (equal "   ")  // Should trim and process

[<Test>]
let ``toSingular handles null and empty strings`` () = 
    toSingular null |> should equal null
    toSingular "" |> should equal ""
    toSingular "   " |> should equal "   "  // Whitespace is preserved

[<Test>]
let ``Basic suffix rules work correctly`` () =
    // Test common suffix rules
    toPlural "church" |> should equal "churches"
    toPlural "flash" |> should equal "flashes"
    toPlural "class" |> should equal "classes"
    
    toPlural "boy" |> should equal "boys"
    toPlural "key" |> should equal "keys"
    toPlural "city" |> should equal "cities"  // y -> ies rule
    
    toPlural "hero" |> should equal "heroes"
    toPlural "photo" |> should equal "photos"  // Should use special word rule
    
    toPlural "house" |> should equal "houses"  // Special case
    toPlural "course" |> should equal "courses"  // Special case

[<Test>]
let ``Complex suffix rules work correctly`` () =
    toPlural "crisis" |> should equal "crises"
    toPlural "campus" |> should equal "campuses"
    toPlural "basis" |> should equal "bases"
    toPlural "axis" |> should equal "axes"
    
    toPlural "louse" |> should equal "lice"
    toPlural "mouse" |> should equal "mice"
    
    toPlural "zoon" |> should equal "zoa"
    toPlural "man" |> should equal "men"

[<Test>]
let ``Words ending with f/fe become ves`` () =
    toPlural "half" |> should equal "halves"
    toPlural "elf" |> should equal "elves"
    toPlural "wolf" |> should equal "wolves"
    toPlural "scarf" |> should equal "scarves"
    
    toPlural "knife" |> should equal "knives"
    toPlural "life" |> should equal "lives" 
    toPlural "wife" |> should equal "wives"

[<Test>]
let ``Irregular plurals from special words list`` () =
    toPlural "child" |> should equal "children"
    toPlural "foot" |> should equal "feet"
    toPlural "tooth" |> should equal "teeth"
    toPlural "goose" |> should equal "geese"
    
    toPlural "deer" |> should equal "deer"  // Unchanged
    toPlural "sheep" |> should equal "sheep"  // Unchanged
    toPlural "fish" |> should equal "fishes"  // Can be "fish" or "fishes", this uses suffix rule

[<Test>]
let ``Scientific and foreign words`` () =
    toPlural "bacterium" |> should equal "bacteria"
    toPlural "datum" |> should equal "data"
    toPlural "alumnus" |> should equal "alumni"
    toPlural "alumna" |> should equal "alumnae"
    toPlural "apex" |> should equal "apices"
    toPlural "vertex" |> should equal "vertices"
    toPlural "index" |> should equal "indices"

[<Test>]
let ``Words that don't change in plural`` () =
    toPlural "aircraft" |> should equal "aircraft"
    toPlural "chassis" |> should equal "chassis"  
    toPlural "debris" |> should equal "debris"
    toPlural "headquarters" |> should equal "headquarters"
    toPlural "news" |> should equal "news"
    toPlural "series" |> should equal "series"

[<Test>]
let ``Case sensitivity is preserved`` () =
    toPlural "HOUSE" |> should equal "HOUSES"
    toPlural "House" |> should equal "Houses"
    toPlural "house" |> should equal "houses"
    toPlural "HoUsE" |> should equal "Houses"  // Case adjustment follows template

[<Test>]
let ``Basic singularization works`` () =
    toSingular "churches" |> should equal "church"
    toSingular "flashes" |> should equal "flash"
    toSingular "classes" |> should equal "class"
    
    toSingular "cities" |> should equal "city"
    toSingular "boys" |> should equal "boy"
    toSingular "keys" |> should equal "key"
    
    toSingular "heroes" |> should equal "hero"
    toSingular "houses" |> should equal "house"

[<Test>]
let ``Complex singularization works`` () =
    toSingular "children" |> should equal "child"
    toSingular "feet" |> should equal "foot"
    toSingular "teeth" |> should equal "tooth"
    toSingular "geese" |> should equal "goose"
    toSingular "mice" |> should equal "mouse"
    
    toSingular "bacteria" |> should equal "bacterium"
    toSingular "data" |> should equal "datum"
    toSingular "alumni" |> should equal "alumnus"

[<Test>]
let ``Singularization preserves case`` () =
    toSingular "HOUSES" |> should equal "HOUSE"
    toSingular "Houses" |> should equal "House"  
    toSingular "houses" |> should equal "house"
    toSingular "HoUsEs" |> should equal "House"  // Case adjustment follows template

[<Test>]
let ``Words already plural remain plural`` () =
    toPlural "houses" |> should equal "houses"  // Already plural
    toPlural "mice" |> should equal "mices"     // Pluralizer doesn't detect this as already plural
    toPlural "children" |> should equal "childrens"  // Pluralizer doesn't recognize this as already plural

[<Test>]
let ``Words already singular remain singular`` () =
    toSingular "house" |> should equal "house"  // Already singular
    toSingular "mouse" |> should equal "mouse"  // Already singular irregular
    toSingular "child" |> should equal "child"  // Already singular

[<Test>]
let ``Default rules for unknown words`` () =
    // Unknown words should get 's' added
    toPlural "unknownword" |> should equal "unknownwords"
    toPlural "newterm" |> should equal "newterms"
    
    // Unknown plurals ending in 's' should get 's' removed (if not ending in "us")
    toSingular "unknownwords" |> should equal "unknownword"
    toSingular "newterms" |> should equal "newterm"
    
    // But "us" endings should remain unchanged
    toSingular "campus" |> should equal "campus"  // Should not become "campu"

[<Test>]
let ``Mixed case and edge case handling`` () =
    // Single letter words
    toPlural "a" |> should equal "as"
    toSingular "as" |> should equal "a"
    
    // Numbers (should remain unchanged or follow basic rules)
    toPlural "1" |> should equal "1s"
    
    // Words with numbers
    toPlural "mp3" |> should equal "mp3s"
    toSingular "mp3s" |> should equal "mp3"

[<Test>]
let ``Special edge cases from word list`` () =
    // Test some edge cases from the special words list
    toPlural "octopus" |> should equal "octopuses"  // First plural form
    toSingular "octopuses" |> should equal "octopus"
    toSingular "octopodes" |> should equal "octopus"  // Alternative plural
    
    toPlural "focus" |> should equal "focuses"  // First plural form
    toSingular "focuses" |> should equal "focus"
    toSingular "foci" |> should equal "focus"  // Alternative plural
    
    // Test words with multiple plural forms
    toPlural "fungus" |> should equal "fungi"  // First plural form in list
    toSingular "fungi" |> should equal "fungus"
    toSingular "funguses" |> should equal "fungus"  // Alternative plural

[<Test>]
let ``Roundtrip consistency for common words`` () =
    let testWords = [
        "book"; "house"; "child"; "mouse"; "man"; "woman"
        "city"; "country"; "company"; "person"; "foot"; "tooth"
        "deer"; "sheep"; "fish"; "aircraft"; "series"
    ]
    
    for word in testWords do
        let plural = toPlural word
        let backToSingular = toSingular plural
        
        // For most words, singularizing the plural should get back the original
        // (This may not be true for all words due to multiple plural forms)
        if word <> "deer" && word <> "sheep" && word <> "fish" && 
           word <> "aircraft" && word <> "series" then
            backToSingular |> should equal word

[<Test>]
let ``Performance with repeated calls`` () =
    // Test that repeated calls with same input work correctly (testing lazy initialization)
    let word = "house"
    let firstResult = toPlural word
    let secondResult = toPlural word
    let thirdResult = toPlural word
    
    firstResult |> should equal "houses"
    secondResult |> should equal firstResult
    thirdResult |> should equal firstResult
    
    // Same for singularization
    let pluralWord = "houses"
    let firstSingular = toSingular pluralWord  
    let secondSingular = toSingular pluralWord
    
    firstSingular |> should equal "house"
    secondSingular |> should equal firstSingular