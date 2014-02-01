/// Structural inference for CSV
module FSharp.Data.Runtime.CsvInference

open System
open System.IO
open System.Text.RegularExpressions
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference

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
   "guid option",    (typeof<Guid>    , TypeWrapper.Option  )
   "string option",  (typeof<string>  , TypeWrapper.Option  )]
  |> dict

// Compiled regex is not supported in Portable profile
let private regexOptions = 
#if FX_NO_REGEX_COMPILATION
  RegexOptions.RightToLeft
#else
  RegexOptions.Compiled ||| RegexOptions.RightToLeft
#endif
let private nameAndTypeRegex = new Regex(@"^(?<name>.+)\((?<type>.+)\)$", regexOptions)
let private typeAndUnitRegex = new Regex(@"^(?<type>.+)<(?<unit>.+)>$", regexOptions)
let private overrideByNameRegex = new Regex(@"^(?<name>.+)(->(?<newName>.+)(=(?<type>.+))?|=(?<type>.+))$", regexOptions)
  
[<RequireQualifiedAccess>]
type private SchemaParseResult =
  | Name of string
  | NameAndUnit of string * Type
  | Full of PrimitiveInferedProperty
  | FullByName of PrimitiveInferedProperty * (*originalName*)string
  | Rename of (*name*)string * (*originalName*)string

let private asOption = function true, x -> Some x | false, _ -> None

/// Parses type specification in the schema for a single column. 
/// This can be of the form: type|measure|type<measure>
let private parseTypeAndUnit tryGetUnit str = 
  let m = typeAndUnitRegex.Match(str)
  if m.Success then
    // type<unit> case, both type and unit have to be valid
    let typ = m.Groups.["type"].Value.TrimEnd().ToLowerInvariant() |> nameToType.TryGetValue |> asOption
    match typ with
    | None -> None, None
    | Some typ ->
        let unitName = m.Groups.["unit"].Value.Trim()
        let unit = tryGetUnit unitName
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
        let unit = tryGetUnit str
        match unit with
        | null -> None, None
        | unit -> None, Some unit
    
/// Parse schema specification for column. This can either be a name
/// with type or just type: name (typeInfo)|typeInfo.
/// If forSchemaOverride is set to true, only Full or Name is returne
/// (if we succeed we override the inferred schema, otherwise, we just
/// override the header name)
let private parseSchemaItem tryGetUnit str forSchemaOverride =     
  let name, typ, unit, isOverrideByName, originalName = 
    let m = overrideByNameRegex.Match str
    if m.Success && forSchemaOverride then
      // name=type|type<measure>
      let originalName = m.Groups.["name"].Value.TrimEnd()
      let newName = m.Groups.["newName"].Value.Trim()
      let typeAndUnit = m.Groups.["type"].Value.Trim()
      let typ, unit = parseTypeAndUnit tryGetUnit typeAndUnit
      if typ.IsNone && typeAndUnit <> "" then
        failwithf "Invalid type: %s" typeAndUnit
      newName, typ, unit, true, originalName
    else
      let m = nameAndTypeRegex.Match(str)
      if m.Success then
        // name (type|measure|type<measure>)
        let name = m.Groups.["name"].Value.TrimEnd()
        let typeAndUnit = m.Groups.["type"].Value.Trim()
        let typ, unit = parseTypeAndUnit tryGetUnit typeAndUnit
        name, typ, unit, false, ""
      elif forSchemaOverride then
        // type|type<measure>
        let typ, unit = parseTypeAndUnit tryGetUnit str
        match typ, unit with
        | None, _ -> str, None, None, false, ""
        | typ, unit -> "", typ, unit, false, ""
      else
        // name
        str, None, None, false, ""

  match typ, unit with
  | Some (typ, typWrapper), unit ->
      let prop = PrimitiveInferedProperty.Create(name, typ, typWrapper, ?unit=unit)
      if isOverrideByName then 
        SchemaParseResult.FullByName(prop, originalName) 
      else 
      SchemaParseResult.Full prop
  | None, None when isOverrideByName -> SchemaParseResult.Rename(name, originalName)
  | None, None -> SchemaParseResult.Name str
  | None, Some _ when forSchemaOverride -> SchemaParseResult.Name str
  | None, Some unit -> SchemaParseResult.NameAndUnit(name, unit)

