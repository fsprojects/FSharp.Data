(**
# F# Data: JSON と XML の相互変換

このチュートリアルではJSONドキュメント( [JSON パーサーの記事](JsonValue.html)
で説明している `JsonValue` 型で表されるドキュメント)と
XMLドキュメント( `XElement` で表されるドキュメント)を
相互に変換する機能を実装する方法を紹介します。
この機能はF# Dataライブラリから直接使用できるものではありませんが、
JSON(あるいはXML)ドキュメントを再帰的に処理するだけで
非常に簡単に実装できます。

JSONとXML間の変換を自身のコードで使いたい場合には
[GitHubにあるソース][jsontoxml] をコピーしてプロジェクトに追加するだけです。
この機能を頻繁に利用することがあり、F# Dataライブラリの機能としてほしい場合には
是非 [機能リクエスト][issues] を投稿してください。

  [jsontoxml]: https://github.com/fsharp/FSharp.Data/blob/master/docs/content/ja/tutorials/JsonToXml.fsx
  [issues]: https://github.com/fsharp/FSharp.Data/issues

初期化
------

ここでは( `System.Xml.Linq.dll` 内にある)LINQ to XMLのAPIと、
`FSharp.Data` 名前空間にある `JsonValue` を使います：
*)

#r "System.Xml.Linq.dll"
#r "../../../../bin/lib/net45/FSharp.Data.dll"
open System.Xml.Linq
open FSharp.Data

(**

このスクリプト内では簡単に処理できる値を返すような変換機能を実装しますが、
リバーシブルな変換ではないことに注意してください
(つまりJSONからXMLに変換した後、JSONに戻したとしても
元と同じ値になるとは限らないということです)。

XMLからJSONへの変換
-------------------

XMLとJSONはかなり似た形式ではありますが、
細かい部分ではそれなりに違いがあります。
たとえばXMLでは **属性** と **子要素** が区別されます。
さらにすべてのXML要素には名前がありますが、
JSONの配列やレコードは無名です(ただしレコードには
名前のつけられたフィールドがあります)。
たとえば以下のようなXMLがあるとします：

    [lang=xml]
    <channel version="1.0">
      <title text="Sample input" />
      <item value="First" />
      <item value="Second" />
    </channel>

これに対して生成するJSONではトップレベルの要素名( `channel` )が無視されます。
生成されるJSONデータはレコード型で、
各属性および子要素に対してユニークなフィールドが含まれます。
ある要素が繰り返し現れる場合には配列へと変換されます：

    [lang=js]
    { "version": "1.0",
      "title": { "text": "Sample input" },
      "items": [ { "value": "First" }, 
                 { "value": "Second" } ]  }

このように、 `item` 要素は自動的に `items` という複数形に変換されて、
`value` 属性の値に関連づけられた2つのレコード値が
配列の要素として含まれるようになります。

変換関数は `XElement` を引数にとり、 `JsonValue` を返すような再帰関数です。
この関数は( `JsonValue.Record` と `JsonValue.Array` を使って)JSONのレコードと
配列を組み立てます。
すべての属性は `JsonValue.String` に変換されます。
ただし今回の例では数値型を適切なJSON型に変換するような機能は実装しません：
*)

/// XML要素に対応するJSON表現を作成する
let rec fromXml (xml:XElement) =

  // すべての属性に対してキー値ペアのコレクションを作成する
  let attrs = 
    [ for attr in xml.Attributes() ->
        (attr.Name.LocalName, JsonValue.String attr.Value) ]

  // XElementのコレクションを(fromXmlを再帰的に使って)
  // JsonValueの配列に変換する関数
  let createArray xelems =
    [| for xelem in xelems -> fromXml xelem |]
    |> JsonValue.Array

  // 子要素を名前でグループ化した後、
  // 単一要素のグループを(再帰的に)レコードへと変換し、
  // 複数要素のグループをcreateArrayでJSON配列に変換する
  let children =
    xml.Elements() 
    |> Seq.groupBy (fun x -> x.Name.LocalName)
    |> Seq.map (fun (key, childs) ->
        match Seq.toList childs with
        | [child] -> key, fromXml child
        | children -> key + "s", createArray children )
        
  // 子要素および属性用に生成された要素を連結する
  Array.append (Array.ofList attrs) (Array.ofSeq children)
  |> JsonValue.Record

(**

JSONからXMLへの変換
-------------------

JSONからXMLへ変換する場合、同じようなミスマッチが起こります。
たとえば以下のようなJSONデータがあるとします：

    [lang=js]
    { "title" : "Sample input",
      "paging" : { "current": 1 },
      "items" : [ "First", "Second" ] }

トップレベルのレコードには名前がないため、
今回の変換機能では(ルート名を指定することになる)ユーザー側で
`XElement` としてラップできるような
`XObject` のリストを生成することにします。
レコード内にあるプリミティブな値のフィールドは属性になり、
(配列やレコードのような)複雑な値はオブジェクトになります：

    [lang=xml]
    <root title="Sample input">
      <items>
        <item>First</item>
        <item>Second</item>
      </items>
      <paging current="1" />
    </root>

変換関数はやはり再帰関数になります。
今回はパターンマッチを使って `JsonValue` のそれぞれ取り得るケースを区別します。
プリミティブ値を表すケースでは単に値を `obj` として返し、
配列やレコードに対してはネストされた(複数の)要素や属性を返します：

*)

/// JSONの値に対するXML表現を作成する
/// (ただしトップレベルの値がオブジェクトまたは配列の場合のみ機能する)
let toXml(x:JsonValue) =
  // XML属性やXML要素を作成するためのヘルパ関数
  let attr name value = 
    XAttribute(XName.Get name, value) :> XObject
  let elem name (value:obj) = 
    XElement(XName.Get name, value) :> XObject

  // 変換機能を実装している内部用再帰関数
  let rec toXml = function
    // Primitive values are returned as objects
    | JsonValue.Null -> null
    | JsonValue.Boolean b -> b :> obj
    | JsonValue.Number number -> number :> obj
    | JsonValue.Float number -> number :> obj
    | JsonValue.String s -> s :> obj

    // JSONオブジェクトは(プリミティブであれば)XML属性か、
    // あるいは子要素になる
    // attributes (for primitives) or child elements
    | JsonValue.Record properties -> 
      properties 
      |> Array.map (fun (key, value) ->
          match value with
          | JsonValue.String s -> attr key s
          | JsonValue.Boolean b -> attr key b
          | JsonValue.Number n -> attr key n
          | JsonValue.Float n -> attr key n
          | _ -> elem key (toXml value)) :> obj

    // JSON配列は <item> 要素のシーケンスになる
    | JsonValue.Array elements -> 
        elements |> Array.map (fun item -> 
          elem "item" (toXml item)) :> obj

  // 変換を実行して、結果をオブジェクトのシーケンスにキャストする
  // (意図しない入力に対しては失敗する可能性あり！)
  (toXml x) :?> XObject seq

(**


## 関連する記事

 * [F# Data: JSON パーサーおよびリーダー](../library/JsonValue.html) -
   JSONの値を動的に処理する方法についての説明があります。
 * [F# Data: JSON 型プロバイダー](../library/JsonProvider.html) -
   型安全な方法でJSONデータにアクセスする機能を持った
   F# 型プロバイダーについて説明しています。
 * [F# Data: XML 型プロバイダー](../library/XmlProvider.html) -
   型安全な方法でXMLデータにアクセスする機能を持った
   F# 型プロバイダーについて説明しています。

*)