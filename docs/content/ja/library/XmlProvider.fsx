(** 
# F# Data: XML 型プロバイダー

このドキュメントではXML型プロバイダーを使用して、静的に型付けされた
方法でXMLドキュメントにアクセスする方法について説明します。
まず、XMLドキュメントの構造が推測される方法について説明した後に、
XML型プロバイダーを使用してRSSフィードを解析するデモを紹介します。

ML型プロバイダーを使用すると、静的に型付けされた方法でXMLドキュメントに
アクセスできます。
このプロバイダーはサンプルとなるドキュメントを入力として受け取ります
(あるいはサンプルとして使用されるような複数の子ノードを持った
ルートXMLノードを含むドキュメントを受け取ります)。
そして生成された型を使用すると、サンプルドキュメントと同じ構造のファイルを
読み取ることができるようになります。
ファイルの構造がサンプルと異なる場合には実行時エラーが発生します
(ただし存在しない要素にアクセスしようとした場合に限られます)。

## プロバイダーの基本

型プロバイダーは `FSharp.Data.dll` アセンブリ内にあります。
このアセンブリが `../../../../bin` ディレクトリにあると仮定すると、
F# Interactive内で読み込むためには以下のようにします
(なおこの型プロバイダーは内部で `XDocument` を使用しているため、
`System.Xml.Linq` への参照も必要になる点に注意してください)：
*)

#r "../../../../bin/lib/net45/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
open FSharp.Data

(**
### サンプルからの型の推論

`XmlProvider<...>` には `string` 型の静的パラメータを1つ指定します。
このパラメータには **サンプルとなるXML文字列** または
**サンプルファイル** のいずれかを指定します
(カレントディレクトリからの相対パスか、 **http** または **https** 経由で
アクセス可能なファイル名を指定します)。
パラメータの値があいまいで認識できないようなケースはほとんど無いでしょう。

以下のサンプルではルートノードに2つの属性を持ったXMLドキュメントを
読み取ることができるような型を生成しています：
*)

type Author = XmlProvider<"""<author name="Paul Feyerabend" born="1924" />""">
let sample = Author.Parse("""<author name="Karl Popper" born="1902" />""")

printfn "%s (%d)" sample.Name sample.Born

(**
型プロバイダーによって生成された型 `Author` には、XMLドキュメントの
ルート要素にある2つの属性と同じ名前のプロパティがあります。
プロパティの型はサンプルとして指定したドキュメントの値から推測されます。
今回の場合、 `Name` プロパティは `string` 型で
`Born` プロパティは `int` 型です。

XMLは非常に柔軟な形式なので、同じドキュメントを異なる形式で表現できます。
具体的には属性を使用する代わりに、値を直接含むようなネストされたノードとして
表現できます( `<author>` 以下に `<name>` および `<born>` をネストさせます)
*)

type AuthorAlt = XmlProvider<"<author><name>Karl Popper</name><born>1902</born></author>">
let doc = "<author><name>Paul Feyerabend</name><born>1924</born></author>"
let sampleAlt = AuthorAlt.Parse(doc)

printfn "%s (%d)" sampleAlt.Name sampleAlt.Born

(**
生成された型を使用すると、同じ形式のドキュメントを読み取る場合には完全に同じAPIで
アクセスできるようになります（ただし1番目の形式を使用するサンプルを `AuthorAlt`
で解析することはできません。両者は単にpublic APIの形式が同じだけであって、
型の実装としてはそれぞれ別のものだからです）。

この型プロバイダーはノードにプリミティブな値だけが含まれていて、子ノードも属性も
持たないような場合に限って、ノードを単純に型付けされたプロパティへと変換します。

### さらに複雑な構造を持つドキュメントに対する型

もう少し興味深い構造を持った例をいくつか見ていくことにしましょう。
まず、ノードが同じ値を持つものの、属性の値が異なる場合にはどうなるでしょうか？
*)

type Detailed = XmlProvider<"""<author><name full="true">Karl Popper</name></author>""">
let info = Detailed.Parse("""<author><name full="false">Thomas Kuhn</name></author>""")

printfn "%s (full=%b)" info.Name.Value info.Name.Full

(**
ノードが( `string` のような)単純型として表現できない場合、
この型プロバイダーは複数のプロパティを持つような型を新しく作成します。
今回の場合、(属性の名前を元にして) `Full` というbool型のプロパティが生成されます。
次に要素のコンテンツを返すような、`Value` という(特別な)名前のプロパティが
追加されます。

### 単純な要素を複数持つドキュメントに対する型

もう1つ興味深い例として、プリミティブ値しか持たないような複数のノードが存在する
場合を考えてみましょう。
以下の例ではルートノード以下に複数の `<value>` ノードがあるドキュメントを
サンプルとして指定しています( `Parse` メソッドにパラメータを指定しなかった場合、
スキーマ用に指定したものと同じテキストが実行時の値に反映されます)。
*)

type Test = XmlProvider<"<root><value>1</value><value>3</value></root>">

Test.GetSample().Values
|> Seq.iter (printfn "%d")

(**
型パラメータは複数の値を配列として返すような `Values` メソッドを生成します。
`<value>` ノードは任意個の属性や子ノードを持つわけではないため、
それぞれ `int` 値となり、 `Values` メソッドも単に `int[]` を返すようになります！

## 哲学者たちをもてなす

このセクションでは特定の話題に関する著者のリストを含んだ、単純なドキュメントを
型プロバイダーで処理する方法についてのデモを紹介します。
サンプルとなるドキュメント [`data/Writers.xml`](../../data/Writers.xml) は
以下のようになっています：

    [lang=xml]
    <authors topic="Philosophy of Science">
      <author name="Paul Feyerabend" born="1924" />
      <author name="Thomas Kuhn" />
    </authors> 

実行時には型プロバイダーから生成された型を使用して、以下のような文字列を
解析します(構造としてはサンプルのドキュメントと同じですが、
`author` ノードに `died` 属性が含まれているという違いがあります)：
*)

