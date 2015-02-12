#r "System"
#r "System.Core"
#r "System.Xml"
#r "System.Xml.Linq"
#load "Net\UriUtils.fs"
#load "Net\Http.fs"
#load "CommonRuntime\IO.fs"
#load "CommonRuntime\Caching.fs"
#load "CommonRuntime\TextConversions.fs"
#load "CommonRuntime\TextRuntime.fs"
#load "CommonRuntime\Pluralizer.fs"
#load "CommonRuntime\NameUtils.fs"
#load "CommonRuntime\StructuralTypes.fs"
#load "CommonRuntime\StructuralInference.fs"
#load "Html/HtmlCharRefs.fs"
#load "Html/HtmlParser.fs"
#load "Html/HtmlOperations.fs"

open System.Runtime.CompilerServices
open FSharp.Data
open System.Xml.Linq
open System.Xml.XPath

[<Literal>]
let simpleHtml = """<html>
                    <head>
                        <script src="food.js" />
                    </head>
                    <body>
                        <table title="table">
                            <tr><th>Date</th><th>Column 1</th><th>Column 2</th><th>Column 3</th><th>Column 4</th></tr>
                            <tr><td>01/01/2013 12:00</td><td>1</td><td>yes</td><td>2</td><td>2</td></tr>
                            <tr><td>01/02/2013 12:00</td><td>2</td><td>no</td><td>2.5</td><td>2</td></tr>
                            <tr><td>01/03/2013 12:00</td><td>3</td><td>true</td><td>3.456</td><td>2.3</td></tr>
                            <tr><td>01/04/2013 12:00</td><td>4</td><td>true</td><td>2.4</td><td>&nbsp;</td></tr>
                        </table>
                    </body>
                </html>"""


let htmlToXml (url:string) = 
    url
    |> HtmlDocument.Load
    |> HtmlDocument.toXDocument

let xDoc = htmlToXml "http://en.wikipedia.org/wiki/The_Championships,_Wimbledon"

fsi.AddPrinter(fun (x:XElement) -> x.Value)

type XElement with

    [<Extension>]
    member x.Elements(names) = 
         x.Elements() 
         |> Seq.filter (fun e -> names |> List.exists (fun n -> e.Name; n = e.Name.ToString().ToLower()))

let tables =  
    xDoc.XPathSelectElements("//table") 
    |> Seq.map (fun table -> table.Elements(["tr";"thead";"tbody";"tfoot"]) |> Seq.map (fun row -> row.Elements(["td";"th"])))
    
tables |> Seq.nth 2 |> Seq.map (Seq.toArray)|> Seq.toArray
   