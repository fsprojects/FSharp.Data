(**
# F# Data: Anonymizing JSON 

This tutorial shows how to implement an anonymizer for a JSON document (represented using
the `JsonValue` type discussed in [JSON parser article](JsonValue.html))
This functionality is not directly available in the F# Data library, but it can
be very easily implemented by recursively walking over the JSON document.

If you want to use the JSON anonymizer in your code, you can copy the 
[source from GitHub][jsonanonymizer] and just include it in your project. If you use these
functions often and would like to see them in the F# Data library, please submit
a [feature request][issues].

  [jsonanonymizer]: https://github.com/fsharp/FSharp.Data/blob/master/docs/content/tutorials/JsonAnonymizer.fsx
  [issues]: https://github.com/fsharp/FSharp.Data/issues

*DISCLAIMER*: Don't use this for sensitive data as it's just a sample

*)

#r "../../../bin/FSharp.Data.dll"
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

    let getRandomChar (c:char) =
        if Char.IsDigit c then digits.[rng.Next(10)]
        elif Char.IsLetter c then
            if Char.IsLower c
            then lowerLetters.[rng.Next(26)]
            else upperLetters.[rng.Next(26)]
        else c

    let randomize (str:string) =
        String(str.ToCharArray() |> Array.map getRandomChar)

    let rec anonymize json =
        match json with
        | JsonValue.String s when valuesToSkip.Contains s -> json
        | JsonValue.String s ->
            let typ = Runtime.StructuralInference.inferPrimitiveType CultureInfo.InvariantCulture s
            if typ = typeof<Guid> then Guid.NewGuid().ToString()
            elif typ = typeof<Runtime.StructuralTypes.Bit0> || typ = typeof<Runtime.StructuralTypes.Bit1> then s
            elif typ = typeof<DateTime> then s
            else 
                let prefix, s =
                    if s.StartsWith "http://" then "http://", s.Substring("http://".Length)
                    elif s.StartsWith "https://" then "https://", s.Substring("https://".Length)
                    else "", s
                prefix + randomize s
            |> JsonValue.String
        | JsonValue.Number d -> 
            let typ = Runtime.StructuralInference.inferPrimitiveType CultureInfo.InvariantCulture (d.ToString())
            if typ = typeof<Runtime.StructuralTypes.Bit0> || typ = typeof<Runtime.StructuralTypes.Bit1>
            then json
            else d.ToString() |> randomize |> Decimal.Parse |> JsonValue.Number
        | JsonValue.Float f -> 
            f.ToString()
            |> randomize 
            |> Double.Parse 
            |> JsonValue.Float
        | JsonValue.Boolean _  | JsonValue.Null -> json
        | JsonValue.Record props -> 
            props 
            |> Array.map (fun (key, value) -> key, if propertiesToSkip.Contains key then value else anonymize value)
            |> JsonValue.Record
        | JsonValue.Array array -> 
            array 
            |> Array.map anonymize 
            |> JsonValue.Array

    member __.Anonymize json = anonymize json

let json = JsonValue.Load (__SOURCE_DIRECTORY__ + "../../data/TwitterStream.json")
printfn "%O" json

let anonymizedJson = (JsonAnonymizer ["lang"]).Anonymize json
printfn "%O" anonymizedJson

(**

## Related articles

 * [F# Data: JSON Parser](../library/JsonValue.html) - a tutorial that introduces
   `JsonValue` for working with JSON values dynamically.
 * [F# Data: JSON Type Provider](../library/JsonProvider.html) - discusses F# type provider
   that provides type-safe access to JSON data.

*)