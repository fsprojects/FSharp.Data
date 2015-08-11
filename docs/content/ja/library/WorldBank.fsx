(** 
# F# Data: WorldBank プロバイダー

[世界銀行](http://www.worldbank.org) (World Bank)は世界中の発展途上国に対して、
経済的かつ技術的な支援を行っている国際組織です。
また、世界銀行は活動の一環として世界中の各国における発展指標や
その他のデータを収集しています。
[data catalog](http://data.worldbank.org/) のページではプログラムからも
アクセスできる8000以上の指標が公開されています。

WorldBank 型プロバイダーを使うと、F#プログラムやスクリプトから型安全かつ
簡単な方法でWorldBankのデータにアクセスできるようになります。
このドキュメントはこの型プロバイダーの基本的な部分のみ説明しています。
WorldBank 型プロバイダーは [Try F#](http://www.tryfsharp.org) の
Webサイトにある"Data Science" のチュートリアルでも使われているため、
そちらも参考にするとよいでしょう。

## 型プロバイダーの基本

以下の例では(F# Interactive上で) `FSharp.Data.dll` ライブラリを読み込んだ後、
`GetDataContext` メソッドを使ってWorldBankへの接続を初期化し、
イギリスで大学に進学した人口の割合を受信しています；
*)

#r "../../../../bin/FSharp.Data.dll"
open FSharp.Data

let data = WorldBankData.GetDataContext()

data
  .Countries.``United Kingdom``
  .Indicators.``School enrollment, tertiary (% gross)``
|> Seq.maxBy fst

(**
データコンテキストが生成されると、WorldBank 型プロバイダーは
世界銀行で把握されているすべての国のリスト、および
利用可能なすべての指針のリストを受信します。
それぞれはプロパティとして参照できるようになっているため、
自動コンプリートの候補から様々なデータソースが利用できることが確認できるでしょう。
多くの指標には長い名前がつけられているため、
名前を `\`\`` で囲む必要があります。

`School enrollment, tertiary (% gross)` プロパティの結果は
異なる年毎の値のシーケンスです。
`Seq.maxBy fst` とすることで直近で利用可能な最新のデータを取得できます。

### 世界銀行のデータをチャート表示する

[FSharp.Charting](http://fsharp.github.io/FSharp.Charting/) ライブラリを使うと
大学進学率の変遷を簡単に図表にできます：

*)

#load "../../../../packages/FSharp.Charting/FSharp.Charting.fsx"
open FSharp.Charting

(*** define-output:chart1 ***)

data.Countries.``United Kingdom``
    .Indicators.``School enrollment, tertiary (% gross)``
|> Chart.Line

(**
`Chart.Line` 関数はXとYの値ペアのシーケンスを受け取ります。
そのため、世界銀行から受信したデータセットをそのまま渡せば
Xが年、Yがその年の値になったチャートが作成できます。

*)

(*** include-it:chart1 ***)

(**
## 世界銀行のデータを非同期的に使う

非常に大量のデータをダウンロードする必要がある場合、
あるいは呼び出し元をブロックせずに処理を実行した場合には、
F#の非同期ワークフローを使って操作ができればいいなと思うのではないでしょうか。
F# Dataライブラリには様々なstatic引数を受け取るような
`WorldBankDataProvider` 型があります。
引数 `Asyncronous` を `true` にすると、
すべての操作を非同期的に実行するような型が
型プロバイダーによって生成されます：
*)

type WorldBank = WorldBankDataProvider<"World Development Indicators", Asynchronous=true>
WorldBank.GetDataContext()

(**
上のコードではデータソース(一般的に利用可能な指標のコレクション)の名前として
"World Development Indicators" を指定して、オプション引数 `Asynchronous` に
`true` を設定しています。
そうするとたとえば `School enrollment, tertiary (% gross)` などのプロパティが
`Async<(int * int)[]>` になります。
これはつまり非同期的に処理を始めることができ、最終的にデータを生成するような
操作であることを示しています。

### データを並列にダウンロードする

非同期バージョンの型プロバイダーのデモとして、
多数の国における大学進学率を並列にダウンロートしてみます。
まずデータコンテキストを作成した後に、
対象とする国を配列として定義します：
*)

let wb = WorldBank.GetDataContext()

// 対象とする国のリストを作成
let countries = 
 [| wb.Countries.``Arab World``
    wb.Countries.``European Union``
    wb.Countries.Australia
    wb.Countries.Brazil
    wb.Countries.Canada
    wb.Countries.Chile
    wb.Countries.``Czech Republic``
    wb.Countries.Denmark
    wb.Countries.France
    wb.Countries.Greece
    wb.Countries.``Low income``
    wb.Countries.``High income``
    wb.Countries.``United Kingdom``
    wb.Countries.``United States`` |]

(**
データを並列にダウンロードするには、非同期計算のリストを作成した後、
それらを `Async.Parallel` で組み合わせ、
(1つになった)計算を実行してすべてのダウンロード処理を行います：
*)

(*** define-output:chart2 ***)

[ for c in countries ->
    c.Indicators.``School enrollment, tertiary (% gross)`` ]
|> Async.Parallel
|> Async.RunSynchronously
|> Array.map Chart.Line
|> Chart.Combine

(**
上のコードは単に `Async.RunSynchronously` を使ってダウンロードするだけではなく、
それぞれのデータセットから1つの折れ線チャートを出力して、
各チャートを `Chart.Combine` で1つの総合チャートにしています。

*)

(*** include-it:chart2 ***)

(**
## 関連する記事

 * [Try F#: Data Science](http://www.tryfsharp.org/Learn/data-science) -
   Try F# の Data Science チュートリアルにはFreebase 型プロバイダーを使った
   様々な例があります。
 * [API リファレンス: WorldBankDataProvider 型プロバイダー](../../reference/fsharp-data-worldbankdataprovider.html)

*)
