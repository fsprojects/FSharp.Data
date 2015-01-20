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

type Parameters = {
    MissingValues: string[]
    CultureInfo: CultureInfo
    UnitsOfMeasureProvider: IUnitsOfMeasureProvider
    PreferOptionals: bool }

let inferColumns parameters (headerNamesAndUnits:_[]) rows = 

    let inferRows = 0
    let schema = Array.init headerNamesAndUnits.Length (fun _ -> None)
    let assumeMissingValues = false

    CsvInference.inferColumnTypes headerNamesAndUnits schema rows inferRows parameters.MissingValues parameters.CultureInfo assumeMissingValues parameters.PreferOptionals

let inferHeaders parameters (rows : string [][]) =
    if rows.Length <= 2 then 
        false, None, None, None //Not enough info to infer anything, assume first row data
    else
        let headers = Some rows.[0]
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