namespace FSharp.Data.Runtime

open System
open System.Globalization
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Xml.Linq
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.HtmlExtensions
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralTypes

#nowarn "10001"



// --------------------------------------------------------------------------------------
/// Representation of an HTML table
type HtmlTable = 
    { Name : string
      HeaderNamesAndUnits : (string * Type option)[] // always set
      HasHeaders: bool // always set at designtime, never at runtime
      Data :  HtmlNode option[][]
      Html : HtmlNode }
    
    member x.ToXElement(hasHeaders:bool, headers:string[]) = 
        let headers = headers |> Array.mapi (fun i c -> i,c) |> Map.ofArray
        let rows =
            XElement(XName.Get "rows", [
                        if hasHeaders then x.Data.[1..] else x.Data
                        |> Array.mapi (fun _ cols ->
                            let data =
                                cols |> Array.mapi (fun colI n -> 
                                    match n with 
                                    | Some (HtmlElement(_,_,contents)) -> 
                                        XElement(XName.Get (headers.[colI]), HtmlNode.convertToXNode contents)
                                    | Some (HtmlText(t)) -> XElement(XName.Get (headers.[colI]), [XText(t)])
                                    | Some _ | None -> XElement(XName.Get (headers.[colI]), [XText("")]))
                            XElement(XName.Get "row", data)
                )
            ])
        XElement(XName.Get x.Name, [rows]) 

     member x.ToJson(hasHeaders:bool, headers:string[]) = 
        let headers = headers |> Array.mapi (fun i c -> i,c) |> Map.ofArray
        let rows =
            JsonValue.Array(
                        if hasHeaders then x.Data.[1..] else x.Data
                        |> Array.mapi (fun _ cols ->
                            let data =
                                cols |> Array.mapi (fun colI n -> 
                                    match n with 
                                    | Some (HtmlElement(_,_,contents)) -> 
                                        match HtmlNode.convertToJson contents with
                                        | [h] -> headers.[colI], h
                                        | h -> headers.[colI], JsonValue.Array (h |> List.toArray)
                                    | Some (HtmlText(t)) -> headers.[colI], JsonValue.String t
                                    | Some _ | None -> headers.[colI], JsonValue.Null)
                            JsonValue.Record(data)
                           ))
        JsonValue.Record([|"Rows", rows|]) 

    override x.ToString() =
        let sb = StringBuilder()
        use wr = new StringWriter(sb) 
        wr.WriteLine(x.Name)
        let data = array2D x.Data
        let rows = data.GetLength(0)
        let columns = data.GetLength(1)
        let nodeText = function
            | Some n -> HtmlNode.innerText n
            | None -> ""
        let widths = Array.zeroCreate columns 
        data |> Array2D.iteri (fun _ c cell ->
            widths.[c] <- max (widths.[c]) ((nodeText cell).Length))
        for r in 0 .. rows - 1 do
            for c in 0 .. columns - 1 do
                wr.Write((nodeText data.[r,c]).PadRight(widths.[c] + 1))
            wr.WriteLine()
        sb.ToString()

/// Representation of an HTML list
type HtmlList = 
    { Name : string
      Values : string[]
      Html : HtmlNode }

/// Representation of an HTML definition list
type HtmlDefinitionList = 
    { Name : string
      Definitions : HtmlList list
      Html : HtmlNode }

/// Representation of an HTML table, list, or definition list
type HtmlObject = 
    | Table of HtmlTable
    | List of HtmlList
    | DefinitionList of HtmlDefinitionList
    member x.Name =
        match x with
        | Table(t) -> t.Name
        | List(l) -> l.Name
        | DefinitionList(l) -> l.Name

// --------------------------------------------------------------------------------------

