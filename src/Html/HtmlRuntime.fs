namespace FSharp.Data.Runtime

open System
open System.ComponentModel
open System.IO
open System.Text
open System.Text.RegularExpressions
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes

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
      HeaderNamesAndUnits : (string * Type option)[] option // always set at designtime, never at runtime
      InferedProperties : PrimitiveInferedProperty list option // sometimes set at designtime, never at runtime
      HasHeaders: bool option // always set at designtime, never at runtime
      Rows :  string [] []
      Html : HtmlNode }
    override x.ToString() =
        let sb = StringBuilder()
        use wr = new StringWriter(sb) 
        wr.WriteLine(x.Name)
        let data = array2D x.Rows
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

// --------------------------------------------------------------------------------------

/// Helper functions called from the generated code for working with HTML tables
module HtmlRuntime =

    let private getTableName defaultName (element:HtmlNode) (parents:HtmlNode list) = 

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
                
    let private parseTable parameters includeLayoutTables makeUnique index (table:HtmlNode, parents:HtmlNode list) = 

        let rows = table.Descendants(["tr"], true, false) |> List.mapi (fun i r -> i,r)
        
        if rows.Length <= 1 then None else

        let cells = rows |> List.map (fun (_,r) -> r.Elements ["td"; "th"] |> List.mapi (fun i e -> i, e))
        let rowLengths = cells |> List.map (fun x -> x.Length)
        let numberOfColumns = List.max rowLengths
        
        if not includeLayoutTables && (rowLengths |> List.filter (fun x -> x > 1) |> List.length <= 1) then None else

        let tableName = makeUnique (getTableName (sprintf "Table%d" (index + 1)) table parents)

        let res = Array.init rows.Length (fun _ -> Array.init numberOfColumns (fun _ -> Empty))
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

        let hasHeaders, headerNamesAndUnits, inferedProperties = 
            match parameters with
            | None -> None, None, None
            | Some parameters ->
                let hasHeaders, headerNames, units, inferedProperties = 
                    if res.[0] |> Array.forall (fun r -> r.IsHeader) 
                    then true, res.[0] |> Array.map (fun x -> x.Data) |> Some, None, None
                    else res
                          |> Array.map (Array.map (fun x -> x.Data))
                          |> HtmlInference.inferHeaders parameters
        
                // headers and units may already be parsed in inferHeaders
                let headerNamesAndUnits =
                  match headerNames, units with
                  | Some headerNames, Some units -> Array.zip headerNames units
                  | _, _ -> CsvInference.parseHeaders headerNames numberOfColumns "" parameters.UnitsOfMeasureProvider |> fst

                Some hasHeaders, Some headerNamesAndUnits, inferedProperties

        let rows = res |> Array.map (Array.map (fun x -> x.Data))

        Some { Name = tableName
               HeaderNamesAndUnits = headerNamesAndUnits
               InferedProperties = inferedProperties
               HasHeaders = hasHeaders
               Rows = rows 
               Html = table }

    let getTables parameters includeLayoutTables (doc:HtmlDocument) =

        let tableElements = 
            [ doc.Elements ["table"]
              |> List.map (fun table -> table, [])
              
              doc.Elements(fun x -> x.Elements ["table"] <> [])
              |> List.collect (fun parent -> parent.Elements ["table"] |> List.map (fun table -> table, [parent]))
              
              doc.Descendants(fun x -> x.Elements(fun x -> x.Elements ["table"] <> []) <> [])
              |> List.collect (fun grandParent -> grandParent.Elements() |> List.collect (fun parent -> parent.Elements ["table"] |> List.map (fun table -> table, [parent; grandParent])))
            ]
            |> List.concat
            |> (fun x -> if includeLayoutTables 
                         then x 
                         else x |> List.filter (fun (e,_) -> (e.HasAttribute("cellspacing", "0") && e.HasAttribute("cellpadding", "0")) |> not)
                )
        
        tableElements
        |> List.mapi (parseTable parameters includeLayoutTables (NameUtils.uniqueGenerator id))
        |> List.choose id

type TypedHtmlDocument internal (doc:HtmlDocument, tables:Map<string,HtmlTable>) =

    member x.Html = doc

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member Create(includeLayoutTables, reader:TextReader) =
        let doc = 
            reader 
            |> HtmlDocument.Load
        let tables = 
            doc
            |> HtmlRuntime.getTables None includeLayoutTables
            |> List.map (fun table -> table.Name, table) 
            |> Map.ofList
        TypedHtmlDocument(doc, tables)

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    member __.GetTable(id:string) = 
       tables |> Map.find id

and HtmlTable<'rowType> internal (name:string, headers:string[] option, values:'rowType[], html:HtmlNode) =

    member __.Name = name
    member __.Headers = headers
    member __.Rows = values

    member __.Html = html

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member Create(rowConverter:Func<string[],'rowType>, doc:TypedHtmlDocument, id:string, hasHeaders:bool) =
       let table = doc.GetTable id
       let headers, rows = 
          if hasHeaders then
              Some table.Rows.[0], table.Rows.[1..]
          else
              None, table.Rows
       HtmlTable<_>(table.Name, headers, Array.map rowConverter.Invoke rows, table.Html)
