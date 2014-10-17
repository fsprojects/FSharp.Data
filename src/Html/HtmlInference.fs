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

let inferHeaders missingValues cultureInfo unitsOfMeasureProvider preferOptionals (rows : string [][]) =
    if rows.Length <= 2
    then 0, None, None, None //Not enough info to infer anything, assume first row data
    else
      let headersAndUnits, _ = CsvInference.parseHeaders (Some rows.[0]) rows.[0].Length "" unitsOfMeasureProvider
      let headerNames = headersAndUnits |> Array.map fst
      let headerUnits = headersAndUnits |> Array.map snd
      let computeRowType row = 
          // Zip units and values to infer cell types
          let cellTypes =
            row
            |> Array.zip headerUnits
            |> Array.map (fun p -> CsvInference.inferCellType preferOptionals missingValues cultureInfo (fst p) (snd p))
          // Zip headers and types to build InferedTypes with proper names
          let props = 
            Array.zip headerNames cellTypes 
            |> Array.map (fun p -> {Name = fst p; Type = snd p})
            |> List.ofArray
          InferedType.Record(None, props, false)

      //Check if header types are different than rest of rows
      let headerRow = computeRowType rows.[0]
      let dataRow =
        rows.[1..]
        |> Array.map computeRowType
        |> Array.reduce (subtypeInfered (not preferOptionals))
      if headerRow = dataRow
      then 0, None, None, Some dataRow
      else 1, Some headerNames, Some headerUnits, Some dataRow