/// Helper functions called from the generated code for working with HTML tables
module HtmlRuntime =

    let private getName defaultName (element:HtmlNode) (parents:HtmlNode list) = 

        let parents = parents |> Seq.truncate 2 |> Seq.toList

        let tryGetName choices =
            choices
            |> List.tryPick (fun attrName -> 
                element 
                |> HtmlNode.tryGetAttribute attrName
                |> Option.map HtmlAttribute.value
            )

        let rec tryFindPrevious f (x:HtmlNode) (parents:HtmlNode list)= 
            match parents with
            | p::rest ->
                let nearest = 
                    p
                    |> HtmlNode.descendants true (fun _ -> true)
                    |> Seq.takeWhile ((<>) x) 
                    |> Seq.filter f
                    |> Seq.toList
                    |> List.rev
                match nearest with
                | [] -> tryFindPrevious f p rest
                | h :: _ -> Some h 
            | [] -> None

        let deriveFromSibling element parents = 
            let isHeading s = s |> HtmlNode.name |> HtmlParser.headingRegex.Value.IsMatch
            tryFindPrevious isHeading element parents

        let cleanup (str:String) =
            HtmlParser.wsRegex.Value.Replace(str.Replace('–', '-'), " ").Replace("[edit]", null).Trim()

        match deriveFromSibling element parents with
        | Some e -> 
            let innerText = e.InnerText()
            if String.IsNullOrWhiteSpace(innerText)
            then defaultName
            else cleanup(innerText)
        | _ ->
                match List.ofSeq <| element.Descendants("caption", false) with
                | [] ->
                     match tryGetName ["id"; "name"; "title"; "summary"] with
                     | Some name -> cleanup name
                     | _ -> defaultName
                | h :: _ -> h.InnerText()

    let private getTableHeaders (numberCols:int) (ip:HtmlInference.Parameters) (rows:(HtmlNode option)[][]) =
        let nodeText = function
            | Some n -> HtmlNode.innerText n
            | None -> ""
        let headerRow, firstDataRow = rows.[0] |> Array.map nodeText, rows.[1] |> Array.map nodeText
        let headerNamesAndUnits = headerRow |> Array.map (fun x -> x,None)
        let schema = Array.init numberCols (fun _ -> None)
        let (headerRowType, _) = CsvInference.inferType headerNamesAndUnits schema [headerRow] 1 ip.MissingValues ip.CultureInfo true ip.PreferOptionals
        let (rowType, _) = CsvInference.inferType headerNamesAndUnits schema [firstDataRow] 1 ip.MissingValues ip.CultureInfo true ip.PreferOptionals
        if headerRowType = rowType 
        then None
        else Some headerRow

    let private parseTable (ip:HtmlInference.Parameters option) includeLayoutTables makeUnique index (table:HtmlNode, parents:HtmlNode list) = 
        let rows =
            let header =
                match table.Descendants("thead", false) |> Seq.toList with
                | [ head ] ->
                    // if we have a tr in here, do nothing - we get all trs next anyway
                    match head.Descendants("tr" ,false) |> Seq.toList with
                    | [] -> [ head ]
                    | _ -> []
                | _ -> []
            header @ (table.Descendants("tr", false) |> List.ofSeq)
            |> List.mapi (fun i r -> i,r)
        
        if rows.Length <= 1 then None 
        else

        let cells = rows |> List.map (fun (_,r) -> r.Elements ["td"; "th"] |> List.mapi (fun i e -> i, e))
        let rowLengths = cells |> List.map (fun x -> x.Length)
        let numberOfColumns = List.max rowLengths
        if not includeLayoutTables && (numberOfColumns < 1) then None else
        
        let tableData = Array.init rows.Length (fun _ -> Array.init numberOfColumns (fun _ -> None))

        for rowindex, _ in rows do
            for colindex, cell in cells.[rowindex] do
                let rowSpan = max 1 (defaultArg (TextConversions.AsInteger CultureInfo.InvariantCulture cell?rowspan) 0) - 1
                let colSpan = max 1 (defaultArg (TextConversions.AsInteger CultureInfo.InvariantCulture cell?colspan) 0) - 1

                let col_i = ref colindex
                while !col_i < tableData.[rowindex].Length && tableData.[rowindex].[!col_i] <> None do incr(col_i)
                for j in [!col_i..(!col_i + colSpan)] do
                    for i in [rowindex..(rowindex + rowSpan)] do
                        if i < rows.Length && j < numberOfColumns
                        then tableData.[i].[j] <- (Some cell)

        let tableName = makeUnique (getName (sprintf "Table%d" (index + 1)) table parents)
        let (hasHeaders, headers) = 
            match ip with
            | Some ip ->  
                let headers = getTableHeaders numberOfColumns ip tableData
                let (headerWithMeasure,_) = CsvInference.parseHeaders headers numberOfColumns "" ip.UnitsOfMeasureProvider
                let headerWithMeasure = 
                    headerWithMeasure 
                    |> Array.map (fun (name, unit) -> 
                                        match unit with
                                        | Some _ -> name.Split('\n').[1].Replace(" ","_"), unit
                                        | None -> name.Split('\n').[0].Replace(" ","_"), unit
                                 )
                match headers with
                | Some _ -> true, headerWithMeasure
                | None -> false, headerWithMeasure
            | None -> false, Array.init numberOfColumns (fun i -> "Column_" + (string i), None)
        
        { 
            Name = tableName
            HeaderNamesAndUnits = headers
            HasHeaders = hasHeaders
            Data = tableData
            Html = table 
        } |> Some


    let private parseList makeUnique index (list:HtmlNode, parents:HtmlNode list) =
        
        let rec walkListItems s (items:HtmlNode list) =
            match items with
            | [] -> s
            | HtmlElement("li", _, elements) :: t -> 
                let state = 
                    elements |> List.fold (fun s node ->
                        match node with
                        | HtmlText(content) -> (content.Trim()) :: s
                        | _ -> s
                    ) s
                    |> List.rev
                walkListItems state t
            | _ :: t -> walkListItems s t
            

        let rows = 
            list.Descendants("li", false) 
            |> List.ofSeq
            |> List.collect (fun node -> walkListItems [] (node.DescendantsAndSelf() |> List.ofSeq))
            |> List.toArray
    
        if rows.Length <= 1 then None else

        let name = makeUnique (getName (sprintf "List%d" (index + 1)) list parents)

        { Name = name
          Values = rows
          Html = list } |> Some

    let private parseDefinitionList makeUnique index (definitionList:HtmlNode, parents:HtmlNode list) =
        
        let rec createDefinitionGroups (nodes:HtmlNode list) =
            let rec loop state ((groupName, _, elements) as currentGroup) (nodes:HtmlNode list) =
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
        
        let data =
            definitionList
            |> HtmlNode.descendantsNamed false ["dt"; "dd"]
            |> List.ofSeq
            |> createDefinitionGroups
            |> List.map (fun (group, node, values) -> { Name = group
                                                        Values = values |> List.rev |> List.toArray
                                                        Html = node })

        if data.Length <= 1 then None else

        let name = makeUnique (getName (sprintf "DefinitionList%d" (index + 1)) definitionList parents)
        
        { Name = name
          Definitions = data
          Html = definitionList } |> Some

    let getTables inferenceParameters includeLayoutTables (doc:HtmlDocument) =
        let tableElements = doc.DescendantsWithPath "table" |> List.ofSeq
        let tableElements = 
            if includeLayoutTables
            then tableElements
            else tableElements |> List.filter (fun (e, _) -> not (e.HasAttribute("cellspacing", "0") && e.HasAttribute("cellpadding", "0")))
        tableElements
        |> List.mapi (parseTable inferenceParameters includeLayoutTables (NameUtils.uniqueGenerator id))
        |> List.choose id

    let getLists (doc:HtmlDocument) =        
        doc
        |> HtmlDocument.descendantsNamedWithPath false ["ol"; "ul"]
        |> List.ofSeq
        |> List.mapi (parseList (NameUtils.uniqueGenerator id))
        |> List.choose id

    let getDefinitionLists (doc:HtmlDocument) =                
        doc
        |> HtmlDocument.descendantsNamedWithPath false ["dl"]
        |> List.ofSeq
        |> List.mapi (parseDefinitionList (NameUtils.uniqueGenerator id))
        |> List.choose id

    let getHtmlObjects inferenceParameters includeLayoutTables (doc:HtmlDocument) = 
        (doc |> getTables inferenceParameters includeLayoutTables |> List.map Table) 
        @ (doc |> getLists |> List.map List)
        @ (doc |> getDefinitionLists |> List.map DefinitionList)

