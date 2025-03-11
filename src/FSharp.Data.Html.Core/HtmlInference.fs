/// Structural inference for HTML tables
module FSharp.Data.Runtime.HtmlInference

open System
open System.Globalization
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralInference
open FSharp.Data.Runtime.StructuralTypes

type internal Parameters =
    { MissingValues: string[]
      CultureInfo: CultureInfo
      UnitsOfMeasureProvider: IUnitsOfMeasureProvider
      PreferOptionals: bool
      InferenceMode: InferenceMode' }

let internal inferColumns parameters (headerNamesAndUnits: _[]) rows =

    let inferRows = 0
    let schema = Array.init headerNamesAndUnits.Length (fun _ -> None)
    let assumeMissingValues = false

    CsvInference.inferColumnTypes
        headerNamesAndUnits
        schema
        rows
        inferRows
        parameters.MissingValues
        parameters.InferenceMode
        parameters.CultureInfo
        assumeMissingValues
        parameters.PreferOptionals
        parameters.UnitsOfMeasureProvider

let internal inferHeaders parameters (rows: string[][]) =
    if rows.Length <= 2 then
        false, None, None, None //Not enough info to infer anything, assume first row data
    else
        let headers = Some rows.[0]
        let numberOfColumns = rows.[0].Length

        let headerNamesAndUnits, _ =
            CsvInference.parseHeaders headers numberOfColumns "" parameters.UnitsOfMeasureProvider

        let headerRowType = inferColumns parameters headerNamesAndUnits [ rows.[0] ]
        let dataRowsType = inferColumns parameters headerNamesAndUnits rows.[1..]

        if headerRowType = dataRowsType then
            false, None, None, None
        else
            let headerNames, units = Array.unzip headerNamesAndUnits
            true, Some headerNames, Some units, Some dataRowsType

let internal inferListType parameters (values: string[]) =

    if values.Length > 0 then
        let inferedtype value =
            // If there's only whitespace, treat it as a missing value and not as a string
            if String.IsNullOrWhiteSpace value || value = "&nbsp;" || value = "&nbsp" then
                InferedType.Null
            // Explicit missing values (NaN, NA, etc.) will be treated as float unless the preferOptionals is set to true
            elif Array.exists ((=) <| value.Trim()) parameters.MissingValues then
                if parameters.PreferOptionals then
                    InferedType.Null
                else
                    InferedType.Primitive(typeof<float>, None, false, false)
            else
                getInferedTypeFromString
                    parameters.UnitsOfMeasureProvider
                    parameters.InferenceMode
                    parameters.CultureInfo
                    value
                    None

        values
        |> Array.map inferedtype
        |> Array.reduce (subtypeInfered (not parameters.PreferOptionals))
    else
        InferedType.Null
