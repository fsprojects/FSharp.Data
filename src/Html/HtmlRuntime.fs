namespace FSharp.Data.Runtime

open System
open System.ComponentModel
open System.Globalization
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Xml
open FSharp.Data
open FSharp.Data.Runtime

#nowarn "10001"

// --------------------------------------------------------------------------------------

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

type HtmlTable = 
    { Name : string
      Headers : string []
      Rows :  string [] []
      Html : HtmlNode }
    override x.ToString() =
        let sb = StringBuilder()
        use wr = new StringWriter(sb) 
        wr.WriteLine(x.Name)
        let data = array2D ((x.Headers |> List.ofArray) :: (x.Rows |> Array.map (List.ofArray) |> List.ofArray))    
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

type HtmlList = 
    {
        Name : string
        Values : string []
        Html : HtmlNode
    }

type HtmlDefinitionList = 
    {
       Name : string
       Definitions : HtmlList list
       Html : HtmlNode
    }

type HtmlObject = 
    | Table of HtmlTable
    | List of HtmlList
    | DefinitionList of HtmlDefinitionList
    with 
        member x.Name
            with get() = 
                match x with
                | Table(t) -> t.Name
                | List(l) -> l.Name
                | DefinitionList(dl) -> dl.Name
        member x.Headers
            with get() = 
                match x with
                | Table(t) -> t.Headers
                | List(_) -> [|"Value"|]
                | DefinitionList(dl) -> dl.Definitions |> List.map (fun l -> l.Name) |> List.toArray
        member x.Values
             with get() = 
                 match x with
                 | Table(t) -> t.Rows
                 | List(t) -> [|t.Values|]
                 | DefinitionList(dl) ->  dl.Definitions |> List.map (fun l -> l.Values) |> List.toArray
        member x.Html
            with get() = 
                match x with
                | List(l) -> l.Html
                | DefinitionList(dl) -> dl.Html
                | Table(t) -> t.Html
                     
 // --------------------------------------------------------------------------------------

