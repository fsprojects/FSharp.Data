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
                </html>""", PreferOptionals=true>.GetSample().Lists.List0
    list.Rows.[0].Value |> should equal 1

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
                </html>""", PreferOptionals=true>.GetSample().Lists.List0
    list.Rows.[0].Authors |> should equal 1

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
    table.Rows.[0].Value |> should equal 1

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
    table.Rows.[0].Value |> should equal 1