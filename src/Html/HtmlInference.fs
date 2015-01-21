/// Structural inference for HTML tables
module FSharp.Data.Runtime.HtmlInference

open System
open System.Globalization
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralInference
open FSharp.Data.Runtime.StructuralTypes

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MicroDataSchema =

    module Utils = 
    
        let (|Attr|_|) (name:string) (n:HtmlNode) = 
            let attr = (HtmlNode.tryGetAttribute name n)
            attr |> Option.map (fun x -> x.Value())

        let getPath str = 
            (match Uri.TryCreate(str, UriKind.Absolute) with 
             | true, uri -> uri.LocalPath 
             | false, _ -> "").Trim('/')

    type Type = 
        | Primitive of ResizeArray<string>
        | Link
        | Img
        | Scoped of string
    
    type Property = {
        Name : string
        Type : Type
        IsOptional : bool
    }
    
    type Scope = {
         FullName : string
         Name : string
         Properties : ResizeArray<Property>
    }
    with
        static member Empty = {
            FullName = String.Empty
            Name = String.Empty
            Properties = new ResizeArray<_>()
        }

    let createScope fullName = 
        {
            FullName = fullName
            Name = (Utils.getPath fullName)
            Properties = new ResizeArray<_>()
        }
    
    let addProperty (prop:Property) (scope:Scope) = 
        match scope.Properties |> Seq.tryFind (fun x -> x.Name = prop.Name) with
        | Some(h) -> 
            match h.Type, prop.Type with
            | Primitive(s), Primitive(s') -> s.AddRange(s')
            | _ -> () 
        | None -> scope.Properties.Add prop

    let getType (n:HtmlNode) = 
        let nodeName = n.Name()
        match nodeName with
        | "a" | "link" -> Link
        | "img" -> Img
        | "meta" -> 
            let valueAttrs = ["content"; "value"; "src"]
            match valueAttrs |> List.tryPick (n.TryGetAttribute) with
            | Some attr -> Primitive (new ResizeArray<_>([attr.Value()]))
            | None -> Primitive (new ResizeArray<_>([n.InnerText()]))
        | _ -> Primitive (new ResizeArray<_>([n.InnerText()]))
   
    let rec walkElements currentScope scopes (n:HtmlNode) = 
        match n with
        | Utils.Attr "itemscope" _ & Utils.Attr "itemtype" scope & Utils.Attr "itemprop" prop ->  
              addProperty { Name = prop; Type = Scoped scope; IsOptional = false } currentScope
              let newScope = createScope scope
              HtmlNode.elements n |> List.fold (walkElements newScope) (newScope :: scopes)
        | Utils.Attr "itemtype" scope -> 
              let newScope = createScope scope
              HtmlNode.elements n |> List.fold (walkElements newScope) (newScope :: scopes)
        | Utils.Attr "itemprop" prop ->
              let typ = getType n
              addProperty { Name = prop; Type = typ; IsOptional = false } currentScope
              HtmlNode.elements n |> List.fold (walkElements currentScope) scopes
        | _ -> HtmlNode.elements n |> List.fold (walkElements currentScope) scopes

    let build (doc:HtmlDocument) = 
        HtmlDocument.descendants false (HtmlNode.hasAttribute "itemscope" "") doc
        |> Seq.fold (walkElements Scope.Empty) []

module internal Utils = 
    
    let (|Attr|_|) (name:string) (n:HtmlNode) = 
        let attr = (HtmlNode.tryGetAttribute name n)
        attr |> Option.map (fun x -> x.Value())

    let getPath str = 
        (match Uri.TryCreate(str, UriKind.Absolute) with 
         | true, uri -> uri.LocalPath 
         | false, _ -> "").Trim('/')
  
type Parameters = {
    MissingValues: string[]
    CultureInfo: CultureInfo
    UnitsOfMeasureProvider: IUnitsOfMeasureProvider
    PreferOptionals: bool }

