(** 
# F# Data: JSON 型プロバイダー

このドキュメントではJSON 型プロバイダーを使って
静的に型付けされた方法でJSONファイルを扱う方法について説明します。
まず型が推測される方法について説明した後、
このプロバイダーを使って世界銀行やTwitterから受け取ったデータをパースするデモを
紹介します。

JSON 型プロバイダーにはJSONドキュメントを静的に型付けされた方法で
アクセスするための機能があります。
このプロバイダーはサンプルとなるドキュメント
(あるいは複数のサンプルをJSON配列として含むようなドキュメント)を
入力として受け取ります
生成された型を使うと、同じ構造のファイルを読み取ることができます。
読み取ったファイルがサンプルとは異なる構造になっている場合には
実行時エラーが発生します(ただし存在しない要素などにアクセスした場合に限ります)。

## プロバイダーの基本

型プロバイダーは `FSharp.Data.dll` アセンブリ内にあります。
このアセンブリが `../../../../bin` ディレクトリにあるとすると、
以下のようにするとF# Interactive上でアセンブリをロードできます：
*)

#r "../../../../bin/lib/net45/FSharp.Data.dll"
open FSharp.Data

(**
### サンプルからの型の推測

`JsonProvider<...>` は `string` 型のstatic引数を1つとります。
この引数にはサンプルとなる文字列あるいはファイル
(カレントディレクトリからの相対パスか、 `http` `https` で
アクセスできるオンライン上のファイル)の **どちらか一方** を指定します。
この引数の値があいまいになることはほとんどありえないでしょう。

以下のコードでは小さなJSON文字列をプロバイダーに渡しています：
*)

type Simple = JsonProvider<""" { "name":"John", "age":94 } """>
let simple = Simple.Parse(""" { "name":"Tomas", "age":4 } """)
simple.Age
simple.Name

(**
生成された型には `Age` という `int` 型のプロパティと、
`Name` という `string` 型のプロパティがあることがわかります。
つまり型プロバイダーがサンプルから適切な型を推測して、
それらを(標準の命名規則に従ってパスカル記法の名前の)プロパティとして
公開しているというわけです。

### 数値型に対する推測

先ほどの例では、サンプルファイルには単なる整数が含まれていたので、
プロバイダーも `int` 型と推測しました。
しかし場合によってはサンプルのドキュメント(あるいはサンプルのリスト)にある型とは
厳密に一致しないことがあります。
たとえば以下のように integer と float が混ざったリストがあるとします：
*)

type Numbers = JsonProvider<""" [1, 2, 3, 3.14] """>
let nums = Numbers.Parse(""" [1.2, 45.1, 98.2, 5] """)
let total = nums |> Seq.sum

(**
サンプルがコレクションの場合、型プロバイダーはサンプル内のすべての値を
格納できるような型を生成します。
今回の場合は一部の値が integer ではないため、最終的に `decimal` 型になります。
型プロバイダーで一般的にサポートされている型は
`int` `int64` `decimal` `float` です
(また、この順序で推測されます)。

その他のプリミティブ型の組み合わせになっている場合には型を1つに限定できません。
たとえばリストに数値と文字列があるような場合です。
この場合、プロバイダーはいずれか一方の型に一致するような値を取得できるように
2種類のメソッドを生成します：
*)

type Mixed = JsonProvider<""" [1, 2, "hello", "world"] """>
let mixed = Mixed.Parse(""" [4, 5, "hello", "world" ] """)

mixed.Numbers |> Seq.sum
mixed.Strings |> String.concat ", "

