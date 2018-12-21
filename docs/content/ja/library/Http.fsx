(**
# F# Data: HTTP ユーティリティ

.NETライブラリにはHTTP Webリクエストを作成して送信するための
強力なAPIが用意されています。
具体的には単純な `WebClient` 型([MSDN][1] を参照)や、
より柔軟な機能を持った `HttpWebRequest` ([MSDN][2] を参照)があります。
しかしこれらはいずれも、HTTP POSTデータや追加のヘッダなど、
特定の引数を指定した単純なHTTPリクエストを手軽に実行することには向いていません。

F# Dataライブラリにはオーバーロードを持った4つのメソッドを含む、
単純な `Http` 型があります。
`RequestString` と `AsyncRequestString` メソッドは単純なリクエストを作成して、
同期的あるいは非同期的にリクエストを送信できます。
また `Request` とその非同期バージョン `AsyncRequest` を使うと
バイナリファイルを送信したり、ステータスコードや応答URL、
受信ヘッダやクッキーなど、レスポンスの詳細情報を知ることができます。

 [1]: http://msdn.microsoft.com/en-us/library/system.net.webclient.aspx
 [2]: http://msdn.microsoft.com/en-us/library/system.net.httpwebrequest.aspx

この型を使うには、まず(F# Interactiveの場合は) `#r` あるいはプロジェクトで
参照の追加を行ってライブラリを参照する必要があります。
この型は `FSharp.Data` 名前空間にあります：
*)

#r "../../../../bin/lib/net45/FSharp.Data.dll"
open FSharp.Data

(**
## 単純なリクエストの送信

特定のWebページをダウンロードするような単純なHTTP (GET) リクエストを送信するには、
`Http.RequestString` あるいは `Http.AsyncRequestString` にたった1つの引数を
指定するだけです：
*)

// Webサイトのコンテンツをダウンロード
Http.RequestString("http://tomasp.net")

// Webサイトから非同期的にダウンロード
async { let! html = Http.AsyncRequestString("http://tomasp.net")
        printfn "%d" html.Length }
|> Async.Start

(** 
`AsyncRequestString` は `RequestString` と全く同じ方法で使うことができるため、
以降では `RequestString` だけを使います。

## クエリ引数とヘッダ

クエリ引数は引数を含んだURLを用意するか、
`query` という省略可能な引数を使って指定できます。
以下では明示的にGETメソッドであることを指定していますが、
省略した場合には自動的にGETになります：
*)

Http.RequestString("http://httpbin.org/get", query=["test", "foo"], httpMethod="Get")

(** 
同じように、省略可能な引数 `headers` を使うと追加のヘッダを指定できます。
このコレクションには独自のヘッダを追加できますが、
Acceptヘッダのような標準的なヘッダも含まれています
( `HttpWebRequest` の場合には特定のプロパティに設定する必要があります)。

以下では [The Movie Database](http://www.themoviedb.org) APIを使って
「batman」という単語を検索しています。
サンプルを実行するためには登録をしてAPIキーを取得する必要があります：
*)

(*** do-not-eval ***)

// http://www.themoviedb.org 用のAPIキー
let apiKey = "<登録してキーを取得してください>"

// HTTP Webリクエストを実行
Http.RequestString
  ( "http://api.themoviedb.org/3/search/movie", httpMethod = "GET",
    query   = [ "api_key", apiKey; "query", "batman" ],
    headers = [ "Accept", "application/json" ])

(**
このライブラリでは(先の例のように)単純かつチェックのない文字列ベースのAPIがサポートされていますが、
スペルミスを防ぐことができるよう、あらかじめ定義されたヘッダ名を使うこともできます。
名前付きヘッダは `HttpRequestHeaders` (および `HttpResponseHeaders`) モジュールで定義されているため、
`HttpRequestHeaders.Accept` のようにフルネームを指定するか、
以下の例のようにモジュールをオープンしておいてから `Accept` のような短い名前で指定することになります。
同様に、 `HttpContentTypes` 列挙体には既知のコンテンツタイプ名が定義されています：
*)

open FSharp.Data.HttpRequestHeaders

(*** do-not-eval ***)

// HTTP Webリクエストを実行
Http.RequestString
  ( "http://api.themoviedb.org/3/search/movie",
    query   = [ "api_key", apiKey; "query", "batman" ],
    headers = [ Accept HttpContentTypes.Json ])

(**
## 特別な情報の取得

先のコードスニペットでは、正しいAPIキーを指定しなかった場合には(401)認証エラーが返され、
例外が発生することに注意してください。
ただし `WebRequest` を直接使用した場合とは異なり、例外メッセージには返されたコンテンツの情報も含まれるため、
サーバーが特別な情報を返すような場合であってもF# Interactiveで簡単にデバッグできます。

また `silentHttpErrors` 引数を設定することによって例外を無効化することもできます:
*)

(*** define-output:request ***)

Http.RequestString("http://api.themoviedb.org/3/search/movie", silentHttpErrors = true)

(** この結果は以下のようになります： *)
(*** include-it:request ***)

(** この場合にはHTTPステータスコードを確認すればよいため、実際にレスポンスとして返されたデータと
エラーメッセージとを混同することもないでしょう。
ステータスコードやレスポンスヘッダー、返されたクッキー、レスポンスURL
(リダイレクトされた場合にはリクエストを投げたURLとは別の値が返されることがあります)など、
レスポンスの詳細を確認したい場合には `RequestString` の代わりに `Request` メソッドを使用します:

*)

let response = Http.Request("http://api.themoviedb.org/3/search/movie", silentHttpErrors = true)

// レスポンスの詳細を確認する
response.Headers
response.Cookies
response.ResponseUrl
response.StatusCode

(**
## リクエストデータの送信

HTTP POSTデータを含んだPOSTリクエストを作成したい場合は、
オプション引数 `body` に追加データを指定するだけです。
この引数は3つのケースを持った判別共用体 `HttpRequestBody` 型です：

* `TextRequest` はリクエストの本体で文字列を送信するために使用します
* `BinaryUpload` はリクエストにバイナリデータを含めて送信する場合に使用します
* `FormValues` は特定のフォームの値を名前と値のペアとして
  送信するために使用します

bodyを指定した場合、引数 `httpMethod` には自動的に `Post` が設定されるようになるため、
明示的に指定する必要はありません。

以下ではリクエストの詳細を返すサービス
[httpbin.org](http://httpbin.org)
を使っています：
*)

Http.RequestString("http://httpbin.org/post", body = FormValues ["test", "foo"])

(**
デフォルトでは `Content-Type` ヘッダには `HttpRequestBody` に指定した値に応じて
`text/plain` `application/x-www-form-urlencoded` `application/octet-stream`
のいずれかが設定されます。
ただしオプション引数 `headers` を使ってヘッダのリストに `content-type` を
追加することでこの動作を変更できます：
*)

Http.RequestString
  ( "http://httpbin.org/post", 
    headers = [ ContentType HttpContentTypes.Json ],
    body = TextRequest """ {"test": 42} """)

(**
## リクエスト間でクッキーを管理する

リクエスト間でクッキーを管理したい場合には、
引数 `cookieContainer` を指定します。
以下では `HttpRequest` クラスに関するMSDNドキュメントをリクエストしています。
そうするとF#ではなくC#のコードスニペットが表示されます：
*)

// 特定のクラスに関するドキュメント用のURLを用意する
let msdnUrl className = 
  let root = "http://msdn.microsoft.com"
  sprintf "%s/en-gb/library/%s.aspx" root className

// ページを取得してF#コードを検索する
let docInCSharp = Http.RequestString(msdnUrl "system.web.httprequest")
docInCSharp.Contains "<a>F#</a>"

(**

別のMSDNのページに移動してF#のコード例をクリックしてから
`HttpRequest` クラスのドキュメントに戻ってくると、
同じ `cookieContainer` を保持し続けている限りはF#のコードが
表示されるようになります：
*)

open System.Net
let cc = CookieContainer()

// 言語を切り替えるためのリクエストを送信
Http.RequestString
  ( msdnUrl "system.datetime", 
    query = ["cs-save-lang", "1"; "cs-lang","fsharp"], 
    cookieContainer = cc) |> ignore

// 再度ドキュメントをリクエストしてF#のコードを検索
let docInFSharp = 
  Http.RequestString
    ( msdnUrl "system.web.httprequest", 
      cookieContainer = cc )
docInFSharp.Contains "<a>F#</a>"

(**
## バイナリデータの送信


`RequestString` メソッドでは常に `string` としてレスポンスが返されます。
しかし `Request` メソッドの場合にはレスポンスの `content-type` ヘッダに応じて
`HttpResponseBody.Text` または `HttpResponseBody.Binary` が返されます：
*)

let logoUrl = "https://raw.github.com/fsharp/FSharp.Data/master/misc/logo.png"
match Http.Request(logoUrl).Body with
| Text text -> 
    printfn "Got text content: %s" text
| Binary bytes -> 
    printfn "Got %d bytes of binary content" bytes.Length

(**
## HTTPリクエストのカスタマイズ

ライブラリに元々備えられていないような機能を使用したい場合には、
`customizeHttpRequest` 引数を設定することになります。
この引数には `HttpWebRequest` を変更するための関数を指定します。

たとえばリクエストにクライアント証明書を追加したいとします。
そのためにはまず `System.Security.Cryptography` 以下の
`X509Certificates` 名前空間をオープンして、
`X509ClientCertificate2` の値を用意し、
この値をリクエストの `ClientCertificates` リストに追加します。

証明書が `myCertificate.pfx` に格納されているとすると、
以下のようなコードになります：
*)

(*** do-not-eval ***)

open System.Security.Cryptography.X509Certificates

// ローカルファイルから証明書を読み取り
let clientCert = 
  new X509Certificate2(".\myCertificate.pfx", "password")

// 証明書付きでリクエストを送信
Http.Request
  ( "http://yourprotectedresouce.com/data",
    customizeHttpRequest = fun req -> req.ClientCertificates.Add(clientCert) |> ignore; req)

(**
## 関連する記事

 * [API リファレンス: HTTP クラス](../../reference/fsharp-data-http.html)
 * [API リファレンス: HttpMethod モジュール](../../reference/fsharp-data-httpmethod.html)
 * [API リファレンス: HttpRequestHeaders モジュール](../../reference/fsharp-data-httprequestheaders.html)
 * [API リファレンス: HttpResponseHeaders モジュール](../../reference/fsharp-data-httpresponseheaders.html)
 * [API リファレンス: HttpContentTypes モジュール](../../reference/fsharp-data-httpcontenttypes.html)
 * [API リファレンス: HttpRequestBody 判別共用体](../../reference/fsharp-data-httprequestbody.html)
 * [API リファレンス: HttpResponse レコード](../../reference/fsharp-data-httpresponse.html)
 * [API リファレンス: HttpResponseBody 判別共用体](../../reference/fsharp-data-httpresponsebody.html)
 * [API リファレンス: HttpResponseWithStream レコード](../../reference/fsharp-data-httpresponsewithstream.html)

*)