/// Helper functions called from the generated code for working with HTML tables
module HtmlRuntime =
    
    let private findElementWithRelations (names:seq<string>) (doc:HtmlDocument) =
        [ doc.Elements names
          |> List.map (fun table -> table, [])
          
          doc.Elements(fun x -> x.Elements names <> [])
          |> List.collect (fun parent -> parent.Elements names |> List.map (fun table -> table, [parent]))
          
          doc.Descendants(fun x -> x.Elements(fun x -> x.Elements names <> []) <> [])
          |> List.collect (fun grandParent -> grandParent.Elements() |> List.collect (fun parent -> parent.Elements names |> List.map (fun table -> table, [parent; grandParent])))
        ]
        |> List.concat

    let private getName defaultName nameSet (element:HtmlNode) (parents:HtmlNode list) = 

        let tryGetName choices =
            choices
            |> List.tryPick (fun (attrName) -> 
                match element.TryGetAttribute attrName with
                | Some(HtmlAttribute(_,value)) -> Some <| value
                | None -> None
            )

        let rec tryFindPrevious f (x:HtmlNode) (parents:HtmlNode list)= 
            match parents with
            | p::rest ->
                let nearest = 
                    HtmlNode.descendants true (fun _ -> true) p 
                    |> Seq.takeWhile ((<>) x) 
                    |> Seq.filter f
                    |> Seq.toList |> List.rev
                match nearest with
                | [] -> tryFindPrevious f p rest
                | h :: _ -> Some h 
            | [] -> None

        let deriveFromSibling element parents = 
            let isHeading s =
                let name = HtmlNode.name s
                Regex.IsMatch(name, """h\d""")
            tryFindPrevious isHeading element parents            

        match deriveFromSibling element parents with
        | Some(e) when not(Set.contains e.InnerText nameSet) -> e.InnerText
        | _ ->
                match element.Descendants ["caption"] with
                | [] ->
                     match tryGetName [ "id"; "name"; "title"; "summary"] with
                     | Some(name) when not(Set.contains name nameSet) -> name
                     | _ -> defaultName
                | h :: _ -> h.InnerText
                
    let private parseTable includeLayoutTables (index, nameSet) (table:HtmlNode) (parents:HtmlNode list) = 

        let rows = table.Descendants(["tr"], true, false) |> List.mapi (fun i r -> i,r)
        
        if rows.Length <= 1 then None else

        let cells = rows |> List.map (fun (_,r) -> r.Elements ["td"; "th"] |> List.mapi (fun i e -> i, e))
        let rowLengths = cells |> List.map (fun x -> x.Length)
        let width = List.max rowLengths
        
        if not includeLayoutTables && (rowLengths |> List.filter (fun x -> x > 1) |> List.length <= 1) then None else

        let res = Array.init rows.Length  (fun _ -> Array.init width (fun _ -> Empty))
        for rowindex, _ in rows do
            for colindex, cell in cells.[rowindex] do
                let rowSpan = max 1 (cell.GetAttributeValue(0, Int32.TryParse, "rowspan")) - 1
                let colSpan = max 1 (cell.GetAttributeValue(0, Int32.TryParse, "colspan")) - 1

                let data =
                    let getContents contents = 
                        (contents |> List.map (HtmlNode.innerTextExcluding ["table"; "li"]) |> String.Concat).Replace(Environment.NewLine, "").Trim()
                    match cell with
                    | HtmlElement("td", _, contents) -> Cell (false, getContents contents)
                    | HtmlElement("th", _, contents) -> Cell (true, getContents contents)
                    | _ -> Empty
                let col_i = ref colindex
                while !col_i < res.[rowindex].Length && res.[rowindex].[!col_i] <> Empty do incr(col_i)
                for j in [!col_i..(!col_i + colSpan)] do
                    for i in [rowindex..(rowindex + rowSpan)] do
                        if i < rows.Length && j < width
                        then res.[i].[j] <- data

        let startIndex, headers = 
            if res.[0] |> Array.forall (fun r -> r.IsHeader) 
            then 1, res.[0] |> Array.map (fun x -> x.Data)
            else HtmlInference.inferHeaders (res |> Array.map (Array.map (fun x -> x.Data)))
            
        let headers = 
            if headers.Length = 0
            then Array.zeroCreate res.[0].Length
            else headers

        let headers = headers |> Array.mapi (fun i header -> 
            if String.IsNullOrWhiteSpace header
            then sprintf "Column%d" (i + 1)
            else header)

        let tableName = getName (sprintf "Table%d" index) nameSet table parents
        let rows = res.[startIndex..] |> Array.map (Array.map (fun x -> x.Data))

        Some { Name = tableName
               Headers = headers
               Rows = rows 
               Html = table }

    let getTables includeLayoutTables (doc:HtmlDocument) =
        let tableElements = 
            findElementWithRelations ["table"] doc
            |> (fun x -> if includeLayoutTables 
                         then x 
                         else x |> List.filter (fun (e,_) -> (e.HasAttribute("cellspacing", "0") && e.HasAttribute("cellpadding", "0")) |> not)
                )
        let (_,_,tables) =
            tableElements
            |> List.fold (fun (index, names, tables) (table, parents) -> 
                            match parseTable includeLayoutTables (index, names) table parents with
                            | Some(table) -> index + 1, Set.add table.Name names, table::tables
                            | None -> index + 1, names, tables
                         ) (0, Set.empty, [])
        tables |> List.rev

    let getLists (doc:HtmlDocument) =
        let getList listType = findElementWithRelations listType doc

        let (count, nonDefinitionLists) =
            let (count, _, lists) = 
                getList ["ol"; "ul"]
                |> List.fold (fun (index, names, lists) (list, parents) -> 
                    let name = getName (sprintf "List%d" index) names list parents
                    let rows = (list.Descendants ["li"]) |> List.map (fun r -> r.InnerText) |> List.toArray
                    let list =
                        { Name = name; 
                          Values = rows
                          Html = list } |> List
                    ((index + 1), (Set.add name names), list::lists)
                ) (0, Set.empty, [])
            (count, lists |> List.rev)

        let definitionLists =
            let rec createDefinitionGroups (nodes:HtmlNode list) =
                let rec loop state ((groupName,_,elements) as currentGroup) (nodes:HtmlNode list) =
                    match nodes with
                    | [] -> (currentGroup :: state) |> List.rev
                    | h::t when h.Name = "dt" ->
                        loop (currentGroup :: state) (NameUtils.nicePascalName h.InnerText, h, []) t
                    | h::t ->
                        loop state (groupName, h, (h.InnerText :: elements)) t
                match nodes with
                | [] -> []
                | h :: t when h.Name = "dt" -> loop [] (NameUtils.nicePascalName h.InnerText, h, []) t
                | h :: t -> loop [] ("Undefined", h, []) t
 
            let (_, _, lists) = 
                getList ["dl"]
                |> List.fold (fun (index, names, lists) (list, parents) -> 
                    let name = getName (sprintf "List%d" index) names list parents
                    let data =
                        list.Descendants ["dt"; "dd"]
                        |> createDefinitionGroups
                        |> List.map (fun (group,node,values) -> { Name = group; Values = values |> List.toArray; Html = node })
                    let list =
                        { Name = name; 
                          Definitions = data
                          Html = list } |> DefinitionList
                    ((index + 1), (Set.add name names), list::lists)
                ) (count, Set.empty, [])
            lists |> List.rev

        nonDefinitionLists @ definitionLists

    let getHtmlElements includeLayoutTables (doc:HtmlDocument) = 
        (getTables includeLayoutTables doc |> List.map Table) @ getLists doc

type TypedHtmlDocument internal (doc:HtmlDocument, tables:Map<string,HtmlObject>) =

    member x.Html = doc

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member Create(includeLayoutTables:bool, reader:TextReader) =
        let doc = 
            reader 
            |> HtmlDocument.Load
        let htmlObjects = 
            doc
            |> HtmlRuntime.getHtmlElements includeLayoutTables
            |> List.map (fun e -> e.Name, e) 
            |> Map.ofList
        TypedHtmlDocument(doc, htmlObjects)

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    member __.GetObject(id:string) = 
       tables |> Map.find id

and HtmlObject<'rowType> internal (htmlObj:HtmlObject, values:'rowType[]) =

    member x.Name = htmlObj.Name
    member x.Headers = htmlObj.Headers
    member x.Rows = values
    member x.Html = htmlObj.Html


    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member Create(rowConverter:Func<string[],'rowType>, doc:TypedHtmlDocument, id:string) =
       let table = doc.GetObject id
       HtmlObject<_>(table, Array.map rowConverter.Invoke table.Values)