(**
このように、 `Mixed` 型には `Numbers` と `Strings` という、
それぞれコレクション内の `int` か `string` の値しか返さないメソッドが
定義されていることがわかります。
つまり型セーフな状態でアクセスできるものの、
元の順序通りには値を取得することはできません
(値の順序が重要であれば、 `mixed.JsonValue` プロパティで `JsonValue` を取得した後、
[`JsonValue` のドキュメント](JsonValue.html) で説明している方法で
処理するとよいでしょう)。

### レコード型の推測

次はレコード型を含むJSONドキュメントをサンプルにしてみましょう。
以下では2つのレコードを使っています。
1つには `name` と `age` 、もう1つには `name` だけがあります。
もしもプロパティが値無しになる場合、型プロバイダーはオプション型として推測します。

また、スキーマと同じテキストを実行時にも使いたい場合には
`GetSamples` メソッドを使います：
*)

type People = JsonProvider<""" [{ "name":"John", "age":94 }, { "name":"Tomas" }] """>

for item in People.GetSamples() do 
  printf "%s " item.Name 
  item.Age |> Option.iter (printf "(%d)")
  printfn ""

(**
`items` 用に推測された型は(無名の)JSONエンティティのコレクションで、
それぞれには `Name` と `Age` というプロパティがあります。
`Age` はサンプルデータ内のすべてのレコードで使われているわけでは無いため、
`option<int>` 型と推測されています。
また、上のコードでは値が利用できる場合に限って表示できるように
`Option.iter` を使っています。

前回の例では各プロパティの値の型は共通していました。
`Name` プロパティは `string` で、 `Age` プロパティは数値でした。
しかしレコードのプロパティに複数の異なる型が使われていた場合にはどうなるでしょう？
この場合、型プロバイダーは以下のように動作します：
*)

type Values = JsonProvider<""" [{"value":94 }, {"value":"Tomas" }] """>

for item in Values.GetSamples() do 
  match item.Value.Number, item.Value.String with
  | Some num, _ -> printfn "数値: %d" num
  | _, Some str -> printfn "テキスト: %s" str
  | _ -> printfn "何かその他の値です！"

