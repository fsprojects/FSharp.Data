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

  let generateCsvRowType culture (replacer:AssemblyReplacer) (parentType:ProvidedTypeDefinition) fields = 

    let objectTy = ProvidedTypeDefinition("Row", Some(replacer.ToRuntime typeof<CsvRow>), HideObjectMethods = true)
    parentType.AddMember objectTy

    for index, field in fields |> Seq.mapi (fun i v -> i, v) do
      let typ, typWithMeasure =
        match field.Type with
        | Primitive(typ, Some unit) -> 
            typ, ProvidedMeasureBuilder.Default.AnnotateType(typ, [unit])
        | Primitive(typ, None) -> typ, typ
        | _ -> typeof<string>, typeof<string>

      let typ, conv = Conversions.convertValue culture field.Name (if field.Optional then TypeWrapper.Nullable else TypeWrapper.None) (typ, typWithMeasure) replacer
      let p = ProvidedProperty(NameUtils.nicePascalName field.Name, typ)
      p.GetterCode <- fun (Singleton row) -> let row = replacer.ToDesignTime row in conv <@@ Operations.AsOption((%%row:CsvRow).Columns.[index]) @@>
      objectTy.AddMember p

    objectTy