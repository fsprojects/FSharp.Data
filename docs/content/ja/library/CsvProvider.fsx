(** 
# F# Data: CSV 型プロバイダー

この記事ではCSV 型プロバイダーを使って
静的に型付けされた方法でCSVファイルを扱う方法を紹介します。
この型プロバイダーは [Try F#](http://www.tryfsharp.org) のWebサイトにある
"Financial Computing" のチュートリアルとよく似ています。
したがってそちらを参考にするともう少し多くの例が見つけられるでしょう。

CSV 型プロバイダーは入力としてサンプルとなるCSVを受け取り、
このサンプル内の列データを元にした型を生成します。
列の名前は1行目(ヘッダ行)が元になり、各列の型は2行目以降にあるデータから
推測されます。

## プロバイダーの基本

この型プロバイダーは `FSharp.Data.Dll` アセンブリに含まれています。
このアセンブリが `../../../../bin` にあるとすると、
F# Interactiveでアセンブリを読み込むには以下のようにします：
*)

#r "../../../../bin/FSharp.Data.dll"
open FSharp.Data

(**

### 株価をパースする

Yahoo FinanceのWebサイトでは以下のような構造のCSV形式で
日単位の株価が公開されています
(より大きな例としては [`data/MSFT.csv`](../../data/MSFT.csv) ファイルを
参照してください)：

    [lang=text]
    Date,Open,High,Low,Close,Volume,Adj Close
    2012-01-27,29.45,29.53,29.17,29.23,44187700,29.23
    2012-01-26,29.61,29.70,29.40,29.50,49102800,29.50
    2012-01-25,29.07,29.65,29.07,29.56,59231700,29.56
    2012-01-24,29.47,29.57,29.18,29.34,51703300,29.34

一般的なCSVファイルと同じく、1行目にはヘッダ(各列の名前)があり、
2行目以降にデータがあります。
このファイルを `CsvProvider` に指定すると、
静的に型付けされた方法でファイルの内容を参照できるようになります：
*)

type Stocks = CsvProvider<"../../data/MSFT.csv">

(**
生成された型にはデータをロードするための2つのstaticメソッドがあります。
データが文字列の場合には `Parse` メソッドを使用します。
データがファイルやWeb上のリソースの場合には `Load` メソッドを使用します
(非同期バージョンの `AsyncLoad` メソッドもあります)。
型プロバイダーに指定するサンプル用の引数には
ローカルのパスだけではなく、Web上のURLを指定することもできます。
以下の例ではYahoo FinanceのWebサイトで実際に公開されているCSVファイルの
URLを使って `Load` メソッドを呼び出しています：
*)
 
// 株価データをダウンロード
let msft = Stocks.Load("http://ichart.finance.yahoo.com/table.csv?s=MSFT")

// 最新の行をチェックする。なお 'Date' プロパティは
// 'DateTime' 型で、 'Open' プロパティは 'decimal' 型であることに注意
let firstRow = msft.Rows |> Seq.head
let lastDate = firstRow.Date
let lastOpen = firstRow.Open

// 株価を四本値形式で表示
for row in msft.Rows do
  printfn "HLOC: (%A, %A, %A, %A)" row.High row.Low row.Open row.Close

(**

> 訳注：四本値とは高値、安値、始値、終値の4種の値段のこと

生成された型にはCSVファイルのデータを行コレクションとして返す
`Rows` プロパティがあります。
ここでは `for` ループを使って各行を走査しています。
見て分かるように、行のための(生成された)型には `High` や `Low` 、
`Close` など、CSVファイルの各列に対応するプロパティがあります。

また、型プロバイダーが各列の型を推測していることも確認できます。
`Date` プロパティは(サンプルファイル中のデータが日付としてパースできるため)
`DateTime` 型として推測されていますが、一方で四本値はそれぞれ
`decimal` として推測されています。

### 株価のチャート表示

[FSharp.Charting](http://fsharp.github.io/FSharp.Charting/) ライブラリを使うと
上場からのMSFTの株価変化を単純な折れ線チャートとして描画できます：
*)

// FSharp.Chartingの読み込み
#load "../../../../packages/FSharp.Charting/FSharp.Charting.fsx"
open System
open FSharp.Charting

(*** define-output:chart1 ***)

// 株価をビジュアル化
[ for row in msft.Rows -> row.Date, row.Open ]
|> Chart.FastLine

(*** include-it:chart1 ***)

(**
もう1つ例として、先月のデータの詳細を確認できるように
`ローソク` (Candlestick)チャートにしてみます：
*)

// 先月の株価を四本値形式で取得
let recent = 
  [ for row in msft.Rows do
      if row.Date > DateTime.Now.AddDays(-30.0) then
        yield row.Date, row.High, row.Low, row.Open, row.Close ]

(*** define-output:chart2 ***)

// ローソクチャートを使って株価をビジュアル化
Chart.Candlestick(recent).WithYAxis(Min = 40.0, Max = 50.0)

(*** include-it:chart2 ***)

(**
## 測定単位を使用する

もう1つ興味深い機能として、CSV 型プロバイダーは測定単位をサポートしています。
標準SI単位の名前あるいは記号がヘッダに含まれている場合、
生成された型では特定の単位が付加された値が返されるようになります。

このセクションでは以下のような単純なデータが含まれた
[`data/SmallTest.csv`](../../data/SmallTest.csv) を使います：

    [lang=text]
    Name,  Distance (metre), Time (s)
    First, 50.0,             3.7

見ての通り、2列目と3列目にはそれぞれ `metre` と `s` という単位があります。
コード側で測定単位を使う場合、
標準単位名を含んだ名前空間をオープンする必要があります。
その後、型プロバイダーのstatic引数に `SmallTest.csv` を指定します。
なお今回は同じデータを実行時にも使用するため、
同じ引数を再度指定して `Load` を呼び出すのではなく、
`GetSample` メソッドを使っていることに注意してください。
*)

let small = CsvProvider<"../../data/SmallTest.csv">.GetSample()

(**
先ほどの例と同じく、行データは値 `small` の `Rows` プロパティで取得できます。
今回は生成されたプロパティ `Distance` と `Time` に単位が付加されています。
以下の単純な計算をみてください：
*)

open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames

for row in small.Rows do
  let speed = row.Distance / row.Time
  if speed > 15.0M<metre/second> then 
    printfn "%s (%A m/s)" row.Name speed

(**
`Distance` と `Time` の数値的な値はいずれも(かなり小さな値なので)
`decimal` として推測されています。
したがって `speed` の型は `decimal<meter/second>` になります。
そうするとコンパイラは互換性のない値を比較していないかどうか、
つまりたとえばメートル毎秒とキロメートル毎時を比較していたりはしないか
チェックするようになります。

## 独自の区切り文字とタブ区切りファイル

CSV 型プロバイダーはデフォルトではカンマ( `,` )を区切り文字とします。
しかし場合によっては `,` ではない区切り文字が
CSVファイルで使われていることがあります。
ヨーロッパの一部の国では `,` が10進数の区切り文字として使われているため、
CSVの列区切り文字には代わりにセミコロン( `;` )が使われます。
`CsvProvider` は省略可能なstatic引数 `Separator` に
代わりとなる区切り文字を指定できます。
つまりこれを使えばタブ区切り形式のテキストも処理できるわけです。
以下では区切り文字として `;` を使っています：
*)

let airQuality = CsvProvider<"../../data/AirQuality.csv", ";">.GetSample()

for row in airQuality.Rows do
  if row.Month > 6 then 
    printfn "Temp: %i Ozone: %f " row.Temp row.Ozone

(**
空気質のデータセット([`data/AirQuality.csv`](../../data/AirQuality.csv))は
統計解析向け言語 R の多くのサンプルで使われているものです。
このデータセットの簡単な説明については
[R の言語マニュアル](http://stat.ethz.ch/R-manual/R-devel/library/datasets/html/airquality.html)
を参照してください。

`\t` を区切り文字とするようなタブ区切りファイルを処理する場合には
やはり区切り文字を明示的に指定してもよいでしょう。
ですが、URLまたはファイルの末尾にある拡張子が `.tsv` になっていると
型プロバイダーはデフォルトで `\t` を区切り文字とするようになります。
以下の例ではstatic引数 `IgnoreErrors` を `true` にして、
要素数が異なる行を自動的に無視するようにもしています
(サンプルファイル [`data/MortalityNY.csv`](../../data/MortalityNY.tsv) には
末尾に構造化されていないデータが追加されています)：
*)

let mortalityNy = CsvProvider<"../../data/MortalityNY.tsv", IgnoreErrors=true>.GetSample()

// 原因名をコードで検索
// (事故で負傷した自転車走者)
let cause = mortalityNy.Rows |> Seq.find (fun r -> 
  r.``Cause of death Code`` = "V13.4")

// 負傷した走者数を出力
printfn "原因: %s" cause.``Cause of death``
for r in mortalityNy.Rows do
  if r.``Cause of death Code`` = "V13.4" then 
    printfn "%s (%d 件)" r.County r.Count

(**

最後に、 `CsvProvider` には複数種類の区切り文字を指定することもできます。
これはたとえばファイルが不正で、セミコロンかコロンのどちらかで
行が区切られているような場合に有効です。
具体的には
`CsvProvider<"../../data/AirQuality.csv", Separator=";,">`
というようにします。

## 値無し

統計的データセットでは一部の値が無しになっているということがよくあります。
[`data/AirQuality.csv`](../../data/AirQuality.csv) ファイルを開くと、
一部のオゾンの観測値が `#N/A` と記録されていることが確認できます。
このような値はfloatとしてパースされ、F#であれば `Double.NaN` という値とみなされます。
デフォルトでは `#N/A` `NA` `:` が値無しを表す値と認識されますが、
`CsvProvider` のstatic引数 `MissingValues` を指定して
カスタマイズすることもできます。

以下のコードでは `Double.NaN` になっている値を除いて、
オゾンの観測値の平均を計算しています。
まず各行の `Ozone` プロパティを取得して値無しを除去した後、
標準の `Seq.average` 関数を使って平均を計算しています：
*)

let mean = 
  airQuality.Rows 
  |> Seq.map (fun row -> row.Ozone) 
  |> Seq.filter (fun elem -> not (Double.IsNaN elem)) 
  |> Seq.average 

(**

サンプルとして指定するデータにはどの列にも値無しのデータが含まれていないものの、
実行時にはどこかで値無しが現れる可能性があるという場合には
`AssumeMissingValues` を `true` に設定すれば、
`CsvProvider` がどこかの列には値無しが現れるだろうと想定するようになります。

## 列の型を制御する

デフォルトではCSV 型プロバイダーは最初の1000行を使って型を推測します。
しかし `CsvProvider` のstatic引数 `InferRows` を使うと
この動作をカスタマイズできます。
0を指定するとファイル全体が使われるようになります。

`0` `1` `Yes` `No` `True` `False` しか含まない列は `bool` になります。
数値を含む列はそれぞれ精度に応じて `int` `int64` `decimal` `float` の
いずれかになります。

いずれかの行で値無しになっている場合、CSV 型プロバイダーは
その行を(`int` および `int64` に対しては)null許容型、
あるいは(`bool` `DateTime` `Guid` に対しては)オプション型とみなします。
`decimal` と推測できる列に値無しが含まれる場合、代わりに `float` とみなされ、
値無しが `Double.NaN` として表現されます。
`string` 型はそれ自体が既にnullを許容するため、
デフォルトでは `string option` と推測されることはありません。
すべての場合においてオプション型になるようにしたい場合には、
static引数 `PreferOptionals` を `true` にします。
この設定を行うと、空の文字列や `Double.NaN` ではなく、
代わりに `None` が返されるようになります。

他にもたとえば `decimal` ではなく `float` として行を扱いたいというような、
別の設定を使いたい場合には、ヘッダ行で列の型を丸括弧で囲んで記述することで
デフォルトの動作を上書きできます。
これは測定単位を指定する方法と同じです。
ヘッダ行による指定方法は `AssumeMissingValues` や `PreferOptionals` の動作を上書きします。
指定可能な型は以下の通りです：

* `int`
* `int?`
* `int option`
* `int64`
* `int64?`
* `int64 option`
* `bool`
* `bool?`
* `bool option`
* `float`
* `float?`
* `float option`
* `decimal`
* `decimal?`
* `decimal option`
* `date`
* `date?`
* `date option`
* `guid`
* `guid?`
* `guid option`
* `string`
* `string option`.

型と測定単位の両方を( `float<metre>` のようにして)指定することもできます。
たとえば以下の通りです：

    [lang=text]
    Name,  Distance (decimal?<metre>), Time (float)
    First, 50,                        3

さらに、 `CsvProvider` のstatic引数 `Schema` で一部またはすべての型を
指定することもできます。
指定可能な形式は以下の通りです：

* `型`
* `型<測定単位>`
* `名前 (型)`
* `名前 (型<測定単位>)`

`Schema` 引数で指定された値はヘッダ行で指定されたものよりも常に優先されます。

ファイルの1行目がヘッダ行ではない場合、static引数 `HasHeaders` を `false` に
すると1行目もデータ行とみなされるようになります。
この場合、それぞれの行は `Schema` 引数で指定されていなければ
`Column1` `Column2` という名前になります。
なお `Schema` 引数で名前だけを上書きしつつ、
型プロバイダーに列の型を推測させることもできます。
たとえば以下のようにします：
*)

let csv = CsvProvider<"1,2,3", HasHeaders = false, Schema = "Duration (float<second>),foo,float option">.GetSample()
for row in csv.Rows do
  printfn "%f %d %f" (row.Duration/1.0<second>) row.Foo (defaultArg row.Column3 1.0)

(**

必ずしもすべての列を上書きしなければいけないわけではなく、
一部をデフォルトのままにしておくこともできます。
たとえばKaggleから取得したタイタニックの乗船者データ
([`data/Titanic.csv`](../../data/Titanic.csv))
を対象にしている時に、3列目( `PClass` 列)を `Passenger Class` 、
6列目( `Fare` 列)を `decimal` ではなく `float` にしたい場合、
これらだけを定義しておいて、その他の行が空になっているようなスキーマを
指定します(末尾で連続するカンマは省略できます)。

*)

let titanic1 = CsvProvider<"../../data/Titanic.csv", Schema=",,Passenger Class,,,float">.GetSample()
for row in titanic1.Rows do
  printfn "%s Class = %d Fare = %g" row.Name row.``Passenger Class`` row.Fare

(**

あるいは位置で指定する代わりに列の名前を使って上書きすることもできます：

*)

let titanic2 = CsvProvider<"../../data/Titanic.csv", Schema="Fare=float,PClass->Passenger Class">.GetSample()
for row in titanic2.Rows do
  printfn "%s Class = %d Fare = %g" row.Name row.``Passenger Class`` row.Fare

(**

これら2つのシンタックスを組み合わせて
`Schema="int64,DidSurvive,PClass->Passenger Class=string"`
とすることもできます。

## CSVファイルの変形

`CsvProvider` はファイルの読み取りだけでなく、データの変形もサポートしています。
`Filter` `Take` `TakeWhile` `Skip` `SkipWhile` `Truncate` という操作が可能です。
これらはいずれもスキーマを維持するようになっているため、
変形後は `Save` メソッドのいずれかのオーバーロードを使って結果を保存できます。
結果をCSV形式で保存したくない場合、あるいはデータの形式を変更したい場合には、
`Rows` プロパティで取得できる行のシーケンスに対して直接
`Seq` モジュールの機能を使うこともできます。

*)

// 値無しのデータを含まない先頭10行を新しいCSVファイルに保存する
airQuality.Filter(fun row -> not (Double.IsNaN row.Ozone) && 
                             not (Double.IsNaN row.``Solar.R``))
          .Truncate(10)
          .SaveToString()

(**

## ビッグデータの処理

デフォルトでは行がキャッシュされるため、 `Rows` プロパティを複数回走査しても
特に問題はありません。
しかし1回しか走査しないのであれば、 `CsvProvider` のstatic引数
`CacheRows` を `false` にすればキャッシュを無効化できます。
行数が非常に多い場合、キャッシュを無効化しなければ
メモリを消費し尽くしてしまうことになるでしょう。
`Cache` メソッドを使えば任意のタイミングでデータをキャッシュできますが、
データセットを小さなサイズに変形した後に限定すべきです：
*)

let stocks = CsvProvider<"http://ichart.finance.yahoo.com/table.csv?s=MSFT", CacheRows=false>.GetSample()
stocks.Take(10).Cache()

(**
## 関連する記事

 * [F# Data: CSV パーサーおよびリーダー](CsvFile.html) -
   CSVドキュメントを動的に処理するための詳しい説明があります。
 * [API リファレンス: CsvProvider 型プロバイダー](../../reference/fsharp-data-csvprovider.html)

*)
