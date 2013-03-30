// --------------------------------------------------------------------------------------
// Implements type inference for CSV
// --------------------------------------------------------------------------------------

module ProviderImplementation.CsvInference

open System
open System.Text.RegularExpressions
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.StructuralTypes
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference

// Compiled regex is not supported in Silverlight
let regexOptions = 
#if FX_NO_REGEX_COMPILATION
  RegexOptions.None
#else
  RegexOptions.Compiled
#endif
let headerRegex = new Regex(@"(?<field>.+) \((?<unit>.+)\)", regexOptions)

/// Infers the type of a CSV file using the specified number of rows
/// (This handles units in the same way as the original MiniCSV provider)
let inferType (csv:CsvFile) count culture =
  
  // Infer the units and names from the headers
  let headers = csv.Headers |> Array.map (fun header ->
    let m = headerRegex.Match(header)
    if m.Success then
      let headerName = m.Groups.["field"].Value
      let unitName = m.Groups.["unit"].Value
      Some(ProvidedMeasureBuilder.Default.SI unitName), headerName
    else None, header)

  // If we have no data, generate empty row with empty strings, 
  // so that we get a type with all the properties (returning string values)
  let rows = 
    if Seq.isEmpty csv.Data then CsvRow([| for i in 1..csv.Headers.Length -> ""|], csv.Headers) |> Seq.singleton 
    elif count > 0 then Seq.truncate count csv.Data
    else csv.Data

  // Infer the type of collection using structural inference
  let types = seq {
    for row in rows ->
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

/// Generates the fields for a CSV row. The CSV provider should be
/// numerical-friendly, so we do a few simple adjustments:
///  
///  - Fields of type 'int + null' are generated as Nullable<int>
///  - Fields of type 'int64 + null' are generated as Nullable<int64>
///  - Fields of type 'float + null' are just floats (and null becomes NaN)
///  - Fields of type 'decimal + null' are generated as floats too
///  - Fields of type 'T + null' for any other T (bool/string/date) become option<T>
///  - All other types are simply strings.
///
let getFields inferedType = 

  /// Matches heterogeneous types that consist of 'null + T' and return the T type
  /// (used below to transform 'float + null => float' and 'int + null => int?')
  let (|TypeOrNull|_|) typ = 
    match typ with 
    | Heterogeneous(map) when map |> Seq.length = 2 && map |> Map.containsKey InferedTypeTag.Null ->
        let kvp = map |> Seq.find (function (KeyValue(InferedTypeTag.Null, _)) -> false | _ -> true)
        Some kvp.Value
    | _ -> None

  /// Can be used to assign value to a variable in a pattern
  /// (e.g. match input with Let 42 (num, input) -> ...)
  let (|Let|) arg inp = (arg, inp)

  match inferedType with 
  | Record(_, fields) -> fields |> List.map (fun field ->
    
      // The inference engine assigns some value to all fields
      // so we should never get an optional field
      if field.Optional then 
        failwithf "getFields: Unexpected optional field %s" field.Name
      
      match field.Type with
      // Match either Primitive or Heterogeneous with Null and Primitive
      | Let true (optional, TypeOrNull(Primitive(typ, unit)))
      | Let false (optional, Primitive(typ, unit)) -> 
          
          // Transform the types as described above
          let typ, typWrapper = 
            if optional && typ = typeof<float> then typ, TypeWrapper.None
            elif optional && typ = typeof<decimal> then typeof<float>, TypeWrapper.None
            elif optional && (typ = typeof<int> || typ = typeof<int64>) then typ, TypeWrapper.Nullable
            elif optional then typ, TypeWrapper.Option
            else typ, TypeWrapper.None
        
          // Annotate the type with measure, if there is one
          let typ, typWithMeasure = 
            match unit with 
            | Some unit -> typ, ProvidedMeasureBuilder.Default.AnnotateType(typ, [unit])
            | _ -> typ, typ
      
          { Name = field.Name
            BasicType = typ
            TypeWithMeasure = typWithMeasure
            TypeWrapper = typWrapper }
      
      | _ -> { Name = field.Name
               BasicType = typeof<string>
               TypeWithMeasure = typeof<string>
               TypeWrapper = TypeWrapper.None } )
      
  | _ -> failwithf "inferFields: Expected record type, got %A" inferedType