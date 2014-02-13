#load "Net/Http.fs"
#load "CommonRuntime/IO.fs"
#load "CommonRuntime/TextConversions.fs"
#load "CommonRuntime/TextRuntime.fs"
#load "CommonRuntime/HtmlParser.fs"
#load "html/HtmlRuntime.fs"

open System
open FSharp.Data.Runtime

type TableCell = {
    IsHeader : bool
    RowSpan : int
    ColSpan : int
    Value : string list
}

let wimbledonFile = """D:\Appdev\FSharp.Data\tests\FSharp.Data.Tests\Data\wimbledon_wikipedia.html"""

let wimbledon = HtmlParser.parse wimbledonFile |> Option.get

let getTables (HtmlDocument(_, es)) =
    List.collect (Html.getElementsNamed ["table"]) es

let getAttributeAsInt name (e:HtmlElement) = 
    match Html.tryGetAttribute name e with
    | Some(HtmlAttribute(_, colspan)) -> Int32.Parse(colspan)
    | None -> 0

let getRows (table:HtmlElement) = 
    Html.getElementsNamed ["tr"] table

let getHeadersAndData (row:HtmlElement) = 
    Html.getElementsNamed ["th"; "td"] row
    |> List.map (fun element ->
                     let rspan, cspan = (getAttributeAsInt "colspan" element), (getAttributeAsInt "rowSpan" element)
                     match element with
                     | HtmlElement("th", _, content) -> { IsHeader = true; RowSpan = rspan; ColSpan = cspan; Value = List.map Html.getValue content }
                     | HtmlElement("td", _, content) -> { IsHeader = false; RowSpan = rspan; ColSpan = cspan; Value = List.map Html.getValue content }
                     | _ -> failwith "Only expected th, td elements")

let parse tables = 
    getTables tables
    |> List.map (getRows >> List.map getHeadersAndData)

