// --------------------------------------------------------------------------------------
// Implements type inference for CSV
// --------------------------------------------------------------------------------------

module ProviderImplementation.CsvInference

open System
open System.Text.RegularExpressions
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.TypeInference
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference

/// Infers the type of a CSV file using the specified number of rows
/// (This handles units in the same way as the original MiniCSV provider)
let inferFields (csv:CsvFile) count culture =
  
  // Infer the units and names from the headers
  let headers = csv.Headers |> Seq.map (fun header ->
    let m = Regex.Match(header, @"(?<field>.+) \((?<unit>.+)\)")
    if m.Success then
      let headerName = m.Groups.["field"].Value
      let unitName = m.Groups.["unit"].Value
      Some(ProvidedMeasureBuilder.Default.SI unitName), headerName
    else None, header)
  
  // Infer the type of collection using structural inference
  let types = seq {
    for row in Seq.truncate count csv.Data ->
      let fields = 
        [ for (unit, header), value in Seq.zip headers row.Columns ->
            // Treat empty values as 'null' values. The inference will
            // infer heterogeneous types e.g. 'null + int', which are then 
            // truned into Nullable<int> (etc.) in the CSV type generator
            let typ = 
              if String.IsNullOrWhiteSpace(value) then Null
              else inferPrimitiveType culture value unit
            { Name = header; Optional = false; Type = typ } ]
      Record(None, fields) }

  Seq.reduce subtypeInfered types  