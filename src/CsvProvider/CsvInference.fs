// --------------------------------------------------------------------------------------
// Implements type inference for CSV
// --------------------------------------------------------------------------------------

module ProviderImplementation.CsvInference

open System.Text.RegularExpressions
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.TypeInference
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference

let headerRegex = new Regex(@"(?<field>.+) \((?<unit>.+)\)", RegexOptions.Compiled)

/// Infers the type of a CSV file using the specified number of rows
/// (This handles units in the same way as the original MiniCSV provider)
let inferFields (csv:CsvFile) count culture =
  
    // Infer the units and names from the headers
    let headers = csv.Headers |> Array.map (fun header ->
        let m = headerRegex.Match(header)
        if m.Success then
            let headerName = m.Groups.["field"].Value
            let unitName = m.Groups.["unit"].Value
            Some(ProvidedMeasureBuilder.Default.SI unitName), headerName
        else None, header)

    let rows = 
        if count > 0 then Seq.truncate count csv.Data
        else csv.Data

    let rows = 
        if Seq.isEmpty rows then CsvRow([| for i in 1..csv.Headers.Length -> ""|], csv.Headers) |> Seq.singleton 
        else rows

    // Infer the type of collection using structural inference
    let types = seq {
        for row in rows ->
            let fields = 
                [ for (unit, header), value in Seq.zip headers row.Columns ->
                    let typ = inferPrimitiveType culture value unit
                    { Name = header; Optional = false; Type = typ } ]
            Record(None, fields) }
 
    let typ = Seq.reduce subtypeInfered types  
   
    let convertOptionalDecimalToFloat = function
    | { Name = name; Optional = true; Type = Primitive(t, unit) } when t = typeof<decimal> -> { Name = name; Optional = false; Type = Primitive(typeof<float>, unit) }
    | p -> p

    match typ with
    | Record(_, fields) -> List.map convertOptionalDecimalToFloat fields
    | _ -> failwith "generateCsvRowType: Type inference returned wrong type"
