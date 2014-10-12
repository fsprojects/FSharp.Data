/// Structural inference for HTML tables
module FSharp.Data.Runtime.HtmlInference

open System
open System.Globalization
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralInference
open FSharp.Data.Runtime.StructuralTypes

let inferRowType preferOptionals missingValues cultureInfo headers row = 

    let makeUnique = NameUtils.uniqueGenerator id

    let getName headers index = 
        if Array.isEmpty headers || String.IsNullOrWhiteSpace headers.[index]
        then "Column" + (index+1).ToString()
        else headers.[index]

    let inferProperty index value =
        
        let name = getName headers index |> makeUnique
        let typ = CsvInference.inferCellType preferOptionals missingValues cultureInfo value None

        { Name = name
          Type = typ }
    
    InferedType.Record(None, row |> Array.mapi inferProperty |> Seq.toList, false)

let inferColumns preferOptionals missingValues cultureInfo headers rows = 

    let rec inferedTypeToProperty name typ =
        match typ with
        | InferedType.Primitive(typ, unit, optional) -> 
            let wrapper = 
                if optional
                then if preferOptionals then TypeWrapper.Option else TypeWrapper.Nullable
                else TypeWrapper.None
            PrimitiveInferedProperty.Create(name, typ, wrapper, unit)
        | _ -> PrimitiveInferedProperty.Create(name, typeof<string>, preferOptionals, None)
    
    let inferedType =
        rows
        |> Seq.map (inferRowType preferOptionals missingValues cultureInfo headers)
        |> Seq.reduce (subtypeInfered (not preferOptionals))
    
    match inferedType with
    | InferedType.Record(None, props, false) -> 
        props |> List.map (fun p -> inferedTypeToProperty p.Name p.Type)
    | _ -> failwithf "inferType: Expected record type, got %A" inferedType

let inferHeaders (rows : string [][]) =
    if rows.Length <= 2
    then 0, [||] //Not enough info to infer anything, assume first row data
    else
        let computeRowType row = 
            inferRowType false TextConversions.DefaultMissingValues CultureInfo.InvariantCulture [||] row
        let headerRow = computeRowType rows.[0]
        let dataRow = rows.[1..] |> Array.map computeRowType |> Array.reduce (subtypeInfered false)
        if headerRow = dataRow
        then 0, [||]
        else 1, rows.[0]
