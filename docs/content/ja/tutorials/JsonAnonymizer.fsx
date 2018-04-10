(**
# F# Data: JSONの匿名化

このチュートリアルではJSONドキュメント
( [JSONパーサーの記事](../library/JsonValue.html) で説明した
`JsonValue` 型で表現されるドキュメント)
の匿名化機能を実装する方法を紹介します。
この機能はF# Dataライブラリに備わっている機能ではありませんが、
JSONドキュメントを再帰的に処理するだけで非常に簡単に実装できます。

JSON匿名化機能をコード内で利用したい場合には、
[GitHubにあるソースコード][jsonanonymizer] をコピーして
プロジェクトに加えるだけです。
この機能を頻繁に利用するようになり、F# Dataライブラリに取り込んでもらいたい
ということであれば [機能のリクエスト][issues] を送信してください。

  [jsonanonymizer]: https://github.com/fsharp/FSharp.Data/blob/master/docs/content/tutorials/JsonAnonymizer.fsx
  [issues]: https://github.com/fsharp/FSharp.Data/issues

**警告**: この機能は単なるサンプルであるため、重要なデータを処理させないでください。

*)

#r "../../../../bin/lib/net45/FSharp.Data.dll"
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

## 関連する記事

 * [F# Data: JSON パーサーおよびリーダー](../library/JsonValue.html) -
   JSONの値を動的に処理する方法についての説明があります。
 * [F# Data: JSON 型プロバイダー](../library/JsonProvider.html) -
   型安全な方法でJSONデータにアクセスする機能を持った
   F# 型プロバイダーについて説明しています。

*)