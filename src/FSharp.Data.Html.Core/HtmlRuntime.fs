namespace FSharp.Data.Runtime

open System
open System.Globalization
open System.Text
open FSharp.Data
open FSharp.Data.HtmlExtensions
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes

#nowarn "10001"

// --------------------------------------------------------------------------------------

/// <summary>Representation of an HTML table cell</summary>
/// <namespacedoc>
///   <summary>Contains the types used by FSharp.Data type providers at runtime.</summary>
/// </namespacedoc>
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

/// Representation of an HTML table cell
type HtmlTable
    internal
    (
        name: string,
        headerNamesAndUnits: (string * Type option)[] option, // always set at designtime, never at runtime
        inferedProperties: PrimitiveInferedProperty list option, // sometimes set at designtime, never at runtime
        hasHeaders: bool option, // always set at designtime, never at runtime
        rows: string[][],
        html: HtmlNode
    ) =
    member _.Name = name

    // always set at designtime, never at runtime
    member internal _.HeaderNamesAndUnits = headerNamesAndUnits

    // sometimes set at designtime, never at runtime
    member internal _.InferedProperties = inferedProperties

    member _.HasHeaders = hasHeaders // always set at designtime, never at runtime

    member _.Rows = rows

    member _.Html = html

    override _.ToString() =
        let sb = StringBuilder()
        sb.AppendLine name |> ignore
        let data = array2D rows
        let rows = data.GetLength(0)
        let columns = data.GetLength(1)
        let widths = Array.zeroCreate columns

        data
        |> Array2D.iteri (fun _ c cell -> widths.[c] <- max (widths.[c]) (cell.Length))

        for r in 0 .. rows - 1 do
            for c in 0 .. columns - 1 do
                sb.Append(data.[r, c].PadRight(widths.[c] + 1)) |> ignore

            sb.AppendLine() |> ignore

        sb.ToString()

/// Representation of an HTML list
type HtmlList =
    { Name: string
      Values: string[]
      Html: HtmlNode }

    override x.ToString() =
        let sb = StringBuilder()
        sb.AppendLine x.Name |> ignore

        for value in x.Values do
            sb.AppendLine value |> ignore

        sb.ToString()

/// Representation of an HTML definition list
type HtmlDefinitionList =
    { Name: string
      Definitions: HtmlList list
      Html: HtmlNode }

    override x.ToString() =
        let sb = StringBuilder()
        sb.AppendLine x.Name |> ignore

        for list in x.Definitions do
            sb.AppendLine list.Name |> ignore

            for value in list.Values do
                sb.AppendLine value |> ignore

        sb.ToString()

/// Representation of an HTML table, list, or definition list
type HtmlObjectDescription =
    | Table of HtmlTable
    | List of HtmlList
    | DefinitionList of HtmlDefinitionList

    member x.Name =
        match x with
        | Table t -> t.Name
        | List l -> l.Name
        | DefinitionList dl -> dl.Name

// --------------------------------------------------------------------------------------

