/// Structural inference for HTML tables
module FSharp.Data.Runtime.HtmlInference

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
    then 0, None, None //Not enough info to infer anything, assume first row data
    else
      let preferOptionals = false // it's irrelevant for this step
      let computeRowType row = 
          let cellTypes =
            row
            |> Array.map (CsvInference.inferCellType preferOptionals missingValues cultureInfo None)
          let props = // Zip headers and types to build properly named InferedTypes
            Array.zip rows.[0] cellTypes 
            |> Array.map (fun p -> {Name = fst p; Type = snd p})
            |> List.ofArray
          InferedType.Record(None, props, false)

      //Check if header types are different than rest of rows
      let headerRow = computeRowType rows.[0]
      let dataRow =
        rows.[1..]
        |> Array.map computeRowType
        |> Array.reduce (subtypeInfered preferOptionals)
      if headerRow = dataRow
      then 0, None, Some dataRow
      else 1, Some rows.[0], Some dataRow
