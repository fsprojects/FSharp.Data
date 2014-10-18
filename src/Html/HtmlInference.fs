/// Structural inference for HTML tables
module FSharp.Data.Runtime.HtmlInference

open System.Globalization
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralInference

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
