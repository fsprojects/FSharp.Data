#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.HtmlProviderLists
#endif

open NUnit.Framework
open FsUnit
open System
open FSharp.Data

[<Test>]
let ``Simple List infers int type correctly ``() = 
    let list = HtmlProvider<"""<html>
                    <body>
                        <ul>
                            <li>1</li>
                            <li>2</li>
                            <li>3</li>
                        </ul>
                    </body>
                </html>""", PreferOptionals=true>.GetSample().Lists.List1
    list.Values |> should equal [1;2;3]

[<Test>]
let ``Can handle nested lists``() = 
    let list = HtmlProvider<"""<html>
                    <body>
                        <ul>
                            <li>
                                <ul>
                                    <li>1</li>
                                    <li>2</li>
                                </ul>
                                Foo Bar
                            </li>
                            <li>2</li>
                            <li>3</li>
                        </ul>
                    </body>
                </html>""", PreferOptionals=true>.GetSample().Lists.List1
    list.Values |> should equal ["Foo Bar";"1";"2";"2";"3"]

[<Test>]
let ``Simple List handles missing values``() = 
    let list = HtmlProvider<"""<html>
                    <body>
                        <ul>
                            <li>1</li>
                            <li>2</li>
                            <li>&nbsp;</li>
                        </ul>
                    </body>
                </html>""", PreferOptionals=true>.GetSample().Lists.List1
    list.Values |> should equal [Some 1;Some 2; None]

[<Test>]
let ``Simple List infers decimal type correctly ``() = 
    let list = HtmlProvider<"""<html>
                    <body>
                        <ul>
                            <li>1.123</li>
                            <li>2.123</li>
                            <li>3.123</li>
                        </ul>
                    </body>
                </html>""", PreferOptionals=true>.GetSample().Lists.List1
    list.Values |> should equal [1.123M;2.123M;3.123M]

[<Test>]
let ``Simple List infers date type correctly ``() = 
    let list = HtmlProvider<"""<html>
                    <body>
                        <ul>
                            <li>01/01/2013</li>
                            <li>02/02/2013</li>
                            <li>03/03/2013</li>
                        </ul>
                    </body>
                </html>""", PreferOptionals=true>.GetSample().Lists.List1
    list.Values |> should equal [DateTime(2013,1,1);DateTime(2013,2,2);DateTime(2013,3,3)]

[<Test>]
let ``Simple List infers hetergenous list as string type correctly ``() = 
    let list = HtmlProvider<"""<html>
                    <body>
                        <ul>
                            <li>01/01/2013</li>
                            <li>1</li>
                            <li>Foobar</li>
                        </ul>
                    </body>
                </html>""", PreferOptionals=true>.GetSample().Lists.List1
    list.Values |> should equal ["01/01/2013";"1";"Foobar"]

[<Test>]
let ``Should find the list as a header``() = 
    let list = HtmlProvider<"""<html>
                    <body>
                        <div>
                            <h2>Example List</h2>
                        </div>
                        <ul>
                            <li>1</li>
                            <li>2</li>
                            <li>3</li>
                        </ul>
                    </body>
                </html>""", PreferOptionals=true>.GetSample().Lists.``Example List``
    list.Values |> should equal [1;2;3]

[<Test>]
let ``Should find the list as a header when nested deeper``() = 
    let list = HtmlProvider<"""<html>
                    <body>
                        <div>
                            <h2>
                                <a href="/I/go/somewhere/">
                                Example List
                                </a>
                            </h2>
                        </div>
                        <ul>
                            <li>1</li>
                            <li>2</li>
                            <li>3</li>
                        </ul>
                    </body>
                </html>""", PreferOptionals=true>.GetSample().Lists.``Example List``
    list.Values |> should equal [1;2;3]

[<Test>]
let ``Handles simple definition list``() = 
    let list = HtmlProvider<"""<html>
                    <body>
                        <dl>
                          <dt>Count</dt>
                          <dd>1</dd>
                          <dd>2</dd>
                          <dt>Dates</dt>
                          <dd>01/01/2014</dd>
                          <dd>02/02/2014</dd>
                          <dt>Decimals</dt>
                          <dd>1.23</dd>
                          <dd>2.23</dd>
                          <dt>Missing</dt>
                          <dd>Foobar</dd>
                          <dd></dd>
                        </dl>
                    </body>
                </html>""", PreferOptionals=true>.GetSample().DefinitionLists.DefinitionList1
    list.Count.Values |> should equal [1;2]
    list.Dates.Values |> should equal [DateTime(2014,1,1); DateTime(2014, 2,2)]
    list.Decimals.Values |> should equal [1.23M; 2.23M]
    list.Missing.Values |> should equal [Some "Foobar"; None]

[<Test>]
let ``Handles definition list correctly``() = 
    let list = HtmlProvider<"""<html>
                    <body>
                        <dl>
                          <dt>Authors:</dt>
                          <dd>Remy Sharp</dd>
                          <dd>Rich Clark</dd>
                          <dt>Editor:</dt>
                          <dd>Brandan Lennox</dd>
                          <dt>Category:</dt>
                          <dd>Comment</dd>
                        </dl>
                    </body>
                </html>""", PreferOptionals=true>.GetSample().DefinitionLists.DefinitionList1
    list.Authors.Values |> should equal ["Remy Sharp"; "Rich Clark"]
    list.Category.Values |> should equal ["Comment"]
    list.Editor.Values |> should equal ["Brandan Lennox"]

[<Test>]
let ``Handles SimpleListHtml file``() = 
    let list = HtmlProvider<"Data/SimpleHtmlLists.html">.GetSample()
    list.Lists.SimpleCount.Values |> should equal [1;2;3]
    list.DefinitionLists.MyDLLists.Count.Values |> should equal [1;2]
