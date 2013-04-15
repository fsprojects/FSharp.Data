// --------------------------------------------------------------------------------------
// Implements type inference for CSV
// --------------------------------------------------------------------------------------

module ProviderImplementation.CsvInference

open System
open System.IO
open System.Text.RegularExpressions
open FSharp.Data.Csv
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.StructuralTypes
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference

/// The schema may be set explicitly. This table specifies the mapping
/// from the names that users can use to the types used.
let private nameToType =
  ["int" ,           (typeof<int>     , TypeWrapper.None    )
   "int64",          (typeof<int64>   , TypeWrapper.None    )
   "bool",           (typeof<bool>    , TypeWrapper.None    )
   "float",          (typeof<float>   , TypeWrapper.None    )
   "decimal",        (typeof<decimal> , TypeWrapper.None    )
   "date",           (typeof<DateTime>, TypeWrapper.None    )
   "guid",           (typeof<Guid>    , TypeWrapper.None    )
   "string",         (typeof<String>  , TypeWrapper.None    )
   "int?",           (typeof<int>     , TypeWrapper.Nullable)
   "int64?",         (typeof<int64>   , TypeWrapper.Nullable)
   "bool?",          (typeof<bool>    , TypeWrapper.Nullable)
   "float?",         (typeof<float>   , TypeWrapper.Nullable)
   "decimal?",       (typeof<decimal> , TypeWrapper.Nullable)
   "date?",          (typeof<DateTime>, TypeWrapper.Nullable)
   "guid?",          (typeof<Guid>    , TypeWrapper.Nullable)
   "int option",     (typeof<int>     , TypeWrapper.Option  )
   "int64 option",   (typeof<int64>   , TypeWrapper.Option  )
   "bool option",    (typeof<bool>    , TypeWrapper.Option  )
   "float option",   (typeof<float>   , TypeWrapper.Option  )
   "decimal option", (typeof<decimal> , TypeWrapper.Option  )
   "date option",    (typeof<DateTime>, TypeWrapper.Option  )
   "guid option",    (typeof<Guid>    , TypeWrapper.Option  )]
  |> dict

// Compiled regex is not supported in Silverlight
let private regexOptions = 
#if FX_NO_REGEX_COMPILATION
  RegexOptions.None
#else
  RegexOptions.Compiled
#endif
let private nameAndTypeRegex = new Regex(@"^(?<name>.+)\((?<type>.+)\)$", regexOptions)
let private typeAndUnitRegex = new Regex(@"^(?<type>.+)<(?<unit>.+)>$", regexOptions)
  
[<RequireQualifiedAccess>]
type private SchemaParseResult =
  | Name
  | NameAndUnit of string * Type
  | Full of PrimitiveInferedProperty

let private asOption = function true, x -> Some x | false, _ -> None

/// Parses type specification in the schema for a single column. 
/// This can be of the form: type|measure|type<measure>
let private parseTypeAndUnit str = 
  let m = typeAndUnitRegex.Match(str)
  if m.Success then
    // type<unit> case, both type and unit have to be valid
    let typ = m.Groups.["type"].Value.TrimEnd().ToLowerInvariant() |> nameToType.TryGetValue |> asOption
    match typ with
    | None -> None, None
    | Some typ ->
        let unitName = m.Groups.["unit"].Value.Trim()
        let unit = ProvidedMeasureBuilder.Default.SI unitName
        match unit with
        | null -> failwithf "Invalid unit of measure %s" unitName
        | unit -> Some typ, Some unit
  else 
    // it is not a full type with unit, so it can be either type or a unit
    let typ = str.ToLowerInvariant() |> nameToType.TryGetValue |> asOption
    match typ with
    | Some (typ, typWrapper) -> 
        // Just type
        Some (typ, typWrapper), None
    | None -> 
        // Just unit
        let unit = ProvidedMeasureBuilder.Default.SI str
        match unit with
        | null -> None, None
        | unit -> None, Some unit
    
/// Parse schema specification for column. This can either be a name
/// with type or just type: name (typeInfo)|typeInfo.
/// If forSchemaOverride is set to true, only Full is returned (this
/// means that we always succeed and override inferred schema)
let private parseSchemaItem str forSchemaOverride =     
  let name, typ, unit = 
    let m = nameAndTypeRegex.Match(str)
    if m.Success then
      // name (type|measure|type<measure>)
      let name = m.Groups.["name"].Value.TrimEnd()
      let typeAndUnit = m.Groups.["type"].Value.Trim()
      let typ, unit = parseTypeAndUnit typeAndUnit
      if forSchemaOverride && typ.IsNone then
        failwithf "Invalid type: %s" typeAndUnit
      name, typ, unit
    elif forSchemaOverride then
      // type|type<measure>
      let typ, unit = parseTypeAndUnit str
      match typ, unit with
      | None, _ -> failwithf "Invalid type: %s" str
      | typ, unit -> "", typ, unit
    else
      // name
      str, None, None

  match typ, unit with
  | Some (typ, typWrapper), unit ->
      let typWithMeasure =
        match unit with
        | None -> typ
        | Some unit -> 
            if supportsUnitsOfMeasure typ
            then ProvidedMeasureBuilder.Default.AnnotateType(typ, [unit])
            else failwithf "Units of measure not supported by type %s" typ.Name
      PrimitiveInferedProperty.Create(name, typ, typWithMeasure, typWrapper)
      |> SchemaParseResult.Full
  | None, Some unit -> SchemaParseResult.NameAndUnit(name, unit)
  | None, None -> SchemaParseResult.Name

