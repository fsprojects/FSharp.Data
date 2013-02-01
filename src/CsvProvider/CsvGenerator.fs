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

  let generateCsvRowProperties culture (replacer:AssemblyReplacer) fields = 

    fields
    |> List.mapi (fun index field ->

      let typ, typWithMeasure =
        match field.Type with
        | Primitive(typ, Some unit) -> 
            typ, ProvidedMeasureBuilder.Default.AnnotateType(typ, [unit])
        | Primitive(typ, None) -> typ, typ
        | _ -> typeof<string>, typeof<string>

      let typeWrapper = 
        if field.Optional then 
            TypeWrapper.Nullable 
        else 
            TypeWrapper.None

      let typ, conv = Conversions.convertValue culture field.Name typeWrapper (typ, typWithMeasure)  replacer
      
      let p = ProvidedProperty(NameUtils.nicePascalName field.Name, typ)
      p.GetterCode <- fun (Singleton row) -> 
        let row = replacer.ToDesignTime row 
        conv <@@ Operations.AsOption((%%row:CsvRow).Columns.[index]) @@>
      
      p)
