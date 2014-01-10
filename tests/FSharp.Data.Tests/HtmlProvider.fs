#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../bin/FSharp.Data.Experimental.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.HtmlProvider
#endif

open NUnit.Framework
open FSharp.Data.Experimental
open FsUnit
open System.Xml
open System.Xml.Linq
open FSharp.Net
open System.Text
open System.IO
open FSharp.Data.Runtime

[<Literal>]
let exampleHtml = 
    """ <html>
        <head></head>
        <body>
        <div>
            <table id="myTable">
              <tr>
                <th>Column1</th><th>Column2</th><th>Column3</th>
              </tr>
              <tr>
                <td>True</td><td>no</td><td>3</td>
              </tr>
              <tr>
                <td>yes</td><td>false</td><td>1.92</td>
              </tr>
            </table>
            </div>
        </body>
        </html>
    """

type ExampleHtml = HtmlProvider<exampleHtml>

let ts = ExampleHtml.Tables.myTable()
//printfn "%s" ts.Id
printfn "%A" ts.Value
let body = 
    let response = FSharp.Net.Http.Request("http://www.bmreports.com/servlet/com.logica.neta.bwp_MarketDepthServlet")
    match response.Body with
    | FSharp.Net.ResponseBody.Text(text) -> Encoding.UTF8.GetBytes(text)
    | ResponseBody.Binary(bytes) -> bytes

let tables = 
    use ms = new MemoryStream(body)
    use sr = new StreamReader(ms)
    let dom = HtmlRuntime.parse sr
    HtmlRuntime.getTables dom |> Seq.map (fun t -> t.Value) |> Seq.toList

printfn "%A" tables
tables.Value
//
//type Wiki = HtmlProvider<>
//
//let w = Wiki.Tables.