let authors = """
  <authors topic="Philosophy of Mathematics">
    <author name="Bertrand Russell" />
    <author name="Ludwig Wittgenstein" born="1889" />
    <author name="Alfred North Whitehead" died="1947" />
  </authors> """

(**
`XmlProvider` の初期化時にはファイル名またはWebのURLを指定できます。
`Load` や `AsyncLoad` メソッドを使用すると、
ファイルあるいはWeb上のリソースを読み取ることができます。
`Parse` メソッドの場合はデータとして文字列を指定できるため、以下のようにすると
データ内の情報を表示することができます：
*)

type Authors = XmlProvider<"../../data/Writers.xml">
let topic = Authors.Parse(authors)

printfn "%s" topic.Topic
for author in topic.Authors do
  printf " - %s" author.Name 
  author.Born |> Option.iter (printf " (%d)")
  printfn ""

(**
値 `topic` には( `string` 型の) `Topic` プロパティがあります。
このプロパティは同名の属性の値を返します。
また、すべての著者名をコレクションとして返すような `Authors` メソッドもあります。
`Born` プロパティはいくつかの著者では指定されていないため、
`option<int>` 型として定義されています。
そのため、 `Option.iter` を使用して表示する必要があります。

`died` 属性はサンプルからの推論時には存在しないため、
静的に型付けられた方法でこの値を取得することはできません
(ただし `author.XElement.Attribute(XName.Get("died"))` というコードを使用して
動的に取得することは可能です)。

## グローバル推測モード

これまでの例では、同じ名前の要素を（再帰的に）含むような要素は
出てきませんでした(つまりたとえば `<author>` 以下に `<author>` ノードは
決して現れないということです)。
しかしXHTMLファイルのようなドキュメントを扱う場合、こういった状況はよくあることです。
例として以下のようなサンプルドキュメントがあるとしましょう
(単純化したバージョンが [`data/HtmlBody.xml`](../../data/HtmlBody.xml) にあります)：

    [lang=xml]
    <div id="root">
      <span>Main text</span>
      <div id="first">
        <div>Second text</div>
      </div>
    </div>

この例では `<div>` 要素内に `<div>` 要素がありますが、いずれも同じ型として
扱われるべきであることは明らかです。
`<div>` 要素を処理する再帰関数を作成できるようになっていてもらいたいはずです。
このような場合には引数 `Global` に `true` を指定します：
*)

type Html = XmlProvider<"../../data/HtmlBody.xml", Global=true>
let html = Html.GetSample()

(**
引数 `Global` を `true` にすると、型プロバイダーは同名の要素すべてを **一元化** します。
つまりすべての `<div>` 要素が同じ型として扱われることになります
( `<div>` に指定されたすべての属性をプロパティとして持ち、
サンプルドキュメント内で見つけられるすべての子要素の組み合わせが考慮されます)。

型は `Html` 以下に定義されます。
したがって、`Html.Div` を引数にとり、
各 `<div>` 要素を処理するような `printDiv` 関数を以下のようにして作成できます：
*)

/// <div> 要素のコンテンツを表示します
let rec printDiv (div:Html.Div) =
  div.Spans |> Seq.iter (printfn "%s")
  div.Divs |> Seq.iter printDiv
  if div.Spans.Length = 0 && div.Divs.Length = 0 then
      div.Value |> Option.iter (printfn "%s")

// すべての子要素と共にルートの <div> 要素を表示します
printDiv html

(**

この関数はまず `<span>` 内のすべてのテキストを表示します
(今回の例の場合、属性が全く指定されていないため、
`string` 型として推論されます)。
次に、すべての `<div>` 要素を再帰的に表示します。
ネストされた要素が見つからない場合は `Value` (インナーテキスト)を表示します。

## RSSフィードの読み取り

今回の総まとめとして、もう少し実用的な例としてRSSフィードを解析してみましょう。
既に説明した通り、型プロバイダーには相対パスあるいはWebページのアドレスを指定できます：
*)

type Rss = XmlProvider<"http://tomasp.net/blog/rss.aspx">

(**
このコードではRSSフィード（および `http://tomasp.net` で使用されている機能）
を表す `Rss` 型を生成しています。
`Rss` 型にはこの型のインスタンスを生成するための機能として、staticメソッド
`Parse` 、 `Load` 、 `AsyncLoad` が定義されています。
今回の場合、スキーマとして指定したものと同じURIを再利用したいので、
staticメソッド `GetSample` を使用します：
*)

let blog = Rss.GetSample()

(**
ここまで来ればRSSフィードのタイトルと直近の投稿一覧を表示することは簡単です。
単に `blog` に続けて `.` と入力すれば、自動補完の候補一覧が確認できるでしょう。
コードとしては以下のようにします：
*)

// Title は文字列を返すプロパティです
printfn "%s" blog.Channel.Title

// すべてのitemノードを取得して、それぞれのタイトルとリンクを表示します
for item in blog.Channel.Items do
  printfn " - %s (%s)" item.Title item.Link

(**

## 関連する記事

 * [API リファレンス: XmlProvider 型プロバイダー](../..reference/fsharp-data-xmlprovider.html)
 * [API リファレンス: XElementExtensions モジュール](../../reference/fsharp-data-xelementextensions.html)

*)
