(** 
# F# Data: Apiary 型プロバイダー (試験用)

この記事では [apiary.io](http://www.apiary.io/) でドキュメント化されたRESTサービスを
簡単に呼び出すことができるようにするような、試験的な型プロバイダーについて
説明します。
Apiary.ioのサービスを使うと、単純なMarkdownに似た言語を使って
APIの **ひな形** を記述するだけで独自のREST APIを作成できます。

ドキュメントでは、RESTリクエストとそれに対して期待されるレスポンスの構造を
規定することが重要です。
たとえば [F# Snippets API用のドキュメント](http://docs.fssnip.apiary.io/) では
`/1/snippet/{id}` という形式のURLに対する
(適切なcontent-typeを含んだ) `GET` リクエストに対しては、
以下のようなJSONデータが返されます：

    [lang=text]
    GET /1/snippet/{id}
    < 200
    < Content-Type: application/json
    { 
      "title":"All subsets of a set", "author": "Tomas Petricek",
      "description": "A function that returns all subsets of a specified set.", 
      "formatted":"<pre class=\"fssnip\">(...)</pre>",
      "published": "1 years ago",
      "tags": ["set","sequences","sequence expressions","subset"]
    }

`ApiaryProvider` ではこのような情報を使ってREST APIの構造が推測されます。
また、サンプルのレスポンスから結果の型を推測する際には
[JSON 型プロバイダー](../library/JsonProvider.html)
を再利用しています。

## プロバイダーの基本

この型プロバイダーは試験用であり、Apiary.ioでホストされているすべてのAPIで
機能するわけではありません。
型プロバイダーは `FSharp.Data.Experimental.dll` にあります：

*)

#r "../../../../bin/FSharp.Data.Experimental.dll"
open System.IO
open FSharp.Data

(**
以下の2つのセクションは [themoviedb.org](http://themoviedb.org) から
受信したデータを使った型プロバイダーのデモになっています。
APIのドキュメントは [こちら](http://docs.themoviedb.apiary.io/) にあります。

### 俳優の検索

APIを表す型を取得するには、 `ApiaryProvider` のstatic引数にAPIの名前を指定します。
今回の場合、APIの名前は "themoviedb" です。
以下のコードでは(稼働中の)REST APIがあるURLを指定しつつ、
型のインスタンスを即座に作成しています：
*)

let db = new ApiaryProvider<"themoviedb">("http://api.themoviedb.org")
db.AddQueryParam("api_key", "6ce0ef5b176501f8c07c634dfa933cff")

(**
`ApiaryProvider` を呼び出すためには [themoviedb.org](http://www.themoviedb.org/)
に登録して独自のAPIキーを取得した後、上にある `AddQueryParam` メソッドで
キーを指定する必要があります。
このメソッドには実行時にすべてのリクエストに対して追加されるような引数を
指定することもできます。
(コンパイル時にはすべての情報がApiary.ioから取得できるため、APIキーは不要です)。

そして `db.` と入力すると、APIに対してどのような型が生成されたのかが確認できます。
たとえば `db.Search` モジュールには映画や俳優などの情報を検索するための機能が
多数用意されていることがわかります：
*)

let res = db.Search.Person(query=["query","craig"])
printfn "%d ページ中 %d ページを表示中。" res.Page res.TotalPages

for person in res.Results do
  printfn "%d %s" person.Id person.Name

(**
一部のAPIはドキュメントから完全に推測することができません。
この場合はオプション引数 `query` や `headers` を使うと
リクエストにプロパティを追加することができます。
先のコードでは引数 `query` を指定して、
リクエスト先のURLに `&query=craig` を追加しています。

結果には返されたリストに関する情報(およびページ情報)を含んだレコードと、
結果のコレクションを含んだ `res.Results` というフィールドが含まれます。
おわかりのように、すべての結果は静的に型付けされているため、
簡単にアクセスできるようになっています。

### エンティティを取得する

`ApiaryProvider` は単純なRESTメソッドとRESTエンティティを区別します。
エンティティとは俳優などのような何らかのオブジェクトに関する情報を
取得するために呼び出すことになる一連のAPIのことです。
これはたとえば `/3/person/{id}` (人物に関する情報を取得するメソッド)や
`/3/person/{id}/credists` (俳優が演じた映画のリストを取得するメソッド)といった
一連のRESTメソッドで構成されます。

エンティティは `GetXyZ` メソッドで取得できるオブジェクトとして公開されます。
取得したオブジェクトには(出演リストのような)さらに詳細な情報を
取得するためのメソッドが含まれます。

以下のコードではDaniel Craig氏に関連する情報を表示しています：
*)

let person = db.Person.GetPerson("8784")
printfn "Born: %s" person.PlaceOfBirth

let credits = person.MovieCredits()
for cast in credits.Cast do
  printfn "%s (as %s)" cast.Title.String.Value cast.Character

(**
## 非同期リクエストを行う

`ApiaryProvider` のすべてのメソッドには同期(ブロッキング)および
非同期(ノンブロッキング)バージョンがあります。
アプリケーションを作成している場合には、スレッドをブロックしてしまわないように
常に非同期バージョンを使うようにすべきでしょう。
(同期バージョンはスクリプトをインタラクティブに作成している場合に有効です)

非同期バージョンは同期バージョンの末尾に `Async` がつけられた名前になっていて、
結果の型もF#の `Async<'T>` 型になっています。
以下のコードではバットマン(Batman)シリーズの映画を非同期的に検索しています：

*)
let printBatman () = async { 
  let! res = db.Search.AsyncMovie(query=["query","batman"])
  for movie in res.Results do
    printfn " - %s" movie.Title }

printBatman() |> Async.Start

(**
## 異なるREST APIを使用する

既に説明した通り、 `ApiaryProvider` にはAPIの名前を特定するstatic引数を指定します。
Apiaryのサービスを使って独自のAPIに対するドキュメントを作成した場合には、
自身のAPIの名前をstatic引数に指定するだけでAPIを呼び出せるようになります
(ただしドキュメントが正しく、かつ `ApiaryProvider` が期待する
形式になっている場合に限ります)。

以下のコードでは別のAPIを使っています。
今回は [F# Snippets](http://www.fssnip.net) のREST APIを使って、
このサイトで新しく共有されたスニペットの一覧を取得しています：
*)

// 直近に投稿されたスニペットを取得
let fs = new ApiaryProvider<"fssnip">("http://api.fssnip.net/")
let snips = fs.Snippet.List()
for snip in snips do 
  printfn "%s" snip.Title

// 特定のスニペットに関する情報を表示
let snip = fs.Snippet.GetSnippet("fj")
snip.Tags

(**

## まとめ

この記事ではF# Dataの試験用機能である `ApiaryProvider` のデモを紹介しました。
このプロバイダーは [apiary.io](http://apiary.io) から取得できるドキュメントから
REST APIの構造を推測して公開するため、F#プログラマは型付けされた方法で
データを処理できるようになります。

現時点ではこの型プロバイダーだけが試験用とされています。
このプロバイダーは自身のAPIで機能するかもしれませんし、しないかもしれません。
もしもプロバイダーの改善に興味がある、あるいはその他のREST APIドキュメント形式を
サポートできるようにしたいと思う場合には、是非
[F# Dataに貢献する](../contributing.html) のページを参照してください！

## 関連する記事

 * [F# Data: XML Type Provider](../library/XmlProvider.html) -
   型安全な方法でXMLデータにアクセスする機能を持った
   F# 型プロバイダーについて説明しています。
 * [F# Data: JSON Type Provider](../library/JsonProvider.html) -
   JSONドキュメントを処理するような、より単純な型プロバイダーについて
   説明しています。(HTTPリクエスト明示的に行っている場合には)こちらの
   安定した機能を使うとよいでしょう。
 * [F# Data: HTTP Utilities](../library/Http.html) -
   HTTPリクエストを明示的に行いたい場合には
   `Http` 型を使うと簡単でしょう。
*)
