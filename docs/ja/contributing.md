F# Data に貢献する
==================

このページには、F# Dataパッケージに貢献しようとするあなたにとって必要な
基本的な情報が揃えられています。
たとえばライブラリの構造に関する簡単な説明や、型プロバイダの作成方法、
F# Dataにおける複数のターゲットに対する処理方法
(デスクトップやSilverlight、ポータブルライブラリで型プロバイダを
利用できるようにする方法)があります。

 * このページはGitHub上のF# Dataプロジェクトに対して
   pullリクエストを送信することで編集できます。
   そのため、F# Dataで遊んでいる間に何かしら学んだことがあれば、是非それを
   [こちら](https://github.com/fsharp/FSharp.Data/blob/master/docs/content/ja/contributing.md)
   に記録として残してください!

 * 機能(あるいはすばらしいアイディア！)に関する議論、
   あるいは貢献方法に関する提案がある場合には、GitHub上の
   [Issue list](https://github.com/fsharp/FSharp.Data/issues) をチェックするか、
   [F# オープンソースメーリングリスト](http://groups.google.com/group/fsharp-opensource)
   宛にメールをください。

## ソリューションファイル

ルートディレクトリには複数のVisual Studioソリューションファイル
(`*.sln`)があり、それぞれ論理的なグループ毎に分けられています。

 * **FSharp.Data.sln** にはF# Dataの機能
   (たとえば実行時あるいはデザイン時の型プロバイダライブラリ)を
   実装する主要なプロジェクトが含まれています。
   未完成だけれども興味深いコードを追加したい場合には試験的プロジェクトの方に
   追加するようにしてください。

 * **FSharp.Data.ExtraPlatforms.sln** には `FSharp.Data.sln` と同じプロジェクトが
   含まれていますが、対象となるプラットフォームとして
   Silverlightやポータブルクラスライブラリなどが追加されています。

 * **FSharp.Data.Tests.sln** にはF# Dataをテストするためのライブラリがあります。
   また、Webサイト用のファイル ( `*.fsx` および `*.md`) も含まれています。
   ドキュメントを編集したい場合はこのソリューションをチェックしてください！

## プロジェクトおよびマルチターゲット

型プロバイダを開発する上で問題となることの1つとして、
複数バージョンの.NETプラットフォームサポートがあります。
型プロバイダは2つのコンポーネントから構成されます：

 * **実行時** コンポーネントは型プロバイダを使用するコンパイル済みのF#コードを
   実行した時に実際に使用される型プロバイダの一部のことです。
   このアセンブリにはJSONやCSVパーサー、HTTPユーティリティなど、
   FSharp.Dataの型プロバイダではないコンポーネントも含まれます。

 * **デザイン時** コンポーネントは型プロバイダを使用するF#コードを
   任意のエディタで記述している間、あるいはコードをコンパイルしている間に
   使用される型プロバイダの一部のことです。
   たとえばCSVプロバイダの場合、このコンポーネントは型推論および型の生成を行います
   (生成された型はコンパイラによって実行時コンポーネントと結びつけられます)。

複数ターゲットをサポートするためには、ターゲット(Silverlight、.NET 4.0、
ポータブルプロファイル)それぞれに **実行時コンポーネント** が必要です。
しかし **デザイン時** コンポーネントはVisual StudioあるいはMonoDevelopを使用して、
デスクトップ用の.NET上でしか実行されないため、1つしか作成する必要がありません。
(本当のことを言うと、 [tryfsharp.org](http://tryfsharp.org) のWebサイトを
サポートするために、Silverlight用の **デザイン時** コンポーネントを
作成する必要があります...)

というわけで、4つの **実行時** コンポーネントと2つの **デザイン時**
コンポーネントがあります。
今のところはコンポーネント毎にそれぞれ別のプロジェクトファイルが用意されていますが、
プロジェクト間で共有されているファイルもあります。
プロジェクトでは単にシンボルがいくつか定義されているだけで、
特定のプラットフォームに対してはそれらのシンボルに対する `#if` で
コードを組み込んだり除外したりするようになっています。
試験的プロジェクトには2つの **実行時** コンポーネントと2つの **デザイン時**
コンポーネントがあります。

`FSharp.Data.sln` を開くと、以下のような **実行時コンポーネント** が
含まれていることが確認できるでしょう：

 * **FSharp.Data** - デスクトップ.NET 4.0 バージョン
 * **FSharp.Data.Portable** - F# ポータブルライブラリバージョン
   (デスクトップの.NET 4.0、Windows Phone 8、 Windows 8を対象とする)
   ポータブルライブラリバージョン

ポータブルライブラリをSilverlight上で使用することは可能ですが、
その場合にはLINQクエリ演算が定義されているSystem.Coreアセンブリへの参照が
正しいバージョンに設定できないという問題があるためにFreebaseプロバイダが
機能しなくなるという点に注意してください。
同様に、ポータブルバージョンあるいはSilverlightバージョンを使用する場合には
XmlProviderもSilverlight上では機能しません。

**デザイン時** コンポーネントとしては以下のプロジェクトがあります：

 * **FSharp.Data.DesignTime** - デスクトップエディタ用のメインバージョン
 * **FSharp.Data.DesignTime.Silverlight** - Try F#用のメインバージョン
 
Silverlight 5のプロジェクトをコンパイルするためには、
[Silverlight 5.0 開発者用ランタイム](http://go.microsoft.com/fwlink/?LinkId=229324)
をインストールしておく必要があります。

### 型プロバイダの構成

F# Dataの型プロバイダのうち、いくつかのものは同じような構造になっています。
CSVやJSON、XMLプロバイダはいずれもサンプルとして入力されたファイル構造から
型を構成します。
さらに、いずれにも実行時コンポーネントがあります(CSVパーサーやJSONパーサー、
あるいは.NETの `XDocument` です)。

では一般的な型プロバイダはどのように実装すればいいのでしょうか？
まず、プロジェクト間で共有されるファイルについてはプロジェクトの
`Common` や `Library` サブディレクトリに配置します。
これらのファイルは(パーサーやHTTPヘルパなど)汎用の **実行時** コンポーネントが
含まれることになります。

次に、いくつかの **デザイン時** コンポーネントがあります。
これらは(DesignTimeプロジェクト内の) `Providers` フォルダ内にあるもので、
F#チームによって作成された `ProvidedTypes` ヘルパや、
`StructureInference.fs`(構造化データに対する型推論機能の実装)、
およびその他いくつかのヘルパ機能を実装したコードがあります。

JSONプロバイダのような型プロバイダの場合、1つのフォルダ内に複数のファイルが置かれます。
一般的には以下のようになります：

 * `JsonRuntime.fs` - 唯一の **実行時** コンポーネントです。
   実行時における型の定義や、生成されたコードから呼び出されるメソッドの
   定義などがあります。

 * `JsonInference.fs` - `StructureInference.fs` にある一般的なAPIを使用して
   構造を推測するような **デザイン時** コンポーネントです。

 * `JsonGenerator.fs` - 公開される型を生成したり、公開される型にプロパティや
   メソッドなどを追加するためのコードがあります。
   このコードでは推論によって得られた情報を使用して、実行時コンポーネントを
   呼び出すコードが生成されます。

 * `JsonProvider.fs` - 型プロバイダのstaticプロパティを定義したり、
   公開される型を登録する処理を行うようなエントリポイントです。

WorldBank、Freebaseの型プロバイダの場合は異なります。
これらには推論が必要ありませんが、やはり **実行時** と **デザイン時**
コンポーネントの区別があります。
したがってこれらの型プロバイダの場合、少なくとも2つのファイル
(およびいくつかのヘルパ用ファイル)がディレクトリ内にあります。

## ソースコード

### Assembly replacer

型プロバイダ内でのコード生成はややトリッキーです
(たとえば `JsonGenerator.fs` を参照してください)。
というのも、生成されたコードでは特定の実行時アセンブリ
(Silverlightやデスクトッププロファイル、ポータブルプロファイル)
への参照が必要になるからです。
特に、F#のクォート式を使用してコードを生成するような場合には顕著です。
ソースコード中に `<@@ foo.Bar @@>` というコードがあると、
このクォート式は現在のアセンブリにある `foo` という型を
直接参照していることになります。

この問題はAssembly replacer
(アセンブリ置換機。`Providers` 内の `AssemblyReplacer.fs` ファイルを参照)
によって処理されます。
Assemlby replacerはクォート式を変換して、正しいバージョンを参照するように
コードを置き換えます。
Assembly replacerの動作の詳細については
[GitHub上での議論](https://github.com/fsharp/FSharp.Data/pull/5)
を参照してください。

`AssemblyReplacer` 型には以下のようなドキュメントがあります：

> 型プロバイダを実行時アセンブリとデザイン時アセンブリに分離することによって、
> クォート式が間違った型を参照するようになってしまったため、
> クォート式を直接使用することができなくなってしまいました。
> `AssemblyReplacer` を使用すると、
> クォート式によって生成された式が変形されて、適切な型を参照するようになるため
> この問題を回避できるようになります。
> 
> `ProvidedMethod` や `ProvidedConstructor` 、
> `ProvidedProperty` の `InvokeCode` や `GetterCode`
> に指定する式は `(fun args -> <@@ doSomethingWith(%%args) @@>)` ではなく、
> `(fun args -> let args = replacer.ToDesignTime args in replacer.ToRuntime <@@ doSomethingWith(%%args) @@>)`
> という式にする必要があります。
> 
> たとえば `ProvidedXYZ` という型を作成する場合は
> 必ず実行時型を指定する必要があります。
> 
> また、この型に対して `InvokeCode` や `GetterCode` で指定された機能が呼び出されると
> 引数として指定された式をまずデザイン時の型に変換する必要があります。
> そして変換後の式をクォート式と連結した後、最終的に
> 実行時型へと変換し直す必要があるわけです。
> 
> もう1つ面倒なことがあります。
> `Expr.Var` には参照同値性があるため、同じ名前で型が異なる新しい `Expr.Var` を
> 単純に新しく作成することはできません。
> これらを実行時からデザイン時の型へと変換する場合、
> 元々のインスタンスと完全に同じインスタンスへと戻すことができるように
> それぞれをディクショナリとして保持しています。
> 
> また、F#の関数をパラメータや返り値として使用するような式を
> 使用することができないため 、
> 代わりに常にデリゲートを使うようにしなければならないという制限もあります
> (この制限は今回のメソッドだけではなく、型プロバイダのメンバいずれにも該当します)。

    [hide]
    open System
    open System.Reflection
    open FSharp.Quotations

標準的な型プロバイダはいずれも生成時に `AssemblyReplacer` のインスタンスを
取得して、それをコードジェネレータに渡し、適切なコードが
生成されるようにすることになります：

    type AssemblyReplacer =
    
      /// Gets the equivalent runtime type
      abstract member ToRuntime : designTimeType:Type -> Type
      
      /// Gets an equivalent expression with all the types 
      /// replaced with runtime equivalents
      abstract member ToRuntime : designTimeTypeExpr:Expr -> Expr

      /// Gets an equivalent expression with all the types 
      /// replaced with designTime equivalents
      abstract member ToDesignTime: runtimeExpr:Expr -> Expr

## ドキュメント

F# Dataライブラリのドキュメントは
[F# Formatting](https://github.com/tpetricek/FSharp.Formatting)
を使用して自動生成されています。
このライブラリを使用すると `*.md` (コードスニペットが埋め込まれた
マークダウンファイル) や `*.fsx` (マークダウンドキュメントが埋め込まれた
F#スクリプトファイル) ファイルを素敵なHTMLドキュメントに変換してくれます。

 * すべてのドキュメントに関連するコードは
   [GitHub上の](https://github.com/fsharp/FSharp.Data/tree/master/samples)
   `samples` ディレクトリにあります。
   バグや新機能を追加した場合には、必ずドキュメントが残るようにしてください！

 * <p>それぞれの型に直接関連したドキュメントだけでなく、
   ([GitHub上の](https://github.com/fsharp/FSharp.Data/tree/master/samples/tutorials))
   `tutorials` フォルダにもドキュメントがあります。
   ここにはF# Dataの機能を紹介するためのサンプルやチュートリアルを追加できます。

 * ドキュメントをビルドしたい場合には単に `build.fsx` スクリプト
   ([GitHubへのリンク](https://github.com/fsharp/FSharp.Data/blob/master/tools/build.fsx))
   を実行するだけでビルドできます。

## 関連する記事

一般的な型プロバイダを作成する方法が知りたい場合には、以下のリソースが役に立つことでしょう：

  * [Writing F# Type Providers with the F# 3.0 Developer Preview - An Introductory Guide and Samples](http://blogs.msdn.com/b/fsharpteam/archive/2011/09/24/developing-f-type-providers-with-the-f-3-0-developer-preview-an-introductory-guide-and-samples.aspx)

  * [F# 3.0 Sample Pack](http://fsharp3sample.codeplex.com/)
    には非常に単純なものからかなり複雑なものまで、
    様々な型プロバイダのサンプルがあります。

  * [Tutorial: Creating a Type Provider (F#)](http://msdn.microsoft.com/en-gb/library/hh361034.aspx)
