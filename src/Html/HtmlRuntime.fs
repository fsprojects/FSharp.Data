namespace FSharp.Data.Runtime

open System
open System.IO
open System.Xml
open System.Reflection
open System.Text

#if INTERACTIVE 
open FSharp.Data.Runtime
#endif


type HtmlTable = {
    Name : string
    Headers : string list
    Rows : string list list   
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

        let private tryGetName (element:HtmlElement) = 
            let tryGetName' choices =
                choices
                |> List.tryPick (fun (attrName) -> 
                    match tryGetAttribute attrName element with
                    | Some(HtmlAttribute(_,value)) -> Some <| value
                    | None -> None
                )
            tryGetName' [ "id"; "name"; "title"; "summary"]

        let private (|THead|_|) (table:HtmlElement) =
            match getElementsNamed ["thead"] table with
            | [] -> None
            | h :: _ -> Some(THead(h |> getElementsNamed ["th"; "td"] |> List.map getValue))

        let private (|ThInFirstRow|_|) (table:HtmlElement) = 
            match getElementsNamed ["tr"] table with
            | [] -> None
            | h :: _ when hasChild ["th"] h-> Some(ThInFirstRow(h |> getElementsNamed ["th"] |> List.map getValue))
            | _ -> None

        let private parseTable index (table:HtmlElement) =
            let (skipRows, headers) = 
                match table with
                | THead headers -> 0, headers
                | ThInFirstRow headers -> 1, headers
                | table -> 
                    match table |> getElementsNamed ["tr"] with
                    | [] -> 0,[]
                    | h :: _ -> 1, h |> getElementsNamed ["td"; "th"] |> List.map getValue

            let tableName = 
                match tryGetName table with
                | None -> 
                    match getElementsNamed ["caption"] table with
                    | [] -> "Table_" + (string index)
                    | h :: _ -> (getValue h)
                | Some(name) -> name
                    
            let parseData skipRows (tableBody:seq<HtmlElement>) = 
                tableBody
                |> Seq.skip skipRows
                |> Seq.filter (fun e ->
                                let name = (name e)
                                name <> "thead" && name <> "tfoot"
                              )
                |> Seq.collect (getElementsNamed ["tr"])
                |> Seq.map (getElementsNamed ["td"; "th"] >> List.map getValue)
                |> Seq.filter (fun x -> (not <| List.isEmpty x) && (x <> headers) && (x.Length = headers.Length))
                |> Seq.toList
                
    
            match table with
            | HtmlElement("table", _, content) as table ->
                {
                   Name = tableName
                   Headers = headers
                   Rows = (parseData skipRows content)
                }
            | _ -> failwithf "expected table element"
    
        let parse (str:string) = 
            match HtmlParser.parse str with
            | None -> List.empty
            | Some(HtmlDocument(_, element)) ->
                let rec tables' (element:HtmlElement) =
                    [
                        if (name element) = "table"
                        then yield element
                        else 
                            for child in (children element) do
                                yield! tables' child
                    ]
                List.collect tables' element 
                |> List.mapi (parseTable)
               // |> List.filter (fun x -> x.Rows.Length > 0)

type HtmlTable<'rowType>internal(name, header, values) =
    member x.Name with get() = name
    member x.Headers with get() = header
    member x.Data with get() = values

    static member Create(rowConverter:Func<string[],'rowType>, id, headers, src:string) =
       let tables = 
            Html.Table.parse src 
            |> Seq.pick (fun table ->
                if table.Name = id 
                then Some table
                else None)
       let data =  
            tables.Rows
            |> Seq.toArray
            |> Array.map (fun r -> rowConverter.Invoke(r |> Seq.toArray))
       new HtmlTable<'rowType>(id, headers, data) 