let private inferNodeType preferOptionals missingValues cultureInfo unit (n:HtmlNode) =
    let nodeName = n.Name()
    match nodeName with
    | "a" | "link" -> 
        InferedType.Record(Some "Link", 
            [
              { Name = "Href"; Type = InferedType.Primitive(typeof<string>, None, false) }
              { Name = "Contents"; Type = InferedType.Primitive(typeof<string>, None, false) }
            ], false)
    | "img" ->
        InferedType.Record(Some "Image", 
            [
              { Name = "Src"; Type = InferedType.Primitive(typeof<string>, None, false) }
            ], false)
    | "meta" -> 
        let valueAttrs = ["content"; "value"; "src"]
        let value = 
            match valueAttrs |> List.tryPick (n.TryGetAttribute) with
            | Some attr -> attr.Value()
            | None -> n.InnerText()
        CsvInference.inferCellType preferOptionals missingValues cultureInfo unit value
    | _ -> CsvInference.inferCellType preferOptionals missingValues cultureInfo unit (n.InnerText())

let private tryInferMicroDataType preferOptionals missingValues cultureInfo unit (n:HtmlNode []) = 

    let rec walk state (n:HtmlNode) = 
        match n with
        | Utils.Attr "itemscope" _ & Utils.Attr "itemtype" scope & Utils.Attr "itemprop" prop ->  
              { Name = prop; Type = InferedType.Record(Some scope, HtmlNode.elements n |> List.fold walk [], false) } :: state
        | Utils.Attr "itemtype" scope -> 
              { Name = scope; Type = InferedType.Record(Some scope, HtmlNode.elements n |> List.fold walk [], false) } :: state
        | Utils.Attr "itemprop" prop ->
              { Name = prop; Type = (inferNodeType preferOptionals missingValues cultureInfo unit n) } :: state
        | _ ->  HtmlNode.elements n |> List.fold (walk) state
    
    match n |> Array.fold walk [] with
    | [] -> None
    | a -> Some (InferedType.Record(None, a, false))


let rec private annotateUnitOfMeasure (name:string) typ =
    let name = name.Split('\n').[0]
    match typ with
    | InferedType.Primitive(typ, unit, optional) ->
        match unit with 
        | Some unit -> 
            if StructuralInference.supportsUnitsOfMeasure typ then
              name, InferedType.Primitive(typ, Some unit, optional)
            else
              name, InferedType.Primitive(typ, None, optional)
        | _ -> name, InferedType.Primitive(typ, None, optional)
    | InferedType.Record(n, props, opts) ->
        name, InferedType.Record(n, props |> List.map (fun p -> let (n, t) = annotateUnitOfMeasure p.Name p.Type in { Name = n; Type = t }), opts)

let internal inferType (headerNamesAndUnits:_[]) schema (rows : seq<(HtmlNode []) []>) inferRows missingValues cultureInfo assumeMissingValues preferOptionals =

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
          yield Array.create headerNamesAndUnits.Length [||]
      }
    else
      Array.create headerNamesAndUnits.Length [||] |> Seq.singleton 
  
  let rows = if inferRows > 0 then Seq.truncate (if assumeMissingValues && inferRows < Int32.MaxValue then inferRows + 1 else inferRows) rows else rows

  // Infer the type of collection using structural inference
  let types = 
    [ for row in rows ->
        let fields = 
          [ for (name, unit), schema, value in Array.zip3 headerNamesAndUnits schema row ->
              let typ = 
                match schema with
                | Some _ -> InferedType.Null // this will be ignored, so just return anything
                | None -> 
                    match tryInferMicroDataType preferOptionals missingValues cultureInfo unit value with
                    | Some t -> t
                    | None -> CsvInference.inferCellType preferOptionals missingValues cultureInfo unit (value |> Array.toList |> HtmlNode.innerTextConcat)
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

let inferColumns parameters (headerNamesAndUnits:_[]) (rows : seq<(HtmlNode []) []>) = 

    let inferRows = 0
    let schema = Array.init headerNamesAndUnits.Length (fun _ -> None)
    let assumeMissingValues = false

    inferType headerNamesAndUnits schema rows inferRows parameters.MissingValues parameters.CultureInfo assumeMissingValues parameters.PreferOptionals

let inferHeaders parameters (rows : (HtmlNode []) [][]) =
    if rows.Length <= 2 then 
        false, None, None, None //Not enough info to infer anything, assume first row data
    else
        let headers = Some (rows.[0] |> Array.map (Array.toList >> HtmlNode.innerTextConcat))
        let numberOfColumns = rows.[0].Length
        let headerNamesAndUnits, _ = CsvInference.parseHeaders headers numberOfColumns "" parameters.UnitsOfMeasureProvider
        let headerRowType = inferColumns parameters headerNamesAndUnits [rows.[0]]
        let dataRowsType = inferColumns parameters headerNamesAndUnits rows.[1..]      
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