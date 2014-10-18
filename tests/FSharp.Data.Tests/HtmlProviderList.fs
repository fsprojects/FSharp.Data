#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
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

//TODO: Missing list items are not infered as optional. :S
//[<Test>]
//let ``Simple List handles missing values``() = 
//    let list = HtmlProvider<"""<html>
//                    <body>
//                        <ul>
//                            <li>1</li>
//                            <li>2</li>
//                            <li>&nbsp;</li>
//                        </ul>
//                    </body>
//                </html>""", PreferOptionals=true>.GetSample().Lists.List1
//    list.Values |> should equal [Some 1;Some 2; None]

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
    let table = HtmlProvider<"""<html>
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
                </html>""", PreferOptionals=true>.GetSample().Lists.ExampleList
    table.Values |> should equal [1;2;3]

[<Test>]
let ``Should find the table as a header when nested deeper``() = 
    let table = HtmlProvider<"""<html>
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
                </html>""", PreferOptionals=true>.GetSample().Lists.ExampleList
    table.Values |> should equal [1;2;3]

//[<Test>]
//let ``Handles definition list correctly``() = 
//    let list = HtmlProvider<"""<html>
//                    <body>
//                        <dl>
//                          <dt>Authors:</dt>
//                          <dd>Remy Sharp</dd>
//                          <dd>Rich Clark</dd>
//                          <dt>Editor:</dt>
//                          <dd>Brandan Lennox</dd>
//                          <dt>Category:</dt>
//                          <dd>Comment</dd>
//                        </dl>
//                    </body>
//                </html>""", PreferOptionals=true>.GetSample().Lists.List0
//    list.Rows.[0].Authors |> should equal 1