/// Helper functions called from the generated code for working with HTML tables
module HtmlRuntime =

    let private normalizeWs (str: String) =
        HtmlParser.wsRegex.Value.Replace(str.Replace('–', '-'), " ").Replace("[edit]", null).Trim()

    let private getName defaultName (element: HtmlNode) (parents: HtmlNode list) =

        let parents = parents |> Seq.truncate 2 |> Seq.toList

        let tryGetName choices =
            choices
            |> List.tryPick (fun attrName ->
                element |> HtmlNode.tryGetAttribute attrName |> Option.map HtmlAttribute.value)

        let rec tryFindPrevious f (x: HtmlNode) (parents: HtmlNode list) =
            match parents with
            | p :: rest ->
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
            let isHeading s =
                s |> HtmlNode.name |> HtmlParser.headingRegex.Value.IsMatch

            tryFindPrevious isHeading element parents

        match deriveFromSibling element parents with
        | Some e ->
            let innerText = e.InnerText()

            if String.IsNullOrWhiteSpace(innerText) then
                defaultName
            else
                normalizeWs innerText
        | _ ->
            match List.ofSeq <| element.Descendants("caption", false) with
            | [] ->
                match tryGetName [ "id"; "name"; "title"; "summary"; "aria-label" ] with
                | Some name -> normalizeWs name
                | _ -> defaultName
            | h :: _ -> h.InnerText()

    module private Array =

        let countWhile predicate array =
            let mutable i = 0

            while i < Array.length array && predicate array.[i] do
                i <- i + 1

            i

    let private innerTextExcluding' recurse exclusions n =
        let exclusions = "style" :: "script" :: exclusions

        let isAriaHidden (n: HtmlNode) =
            match n.TryGetAttribute "aria-hidden" with
            | Some a ->
                match bool.TryParse(a.Value()) with
                | true, v -> v
                | false, _ -> false
            | None -> false

        let rec innerText' inRoot n =
            let exclusions = if inRoot then [ "style"; "script" ] else exclusions

            match n with
            | HtmlElement(name, _, content) when List.forall ((<>) name) exclusions && not (isAriaHidden n) ->
                seq {
                    for e in content do
                        match e with
                        | HtmlText(text) -> yield text
                        | HtmlComment(_) -> yield ""
                        | elem -> if recurse then yield innerText' false elem else yield ""
                }
                |> String.Concat
            | HtmlText(text) -> text
            | _ -> ""

        innerText' true n

    let private innerTextExcluding exclusions n = innerTextExcluding' true exclusions n

    let private parseTable
        inferenceParameters
        includeLayoutTables
        makeUnique
        index
        (table: HtmlNode, parents: HtmlNode list)
        =
        let rowSpan cell =
            max 1 (defaultArg (TextConversions.AsInteger CultureInfo.InvariantCulture cell?rowspan) 0)

        let colSpan cell =
            max 1 (defaultArg (TextConversions.AsInteger CultureInfo.InvariantCulture cell?colspan) 0)

        let rows =
            let header =
                match table.Descendants("thead", false) |> Seq.toList with
                | [ head ] ->
                    // if we have a tr in here, do nothing - we get all trs next anyway
                    match head.Descendants("tr", false) |> Seq.toList with
                    | [] -> [ head ]
                    | _ -> []
                | _ -> []

            header @ (table.Descendants("tr", false) |> List.ofSeq)
            |> List.mapi (fun i r -> i, r)

        if rows.Length <= 1 then
            None
        else

            let cells =
                rows
                |> List.map (fun (_, r) -> r.Elements [ "td"; "th" ] |> List.mapi (fun i e -> i, e))

            let rowLengths = cells |> List.map (fun row -> row |> List.sumBy (snd >> colSpan))

            let numberOfColumns = List.max rowLengths

            if not includeLayoutTables && (numberOfColumns < 1) then
                None
            else

                let name = makeUnique (getName (sprintf "Table%d" (index + 1)) table parents)

                let res =
                    Array.init rows.Length (fun _ -> Array.init numberOfColumns (fun _ -> Empty))

                for rowindex, _ in rows do
                    for colindex, cell in cells.[rowindex] do
                        let data =
                            let getContents contents =
                                contents
                                |> List.map (innerTextExcluding [ "table"; "ul"; "ol"; "dl"; "sup"; "sub" ])
                                |> String.Concat
                                |> normalizeWs

                            match cell with
                            | HtmlElement("td", _, contents) -> Cell(false, getContents contents)
                            | HtmlElement("th", _, contents) -> Cell(true, getContents contents)
                            | _ -> Empty

                        let mutable col_i = colindex

                        while col_i < res.[rowindex].Length && res.[rowindex].[col_i] <> Empty do
                            col_i <- col_i + 1

                        for j in [ col_i .. (col_i + colSpan cell - 1) ] do
                            for i in [ rowindex .. (rowindex + rowSpan cell - 1) ] do
                                if i < rows.Length && j < numberOfColumns then
                                    res.[i].[j] <- data

                let numberOfHeaderRows =
                    res |> Array.countWhile (Array.forall (fun cell -> cell.IsHeader))

                let hasRealHeaders, res =
                    match numberOfHeaderRows with
                    | 0 -> false, res
                    | 1 -> true, res
                    | _ ->
                        for i = 1 to numberOfHeaderRows - 1 do
                            for j = 0 to numberOfColumns - 1 do
                                let previousCell = res.[i - 1].[j]
                                let thisCell = res.[i].[j]

                                if
                                    previousCell.Data <> ""
                                    && thisCell.Data <> ""
                                    && thisCell.Data <> previousCell.Data
                                then
                                    res.[i].[j] <- Cell(true, previousCell.Data + " - " + thisCell.Data)

                        true, res.[numberOfHeaderRows - 1 ..]

                let hasHeaders, headerNamesAndUnits, inferedProperties =
                    match inferenceParameters with
                    | None -> None, None, None
                    | Some inferenceParameters ->
                        let hasHeaders, headerNames, units, inferedProperties =
                            if hasRealHeaders then
                                true, res.[0] |> Array.map (fun x -> x.Data) |> Some, None, None
                            else
                                res
                                |> Array.map (Array.map (fun x -> x.Data))
                                |> HtmlInference.inferHeaders inferenceParameters

                        // headers and units may already be parsed in inferHeaders
                        let headerNamesAndUnits =
                            match headerNames, units with
                            | Some headerNames, Some units -> Array.zip headerNames units
                            | _, _ ->
                                CsvInference.parseHeaders
                                    headerNames
                                    numberOfColumns
                                    ""
                                    inferenceParameters.UnitsOfMeasureProvider
                                |> fst

                        Some hasHeaders, Some headerNamesAndUnits, inferedProperties

                let rows = res |> Array.map (Array.map (fun x -> x.Data))

                HtmlTable(name, headerNamesAndUnits, inferedProperties, hasHeaders, rows, table)
                |> Some

    let private parseList makeUnique index (list: HtmlNode, parents: HtmlNode list) =

        let rows =
            list.Descendants("li", true)
            |> Seq.map (innerTextExcluding [ "table"; "ul"; "ol"; "dl"; "sup"; "sub" ] >> normalizeWs)
            |> Seq.toArray

        if rows.Length <= 1 then
            None
        else

            let name = makeUnique (getName (sprintf "List%d" (index + 1)) list parents)

            { Name = name
              Values = rows
              Html = list }
            |> Some

    let private parseDefinitionList makeUnique index (definitionList: HtmlNode, parents: HtmlNode list) =

        let rec createDefinitionGroups (nodes: HtmlNode list) =
            let rec loop state ((groupName, _, elements) as currentGroup) (nodes: HtmlNode list) =
                match nodes with
                | [] -> (currentGroup :: state) |> List.rev
                | h :: t when HtmlNode.name h = "dt" ->
                    loop (currentGroup :: state) (NameUtils.nicePascalName (HtmlNode.innerText h), h, []) t
                | h :: t -> loop state (groupName, h, ((HtmlNode.innerText h) :: elements)) t

            match nodes with
            | [] -> []
            | h :: t when HtmlNode.name h = "dt" -> loop [] (NameUtils.nicePascalName (HtmlNode.innerText h), h, []) t
            | h :: t -> loop [] ("Undefined", h, []) t

        let data =
            definitionList
            |> HtmlNode.descendantsNamed false [ "dt"; "dd" ]
            |> List.ofSeq
            |> createDefinitionGroups
            |> List.map (fun (group, node, values) ->
                { Name = group
                  Values = values |> List.rev |> List.toArray
                  Html = node })

        if data.Length <= 1 then
            None
        else

            let name =
                makeUnique (getName (sprintf "DefinitionList%d" (index + 1)) definitionList parents)

            { Name = name
              Definitions = data
              Html = definitionList }
            |> Some

    let internal getTables inferenceParameters includeLayoutTables (doc: HtmlDocument) =
        let tableElements = doc.DescendantsWithPath "table" |> List.ofSeq

        let tableElements =
            if includeLayoutTables then
                tableElements
            else
                tableElements
                |> List.filter (fun (e, _) ->
                    not (e.HasAttribute("cellspacing", "0") && e.HasAttribute("cellpadding", "0")))

        tableElements
        |> List.mapi (parseTable inferenceParameters includeLayoutTables (NameUtils.uniqueGenerator id))
        |> List.choose id

    let getLists (doc: HtmlDocument) =
        doc
        |> HtmlDocument.descendantsNamedWithPath false [ "ol"; "ul" ]
        |> List.ofSeq
        |> List.mapi (parseList (NameUtils.uniqueGenerator id))
        |> List.choose id

    let getDefinitionLists (doc: HtmlDocument) =
        doc
        |> HtmlDocument.descendantsNamedWithPath false [ "dl" ]
        |> List.ofSeq
        |> List.mapi (parseDefinitionList (NameUtils.uniqueGenerator id))
        |> List.choose id

    let internal getHtmlObjects inferenceParameters includeLayoutTables (doc: HtmlDocument) =
        Seq.concat
            [ doc |> getTables inferenceParameters includeLayoutTables |> List.map Table
              doc |> getLists |> List.map List
              doc |> getDefinitionLists |> List.map DefinitionList ]

