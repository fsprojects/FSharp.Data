/// Structural inference for HTML tables
module FSharp.Data.Runtime.HtmlInference

open System
open System.Globalization
open FSharp.Data.Runtime.StructuralInference
open FSharp.Data.Runtime.StructuralTypes

let inferRowType preferOptionals missingValues cultureInfo headers row = 

    let getName headers index = 
        if Array.isEmpty headers || index >= headers.Length || String.IsNullOrWhiteSpace headers.[index]
        then "Column" + (index + 1).ToString()
        else headers.[index]

    let inferProperty index value =
        
        let inferedtype = 
            // If there's only whitespace, treat it as a missing value and not as a string
            if String.IsNullOrWhiteSpace value || value = "&nbsp;" || value = "&nbsp" then InferedType.Null
            // Explicit missing values (NaN, NA, etc.) will be treated as float unless the preferOptionals is set to true
            elif Array.exists ((=) <| value.Trim()) missingValues then 
                if preferOptionals then InferedType.Null else InferedType.Primitive(typeof<float>, None, false)
            else getInferedTypeFromString cultureInfo value None

        { Name = getName headers index
          Type = inferedtype }
    
    StructuralTypes.InferedType.Record(None, row |> Array.mapi inferProperty |> Seq.toList, false)

let inferColumns preferOptionals missingValues cultureInfo headers rows = 

    let rec inferedTypeToProperty name typ =
        match typ with
        | InferedType.Primitive(typ, unit, optional) -> 
            let wrapper = 
                if optional
                then if preferOptionals then TypeWrapper.Option else TypeWrapper.Nullable
                else TypeWrapper.None
            PrimitiveInferedProperty.Create(name, typ, wrapper, unit)
        | InferedType.Null -> PrimitiveInferedProperty.Create(name, typeof<float>, false, None)
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
    let computeRowType row = 
        inferRowType false TextConversions.DefaultMissingValues CultureInfo.InvariantCulture [||] row

//    let inferedTypeOfHeadersString row = 
//        match row with
//        | StructuralTypes.InferedType.Record(None, props, false) ->
//            props |> List.forall (fun p -> p.Type = InferedType.Primitive(typeof<string>, None, false))
//        | _ -> false

    let headerRow = computeRowType rows.[0]
    let dataRow =  rows.[1..] |> Array.map computeRowType |> Array.reduce (subtypeInfered false)
    if headerRow = dataRow
    then 0, [||]
    else 1, rows.[0] 