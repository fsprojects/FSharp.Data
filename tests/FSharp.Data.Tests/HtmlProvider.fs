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
                <th>Column1</th><th>Column6</th><th>Column3</th>
              </tr>
              <tr>
                <td>True</td><td>1</td><td>3</td>
              </tr>
              <tr>
                <td>yes</td><td>2</td><td>1.92</td>
              </tr>
            </table>
        </div>
        <div>
            <table>
              <tr>
                <th>Date</th><th>CptyId</th><th>Value</th>
              </tr>
              <tr>
                <td>01/01/2012</td><td>1</td><td>3</td>
              </tr>
              <tr>
                <td>01/02/2012</td><td>2</td><td>1.92</td>
              </tr>
            </table>
        </div>
        </body>
        </html>
    """

type ExampleHtml = HtmlProvider<exampleHtml>

let dataHtml = """
        <html>
        <head></head>
        <body>
        <div>
            <table id="myTable">
              <tr>
                <th>Column1</th><th>Column6</th><th>Column3</th>
              </tr>
              <tr>
                <td>True</td><td>1</td><td>3</td>
              </tr>
              <tr>
                <td>yes</td><td>2</td><td>45.67</td>
              </tr>
            </table>
            </div>
        </body>
        </html>
    """

let myTable = ExampleHtml.Tables.myTable.Load(dataHtml)
myTable.Rows |> printfn "%A"

let anotherTable = ExampleHtml.Tables.Table_1.Load(exampleHtml)
anotherTable.Rows |> Seq.map (fun r -> r.CptyId, r.Date) |> printfn "%A"

let marketDepthFile = File.ReadAllText(__SOURCE_DIRECTORY__ + "/Data/MarketDepth.htm")
let ms = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(marketDepthFile))
let sr = new System.IO.StreamReader(ms)

//let elem = 
FSharp.Data.Runtime.Html.parse(sr) 
|> Html.getTables 
|> Seq.iteri (fun i s -> 
                let fname = __SOURCE_DIRECTORY__ + "/Data/PArsedMaarketDepth_" + string i + ".htm"
                if File.Exists(fname) then File.Delete(fname)
                use wr = new System.IO.StreamWriter(File.OpenWrite(fname))
                FSharp.Data.Runtime.Html.write wr s) 
                
//.Tables() |> Seq.head// |> Seq.nth 3
//elem.Rows |> Seq.map (fun r -> r.Data) |> printfn "%A"
//
//type BmReports = HtmlProvider<"Data/MarketDepth.htm", Culture="en-GB">
//
//BmReports.Tables.Table_0.
//let rep = BmReports.Tables.Table_3.Load(File.ReadAllText(__SOURCE_DIRECTORY__ + "/Data/MarketDepth.htm"))
//let result = rep.Rows |> Seq.map (fun r -> r.``Settlement Day``, r.``Accepted Bid Vol``)
//
//result |> Seq.iter (printfn "%A")

//type Wiki = HtmlProvider<"http://en.wikipedia.org/wiki/2013_Wimbledon_Championships">
//
//Wiki