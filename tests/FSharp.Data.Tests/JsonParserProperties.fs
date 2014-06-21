#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#r "../../packages/FsCheck.0.9.2.0/lib/net40-Client/FsCheck.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.JsonParserProperties
#endif

open NUnit.Framework
open System
open FSharp.Data
open FsUnit
open FsCheck

#if INTERACTIVE
Runner.init.Force()
#endif

let escaped = Set(['t';'r';'b';'n';'f';'\\';'/';'"'])

let rec isValidJsonString s = 
    match s with
    | [] -> true
    | ['\\'] -> false
    | '"' :: t -> false
    | h :: t when Globalization.CharUnicodeInfo.GetUnicodeCategory h 
                    = Globalization.UnicodeCategory.Control -> false
    | '\\' :: 'u' :: d1 :: d2 :: d3 :: d4 :: t 
        when [d1;d2;d3;d4] |> Seq.forall 
            (fun c -> (Char.IsDigit c || Text.RegularExpressions.Regex("[a-fA-F]").IsMatch(c.ToString())))
                -> isValidJsonString t
    | '\\' :: i :: t when escaped |> (not << Set.contains i) -> false
    | '\\' :: i :: t when escaped |> Set.contains i -> isValidJsonString t
    | h :: t -> isValidJsonString t 

let validJsonStringGen = 
    Arb.generate<string> 
    |> Gen.suchThat ((<>) null)
    |> Gen.suchThat (Seq.toList >> isValidJsonString)

let jsonValueGen : Gen<JsonValue> =  

    let stringValGen = Gen.map JsonValue.String validJsonStringGen
    let booleanValGen = Gen.map JsonValue.Boolean Arb.generate<bool>
    let nullValGen = Gen.constant JsonValue.Null
    let numberValGen = Gen.map JsonValue.Number Arb.generate<decimal>

    let recordGen record =
        record
        ||> Gen.map2 (fun x y -> (x,y)) 
        |> Gen.listOf
        |> Gen.map List.toArray
        |> Gen.map JsonValue.Record
    
    let rec tree() =
        let tree' s =
            match s with
            | 0 -> Gen.oneof [ booleanValGen; stringValGen; nullValGen; numberValGen ]
            | n when n>0 -> 
                let subtree = 
                    (validJsonStringGen, tree()) 
                    |> recordGen
                    |> Gen.resize (s|>float|>sqrt|>int) 
                let arrayGen =
                    tree()
                    |> Gen.listOf
                    |> Gen.map List.toArray
                    |> Gen.map JsonValue.Array
                    |> Gen.resize (s|>float|>sqrt|>int) 
                Gen.oneof [ subtree; arrayGen; booleanValGen; stringValGen; nullValGen; numberValGen ]
            | _ -> invalidArg "s" "Only positive arguments are allowed"
        Gen.sized tree'

    (validJsonStringGen, tree())
    |> recordGen

let canonicalizeNumString (s :string) =
    let hasOnlyZeros = 
        Seq.filter Char.IsDigit >> Seq.forall ((=) '0')
    if (s |> hasOnlyZeros)
    then "0"
    else s

let numberStringGen : Gen<string> =
    let concat gs =
        gs
        |> Gen.sequence
        |> Gen.map (String.concat String.Empty)
    
    let emptyGen = Gen.constant String.Empty
    let signGen = Gen.frequency [20, Gen.constant "-"; 80, emptyGen]

    let decimalPrec = 28

    let digGen listGenFun =
        Gen.choose(0,9)
        |> Gen.map Convert.ToString
        |> listGenFun
        |> Gen.resize (decimalPrec/2)
        |> Gen.map (String.concat String.Empty)

    let digitsGen = digGen Gen.listOf
    let nonEmptyDigitsGen = digGen Gen.nonEmptyListOf

    let zeroGen = Gen.constant "0"
    let nonZeroDigitGen = Gen.choose(1,9) |> Gen.map Convert.ToString
    
    let integerPartGen = Gen.frequency 
                          [40, zeroGen; 
                           60, concat [nonZeroDigitGen; digitsGen]]

    let fractionPartGen = Gen.oneof
                            [emptyGen;
                             concat [Gen.constant "."; nonEmptyDigitsGen]]

    [signGen; integerPartGen; fractionPartGen]
    |> concat
    |> Gen.map canonicalizeNumString

let jsonStringGen : Gen<string> =
 
    let validJsonStringGen' = 
        validJsonStringGen
        |> Gen.map (sprintf "\"%s\"")
    
    let boolGen = Gen.elements ["true"; "false"]
    let nullGen = Gen.constant "null"

    let recordGen record =
        record
        ||> Gen.map2 (sprintf "{%s:%s}") 

    let rec tree() =
        let tree' s  =
            match s with
            | 0 -> Gen.oneof [validJsonStringGen'; boolGen; nullGen; numberStringGen]
            | n when n>0 ->
                let subtree =
                    (validJsonStringGen', tree())
                    |> recordGen
                    |> Gen.resize (s|>float|>sqrt|>int)
                let arrayGen =
                    tree()
                    |> Gen.listOf
                    |> Gen.map (fun l -> sprintf "[%s]" (String.Join(",",l)))
                    |> Gen.resize (s|>float|>sqrt|>int) 
                Gen.oneof [ subtree;  arrayGen; validJsonStringGen'; boolGen; nullGen; numberStringGen]
            | _ -> invalidArg "s" "Only positive arguments are allowed"
        Gen.sized tree'

    (validJsonStringGen', tree())
    |> recordGen


type Generators = 
    static member JsonValue() =
         {new Arbitrary<JsonValue>() with
            override x.Generator = jsonValueGen
            override x.Shrinker j = 
                match j with
                | JsonValue.Array elems -> elems :> seq<JsonValue>
                | JsonValue.Record [|prop|] -> Seq.singleton (prop |> snd)
                | JsonValue.Record props -> seq {for n in props -> JsonValue.Record([|n|])}
                | _ -> Seq.empty }
     

[<TestFixtureSetUp>]
let fixtureSetup() =
    Arb.register<Generators>() |> ignore

#if INTERACTIVE
fixtureSetup()
#endif

[<Test>]
let  ``Parsing stringified JsonValue returns the same JsonValue`` () =
    let parseStringified (json: JsonValue) = 
        json.ToString(JsonSaveOptions.DisableFormatting)
        |> JsonValue.Parse = json
    Check.QuickThrowOnFailure parseStringified

[<Test>]
let ``Stringifing parsed string returns the same string`` () =
    let stringifyParsed (s : string) =
        let jsonValue = JsonValue.Parse s
        jsonValue.ToString(JsonSaveOptions.DisableFormatting) = s
    let jsonStringArb = Arb.fromGen (jsonStringGen)
    Check.QuickThrowOnFailure (Prop.forAll jsonStringArb stringifyParsed)