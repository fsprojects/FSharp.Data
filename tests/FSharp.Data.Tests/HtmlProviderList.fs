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

[<Literal>]
let simpleHtml = """<html>
                    <body>
                        <ul>
                            <li>1</li>
                            <li>2</li>
                            <li>3</li>
                        </ul>
                    </body>
                </html>"""

type HtmlList = HtmlProvider<simpleHtml, PreferOptionals=true>

[<Test>]
let ``SimpleHtml infers int type correctly ``() = 
    let list = HtmlList().Lists.List0
    list.Rows.[0].Value |> should equal 1

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