/// Infers the type of a CSV file using the specified number of rows
/// (This handles units in the same way as the original MiniCSV provider)
let internal inferType tryGetUnit (csv:CsvFile) count missingValues culture schema assumeMissingValues preferOptionals =

  // This has to be done now otherwise subtypeInfered will get confused
  let makeUnique = NameUtils.uniqueGenerator id

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
      let schemaStr = CsvReader.readCsvFile reader "," '"' |> Seq.exactlyOne |> fst
      if schemaStr.Length > headers.Length then
        failwithf "Schema was expected to have at most %d items, but has %d" headers.Length schemaStr.Length
      let schema = Array.zeroCreate headers.Length
      for index = 0 to schemaStr.Length-1 do
        let item = schemaStr.[index].Trim()
        match item with
        | "" -> ()
        | item -> 
            let parseResult = parseSchemaItem tryGetUnit item true
            match parseResult with
            | SchemaParseResult.Name name ->
                headers.[index] <- name
            | SchemaParseResult.Full prop -> 
                let name = 
                  if prop.Name = "" then headers.[index]
                  else prop.Name
                schema.[index] <- Some { prop with Name = makeUnique name }
            | SchemaParseResult.Rename (name, originalName) ->
                let index = headers |> Array.tryFindIndex (fun header -> header.Equals(originalName, StringComparison.OrdinalIgnoreCase))
                match index with
                | Some index -> 
                    headers.[index] <- name
                | None -> failwithf "Column '%s' not found in '%s'" originalName (headers |> String.concat ",")
            | SchemaParseResult.FullByName (prop, originalName) -> 
                let index = headers |> Array.tryFindIndex (fun header -> header.Equals(originalName, StringComparison.OrdinalIgnoreCase))
                match index with
                | Some index -> 
                    let name = 
                      if prop.Name = "" then headers.[index]
                      else prop.Name
                    schema.[index] <- Some { prop with Name = makeUnique name }
                | None -> failwithf "Column '%s' not found in '%s'" originalName (headers |> String.concat ",")
            | _ -> failwithf "inferType: Unexpected SchemaParseResult for schema: %A" parseResult
      schema

  // Merge the previous information with the header names that we get from the
  // first row of the file (if the schema specifies just types, we want to use the
  // names from the file; if the schema specifies name & type, it overrides the file)            
  let headerNamesAndUnits = headers |> Array.mapi (fun index item ->
    match schema.[index] with
    | Some prop -> prop.Name, None
    | None ->
        let parseResult = parseSchemaItem tryGetUnit item false
        match parseResult with
        | SchemaParseResult.Name name -> 
            makeUnique name, None
        | SchemaParseResult.NameAndUnit (name, unit) -> 
            // store the original header because the inferred type might not support units of measure
            (makeUnique item) + "\n" + (makeUnique name), Some unit
        | SchemaParseResult.Full prop -> 
            let prop = { prop with Name = makeUnique prop.Name }
            schema.[index] <- Some prop
            prop.Name, None
        | _ -> failwithf "inferType: Unexpected SchemaParseResult for header: %A" parseResult)

  // If we have no data, generate one empty row with empty strings, 
  // so that we get a type with all the properties (returning string values)
  let rowsIterator = csv.Rows.GetEnumerator()
  let rows = 
    if rowsIterator.MoveNext() then
      seq {
        yield rowsIterator.Current
        try
          while rowsIterator.MoveNext() do
            yield rowsIterator.Current
        finally
          rowsIterator.Dispose()
        if assumeMissingValues then
          yield CsvRow(csv, Array.create headers.Length "")
      }
    else
      CsvRow(csv, Array.create headers.Length "") |> Seq.singleton 
  
  let rows = if count > 0 then Seq.truncate (if assumeMissingValues && count < Int32.MaxValue then count + 1 else count) rows else rows

  // Infer the type of collection using structural inference
  let types = 
    [ for row in rows ->
        let fields = 
          [ for (name, unit), index, value in Array.zip3 headerNamesAndUnits [| 0..headerNamesAndUnits.Length-1 |] row.Columns ->
              let typ = 
                match schema.[index] with
                | Some _ -> InferedType.Null // this will be ignored, so just return anything
                | None ->
                    // If there's only whitespace between commas, treat it as a missing value
                    if String.IsNullOrWhiteSpace value then InferedType.Null
                    // Explicit missing values (NaN, NA, etc.) will be treated as float unless the preferOptionals is set to true
                    elif Array.exists ((=) <| value.Trim()) missingValues then 
                        if preferOptionals then InferedType.Null else InferedType.Primitive(typeof<float>, unit)
                    else inferPrimitiveType culture value unit
              { Name = name
                Optional = false
                Type = typ } ]
        InferedType.Record(None, fields) ]

  let inferedType = 
    if schema |> Seq.forall Option.isSome then
        // all the columns types are already set, so all the rows will be the same
        types |> Seq.head
    else
        List.reduce (StructuralInference.subtypeInfered (not preferOptionals)) types
  
  inferedType, schema

