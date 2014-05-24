F# Data: データアクセス用ライブラリ
===================================

F# Data ライブラリ (`FSharp.Data.dll`) にはF#アプリケーションやスクリプトから
データにアクセスする際に必要となるすべての機能が揃えられています。
このライブラリには構造的な形式を持ったファイル（CSVやJSON、XML）を
操作するためのF#型プロバイダや、WorldBankやFreebaseのデータにアクセスするための
型プロバイダがあります。
また、JSONやCSVファイルを解析する機能や、
HTTPリクエストを送信するための機能もあります。

<div class="container-fluid" style="margin:15px 0px 15px 0px;">
    <div class="row-fluid">
        <div class="span1"></div>
        <div class="span10" id="anim-holder">
            <div id="wbtn" style="right:10px">WorldBank</div>
            <div id="jbtn" style="right:110px">JSON</div>
            <div id="cbtn" style="right:210px">CSV</div>
            <a id="lnk" href="../images/start.png"><img id="anim" src="../images/start.png" /></a>
        </div>
        <div class="span1"></div>
    </div> 
</div>
<script type="text/javascript">
$(function(){
  var wi = new Image();
  var ji = new Image();
  var ci = new Image();
  wi.src ='../images/wb.gif';
  ji.src ='../images/json.gif';
  ci.src ='../images/csv.gif';
  $('#wbtn').click(function(){ $('#anim').attr('src',wi.src); $('#lnk').attr('href',wi.src); });
  $('#jbtn').click(function(){ $('#anim').attr('src',ji.src); $('#lnk').attr('href',ji.src); });
  $('#cbtn').click(function(){ $('#anim').attr('src',ci.src); $('#lnk').attr('href',ci.src); });
});</script>

F# Data Libraryは <a href="https://nuget.org/packages/FSharp.Data">NuGetの
FSharp.Dataパッケージ</a>として公開されています。
ライブラリをインストールするには、
<a href="http://docs.nuget.org/docs/start-here/using-the-package-manager-console">
パッケージ マネージャー コンソール</a>上から以下のコマンドを実行します：
`Install-Package FSharp.Data`.
あるいは [ソースコードをZIPファイルとしてダウンロード][source] したり、
[コンパイル済みバイナリ][compiled] をZIPファイルとして
ダウンロードすることもできます。

ドキュメント
------------

このライブラリの重要な利点の1つとしては、包括的なドキュメントが
揃えられているということです。
ドキュメントはいずれも [samples フォルダ][samples] 内にある `*.fsx`
ファイルから自動生成されています。
もしも誤字脱字など見つけた場合には是非pullリクエストを投げてください！

 * [F# Data](fsharpdata.html) はドキュメントのホームページで、
   それぞれの型プロバイダ（CSVやXML、JSON、WorldBank、Freebase）に関する
   ドキュメントへのリンクが揃っています。
   また、 `FSharp.Data.dll` 内のpublic型に関するドキュメントへのリンクもあります。

貢献方法
------------

プロジェクトは [GitHub][gh] 上でホストされており、
[Issue(問題)を報告][issues] したり、プロジェクトをフォークして
pullリクエストを投げたりすることができます。
公開APIを独自に追加した場合、是非 [samples][samples] にもそれを反映して、
ドキュメント化されるようにしてください。

 * 不具合あるいはライブラリに追加する機能に関する議論がある場合には
   GitHub上で [不具合または新機能][issues] に関するNew Issueを投稿するか、
   [F# オープンソース][fsharp-oss] (英語)のメーリングリストに
   メールを投げてください。

 * ライブラリのアーキテクチャや構成、その他の話題
   (たとえばWindows PhoneやSilverlightなどのためのポータブルライブラリサポート)
   など、より詳しい話題については [F# Data に貢献する](contributing.html)
   のページを参照してください。

### ライブラリの方針

このライブラリは構造的なドキュメントやその他のデータソースに対して、
単純かつ読み取り専用のアクセスをサポートすることに重点を置いています。
F#型プロバイダに関する包括的なコレクションとなることを意図しているわけではありません
（一般的にはF#型プロバイダには様々な用途があります）。
さらに、このライブラリでは（現時点では）ドキュメントを作成したり、
変更したりするようなAPIは公開されていません。

### ライブラリのライセンス

ライブラリは Apache 2.0ライセンスの元に公開されています。
詳細についてはGitHubレポジトリ内の [ライセンスファイル][license] を参照してください。
要約すると、このライブラリは自由に商用利用したり、
派生のライブラリを作成したり、変更したりすることができるということです。



  [source]: https://github.com/fsharp/FSharp.Data/zipball/master
  [compiled]: https://github.com/fsharp/FSharp.Data/zipball/release
  [samples]: https://github.com/fsharp/FSharp.Data/tree/master/samples
  [gh]: https://github.com/fsharp/FSharp.Data
  [issues]: https://github.com/fsharp/FSharp.Data/issues
  [license]: https://github.com/fsharp/FSharp.Data/blob/master/LICENSE.md
  [fsharp-oss]: http://groups.google.com/group/fsharp-opensource