/// Infers the type of a CSV file using the specified number of rows
/// (This handles units in the same way as the original MiniCSV provider)
let inferType (csv:CsvFile) count (missingValues, culture) schema =

  // This has to be done now otherwise subtypeInfered will get confused
  let makeUnique = NameUtils.uniqueGenerator id
  makeUnique "AsTuple" |> ignore

  // If we do not have header names, then automatically generate names
  let headers = 
    match csv.Headers with
    | Some headers ->
        headers |> Array.mapi (fun i header -> 
          if String.IsNullOrEmpty header then 
            "Column" + (i+1).ToString()
          else
            header)
    | None -> Array.init csv.NumberOfColumns (fun i -> "Column" + (i+1).ToString())

  // If the schema is specified explicitly, then parse the schema
  // (This can specify just types, names of columns or a mix of both)
  let schema =
    if String.IsNullOrWhiteSpace schema then
      Array.zeroCreate headers.Length
    else
      use reader = new StringReader(schema)
      let schema = CsvReader.readCsvFile reader "," '"' |> Seq.exactlyOne |> fst
      if schema.Length <> headers.Length then
        failwithf "Schema was expected to have %d items, but has %d" headers.Length schema.Length
      schema |> Array.mapi (fun index item -> 
        let item = item.Trim()
        match item with
        | "" -> None
        | item -> 
            let parseResult = parseSchemaItem item true
            match parseResult with
            | SchemaParseResult.Full prop -> 
                let name = 
                  if prop.Name = "" then headers.[index]
                  else prop.Name
                Some { prop with Name = makeUnique name }
            | _ -> failwithf "inferType: Unexpected SchemaParseResult: %A" parseResult)

  // Merge the previous information with the header names that we get from the
  // first row of the file (if the schema specifies just types, we want to use the
  // names from the file; if the schema specifies name & type, it overrides the file)            
  let headerNamesAndUnits = headers |> Array.mapi (fun index item ->
    match schema.[index] with
    | Some prop -> prop.Name, None
    | None ->
        let parseResult = parseSchemaItem item false
        match parseResult with
        | SchemaParseResult.Name -> 
            makeUnique item, None
        | SchemaParseResult.NameAndUnit (name, unit) -> 
            // store the original header because the inferred type might not support units of measure
            (makeUnique item) + "\n" + (makeUnique name), Some unit
        | SchemaParseResult.Full prop -> 
            let prop = { prop with Name = makeUnique prop.Name }
            schema.[index] <- Some prop
            prop.Name, None)

  // If we have no data, generate one empty row with empty strings, 
  // so that we get a type with all the properties (returning string values)
  let rows = 
    if Seq.isEmpty csv.Data then CsvRow(csv, [| for i in 1..headers.Length -> "" |]) |> Seq.singleton 
    elif count > 0 then Seq.truncate count csv.Data
    else csv.Data

  // Infer the type of collection using structural inference
  let types = seq {
    for row in rows ->
      let fields = 
        [ for (name, unit), index, value in Seq.zip3 headerNamesAndUnits { 0..headerNamesAndUnits.Length-1 } row.Columns ->
            let typ = 
              match schema.[index] with
              | Some _ -> Null // this will be ignored, so just return anything
              | None ->
                  // Treat empty values as 'null' values. The inference will
                  // infer heterogeneous types e.g. 'null + int', will then 
                  // be turned into Nullable<int> (etc.) in the getFields function
                  if String.IsNullOrWhiteSpace value then Null
                  else inferPrimitiveType (missingValues, culture) value unit
            { Name = name
              Optional = false
              Type = typ } ]
      Record(None, fields) }

  let inferedType = 
    if schema |> Seq.forall Option.isSome then
        // all the columns types are already set, so all the rows will be the same
        types |> Seq.head
    else
        Seq.reduce subtypeInfered types
  
  inferedType, schema

/// Generates the fields for a CSV row. The CSV provider should be
/// numerical-friendly, so we do a few simple adjustments:
///  
///  - Fields of type 'int + null' are generated as Nullable<int>
///  - Fields of type 'int64 + null' are generated as Nullable<int64>
///  - Fields of type 'float + null' are just floats (and null becomes NaN)
///  - Fields of type 'decimal + null' are generated as floats too
///  - Fields of type 'T + null' for any other non-nullable T (bool/date/guid) become option<T>
///  - All other types are simply strings.
///
let getFields inferedType schema = 

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
  let inline (|Let|) arg inp = (arg, inp)

  match inferedType with 
  | Record(_, fields) -> fields |> List.mapi (fun index field ->
    
      // The inference engine assigns some value to all fields
      // so we should never get an optional field
      if field.Optional then 
        failwithf "Column %s is not present in all rows used for inference" field.Name
      
      match Array.get schema index with
      | Some prop -> prop
      | None ->
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
              let typ, typWithMeasure, name = 
                match unit with 
                | Some unit -> 
                    if supportsUnitsOfMeasure typ then
                      typ, ProvidedMeasureBuilder.Default.AnnotateType(typ, [unit]), field.Name.Split('\n').[1]
                    else
                      typ, typ, field.Name.Split('\n').[0]
                | _ -> typ, typ, field.Name.Split('\n').[0] 
          
              PrimitiveInferedProperty.Create
                (name, typ, typWithMeasure, typWrapper)
          
          | _ -> 
              PrimitiveInferedProperty.Create
                (field.Name.Split('\n').[0], typeof<string>, typeof<string>) )
          
  | _ -> failwithf "inferFields: Expected record type, got %A" inferedType