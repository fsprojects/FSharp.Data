#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#r "../../packages/FsCheck.0.9.2.0/lib/net40-Client/FsCheck.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.JsonValueProperties
#endif

open NUnit.Framework
open System
open FSharp.Data
open FsUnit
open FsCheck

let escaped = ['t';'r';'b';'n';'f';'\\';'/';'"']

let rec isValidJsonString s = 
    match s with
    | [] -> true
    | ['\\'] -> false
    | '"' :: t -> false
    | '\\' :: 'u' :: d1 :: d2 :: d3 :: d4 :: t 
        when [d1;d2;d3;d4] |> Seq.forall 
            (fun c -> (Char.IsDigit c || System.Text.RegularExpressions.Regex("[a-fA-F]").IsMatch(c.ToString())))
                -> isValidJsonString t
    | '\\' :: i :: t when escaped |> Seq.exists ((=) i) |> not -> false
    | '\\' :: i :: t when escaped |> Seq.exists ((=) i) -> isValidJsonString t
    | h :: t -> isValidJsonString t 

let jsonValueGen : Gen<JsonValue> =  
 
    let stringGen = Gen.map JsonValue.String (Arb.generate<string> |> Gen.suchThat (Seq.toList >> isValidJsonString))
    let booleanGen = Gen.map JsonValue.Boolean Arb.generate<bool>
    let nullGen = gen {return JsonValue.Null}
    let numberGen = Gen.map JsonValue.Number Arb.generate<decimal>

    let recordGen nameGen jValGen =
        (nameGen, jValGen)
        ||> Gen.map2 (fun x y -> (x,y)) 
        |> Gen.listOf
        |> Gen.map List.toArray
        |> Gen.map JsonValue.Record
    
    let name = Arb.generate<string> |> Gen.suchThat (Seq.toList >> isValidJsonString)
    
    let rec tree() =
        let tree' s =
            match s with
            | 0 -> Gen.oneof [ booleanGen; stringGen; nullGen; numberGen ]
            | n when n>0 -> 
                let subtree = 
                    (name, tree()) 
                    ||> recordGen
                    |> Gen.resize (s|>float|>sqrt|>int) 
                let arrayGen =
                    tree()
                    |> Gen.listOf
                    |> Gen.map List.toArray
                    |> Gen.map JsonValue.Array
                    |> Gen.resize (s|>float|>sqrt|>int) 
                Gen.oneof [ subtree; arrayGen; booleanGen; stringGen; nullGen; numberGen ]
            | _ -> invalidArg "s" "Only positive arguments are allowed"
        Gen.sized tree'

    (name, tree())
    ||> recordGen


type Generators = 
    static member JsonValue() =
         {new Arbitrary<JsonValue>() with
            override x.Generator = jsonValueGen
            override x.Shrinker j = 
                match j with
                | JsonValue.Array elems -> seq {for n in elems -> n}
                | JsonValue.Record [|prop|] -> Seq.empty
                | JsonValue.Record props -> seq {for n in props -> JsonValue.Record([|n|])}
                | _ -> Seq.empty }
     

let parseStringified (json: JsonValue) = 
    json.ToString(JsonSaveOptions.DisableFormatting)    
    |> JsonValue.Parse = json

[<Test>]
let  ``Parsing stringified JsonValue returns the same value`` () =
    Arb.register<Generators>() |> ignore
    Check.QuickThrowOnFailure parseStringified