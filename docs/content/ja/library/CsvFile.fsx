(**
# F# Data: CSV パーサーおよびリーダー

F#の [CSV 型プロバイダー](CsvProvider.html) はF#で作成された
効率の良いCSVパーサーを元にしています。
また、動的に値にアクセスできるような単純なAPIも備えられています。

厳密に定義されたCSV形式のドキュメントを扱う場合には、
[型プロバイダー](CsvProvider.html) を使用したほうが簡単です。
しかし動的アクセスが必要になるシナリオや、
単純なスクリプトを手軽に作成したい場合には
パーサーを直接使ったほうがおそらくは簡単でしょう。

## CSVドキュメントの読み取り

サンプルとなるCSVドキュメントを読み込むには、まず
(F# Interactiveを使用している場合は) `FSharp.Data.dll` への参照を追加するか、
プロジェクトで参照を追加する必要があります。
*)

#r "../../../../bin/lib/net45/FSharp.Data.dll"
open FSharp.Data

(**
`FSharp.Data` 名前空間には `CsvFile` 型が含まれていて、
この型ではデータの読み取り用に2つのstaticメソッドが公開されています。
`Parse` メソッドはデータを `string` 型の値として指定できます。
`Load` メソッドはデータをファイルあるいはWeb上のリソースから読み取ることができます
(また、非同期バージョンの `LoadAsync` もあります)。
以下の例ではYahoo financeのウェブサイトにある実際のCSVファイルのURLを指定して
`Load` メソッドを呼び出しています：
*)
 
// 株価データをダウンロード
let msft = CsvFile.Load("http://ichart.finance.yahoo.com/table.csv?s=MSFT").Cache()

// HLOC形式で株価を表示
for row in msft.Rows do
  printfn "HLOC: (%s, %s, %s)" (row.GetColumn "High") (row.GetColumn "Low") (row.GetColumn "Date")

(**

`CsvProvider` とは異なり、 `CsvFile` はパフォーマンスを上げるために
ストリーミングモードで動作します。
つまり `Rows` は1度だけしか走査できません。
複数回走査する必要がある場合には `Cache` メソッドを使用することになります。
ただしこのメソッドはメモリを多く使用するため、
巨大なデータセットを処理する場合には使用すべきではありません。

## CSV の拡張機能を使用する

ここでは `FSharp.Data.CsvExtensions` 名前空間をオープンすることで
使用できるようになる拡張機能について説明します。
この名前空間をオープンすると以下のような記述ができるようになります：

 * `row?column` という動的演算子を使うと `column` 列の値を取得できます。
    あるいは `row.[column]` というインデクサ形式でも取得できます。
 * `value.AsBoolean()` は値が `true` または `false` であれば
    それをブール値として返します。
 * `value.AsInteger()` は値が数値型であり、整数として変換可能であれば
    それを整数値として返します。
    同じように `value.AsInteger64()` 、 `value.AsDecimal()` 、
   `value.AsFloat()` といったメソッドもあります。
 * `value.AsDateTime()` は [ISO 8601](http://en.wikipedia.org/wiki/ISO_8601)
    形式か、あるいは1970/1/1からのミリ秒を含んだ `\/Date(...)\/` JSON 形式の値を
    `DateTime` として返します。
 * `value.AsGuid()` は値を `Guid` として返します。

数値あるいは日付の値をパースする必要があるメソッドには
省略可能なパラメータとしてカルチャを指定できます。

以下の例では先ほどのCSVをサンプルにして
これらの拡張メソッドを呼び出しています：
*)

open FSharp.Data.CsvExtensions

for row in msft.Rows do
  printfn "HLOC: (%f, %M, %O)" (row.["High"].AsFloat()) (row?Low.AsDecimal()) (row?Date.AsDateTime())

(**

## CSVファイルを変形する

`CsvFiles` は読み取りだけではなく、CSVファイルの変形もサポートしています。
`Filter` `Take` `TakeWhile` `Skip` `SkipWhile` `Truncate` といった操作が可能です。
変形後は `Save` メソッドのオーバーロードのいずれかを使って結果を保存できます。
保存時には別の区切り文字やクォート文字を指定できます。
*)

// 終値が始値よりも高いもののうち、上位10位の株価をTSV(タブ区切り)形式で保存します：
msft.Filter(fun row -> row?Close.AsFloat() > row?Open.AsFloat())
    .Truncate(10)
    .SaveToString('\t')

(**

## 関連する記事

 * [F# Data: CSV 型プロバイダー](CsvProvider.html) - 
   型セーフな方法でCSVデータにアクセスできるような
   F# 型プロバイダーについて説明しています。
 * [API リファレンス: CsvFile クラス](../../reference/fsharp-data-csvfile.html)
 * [API リファレンス: CsvRow クラス](../../reference/fsharp-data-csvrow.html)
 * [API リファレンス: CsvExtensions モジュール](../../reference/fsharp-data-csvextensions.html)
*)
