// Property-based tests for NameUtils using FsCheck.
// Verifies structural invariants: character set, case constraints, uniqueness, and consistency.
module FSharp.Data.Tests.NameUtilsProperties

open NUnit.Framework
open System
open FSharp.Data.Runtime.NameUtils
open FsCheck

// -----------------------------------------------------------------------
// Generators
// -----------------------------------------------------------------------

/// Arbitrary non-null strings (FsCheck default can produce null on .NET).
let nonNullStringArb =
    Arb.fromGen (Arb.generate<string> |> Gen.map (fun s -> if s = null then "" else s))

/// Strings with length >= 2 (single-char strings are handled specially by nicePascalName).
let nonNullLongStringArb =
    Arb.fromGen (
        Arb.generate<string>
        |> Gen.map (fun s -> if s = null then "" else s)
        |> Gen.filter (fun s -> s.Length >= 2)
    )

// -----------------------------------------------------------------------
// nicePascalName properties
// -----------------------------------------------------------------------

/// For strings of length >= 2, nicePascalName filters to only alphanumeric segments.
/// (Single-char strings are returned as-is via ToUpperInvariant, which may be non-alphanumeric.)
[<Test>]
let ``nicePascalName output contains only alphanumeric characters for multi-char inputs`` () =
    let prop (s: string) =
        let result = nicePascalName s
        result |> Seq.forall Char.IsLetterOrDigit

    Check.One(
        { Config.QuickThrowOnFailure with MaxTest = 2000 },
        Prop.forAll nonNullLongStringArb prop
    )

/// If the nicePascalName output is non-empty and derived from a multi-char input,
/// the first character is always an uppercase letter or a digit.
[<Test>]
let ``nicePascalName non-empty output starts with uppercase letter or digit for multi-char inputs`` () =
    let prop (s: string) =
        let result = nicePascalName s
        result.Length = 0 || Char.IsUpper result.[0] || Char.IsDigit result.[0]

    Check.One(
        { Config.QuickThrowOnFailure with MaxTest = 2000 },
        Prop.forAll nonNullLongStringArb prop
    )

/// A single PascalCase word (first letter uppercase, rest lowercase, all letters) is a fixed point.
[<Test>]
let ``nicePascalName on a single PascalCase word is identity`` () =
    let pascalWordGen =
        gen {
            let! len = Gen.choose (2, 12)
            let! first = Gen.elements [ 'A' .. 'Z' ]
            let! rest = Gen.arrayOfLength (len - 1) (Gen.elements [ 'a' .. 'z' ])
            return String(Array.append [| first |] rest)
        }

    let prop (s: string) = nicePascalName s = s

    Check.One(
        { Config.QuickThrowOnFailure with MaxTest = 1000 },
        Prop.forAll (Arb.fromGen pascalWordGen) prop
    )

// -----------------------------------------------------------------------
// niceCamelName properties
// -----------------------------------------------------------------------

/// niceCamelName is always defined as lowercase-first(nicePascalName), regardless of input.
[<Test>]
let ``niceCamelName result equals nicePascalName with lowercased first char`` () =
    let prop (s: string) =
        let s = if s = null then "" else s
        let camel = niceCamelName s
        let pascal = nicePascalName s

        if pascal.Length = 0 then
            camel = ""
        else
            camel = pascal.[0].ToString().ToLowerInvariant() + pascal.Substring(1)

    Check.One({ Config.QuickThrowOnFailure with MaxTest = 2000 }, Prop.forAll nonNullStringArb prop)

/// For multi-char strings, niceCamelName produces only alphanumeric characters.
[<Test>]
let ``niceCamelName output contains only alphanumeric characters for multi-char inputs`` () =
    let prop (s: string) =
        niceCamelName s |> Seq.forall Char.IsLetterOrDigit

    Check.One(
        { Config.QuickThrowOnFailure with MaxTest = 2000 },
        Prop.forAll nonNullLongStringArb prop
    )

/// For multi-char inputs, a non-empty niceCamelName output starts with a lowercase letter or digit.
[<Test>]
let ``niceCamelName non-empty output starts with lowercase letter or digit for multi-char inputs`` () =
    let prop (s: string) =
        let result = niceCamelName s
        result.Length = 0 || Char.IsLower result.[0] || Char.IsDigit result.[0]

    Check.One(
        { Config.QuickThrowOnFailure with MaxTest = 2000 },
        Prop.forAll nonNullLongStringArb prop
    )

// -----------------------------------------------------------------------
// capitalizeFirstLetter properties
// -----------------------------------------------------------------------

