namespace FSharp.Data.Runtime

open System
open System.ComponentModel
open System.IO
open System.Text
open System.Xml
open FSharp.Data.Runtime

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

type HtmlTable = {
    Name : string
    Headers : string []
    Rows :  string [] []  
}

module Html = 

    let name (x:HtmlElement) =
        match x with
        | HtmlElement(name, _, _) -> name.ToLowerInvariant()
        | HtmlScript(_) -> "script"
        | _ -> String.Empty

    let tryGetAttribute (name : string) (x : HtmlElement) =
        match x with
        | HtmlElement(_,attr,_) -> attr |> List.tryFind (fun a -> a.Name.ToLowerInvariant() = (name.ToLowerInvariant()))
        | _ -> None

    let getAttributeAs parseF name (e:HtmlElement) = 
        match tryGetAttribute name e with
        | Some(HtmlAttribute(_, colspan)) -> parseF(colspan)
        | None -> 0

    let children (x:HtmlElement) =
        match x with
        | HtmlElement(_, _, children) -> children
        | _ -> []

    let hasAttribute name (value:string) (x:HtmlElement) =
        tryGetAttribute name x
        |> function 
           | Some(attr) ->  attr.Value.ToLowerInvariant() = (value.ToLowerInvariant())
           | None -> false 

    let getElementsNamed (names:seq<string>) (e:HtmlElement) =
        let nameSet = Set.ofSeq (names |> Seq.map (fun n -> n.ToLowerInvariant()))
        let rec named' (e:HtmlElement) = 
            [
                  if nameSet.Contains(name e)
                  then yield e   
                  else 
                    for child in (children e) do
                        yield! named' child     
            ]
        named' e

    let hasChild (names:seq<string>) (e:HtmlElement) =
        let nameSet = Set.ofSeq (names |> Seq.map (fun n -> n.ToLowerInvariant()))
        (children e) |> List.exists (name >> nameSet.Contains)
    
    let rec getValue = function
        | HtmlElement(_,_, content) ->
            String.Join(" ", seq { for e in content do
                                       match e with
                                       | HtmlText(text) -> yield text.Trim()
                                       | elem -> yield getValue elem })
        | HtmlText(text) | HtmlCharRef(text) -> text.Trim()
        | HtmlScript _ | HtmlComment _ | HtmlStyle _ -> String.Empty

    let tryGetBody (HtmlDocument(_, es)) = 
        es
        |> List.tryPick (fun e ->
            match getElementsNamed ["body"] e with
            | [] -> None
            | h::_ -> Some(h)
        )
            

    let write (writer:TextWriter) (element:HtmlElement) =
        let createXmlWriter(baseWriter:TextWriter) =
            let s = new System.Xml.XmlWriterSettings(Indent = false,
                                                     OmitXmlDeclaration = true, 
                                                     ConformanceLevel = System.Xml.ConformanceLevel.Auto)
            XmlWriter.Create(baseWriter, s)
        
        let rec writeElement (writer:XmlWriter) = function
            | HtmlText(c) -> writer.WriteValue(c)
            | HtmlCharRef(c) -> writer.WriteValue(c)
            | HtmlComment(c) -> writer.WriteComment(c)
            | HtmlScript(c) -> writer.WriteCData(c)
            | HtmlStyle(c) -> writer.WriteCData(c)
            | HtmlElement(name, attrs, elems) ->
                writer.WriteStartElement(name)
                for attr in attrs do
                    match attr with
                    | HtmlAttribute(key,value) -> 
                        if String.IsNullOrEmpty(value)
                        then writer.WriteStartAttribute(key); writer.WriteEndAttribute()
                        else writer.WriteAttributeString(key, value)
                for elem in elems do 
                    writeElement writer elem

                writer.WriteEndElement()
    
        use writer = createXmlWriter(writer)
        writeElement writer element

    module Table =

        let private getName defaultName (element:HtmlElement) = 
            let tryGetName' choices =
                choices
                |> List.tryPick (fun (attrName) -> 
                    match tryGetAttribute attrName element with
                    | Some(HtmlAttribute(_,value)) -> Some <| value
                    | None -> None
                )
            match tryGetName' [ "id"; "name"; "title"; "summary"] with
            | Some(name) -> name
            | None ->
                    match getElementsNamed ["caption"] element with
                    | [] -> defaultName
                    | h :: _ -> (getValue h)

                    
        let parseTable index (table:HtmlElement) = 
            let rows = getElementsNamed ["tr"] table |> List.mapi (fun i r -> i,r)
            if rows.Length <= 1 
            then None
            else
                let cells = rows |> List.map (snd >> getElementsNamed ["td"; "th"] >> List.mapi (fun i e -> (i,e)))
                let width = (cells |> List.maxBy (fun x -> x.Length)).Length
                let res = Array.init rows.Length  (fun _ -> Array.init width (fun _ -> Empty))
                for (rowindex, _) in rows do
                    for (colindex, cell) in cells.[rowindex] do
                        let rowSpan, colSpan = (max 1 (getAttributeAs Int32.Parse "rowspan" cell)) - 1,(max 1 (getAttributeAs Int32.Parse "colspan" cell)) - 1
                        let data =
                            let getContents contents = String.Join(" ", List.map getValue contents).Trim()
                            match cell with
                            | HtmlElement("td",_,contents) -> Cell (false, getContents contents)
                            | HtmlElement("th",_,contents) -> Cell (true, getContents contents)
                            | _ -> Empty
                        let col_i = ref colindex
                        while res.[rowindex].[!col_i] <> Empty do incr(col_i)
                        for j in [!col_i..(!col_i + colSpan)] do
                            for i in [rowindex..(rowindex + rowSpan)] do
                                res.[i].[j] <- data

                let headers = 
                    if res.[0] |> Array.forall (fun r -> r.IsHeader) 
                    then res.[0] |> Array.map (fun x -> x.Data)
                    else res.[0] |> Array.map (fun x -> x.Data) //Humm!! need better semantics around detecting headers
                    
                {
                    Name = (getName ("Table_" + (string index)) table)
                    Headers = headers
                    Rows = res.[1..] |> Array.map (Array.map (fun x -> x.Data))
                } |> Some
    
        let getTables (HtmlDocument(_, doc)) =
            List.collect (getElementsNamed ["table"]) doc
            |> List.mapi parseTable
    
        let parse (str:string) = 
            match HtmlParser.parse str with
            | None -> List.empty
            | Some(doc) ->
                getTables doc
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

type HtmlTable<'rowType> internal (name: string, header : string[], values: 'rowType[]) =
    member x.Name with get() = name
    member x.Headers with get() = header
    member x.Data with get() = values

    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
    static member Create(rowConverter:Func<string[],'rowType>, id:string, src:string) =
       let tables = Html.Table.parse src
       let table = tables |> Seq.pick (fun table -> if table.Name = id then Some table else None)
       let convertRow r = rowConverter.Invoke(r)
       let data =  
            table.Rows
            |> Array.map (convertRow)
       let result = 
            new HtmlTable<_>(table.Name, table.Headers, data) 
       result