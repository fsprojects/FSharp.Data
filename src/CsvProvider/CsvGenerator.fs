// --------------------------------------------------------------------------------------
// JSON type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open FSharp.Data
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.TypeInference
open ProviderImplementation.ProvidedTypes

module internal CsvTypeBuilder =

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

  /// Generate type for a CSV row. The CSV provider should be numerical-friendly, so
  /// we do a few simple adjustments:
  ///  
  ///  - Fields of type 'int + null' are generated as Nullable<int>
  ///  - Fields of type 'float + null' are just floats (and null becomes NaN)
  ///  - Fields of type 'decimal + null' are generated as floats too
  ///  - Fields of type 'T + null' for any other T become option<T>
  ///  - All other types are simply strings.
  ///
  let generateCsvRowProperties culture (replacer:AssemblyReplacer) typ = [
    match typ with 
    | Record(_, fields) ->
      for index, field in fields |> Seq.mapi (fun i v -> i, v) do
        
        // The inference engine assigns some value to all fields
        // so we should never get an optional field
        if field.Optional then 
          failwith "generateCsvRowType: Unexpected optional record field."

        let typ, typWithMeasure, typWrapper =        
          match field.Type with
          // Match either Primitive or Heterogeneous with Null and Primitive
          | Let true (optional, TypeOrNull(Primitive(typ, unit)))
          | Let false (optional, Primitive(typ, unit)) -> 
              
              // Transform the types as described above
              let typ, typWrapper = 
                if optional && typ = typeof<float> then typ, TypeWrapper.None
                elif optional && typ = typeof<decimal> then typeof<float>, TypeWrapper.None
                elif optional && typ = typeof<int> then typ, TypeWrapper.Nullable
                elif optional then typ, TypeWrapper.Option
                else typ, TypeWrapper.None
            
              // Annotate the type with measure, if there is one
              let typ, typWithMeasure = 
                match unit with 
                | Some unit -> typ, ProvidedMeasureBuilder.Default.AnnotateType(typ, [unit])
                | _ -> typ, typ
              typ, typWithMeasure, typWrapper
          | _ -> typeof<string>, typeof<string>, TypeWrapper.None

        // Generate the property 
        let typ, conv = Conversions.convertValue culture field.Name typWrapper (typ, typWithMeasure) replacer
        let p = ProvidedProperty(NameUtils.nicePascalName field.Name, typ)
        p.GetterCode <- fun (Singleton row) -> 
          let row = replacer.ToDesignTime row 
          conv <@@ Operations.AsOption((%%row:CsvRow).Columns.[index]) @@>
        yield p

    | _ -> failwith "generateCsvRowType: Expected record type." ]