// --------------------------------------------------------------------------------------

namespace FSharp.Data.Runtime.BaseTypes

open System
open System.ComponentModel
open System.IO
open FSharp.Data
open FSharp.Data.Runtime

/// Underlying representation of the root types generated by HtmlProvider
type HtmlDocument internal (doc:FSharp.Data.HtmlDocument, htmlObjects:Map<string,HtmlObject>) =

    member __.Html = doc

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member Create(includeLayoutTables, reader:TextReader) =
        let doc = 
            reader 
            |> HtmlDocument.Load
        let htmlObjects = 
            doc
            |> HtmlRuntime.getHtmlObjects None includeLayoutTables
            |> List.map (fun e -> e.Name, e) 
            |> Map.ofList
        HtmlDocument(doc, htmlObjects)

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsError=false)>]
    member __.GetObject(id:string) = 
        htmlObjects |> Map.find id

open System.Xml.Linq

type HtmlRuntimeTable() = 
    
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member Create(doc:HtmlDocument, id:string, hasHeaders:bool, headers:string[]) =
        match doc.GetObject id with
        | Table table -> 
            let table = table.ToXElement(hasHeaders, headers)           
            { XElement = table }
        | _ -> failwithf "Element %s is not a table" id

/// Underlying representation of list types generated by HtmlProvider
type HtmlList<'ItemType> internal (name:string, values:'ItemType[], html) = 
    
    member __.Name = name
    member __.Values = values
    member __.Html = html

    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member Create(rowConverter:Func<string,'ItemType>, doc:HtmlDocument, id:string) =
        match doc.GetObject id with
        | List list -> HtmlList<_>(list.Name, Array.map rowConverter.Invoke list.Values, list.Html)
        | _ -> failwithf "Element %s is not a list" id

    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member CreateNested(rowConverter:Func<string,'ItemType>, doc:HtmlDocument, id:string, index:int) =
        let list = 
            match doc.GetObject id with
            | List list-> list
            | DefinitionList definitionList -> definitionList.Definitions.[index]
            | _ -> failwithf "Element %s is not a list" id
        HtmlList<_>(list.Name, Array.map rowConverter.Invoke list.Values, list.Html)