// --------------------------------------------------------------------------------------

namespace FSharp.Data.Runtime.BaseTypes

open System
open System.ComponentModel
open System.IO
open FSharp.Data
open FSharp.Data.Runtime

/// Underlying representation of the root types generated by HtmlProvider
type HtmlDocument internal (doc, tables, lists, definitionLists) =

    member _.Html = doc

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    static member Create(includeLayoutTables, reader: TextReader) =
        let doc = reader |> HtmlDocument.Load

        let tables =
            doc
            |> HtmlRuntime.getTables None includeLayoutTables
            |> List.map (fun e -> e.Name, e)
            |> Map.ofList

        let lists =
            doc |> HtmlRuntime.getLists |> List.map (fun e -> e.Name, e) |> Map.ofList

        let definitionLists =
            doc
            |> HtmlRuntime.getDefinitionLists
            |> List.map (fun e -> e.Name, e)
            |> Map.ofList

        HtmlDocument(doc, tables, lists, definitionLists)

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    member _.GetTable(id: string) = tables |> Map.find id

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    member _.GetList(id: string) = lists |> Map.find id

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    member _.GetDefinitionList(id: string) = definitionLists |> Map.find id

/// Underlying representation of table types generated by HtmlProvider
type HtmlTable<'RowType> internal (name: string, headers: string[] option, values: 'RowType[], html: HtmlNode) =

    member _.Name = name
    member _.Headers = headers
    member _.Rows = values
    member _.Html = html

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    static member Create(rowConverter: Func<string[], 'RowType>, doc: HtmlDocument, id: string, hasHeaders: bool) =
        let table = doc.GetTable id

        let headers, rows =
            if hasHeaders then
                Some table.Rows.[0], table.Rows.[1..]
            else
                None, table.Rows

        HtmlTable<_>(table.Name, headers, Array.map rowConverter.Invoke rows, table.Html)

/// Underlying representation of list types generated by HtmlProvider
type HtmlList<'ItemType> internal (name: string, values: 'ItemType[], html) =

    member _.Name = name
    member _.Values = values
    member _.Html = html

    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    static member Create(rowConverter: Func<string, 'ItemType>, doc: HtmlDocument, id: string) =
        let list = doc.GetList id
        HtmlList<_>(list.Name, Array.map rowConverter.Invoke list.Values, list.Html)

    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    static member CreateNested(rowConverter: Func<string, 'ItemType>, doc: HtmlDocument, id: string, index: int) =
        let definitionList = doc.GetDefinitionList id
        let list = definitionList.Definitions.[index]
        HtmlList<_>(list.Name, Array.map rowConverter.Invoke list.Values, list.Html)
