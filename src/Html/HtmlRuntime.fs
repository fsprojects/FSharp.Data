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
      HeaderNamesAndUnits : (string * Type option)[]
      Rows :  string [] []
      Html : HtmlNode }
    member x.Headers = Array.map fst x.HeaderNamesAndUnits
    override x.ToString() =
        let sb = StringBuilder()
        use wr = new StringWriter(sb) 
        wr.WriteLine(x.Name)
        let headers = 
            x.HeaderNamesAndUnits 
            |> Array.map (fun (header, unit) ->
                match unit with
                | None -> header
                | Some unit -> header + " (" + unit.Name + ")")
        let data = array2D <| List.ofArray headers :: (x.Rows |> Array.map (List.ofArray) |> List.ofArray)
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
                | DefinitionList(l) -> l.Name
                     
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

    let private getName defaultName (element:HtmlNode) (parents:HtmlNode list) = 

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

        let cleanup (str:String) =
            HtmlParser.wsRegex.Value.Replace(str.Replace('–', '-'), " ").Replace("[edit]", null).Trim()

        match deriveFromSibling element parents with
        | Some e -> cleanup(e.InnerText())
        | _ ->
                match element.Descendants ["caption"] with
                | [] ->
                     match tryGetName ["id"; "name"; "title"; "summary"] with
                     | Some name -> cleanup name
                     | _ -> defaultName
                | h :: _ -> h.InnerText()
                
    let private parseTable includeLayoutTables missingValues cultureInfo unitsOfMeasureProvider makeUnique index (table:HtmlNode, parents:HtmlNode list)= 

        let rows = table.Descendants(["tr"], true, false) |> List.mapi (fun i r -> i,r)
        
        if rows.Length <= 1 then None else

        let cells = rows |> List.map (fun (_,r) -> r.Elements ["td"; "th"] |> List.mapi (fun i e -> i, e))
        let rowLengths = cells |> List.map (fun x -> x.Length)
        let numberOfColumns = List.max rowLengths
        
        if not includeLayoutTables && (rowLengths |> List.filter (fun x -> x > 1) |> List.length <= 1) then None else

        let res = Array.init rows.Length  (fun _ -> Array.init numberOfColumns (fun _ -> Empty))
        for rowindex, _ in rows do
            for colindex, cell in cells.[rowindex] do
                let rowSpan = max 1 (cell.GetAttributeValue(0, Int32.TryParse, "rowspan")) - 1
                let colSpan = max 1 (cell.GetAttributeValue(0, Int32.TryParse, "colspan")) - 1

                let data =
                    let getContents contents = 
                        (contents |> List.map (HtmlNode.innerTextExcluding ["table"; "ul"; "ol"; "sup"; "sub"]) |> String.Concat).Replace(Environment.NewLine, "").Trim()
                    match cell with
                    | HtmlElement("td", _, contents) -> Cell (false, getContents contents)
                    | HtmlElement("th", _, contents) -> Cell (true, getContents contents)
                    | _ -> Empty
                let col_i = ref colindex
                while !col_i < res.[rowindex].Length && res.[rowindex].[!col_i] <> Empty do incr(col_i)
                for j in [!col_i..(!col_i + colSpan)] do
                    for i in [rowindex..(rowindex + rowSpan)] do
                        if i < rows.Length && j < numberOfColumns
                        then res.[i].[j] <- data

        let startIndex, headers = 
            if res.[0] |> Array.forall (fun r -> r.IsHeader) 
            then 1, res.[0] |> Array.map (fun x -> x.Data) |> Some
            else res |> Array.map (Array.map (fun x -> x.Data)) |> HtmlInference.inferHeaders missingValues cultureInfo
            
        let headerNamesAndUnits, _ = CsvInference.parseHeaders headers numberOfColumns "" unitsOfMeasureProvider

        let tableName = makeUnique (getName (sprintf "Table%d" (index + 1)) table parents)
        let rows = res.[startIndex..] |> Array.map (Array.map (fun x -> x.Data))

        Some { Name = tableName
               HeaderNamesAndUnits = headerNamesAndUnits
               Rows = rows 
               Html = table }

    let getTables includeLayoutTables missingValues cultureInfo unitsOfMeasureProvider (doc:HtmlDocument) =

        let tableElements = 
            findElementWithRelations ["table"] doc
            |> (fun x -> if includeLayoutTables 
                         then x 
                         else x |> List.filter (fun (e,_) -> (e.HasAttribute("cellspacing", "0") && e.HasAttribute("cellpadding", "0")) |> not)
                )
        
        tableElements
        |> List.mapi (parseTable includeLayoutTables missingValues cultureInfo unitsOfMeasureProvider (NameUtils.uniqueGenerator id))
        |> List.choose id

    let private getList listType doc = findElementWithRelations listType doc

    let getLists (doc:HtmlDocument) =
        let (count, lists) = 
            getList ["ol"; "ul"] doc
            |> List.fold (fun (index, lists) (list, parents) -> 
                let name = getName (sprintf "List%d" (index + 1)) list parents
                let rows = (list.Descendants(["li"])) |> List.map (HtmlNode.innerText) |> List.toArray
                let list =
                    { Name = name; 
                      Values = rows
                      Html = list } |> List
                ((index + 1), list::lists)
            ) (0, [])
        lists |> List.rev

    let getDefinitionLists (doc:HtmlDocument) =
        let rec createDefinitionGroups (nodes:HtmlNode list) =
            let rec loop state ((groupName,_,elements) as currentGroup) (nodes:HtmlNode list) =
                match nodes with
                | [] -> (currentGroup :: state) |> List.rev
                | h::t when HtmlNode.name h = "dt" ->
                    loop (currentGroup :: state) (NameUtils.nicePascalName (HtmlNode.innerText h), h, []) t
                | h::t ->
                    loop state (groupName, h, ((HtmlNode.innerText h) :: elements)) t
            match nodes with
            | [] -> []
            | h :: t when HtmlNode.name h = "dt" -> loop [] (NameUtils.nicePascalName (HtmlNode.innerText h), h, []) t
            | h :: t -> loop [] ("Undefined", h, []) t
 
        let (_, lists) = 
            getList ["dl"] doc
            |> List.fold (fun (index, lists) (list, parents) -> 
                let name = getName (sprintf "DefinitionList%d" (index + 1)) list parents
                let data =
                    list.Descendants ["dt"; "dd"]
                    |> createDefinitionGroups
                    |> List.map (fun (group,node,values) -> { Name = group; Values = values |> List.rev |> List.toArray; Html = node })
                let list =
                    { Name = name; 
                      Definitions = data
                      Html = list } |> DefinitionList
                ((index + 1), list::lists)
            ) (0, [])
        lists |> List.rev

    let getHtmlElements includeLayoutTables missingValues cultureInfo unitsOfMeasureProvider (doc:HtmlDocument) = 
        (getTables includeLayoutTables missingValues cultureInfo unitsOfMeasureProvider doc |> List.map Table) 
        @ getLists doc
        @ getDefinitionLists doc

