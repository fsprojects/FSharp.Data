(** 
# F# Data: Freebase プロバイダー

[Freebase グラフデータベース](http://www.freebase.com) には
2,300万件以上の情報が格納されています。
この中からは書籍や映画、歴史的人物や出来事、化学元素など、
ありとあらゆる情報が互いに関連を持った形で見つけられます。

Freebase 型プロバイダーを使うと、こういった情報を手軽に取ってきて、
厳密に型付けされた状態でデータの山から宝物を見つけ出すことができます。

この型プロバイダーは [Try F#](http://www.tryfsharp.org) の
サイトにある「Data Science」のチュートリアルでも使われています。
そのためそちらも是非参考にしてみてください。
Visual Studio F#チームのブログではこの型プロバイダーについて
4つの記事が
[こちら](http://blogs.msdn.com/b/fsharpteam/archive/2012/09/21/the-f-3-0-freebase-type-provider-sample-integrating-internet-scale-data-sources-into-a-strongly-typed-language.aspx)
に掲載されています。
また、Don Syme氏による動画デモが
[こちら](http://skillsmatter.com/podcast/scala/an-informal-deep-dive-with-don-syme-the-freebase-type-provider)
で見られます。

## プロバイダーの基本

以下のコードでは `FSharp.Data.dll` ライブラリを(F# Interactive上で)読み込み、
`GetDataContext` メソッドを使ってFreebaseへの接続を初期化しています：
*)

#r "../../../../bin/FSharp.Data.dll"
open FSharp.Data

let data = FreebaseData.GetDataContext()

(**

### Freebaseデータの探索

さてこれで `data.` と入力して表示される自動コンプリートの
リストをチェックするとFreebaseのデータスキーマを探索することができます。
たとえば以下のコードでは化学元素(Chemical Elements)のデータを取得して
水素(Hydrogen)の詳細情報を表示させています：
*)

let elements = data.``Science and Technology``.Chemistry.``Chemical Elements``

let all = elements |> Seq.toList
printfn "見つかった元素の数: %d" (Seq.length all)

let hydrogen = elements.Individuals.Hydrogen
printfn "原子番号: %A" hydrogen.``Atomic number``

(**

### テストケースの生成

Freebaseには非常に多様なデータがあるため、
ありとあらゆる用途にこのデータベースを利用できます。
以下のコードではデータベース上の婚姻データを使って
それらしい名前をテスト用に生成しています。
まず2つの配列を用意します。
1つは(婚姻データを元にした)100件の名、そしてもう1つは
(Freebaseに登録されている名字リストを元にした)100件の姓を含んでいます：
*)

let firstnames = 
    data.Society.Celebrities.Celebrities
    |> Seq.truncate 100
    |> Seq.map (fun celeb -> celeb.Name.Split(' ').[0])
    |> Array.ofSeq

let surnames = 
    data.Society.People.``Family names``
    |> Seq.truncate 100
    |> Seq.map (fun name -> name.Name)
    |> Array.ofSeq

(**
それらしいテストケース用のデータを生成するためには
これらの配列からランダムに要素を抽出するヘルパ関数を用意して、
ランダム取り出した名と姓を連結します：
*)

let randomElement = 
    let random = new System.Random()
    fun (arr : string[]) -> arr.[random.Next(arr.Length)]

for i in 0 .. 10 do
  let name = 
    (randomElement firstnames) + " " +
    (randomElement surnames)
  printfn "%s" name

(**
## Freebaseのデータにクエリを投げる

先ほどの例ではFreebase 型プロバイダーから返されたコレクションを処理するために
`Seq` 関数を使っていました。
単純な場合にはこれでいいのですが、そうではなくデータをフィルタしたり
クエリを投げたりする必要がある場合にはこのままではうまくいきません。

そこでFreebase プロバイダーにはクエリをサポートするための機能が用意されています。
F# 3.0のLINQシンタックスで記述されたクエリは
(Freebaseで使われているクエリ言語である)MQLに変換されます。
これはつまりF# 3.0であれば自動補完のサポートも有効で、
厳密に型付けされた方法でクエリを作成できる上に、
少なくともMQLへと変換されたクエリはFreebaseのサーバー上で
効率的に処理されるというわけです。

以下では地球からの距離とあわせて恒星の名前を取得しています
(距離が不定のデータは除きます)：
*)

let astronomy = data.``Science and Technology``.Astronomy

query { for e in astronomy.Stars do 
        where e.Distance.HasValue
        select (e.Name, e.Distance) } 
      |> Seq.toList

(**
例では簡単のために、まず恒星に関するデータを `astronomy` という名前で定義しています。
また、クエリを実際に実行するために、最後に `Seq.toList` を呼び出す必要もあります。

以下のクエリは距離が分かっていて、地球に近い恒星のデータを返します：
*)

query { for e in astronomy.Stars do 
        where (e.Distance.Value < 4.011384e+18<_>)
        select e } 
      |> Seq.toList

(**
クエリ言語では単純な `where` や `select` 以外にも、
様々な演算子がサポートされています。
たとえば地球からの距離でソートした後、近い距離にある恒星を
上位10個取得することもできます：
*)

query { for e in astronomy.Stars do 
        sortBy e.Distance.Value
        take 10
        select e } 
      |> Seq.toList

(**

### Freebase クエリ演算子

F# 3.0のクエリ演算子の他に、
`FSharp.Data.FreebaseOperators` 名前空間には
`ApproximatelyMatches` `ApproximatelyOneOf` `ApproximateCount` `Count`
といったFreebase固有の演算子が定義されています。
これらはそれぞれ固有のMQL演算子に変換されます。

たとえば以下のコードでは `Count` と `ApproximateCount` を使って
歴代のアメリカ大統領の人数を数えています
(今回の場合、正確な人数を数えれば十分なので `ApproximateCount` は
あまり有効ではありません)：
*)

open FSharp.Data.FreebaseOperators

data.Society.Government.``US Presidents``.Count()
data.Society.Government.``US Presidents``.ApproximateCount()

(**
たとえば文字列を扱う場合には `ApproximatelyMatches` を使うとよいでしょう。
以下では特定の文字列に **およそ一致する** 書籍を検索しています：
*)

let topBooksWithNameContaining (s:string) = 
    query { for book in data.``Arts and Entertainment``.Books.Books do
            where (book.Name.ApproximatelyMatches s)
            take 10 
            select book.Name }
 
topBooksWithNameContaining "1984" |> Seq.toList

(**

## プロバイダーの詳細機能

Freebase 型プロバイダーには非常に多くの機能があるため、
それらのすべてをここで紹介することはできません。
一部の機能については既に紹介しましたが、より詳しいドキュメントについては
このページからリンクしてある記事を参照してください。
簡単に紹介すると以下のような機能があります：

* 多くのクエリは効率よくMQL言語へと変換されます。
  これらはデフォルトではクライアントサイドでは実行できません。
* 特定のサンプルについては `Individuals` 以下にあるオブジェクトの
  各コレクションから取得できます。
  この機能を使うとたとえば `Hydrogen` (水素)や `Bob Dylan` といった
  厳密な名前をつかって特定のデータをプログラム上で取得することができます。
* Freebase上のデータに対して大まかな数を計算したり、
  文字列を大まかに一致させるための独自の演算子がサポートされています。
* 画像のURLは `GetImages()` メソッドで取得できます。
  また、1番目の画像は `MainImage` プロパティで取得できます。
* Freebaseの日付によるスナップショットをサポートしています。
  つまり特定の日付におけるFreebaseデータベースの状態を参照できます
  (また、スキーマが変更されない限りはアプリケーションが
  壊れないということでもあります)。
* スキーマ情報をクライアントサイドでキャッシュするというオプションの機能を
  使うことによって、型を迅速かつ効果的にチェックできます。
* 測定単位をサポートします。
  たとえば化学元素の `Atomic mass` (原子質量)プロパティは自動的にSI単位である
  キログラムへと変換されます。
  この情報は測定単位を使ってF#の型システム上で静的に伝搬されます。
* 大量のFreebaseデータに対してクエリを投げたい場合、
  Googleで登録した後にカスタムAPIキーを取得します。
  このキーは型プロバイダーのstatic引数で指定できます。

### APIキーを指定する

FreebaseのAPIにはリクエスト数の制限があり、
当初はデバッグ用にある程度の割り当て分しか使うことが出来ません。
もしも(403) Forbiddenエラーが出たのであれば、それはつまり
リクエスト数の上限に達したということです。
Freebaseサービスを有効化するためにはAPIキーが必要です。
そうすれば1日あたり100,000件のリクエストを送信できるようになります。
F# Data ライブラリには `FreebaseDataProvider` という型があり、
この型にはAPIキーなど、いくつかのstatic引数を指定することができます：
*)

[<Literal>]
let FreebaseApiKey = "<freebaseを有効にするgoogle API キーをここに入力>"

//type FreebaseDataWithKey = FreebaseDataProvider<Key=FreebaseApiKey>
//let dataWithKey = FreebaseDataWithKey.GetDataContext()

(**
### MQLクエリのデバッグ

Freebase 型プロバイダーの仕組みを知りたい場合、
あるいはパフォーマンスの問題をデバッグしたい場合には
プロバイダーがFreebaseに送信するリクエストを確認するとよいでしょう。
そのためには `SendingRequest` イベントに登録します：
*)

data.DataContext.SendingRequest.Add (fun e -> 
  printfn "request: %A" e.RequestUri)

data.``Science and Technology``.Chemistry.
     ``Chemical Elements``.Individuals.Hydrogen.``Atomic mass``.Mass

(**
## 関連する記事

 * [Try F#: Data Science](http://www.tryfsharp.org/Learn/data-science) -
   Try F# の Data Science チュートリアルにはFreebase 型プロバイダーを使った
   様々な例があります。
 * [Visual F# Team Blog: Integrating Internet-Scale Data Sources into a Strongly Typed Language](http://blogs.msdn.com/b/fsharpteam/archive/2012/09/21/the-f-3-0-freebase-type-provider-sample-integrating-internet-scale-data-sources-into-a-strongly-typed-language.aspx) - Freebase 型プロバイダーの基本に関する4つのシリーズ記事です
 * [Don Syme氏によるデモ](http://skillsmatter.com/podcast/scala/an-informal-deep-dive-with-don-syme-the-freebase-type-provider) - An Informal Deep Dive With Don Syme: The Freebase Type Provider
 * [API リファレンス: FreebaseDataProvider 型プロバイダー](../../reference/fsharp-data-freebasedataprovider.html)
 * [API リファレンス: FreebaseOperators モジュール](../../reference/fsharp-data-freebaseoperators.html)

*)