/// Generates the fields for a CSV row. The CSV provider should be
/// numerical-friendly, so we do a few simple adjustments.
/// When preferOptionals is false:
///  
///  - Fields of type 'int + null' are generated as Nullable<int>
///  - Fields of type 'int64 + null' are generated as Nullable<int64>
///  - Fields of type 'float + null' are just floats (and null becomes NaN)
///  - Fields of type 'decimal + null' are generated as floats too
///  - Fields of type 'T + null' for any other non-nullable T (bool/date/guid) become option<T>
///  - All other types are simply strings.
///
/// When preferOptionals is true:
///  
///  - All fields of type 'T + null' for any type become option<T>, incude strings

let internal getFields preferOptionals inferedType schema = 

  /// Matches heterogeneous types that consist of 'null + T' and return the T type
  /// (used below to transform 'float + null => float' and 'int + null => int?')
  let inline (|TypeOrNull|_|) typ = 
    match typ with 
    | InferedType.Heterogeneous(map) when map.Count = 2 && map |> Map.containsKey InferedTypeTag.Null ->
        let kvp = map |> Seq.find (function KeyValue(InferedTypeTag.Null, _) -> false | _ -> true)
        Some kvp.Value
    | _ -> None

  /// Can be used to assign value to a variable in a pattern
  /// (e.g. match input with Let 42 (num, input) -> ...)
  let inline (|Let|) arg inp = (arg, inp)

  match inferedType with 
  | InferedType.Record(_, fields) -> fields |> List.mapi (fun index field ->
    
      // The inference engine assigns some value to all fields
      // so we should never get an optional field
      if field.Optional then 
        failwithf "Column %s is not present in all rows used for inference" field.Name
      
      match Array.get schema index with
      | Some prop -> prop
      | None ->
          match field.Type with
          // Match either Primitive or Heterogeneous with Null and Primitive
          | Let true (optional, TypeOrNull(InferedType.Primitive(typ, unit)))
          | Let false (optional, InferedType.Primitive(typ, unit)) -> 
              
              // Transform the types as described above
              let typ, typWrapper = 
                if optional then
                  if preferOptionals then typ, TypeWrapper.Option
                  elif typ = typeof<float> then typ, TypeWrapper.None
                  elif typ = typeof<decimal> then typeof<float>, TypeWrapper.None
                  elif typ = typeof<Bit0> || typ = typeof<Bit1> || typ = typeof<int> || typ = typeof<int64> then typ, TypeWrapper.Nullable
                  else typ, TypeWrapper.Option
                else typ, TypeWrapper.None
            
              // Annotate the type with measure, if there is one
              let typ, unit, name = 
                match unit with 
                | Some unit -> 
                    if StructuralInference.supportsUnitsOfMeasure typ then
                      typ, Some unit, field.Name.Split('\n').[1]
                    else
                      typ, None, field.Name.Split('\n').[0]
                | _ -> typ, None, field.Name.Split('\n').[0] 
          
              PrimitiveInferedProperty.Create(name, typ, typWrapper, ?unit=unit)
          
          | _ -> 
              PrimitiveInferedProperty.Create(field.Name.Split('\n').[0], typeof<string>, preferOptionals) )
          
  | _ -> failwithf "inferFields: Expected record type, got %A" inferedType

type CsvFile with
    /// Infers the types of the columns of a CSV file
    /// Parameters:
    /// * inferRows - Number of rows to use for inference. If this is zero, all rows are used
    /// * missingValues - The set of strings recogized as missing values
    /// * culture - The culture used for parsing numbers and dates
    /// * schema - Optional column types, in a comma separated list. Valid types are "int", "int64", "bool", "float", "decimal", "date", "guid", "string", "int?", "int64?", "bool?", "float?", "decimal?", "date?", "guid?", "int option", "int64 option", "bool option", "float option", "decimal option", "date option", "guid option" and "string option". You can also specify a unit and the name of the column like this: Name (type&lt;unit&gt;). You can also override only the name. If you don't want to specify all the columns, you can specify by name like this: 'ColumnName=type'
    /// * assumeMissingValues - Assumes all columns can have missing values
    /// * preferOptionals - when set to true, inference will prefer to use the option type instead of nullable types, double.NaN or "" for missing values
    /// * getUnitOfMeasure - optional function to resolve Units of Measure
    member x.InferColumnTypes(inferRows, missingValues, culture, schema, assumeMissingValues, preferOptionals, ?getUnitOfMeasure) =
        let tryGetUnit = defaultArg getUnitOfMeasure (fun (_:string) -> null:Type) 
        inferType tryGetUnit x inferRows missingValues culture schema assumeMissingValues preferOptionals
        ||> getFields preferOptionals