/// capitalizeFirstLetter is idempotent: applying it twice equals applying it once.
[<Test>]
let ``capitalizeFirstLetter is idempotent`` () =
    let prop (s: string) =
        let s = if s = null then "" else s
        capitalizeFirstLetter (capitalizeFirstLetter s) = capitalizeFirstLetter s

    Check.One({ Config.QuickThrowOnFailure with MaxTest = 2000 }, Prop.forAll nonNullStringArb prop)

/// capitalizeFirstLetter on a letter-first string always produces an uppercase first char.
[<Test>]
let ``capitalizeFirstLetter non-empty output starts with an uppercase letter`` () =
    let letterFirstGen =
        gen {
            let! first = Gen.elements ([ 'a' .. 'z' ] @ [ 'A' .. 'Z' ])
            let! rest = Arb.generate<string> |> Gen.map (fun s -> if s = null then "" else s)
            return string first + rest
        }

    let prop (s: string) =
        let result = capitalizeFirstLetter s
        Char.IsUpper result.[0]

    Check.One(
        { Config.QuickThrowOnFailure with MaxTest = 1000 },
        Prop.forAll (Arb.fromGen letterFirstGen) prop
    )

// -----------------------------------------------------------------------
// uniqueGenerator properties
// -----------------------------------------------------------------------

/// Repeatedly calling the generator with the same input never produces the same name twice.
[<Test>]
let ``uniqueGenerator never returns duplicates for repeated same-input calls`` () =
    let prop (count: int) =
        let n = (abs count % 50) + 2 // 2..51 calls
        let gen = uniqueGenerator nicePascalName
        let results = [ for _ in 1..n -> gen "name" ]
        results |> List.length = (results |> Set.ofList |> Set.count)

    Check.One({ Config.QuickThrowOnFailure with MaxTest = 200 }, prop)

/// The generator produces unique names across a mix of different inputs.
[<Test>]
let ``uniqueGenerator never returns duplicates across many different inputs`` () =
    let inputGen =
        Gen.listOfLength 50 (Arb.generate<string> |> Gen.map (fun s -> if s = null then "" else s))

    let prop (inputs: string list) =
        let gen = uniqueGenerator nicePascalName
        let results = inputs |> List.map gen
        results.Length = (results |> Set.ofList |> Set.count)

    Check.One(
        { Config.QuickThrowOnFailure with MaxTest = 200 },
        Prop.forAll (Arb.fromGen inputGen) prop
    )

/// The very first call to a fresh generator for any input returns nicePascalName of that input
/// (or "Unnamed" when the name is empty).
[<Test>]
let ``uniqueGenerator first result for a fresh input equals nicePascalName of that input`` () =
    let prop (s: string) =
        let s = if s = null then "" else s
        let expected = nicePascalName s
        let finalExpected = if expected = "" then "Unnamed" else expected
        let gen = uniqueGenerator nicePascalName
        gen s = finalExpected

    Check.One({ Config.QuickThrowOnFailure with MaxTest = 1000 }, Prop.forAll nonNullStringArb prop)

// -----------------------------------------------------------------------
// trimHtml properties
// -----------------------------------------------------------------------

/// trimHtml is idempotent: stripping tags from already-stripped text is a no-op.
[<Test>]
let ``trimHtml is idempotent`` () =
    let prop (s: string) =
        let s = if s = null then "" else s
        trimHtml (trimHtml s) = trimHtml s

    Check.One({ Config.QuickThrowOnFailure with MaxTest = 2000 }, Prop.forAll nonNullStringArb prop)

/// On text with no angle brackets, trimHtml returns TrimEnd of the original.
[<Test>]
let ``trimHtml on plain text (no angle brackets) returns TrimEnd of original`` () =
    let noAngleBracketsGen =
        Arb.generate<string>
        |> Gen.map (fun s -> if s = null then "" else s)
        |> Gen.filter (fun s -> not (s.Contains('<')) && not (s.Contains('>')))

    let prop (s: string) = trimHtml s = s.TrimEnd()

    Check.One(
        { Config.QuickThrowOnFailure with MaxTest = 1000 },
        Prop.forAll (Arb.fromGen noAngleBracketsGen) prop
    )

/// trimHtml never lets a '<' through; tags are always stripped.
/// (Note: stray '>' without a matching '<' may still appear in output — that is by design.)
[<Test>]
let ``trimHtml output never contains opening angle brackets`` () =
    let prop (s: string) =
        let s = if s = null then "" else s
        not (trimHtml s |> Seq.contains '<')

    Check.One({ Config.QuickThrowOnFailure with MaxTest = 2000 }, Prop.forAll nonNullStringArb prop)

