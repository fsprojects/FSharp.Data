namespace FSharp.Data.Runtime

open System
open System.ComponentModel
open System.Globalization
open System.IO
open System.Text
open System.Xml
open FSharp.Data
open FSharp.Data.Runtime

#nowarn "10001"

// --------------------------------------------------------------------------------------

/// [omit]
type HtmlTableCell = 
    | Cell of bool * string
    | Empty
    member x.IsHeader =
        match x with
        | Empty -> true
        | Cell(h, _) -> h
    member x.Data = 
        match x with
        | Empty -> ""
        | Cell(_, d) -> d

/// [omit]
type HtmlTable = 
    { Name : string
      Headers : string []
      Rows :  string [] [] }

// --------------------------------------------------------------------------------------

/// Helper functions called from the generated code for working with HTML tables
module HtmlRuntime =

    open Html

    let private getName defaultName (element:HtmlElement) = 
        let tryGetName' choices =
            choices
            |> List.tryPick (fun (attrName) -> 
                match element.TryGetAttribute attrName with
                | Some(HtmlAttribute(_,value)) -> Some <| value
                | None -> None
            )
        match tryGetName' [ "id"; "name"; "title"; "summary"] with
        | Some(name) -> name
        | None ->
                match element.Elements ["caption"] with
                | [] -> defaultName
                | h :: _ -> h.InnerText

    let private parseTable index (table:HtmlElement) = 
        let rows = table.Elements ["tr"] |> List.mapi (fun i r -> i,r)
        if rows.Length <= 1 
        then None
        else
            let cells = rows |> List.map (fun (_,r) -> r.Elements ["td"; "th"] |> List.mapi (fun i e -> (i,e)))
            let width = (cells |> List.maxBy (fun x -> x.Length)).Length
            let res = Array.init rows.Length  (fun _ -> Array.init width (fun _ -> Empty))
            for rowindex, _ in rows do
                for colindex, cell in cells.[rowindex] do
                    let rowSpan, colSpan = (max 1 (cell.GetAttributeValue(0,Int32.TryParse,"rowspan"))) - 1,(max 1 (cell.GetAttributeValue (0,Int32.TryParse,"colspan"))) - 1
                    let data =
                        let getContents contents = String.Join(" ", contents |> List.map (fun (x:HtmlElement) -> x.InnerText)).Trim()
                        match cell with
                        | HtmlElement("td", _, contents) -> Cell (false, getContents contents)
                        | HtmlElement("th", _, contents) -> Cell (true, getContents contents)
                        | _ -> Empty
                    let col_i = ref colindex
                    while res.[rowindex].[!col_i] <> Empty do incr(col_i)
                    for j in [!col_i..(!col_i + colSpan)] do
                        for i in [rowindex..(rowindex + rowSpan)] do
                            res.[i].[j] <- data

            let (startIndex, headers) = 
                if res.[0] |> Array.forall (fun r -> r.IsHeader) 
                then 1, res.[0] |> Array.map (fun x -> x.Data)
                else HtmlInference.inferHeaders (res |> Array.map (Array.map (fun x -> x.Data)))
                    

            { Name = (getName ("Table_" + (string index)) table)
              Headers = headers
              Rows = res.[startIndex..] |> Array.map (Array.map (fun x -> x.Data)) } |> Some

    let getTables (doc:HtmlDocument) =
        let tableElements = doc.Elements |> List.collect (fun e -> e.Elements ["table"])
        tableElements
        |> List.mapi parseTable 
        |> List.choose id

    let formatTable (data:HtmlTable) =
        let sb = StringBuilder()
        use wr = new StringWriter(sb)  
        let data = array2D ((data.Headers |> List.ofArray) :: (data.Rows |> Array.map (List.ofArray) |> List.ofArray))    
        let rows = data.GetLength(0)
        let columns = data.GetLength(1)
        let widths = Array.zeroCreate columns 
        data |> Array2D.iteri (fun _ c cell ->
            widths.[c] <- max (widths.[c]) (cell.Length))
        for r in 0 .. rows - 1 do
            for c in 0 .. columns - 1 do
            wr.Write(data.[r,c].PadRight(widths.[c] + 1))
            wr.WriteLine()
        sb.ToString()

/// [omit]
type TypedHtmlDocument internal (tables:Map<string,HtmlTable>) =

    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
    static member Create(reader:TextReader) =
        let tables = 
            reader 
            |> HtmlDocument.Load
            |> HtmlRuntime.getTables
            |> List.map (fun table -> table.Name, table) 
            |> Map.ofList
        TypedHtmlDocument tables

    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
    member __.GetTable(id:string) = 
       tables |> Map.find id

/// [omit]
and HtmlTable<'rowType> internal (name:string, header:string[], values:'rowType[]) =

    member x.Name with get() = name
    member x.Headers with get() = header
    member x.Rows with get() = values

    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
    static member Create(rowConverter:Func<string[],'rowType>, doc:TypedHtmlDocument, id:string) =
       let table = doc.GetTable id
       HtmlTable<_>(table.Name, table.Headers, Array.map rowConverter.Invoke table.Rows) 
