/// Structural inference for HTML tables
module FSharp.Data.Runtime.HtmlInference

open System
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralInference
open FSharp.Data.Runtime.StructuralTypes

let inferColumns preferOptionals missingValues cultureInfo (headerNamesAndUnits:_[]) rows = 

    let inferRows = 0
    let schema = Array.init headerNamesAndUnits.Length (fun _ -> None)
    let assumeMissingValues = false

    CsvInference.inferColumnTypes headerNamesAndUnits schema rows inferRows missingValues cultureInfo assumeMissingValues preferOptionals

let inferHeaders missingValues cultureInfo (rows : string [][]) =
    if rows.Length <= 2
    then 0, None //Not enough info to infer anything, assume first row data
    else
      let preferOptionals = false // it's irrelevant for this step
      let computeRowType row = 
          let props =
              row 
              |> Array.map (CsvInference.inferCellType preferOptionals missingValues cultureInfo None)
              |> Array.map (fun typ -> { Name = ""; Type = typ })
              |> List.ofArray
          InferedType.Record(None, props, false)
      let headerRow = computeRowType rows.[0]
      let dataRow = rows.[1..] |> Array.map computeRowType |> Array.reduce (subtypeInfered false)
      if headerRow = dataRow
      then 0, None
      else 1, Some rows.[0]

let inferListType preferOptionals missingValues cultureInfo values = 

    let inferedtype value = 
        // If there's only whitespace, treat it as a missing value and not as a string
        if String.IsNullOrWhiteSpace value || value = "&nbsp;" || value = "&nbsp" then InferedType.Null
        // Explicit missing values (NaN, NA, etc.) will be treated as float unless the preferOptionals is set to true
        elif Array.exists ((=) <| value.Trim()) missingValues then 
            if preferOptionals then InferedType.Null else InferedType.Primitive(typeof<float>, None, false)
        else getInferedTypeFromString cultureInfo value None

    values
    |> Seq.map inferedtype
    |> Seq.reduce (subtypeInfered (not preferOptionals))