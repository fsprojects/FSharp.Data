(**
---
category: Tutorials
categoryindex: 3
index: 1
---
*)
(*** condition: prepare ***)
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Http.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Runtime.Utilities.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Json.Core.dll"
(*** condition: fsx ***)
#if FSX
#r "nuget: FSharp.Data,{{fsdocs-package-version}}"
#endif
(*** condition: ipynb ***)
#if IPYNB
#r "nuget: FSharp.Data,{{fsdocs-package-version}}"

Formatter.SetPreferredMimeTypesFor(typeof<obj>, "text/plain")
Formatter.Register(fun (x: obj) (writer: TextWriter) -> fprintfn writer "%120A" x)
#endif
(**
# Anonymizing JSON

[![Binder](../img/badge-binder.svg)](https://mybinder.org/v2/gh/diffsharp/diffsharp.github.io/master?filepath={{fsdocs-source-basename}}.ipynb)&emsp;
[![Script](../img/badge-script.svg)]({{root}}/{{fsdocs-source-basename}}.fsx)&emsp;
[![Notebook](../img/badge-notebook.svg)]({{root}}/{{fsdocs-source-basename}}.ipynb)

This tutorial shows how to implement an anonymizer for a JSON document (represented using
the `cref:T:FSharp.Data.JsonValue` type discussed in [JSON parser article](JsonValue.html))
This functionality is not directly available in the FSharp.Data package, but it can
be very easily implemented by recursively walking over the JSON document.

If you want to use the JSON anonymizer in your code, you can copy the
[source from GitHub][jsonanonymizer] and just include it in your project. If you use these
functions often and would like to see them in the FSharp.Data package, please submit
a [feature request][issues].

  [jsonanonymizer]: https://github.com/fsharp/FSharp.Data/blob/master/docs/content/tutorials/JsonAnonymizer.fsx
  [issues]: https://github.com/fsharp/FSharp.Data/issues

*DISCLAIMER*: Don't use this for sensitive data as it's just a sample

*)

open System
open System.Globalization
open FSharp.Data

type JsonAnonymizer(?propertiesToSkip, ?valuesToSkip) =

    let propertiesToSkip = Set.ofList (defaultArg propertiesToSkip [])
    let valuesToSkip = Set.ofList (defaultArg valuesToSkip [])

    let rng = Random()

    let digits = [| '0' .. '9' |]
    let lowerLetters = [| 'a' .. 'z' |]
    let upperLetters = [| 'A' .. 'Z' |]

    let getRandomChar (c: char) =
        if Char.IsDigit c then
            digits.[rng.Next(10)]
        elif Char.IsLetter c then
            if Char.IsLower c then
                lowerLetters.[rng.Next(26)]
            else
                upperLetters.[rng.Next(26)]
        else
            c

    let randomize (str: string) =
        String(str.ToCharArray() |> Array.map getRandomChar)

    let isType testType typ =
        match typ with
        | Runtime.StructuralTypes.InferedType.Primitive (typ, _, _, _, _) -> typ = testType
        | _ -> false

    let rec anonymize json =
        match json with
        | JsonValue.String s when valuesToSkip.Contains s -> json
        | JsonValue.String s ->
            let typ =
                Runtime.StructuralInference.inferPrimitiveType
                    Runtime.StructuralInference.defaultUnitsOfMeasureProvider
                    Runtime.StructuralInference.InferenceMode'.ValuesOnly
                    CultureInfo.InvariantCulture
                    s
                    None

            (if typ |> isType typeof<Guid> then
                 Guid.NewGuid().ToString()
             elif typ |> isType typeof<Runtime.StructuralTypes.Bit0>
                  || typ |> isType typeof<Runtime.StructuralTypes.Bit1> then
                 s
             elif typ |> isType typeof<DateTime> then
                 s
             else
                 let prefix, s =
                     if s.StartsWith "http://" then
                         "http://", s.Substring("http://".Length)
                     elif s.StartsWith "https://" then
                         "https://", s.Substring("https://".Length)
                     else
                         "", s

                 prefix + randomize s)
            |> JsonValue.String
        | JsonValue.Number d ->
            let typ =
                Runtime.StructuralInference.inferPrimitiveType
                    Runtime.StructuralInference.defaultUnitsOfMeasureProvider
                    Runtime.StructuralInference.InferenceMode'.ValuesOnly
                    CultureInfo.InvariantCulture
                    (d.ToString())
                    None

            if typ |> isType typeof<Runtime.StructuralTypes.Bit0>
               || typ |> isType typeof<Runtime.StructuralTypes.Bit1> then
                json
            else
                d.ToString()
                |> randomize
                |> Decimal.Parse
                |> JsonValue.Number
        | JsonValue.Float f ->
            f.ToString()
            |> randomize
            |> Double.Parse
            |> JsonValue.Float
        | JsonValue.Boolean _
        | JsonValue.Null -> json
        | JsonValue.Record props ->
            props
            |> Array.map (fun (key, value) ->
                let newValue =
                    if propertiesToSkip.Contains key then
                        value
                    else
                        anonymize value

                key, newValue)
            |> JsonValue.Record
        | JsonValue.Array array -> array |> Array.map anonymize |> JsonValue.Array

    member _.Anonymize json = anonymize json

let json =
    JsonValue.Load(
        __SOURCE_DIRECTORY__
        + "../../data/TwitterStream.json"
    )

printfn "%O" json

let anonymizedJson = (JsonAnonymizer [ "lang" ]).Anonymize json
printfn "%O" anonymizedJson

(**

## Related articles

 * API Reference: `cref:T:FSharp.Data.JsonValue`
 * [JSON Parser](../library/JsonValue.html) - a tutorial that introduces
   `cref:T:FSharp.Data.JsonValue` for working with JSON values dynamically.
 * [JSON Type Provider](../library/JsonProvider.html) - discusses F# type provider
   that provides type-safe access to JSON data.

*)