(**
このように、 `Value` プロパティは数値か文字列になります。
型プロバイダーは取り得る型それぞれをオプション型のプロパティとして定義します。
そのため、 `option<int>` と `option<string>` の値に対する
単純なパターンマッチを使ってそれぞれの値を取得できます。
この方法は多種多様なデータを含む配列を処理する方法に似ています。

ここではサンプルがJSONのリストになっているために `GetSamples` を使っている
という点に注意してください。
もしもサンプルがJSONオブジェクトの場合には `GetSample` を使います。

## 世界銀行のデータの読み取り

では型プロバイダーを使って実際のデータを処理してみましょう。
[世界銀行](http://data.worldbank.org) (World Bank)から
受信したデータセットを使います。
このデータは以下のような構造になっています：

    [lang=js]
    [ { "page": 1, "pages": 1, "total": 53 },
      [ { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":null,"decimal":"1","date":"2000"},
        { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":"16.6567773464055","decimal":"1","date":"2010"} ] ]

2つの要素を含んだ1つの配列がレスポンスとして返されます。
1つめの要素にはレスポンスに関する一般的な情報(ページ数、総ページ数など)があり、
2つめの要素には実際のデータ点を表すような別の配列があります。
それぞれのデータ点について何らかの情報や実際の `value` (値)を取得できます。
なお `value` は(何らかの理由により)文字列として返されていることに注意してください。
引用符で囲われているため、型プロバイダーはこの型を `string` と推測します
(そして手動で変換する必要があります)。

以下では [`data/WorldBank.json`](../../data/WorldBank.json) をサンプルファイルにして
型を生成した後、同じファイルを読み込んでいます：
*)

type WorldBank = JsonProvider<"../../data/WorldBank.json">
let doc = WorldBank.GetSample()

(**
型プロバイダーのサンプル用の引数と、 `Load` メソッドの引数にはいずれも
Web上のURLを指定して直接データを読み取ることができることに注意してください。
また、非同期バージョンの `AsyncLoad` メソッドもあります：
*)

let docAsync = WorldBank.AsyncLoad("http://api.worldbank.org/country/cz/indicator/GC.DOD.TOTL.GD.ZS?format=json")

(**
`doc` は多種多様な型を含んだ配列なので、
それぞれレコードや配列を取得できるようなプロパティを持った型が生成されます。
なお型プロバイダーは1つのレコードと1つの配列だけが含まれているものとして
型を推測していることに注意してください(以前の例では
複数の数値と複数の文字列が配列中にありました)。
この場合はメソッドは生成されず、
単に `Record` や `Array` というプロパティが生成されます。
したがって以下のようにすればデータセットを表示できます：
*)

// 一般的な情報を表示
let info = doc.Record
printfn "%d ページ中 %d ページ目を表示中。総レコード数 %d" 
  info.Pages info.Page info.Total

// すべてのデータ点を表示
for record in doc.Array do
  record.Value |> Option.iter (fun value ->
    printfn "%d: %f" record.Date value)

(**
データ点を表示する場合、一部で値無しになっていることがあります
(入力データでは適切な値の代わりに `null` という値になっています)。
ここでもまた多種多様な型になっています。
つまり型は `Number` か、あるいは( `null` を表すような)別の型のどちらかです。
したがって `record.Value` には(値が数値であれば) `Number` プロパティがあり、
このプロパティを使えばデータ点が有効である場合に限り結果を表示できます。

## Twitterのストリームをパースする

次の例として、 [Twitter API](http://dev.twitter.com/) から返される
ツイートをパースする例を紹介しましょう。
ツイートには非常に多種多様なデータが含まれているため、
単に文字列を1つ指定するのではなく、入力 **リスト** を使って
型を推測させることにします。
そのため、 `SampleIsList=true` という、サンプルが **サンプルのリスト**
になっていることを明示するオプションを指定した状態で
[`data/TwitterStream.json`](../../data/TwitterStream.json)
ファイルを使います：

*)

type Tweet = JsonProvider<"../../data/TwitterStream.json", SampleIsList=true>
let text = (*[omit:(omitted)]*)""" {"in_reply_to_status_id_str":null,"text":"\u5927\u91d1\u6255\u3063\u3066\u904a\u3070\u3057\u3066\u3082\u3089\u3046\u3002\u3082\u3046\u3053\u306e\u4e0a\u306a\u3044\u8d05\u6ca2\u3002\u3067\u3082\uff0c\u5b9f\u969b\u306b\u306f\u305d\u306e\u8d05\u6ca2\u306e\u672c\u8cea\u3092\u6e80\u55ab\u3067\u304d\u308b\u4eba\u306f\u9650\u3089\u308c\u3066\u308b\u3002\u305d\u3053\u306b\u76ee\u306b\u898b\u3048\u306a\u3044\u968e\u5c64\u304c\u3042\u308b\u3068\u304a\u3082\u3046\u3002","in_reply_to_user_id_str":null,"retweet_count":0,"geo":null,"source":"web","retweeted":false,"truncated":false,"id_str":"263290764686155776","entities":{"user_mentions":[],"hashtags":[],"urls":[]},"in_reply_to_user_id":null,"in_reply_to_status_id":null,"place":null,"coordinates":null,"in_reply_to_screen_name":null,"created_at":"Tue Oct 30 14:46:24 +0000 2012","user":{"notifications":null,"contributors_enabled":false,"time_zone":"Tokyo","profile_background_color":"FFFFFF","location":"Kodaira Tokyo Japan","profile_background_tile":false,"profile_image_url_https":"https:\/\/si0.twimg.com\/profile_images\/1172376796\/70768_100000537851636_3599485_q_normal.jpg","default_profile_image":false,"follow_request_sent":null,"profile_sidebar_fill_color":"17451B","description":"KS(Green62)\/WasedaUniv.(Schl Adv Sci\/Eng)\/SynBio\/ChronoBio\/iGEM2010-2012\/Travel\/Airplane\/ \u5bfa\u30fb\u5ead\u3081\u3050\u308a","favourites_count":17,"screen_name":"Merlin_wand","profile_sidebar_border_color":"000000","id_str":"94788486","verified":false,"lang":"ja","statuses_count":8641,"profile_use_background_image":true,"protected":false,"profile_image_url":"http:\/\/a0.twimg.com\/profile_images\/1172376796\/70768_100000537851636_3599485_q_normal.jpg","listed_count":31,"geo_enabled":true,"created_at":"Sat Dec 05 13:07:32 +0000 2009","profile_text_color":"000000","name":"Marin","profile_background_image_url":"http:\/\/a0.twimg.com\/profile_background_images\/612807391\/twitter_free1.br.jpg","friends_count":629,"url":null,"id":94788486,"is_translator":false,"default_profile":false,"following":null,"profile_background_image_url_https":"https:\/\/si0.twimg.com\/profile_background_images\/612807391\/twitter_free1.br.jpg","utc_offset":32400,"profile_link_color":"ADADAD","followers_count":426},"id":263290764686155776,"contributors":null,"favorited":false} """(*[/omit]*)
let tweet = Tweet.Parse(text)

printfn "%s (%d 回リツイートされました)\n:%s"
  tweet.User.Value.Name tweet.RetweetCount.Value tweet.Text.Value

(**
`Tweet` 型を作成した後に、サンプルのツイートを1つパースした後、
ツイートに関する詳細を表示しています。
実際に試してみるとわかりますが、 `tweet.User` プロパティは
オプション型として推測されているため
(つまり作者のいないツイートもあり得るということ？)、
常に `Value` プロパティから値が取得できるとは限りません。
同じように `RetweetCount` と `Text` プロパティも値無しになることがあるため、
上のコードは安全ではないことに注意してください。

## GitHubのIssuesを取得および作成する

この例ではJSONを作成するだけでなく、それを実際に使用する方法を紹介します。
まず FSharp.Data リポジトリのオープンされているIssuesのうちで
直近の5つを取得してみましょう。

*)

// GitHub.json downloaded from https://api.github.com/repos/fsharp/FSharp.Data/issues to prevent rate limit when generating these docs
type GitHub = JsonProvider<"../../data/GitHub.json">

let topRecentlyUpdatedIssues = 
    GitHub.GetSamples()
    |> Seq.filter (fun issue -> issue.State = "open")
    |> Seq.sortBy (fun issue -> System.DateTime.Now - issue.UpdatedAt)
    |> Seq.truncate 5

for issue in topRecentlyUpdatedIssues do
    printfn "#%d %s" issue.Number issue.Title

(**

次に新しいIssueを作成してみます。
GitHubのドキュメント http://developer.github.com/v3/issues/#create-an-issue
を見てみると、以下のようなJSON値をポストすればいいことがわかります：

*)

[<Literal>]
let issueSample = """
{
  "title": "Found a bug",
  "body": "I'm having a problem with this.",
  "assignee": "octocat",
  "milestone": 1,
  "labels": [
    "Label1",
    "Label2"
  ]
}
"""

(**

このJSONデータは先ほどAPIを呼び出して取得した
Issueそれぞれに対応するものとは異なります。
そのため、このサンプルデータを元にして新しい型を定義し、
そのインスタンスを作成してリクエストをPOSTします：

*)

(*** do-not-eval ***)

type GitHubIssue = JsonProvider<issueSample, RootName="issue">

let newIssue = GitHubIssue.Issue("Test issue",
                                 "This is a test issue created in F# Data documentation", 
                                 assignee = "",
                                 labels = [| |], 
                                 milestone = 0)
newIssue.JsonValue.Request "https://api.github.com/repos/fsharp/FSharp.Data/issues"

(**
## 関連する記事

 * [F# Data: JSON パーサーおよびリーダー](JsonValue.html) -
   JSONの値を動的に処理する方法についての説明があります。
 * [API リファレンス: JsonProvider 型プロバイダー](../../reference/fsharp-data-jsonprovider.html)
 * [API リファレンス: JsonValue 判別共用体](../../reference/fsharp-data-jsonvalue.html)

*)
