/// Structural inference for HTML tables
module FSharp.Data.Runtime.HtmlInference

open System
open System.Globalization
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralInference
open FSharp.Data.Runtime.StructuralTypes

type XPath = string

type HtmlValue = 
    | Primitive of string * XPath
    | Link of string * HtmlValue 
    | Img of string
    | Property of string * HtmlValue
    | Record of string * HtmlValue list
    | List of HtmlValue list
    | Null
  
type Parameters = {
    MissingValues: string[]
    CultureInfo: CultureInfo
    UnitsOfMeasureProvider: IUnitsOfMeasureProvider
    PreferOptionals: bool }

let rec private annotateUnitOfMeasure (name:string) typ = 
    match typ with
    | InferedType.Primitive(typ, unit, optional) ->
        match unit with 
        | Some unit -> 
            if StructuralInference.supportsUnitsOfMeasure typ then
              name.Split('\n').[1], InferedType.Primitive(typ, Some unit, optional)
            else
              name.Split('\n').[0], InferedType.Primitive(typ, None, optional)
        | _ -> name.Split('\n').[0], InferedType.Primitive(typ, None, optional)
    | InferedType.Record(n, props, opts) ->
        name.Split('\n').[0], InferedType.Record(n, props |> List.map (fun p -> let (n, t) = annotateUnitOfMeasure p.Name p.Type in { Name = n; Type = t }), opts)
    | _ -> name.Split('\n').[0], typ

let rec private inferHtmlValueType preferOptionals missingValues cultureInfo unit (n:HtmlValue) =
    match n with
    | Primitive(value,_) -> CsvInference.inferCellType preferOptionals missingValues cultureInfo unit value
    | Link(href, content) -> 
        InferedType.Record(Some "Link", 
            [
              { Name = "Href"; Type = InferedType.Primitive(typeof<string>, None, false) }
              { Name = "Contents"; Type = (inferHtmlValueType preferOptionals missingValues cultureInfo unit content) }
            ], false)
    | Img(_) ->
        InferedType.Record(Some "Image", 
            [
              { Name = "Src"; Type = InferedType.Primitive(typeof<string>, None, false) }
            ], false)
    | Record(name, props) ->
        InferedType.Record(Some name, 
            props 
            |> List.map (function 
                         | Property(name, value) -> { Name = name; Type = (inferHtmlValueType preferOptionals missingValues cultureInfo unit value) }
                         | _ -> failwith "Records can only contain properties"
                        ), false)
    | Property _ -> failwith "Properties can only be contained within records"
    | List(values) ->
        values 
        |> List.map (inferHtmlValueType preferOptionals missingValues cultureInfo unit)
        |> StructuralInference.inferCollectionType false
    | Null -> InferedType.Null
    
let internal inferType (headerNamesAndUnits:_[]) schema (rows : seq<HtmlValue []>) inferRows missingValues cultureInfo assumeMissingValues preferOptionals =

  // If we have no data, generate one empty row with empty strings, 
  // so that we get a type with all the properties (returning string values)
  let rowsIterator = rows.GetEnumerator()
  let rows = 
    if rowsIterator.MoveNext() then
      seq {
        yield rowsIterator.Current
        try
          while rowsIterator.MoveNext() do
            yield rowsIterator.Current
        finally
          rowsIterator.Dispose()
        if assumeMissingValues then
          yield Array.create headerNamesAndUnits.Length HtmlValue.Null
      }
    else
      Array.create headerNamesAndUnits.Length HtmlValue.Null |> Seq.singleton 
  
  let rows = 
      if inferRows > 0 then Seq.truncate (if assumeMissingValues && inferRows < Int32.MaxValue then inferRows + 1 else inferRows) rows else rows
      |> Seq.toList

  // Infer the type of collection using structural inference
  let types = 
    [ for row in rows ->
        let fields = 
          [ for (name, unit), schema, value in Array.zip3 headerNamesAndUnits schema row ->
              let typ = 
                match schema with
                | Some _ -> InferedType.Null // this will be ignored, so just return anything
                | None -> inferHtmlValueType preferOptionals missingValues cultureInfo unit value
              let name, typ = annotateUnitOfMeasure name typ
              { Name = name
                Type = typ } ]
        InferedType.Record(None, fields, false) ]

  let inferedType = 
    if schema |> Array.forall Option.isSome then
        // all the columns types are already set, so all the rows will be the same
        types |> List.head
    else
        List.reduce (StructuralInference.subtypeInfered ((*allowEmptyValues*)not preferOptionals)) types
  
  match inferedType with
  | InferedType.Record(_, fields, _) -> fields
  | _ -> failwith "Expected record type"
  |> Seq.toList 

let inferColumns parameters (headerNamesAndUnits:_[]) (rows : seq<HtmlValue []>) = 

    let inferRows = 0
    let schema = Array.init headerNamesAndUnits.Length (fun _ -> None)
    let assumeMissingValues = false

    let inferedTypes = inferType headerNamesAndUnits schema rows inferRows parameters.MissingValues parameters.CultureInfo assumeMissingValues parameters.PreferOptionals

    if (Seq.length rows) = 1
    then Seq.zip (Seq.nth 0 rows) inferedTypes
    else Seq.zip (Seq.nth 1 rows) inferedTypes
    |> Seq.toList
    

let inferHeaders parameters (rows : HtmlValue [][]) =
    if rows.Length <= 2 then 
        false, None, None, None //Not enough info to infer anything, assume first row data
    else
        let headers = Some (rows.[0] |> Array.map (function | Primitive (d,_) -> d | _ -> ""))
        let numberOfColumns = rows.[0].Length
        let headerNamesAndUnits, _ = CsvInference.parseHeaders headers numberOfColumns "" parameters.UnitsOfMeasureProvider
        let headerRowType = inferColumns parameters headerNamesAndUnits [rows.[0]] |> List.map snd
        let dataRowsType = inferColumns parameters headerNamesAndUnits rows.[1..] |> List.map snd
        if headerRowType = dataRowsType then 
            false, None, None, None
        else 
            let headerNames, units = Array.unzip headerNamesAndUnits
            true, Some headerNames, Some units, Some dataRowsType

let inferListType parameters values = 

    let inferedtype value = 
        // If there's only whitespace, treat it as a missing value and not as a string
        if String.IsNullOrWhiteSpace value || value = "&nbsp;" || value = "&nbsp" then InferedType.Null
        // Explicit missing values (NaN, NA, etc.) will be treated as float unless the preferOptionals is set to true
        elif Array.exists ((=) <| value.Trim()) parameters.MissingValues then 
            if parameters.PreferOptionals then InferedType.Null else InferedType.Primitive(typeof<float>, None, false)
        else getInferedTypeFromString parameters.CultureInfo value None

    values
    |> Seq.map inferedtype
    |> Seq.reduce (subtypeInfered (not parameters.PreferOptionals))