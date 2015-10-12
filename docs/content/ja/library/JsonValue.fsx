(**
# F# Data: JSON パーサーおよびリーダー

F#の [JSON 型プロバイダー](JsonProvider.html) はF#で作成された
効率の良いJSONパーサーを元にしています。
このパーサーは [F# 3.0 Sample Pack](http://fsharp3sample.codeplex.com)
内にあるJSONパーサーを元にしていますが、
F# Dataでは値を動的にアクセスできるようにするための単純なAPIが追加されています。

厳密に定義されたJSONドキュメントを処理する場合、
[型プロバイダー](JsonProvider.html) を使うと簡単なのですが、
動的に処理するようなシナリオであったり、
単純なスクリプトを手軽に用意したいような場合には
パーサーを使ったほうが簡単でしょう。

## JSONドキュメントの読み取り

サンプルとなるJSONドキュメントを読み取るには
(F# Interactiveの場合)`FSharp.Data.dll` ライブラリへの参照を追加するか、
プロジェクトで参照を追加します。
*)

#r "../../../../bin/FSharp.Data.dll"
open FSharp.Data

(**
`FSharp.Data` 名前空間にある `JsonValue` 型を使うと、
以下のようにしてJSON形式の文字列をパースできます：
*)

let info =
  JsonValue.Parse(""" 
    { "name": "Tomas", "born": 1985,
      "siblings": [ "Jan", "Alexander" ] } """)

(**
`JsonValue` 型は `Record` や `Collection` などのケースを持った判別共用体なので
パターンマッチを使ってパース後の値の構造を調査することができます。

## JSON用拡張機能を使用する

ここではすべての機能を紹介しません。
その代わり、 `FSharp.Data.JsonExtensions` 名前空間をオープンすることで
利用できるようになるいくつかの拡張機能について説明します。
この名前空間をオープンすると、以下のような記述ができるようになります：

 * `value.AsBoolean()` は値が `true` または `false` の場合にブール値を返します。
 * `value.AsInteger()` は値が数値型で、整数として変換可能であれば整数値を返します。
   同様に `value.AsInteger64()` `value.AsDecimal()` `value.AsFloat()`
   といったものもあります。
 * `value.AsString()` は値を文字列として返します。
 * `value.AsDateTime()` は値を [ISO 8601](http://en.wikipedia.org/wiki/ISO_8601) か、
   1970/1/1からのミリ秒を含んだJSON形式の `\/Date(...)\/` でパースして
   `DateTime` を返します。
 * `value.AsGuid()` は値を `Guid` としてパースします。
 * `value?child` は `child` という名前のレコードメンバーを
   取得するための動的演算子です。
   あるいは `value.GetProperty(child)` やインデクサ `value.[child]`
   を使うこともできます。
 * `value.TryGetProperty(child)` はレコードメンバーを安全に取得できます
   (もしメンバーが値無しあるいはレコードではなかった場合、 `TryGetProperty` は
    `None` を返します)。
 * `[ for v in value -> v ]` とすると `value` をコレクションとして扱い、
   含まれている要素を走査します。
   また、 `value.AsArray()` とすると、すべての要素を配列として取得できます。
 * `value.Properties()` はレコードノードの全プロパティのリストを返します。
 * `value.InnerText()` はすべてのテキストあるいは配列内のテキスト
   (たとえば複数行文字列を表すデータ)を連結します

数値または日付データとしてパースする( `AsFloat` や `AsDateTime` などの)メソッドには
省略可能な引数としてカルチャを指定出来ます。

以下のコードはサンプルで指定したJSONの値を処理する方法の一例です：
*)

open FSharp.Data.JsonExtensions

// 名前と誕生日を表示
let n = info?name
printfn "%s (%d)" (info?name.AsString()) (info?born.AsInteger())

// 兄弟姉妹全員の名前を表示
for sib in info?siblings do
  printfn "%s" (sib.AsString())

(**
`JsonValue` 型は実際には `IEnumerable<'T>` インターフェイスを
実装しているわけではありません(つまり `Seq.xyz` 関数に渡す事はできません)。
`GetEnumerator` だけが定義されているため、シーケンス式内や
`for` ループで使うことができるというわけです。
*)

(**
## WorldBankからのレスポンスをパースする

もう少し複雑な例として、WorldBankへのリクエストに対する
レスポンスデータ [`data/WorldBank.json`](../../data/WorldBank.json) を
サンプルドキュメントにしてみます。
(より便利な方法としては [型プロバイダー](WorldBank.html) を使って
WorldBankにアクセスすることもできます)。
このドキュメントは以下のようになっています：

    [lang=js]
    [ { "page": 1, "pages": 1, "total": 53 },
      [ { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":null,"decimal":"1","date":"2000"},
        { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":"16.6567773464055","decimal":"1","date":"2010"} ] ]

このように、全体としては配列になっていて、
1番目の要素にはレコード、2番目の要素にはデータ点のコレクションが
含まれた形式になっています。
このドキュメントは以下のようにして読み取りおよびパースできます：
*)

let value = JsonValue.Load(__SOURCE_DIRECTORY__ + "../../../data/WorldBank.json")

(**
なおWeb上から直接データを読み取ることもできます。
また、読み取りを非同期的に実行するバージョンもあります：
**)

let valueAsync = JsonValue.AsyncLoad("http://api.worldbank.org/country/cz/indicator/GC.DOD.TOTL.GD.ZS?format=json")

(**
最上位の配列を1番目の(概要を含んだ)レコードとデータ点のコレクションに分けるためには
`value` に対してパターンマッチを使って
`Jsonvalue.Array` のコンストラクタとマッチするかどうか調べます：
*)

match value with
| JsonValue.Array [| info; data |] ->
    // 概要を表示
    let page, pages, total = info?page, info?pages, info?total
    printfn 
      "%d ページ中の %d ページ目を表示中。 全体のレコード数は %d" 
      (pages.AsInteger()) (page.AsInteger()) (total.AsInteger())
    
    // 非nullのデータ点をそれぞれ表示
    for record in data do
      if record?value <> JsonValue.Null then
        printfn "%d: %f" (record?date.AsInteger()) 
                         (record?value.AsFloat())
| _ -> printfn "失敗しました"

(**
データ点の `value` プロパティは常に使用できるわけではありません。
直前で説明してある通り、この値は `null` になることがあります。
その場合にはデータ点をスキップします。
プロパティが `null` かどうか調べる場合は単に `JsonValue.Null` と
比較するだけです。

また `date` と `value` のプロパティは元のファイルでは( `1990` のような)数値ではなく、
( `"1990"` のような)文字列形式になっている点に注意してください。
この値を int または float として取得しようとすると、
`JsonValue` は自動的に文字列を特定の形式になるようにパースします。
一般的にはこのAPIがファイルをパースする場合、
できるだけ寛容に値を受け入れるようになっています。

## 関連する記事

 * [F# Data: JSON 型プロバイダー](JsonProvider.html) -
   型安全な方法でJSONデータにアクセスする機能を持った
   F# 型プロバイダーについて説明しています。
 * [F# Data: WorldBank プロバイダー](WorldBank.html) -
   WorldBank 型プロバイダーを使うとWorldBankから受け取ったデータを
   簡単に処理出来るようになります。
 * [API リファレンス: JsonValue 判別共用体](../../reference/fsharp-data-jsonvalue.html)
 * [API リファレンス: JsonExtensions モジュール](../../reference/fsharp-data-jsonextensions.html)

*)
