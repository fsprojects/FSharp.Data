/// Structural inference for HTML tables
module FSharp.Data.Runtime.HtmlInference

open FSharp.Data.Runtime

let inferColumns preferOptionals missingValues cultureInfo (headerNamesAndUnits:_[]) rows = 

    let inferRows = 0
    let schema = Array.init headerNamesAndUnits.Length (fun _ -> None)
    let assumeMissingValues = false

    CsvInference.inferColumnTypes headerNamesAndUnits schema rows inferRows missingValues cultureInfo assumeMissingValues preferOptionals

let inferHeaders missingValues cultureInfo unitsOfMeasureProvider preferOptionals (rows : string [][]) =
    if rows.Length <= 2 then 
        0, None, None, None //Not enough info to infer anything, assume first row data
    else
        let headers = Some rows.[0]
        let numberOfColumns = rows.[0].Length
        let headerNamesAndUnits, _ = CsvInference.parseHeaders headers numberOfColumns "" unitsOfMeasureProvider
        let headerRowType = inferColumns preferOptionals missingValues cultureInfo headerNamesAndUnits [rows.[0]]
        let dataRowsType = inferColumns preferOptionals missingValues cultureInfo headerNamesAndUnits rows.[1..]      
        if headerRowType = dataRowsType then 
            0, None, None, None
        else 
            let headerNames, units = Array.unzip headerNamesAndUnits
            1, Some headerNames, Some units, Some dataRowsType