type TypedHtmlDocument internal (doc:HtmlDocument, tables:Map<string,HtmlObject>) =

    member x.Html = doc

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member Create(includeLayoutTables, missingValuesStr, cultureStr, reader:TextReader) =
        let missingValues = TextRuntime.GetMissingValues missingValuesStr
        let cultureInfo = TextRuntime.GetCulture cultureStr
        let doc = 
            reader 
            |> HtmlDocument.Load
        let htmlObjects = 
            doc
            |> HtmlRuntime.getHtmlElements includeLayoutTables missingValues cultureInfo None
            |> List.map (fun e -> e.Name, e) 
            |> Map.ofList
        TypedHtmlDocument(doc, htmlObjects)

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    member __.GetObject(id:string) = 
       tables |> Map.find id

and HtmlTable<'rowType> internal (name:string, values:'rowType[], headers:string[], html) =

    member x.Name = name
    member x.Headers = headers
    member x.Rows = values
    member x.Html = html

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member Create(rowConverter:Func<string[],'rowType>, doc:TypedHtmlDocument, id:string) =
       let table = doc.GetObject id
       match table with
       | Table(t) -> HtmlTable<_>(t.Name, Array.map rowConverter.Invoke t.Rows, t.Headers, t.Html)
       | _ -> failwithf "Element %s is not a table" id

and HtmlList<'itemType> internal (name:string, values:'itemType[], html) = 
    
    member x.Name = name
    member x.Values = values
    member x.Html = html

    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member Create(rowConverter:Func<string,'itemType>, doc:TypedHtmlDocument, id:string) =
       let list = doc.GetObject id
       match list with
       | List(l) -> HtmlList<_>(l.Name, Array.map rowConverter.Invoke l.Values, l.Html)
       | _ -> failwithf "Element %s is not a list" id

    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member CreateNested(rowConverter:Func<string,'itemType>, doc:TypedHtmlDocument, id:string, index:int) =
       let list = doc.GetObject id
       match list with
       | List(l) -> HtmlList<_>(l.Name, Array.map rowConverter.Invoke l.Values, l.Html)
       | DefinitionList(dl) -> 
            let l = dl.Definitions.[index]
            HtmlList<_>(l.Name, Array.map rowConverter.Invoke l.Values, l.Html)
       | _ -> failwithf "Element %s is not a list" id