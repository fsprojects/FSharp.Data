// --------------------------------------------------------------------------------------
// JSON type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.TypeInference
open ProviderImplementation.ProvidedTypes

module internal CsvTypeBuilder =

    let generateCsvRowType culture (replacer:AssemblyReplacer) (parentType:ProvidedTypeDefinition) fields = 

        let objectTy = ProvidedTypeDefinition("Row", Some(replacer.ToRuntime typeof<CsvRow>), HideObjectMethods = true)
        parentType.AddMember objectTy

        for index, field in fields |> Seq.mapi (fun i v -> i, v) do
            let baseTyp, propTyp =
                match field.Type with
                | Primitive(typ, Some unit) -> 
                    typ, ProvidedMeasureBuilder.Default.AnnotateType(typ, [unit])
                | Primitive(typ, None) -> typ, typ
                | _ -> typeof<string>, typeof<string>

            let propTyp = if field.Optional then typedefof<option<_>>.MakeGenericType [| propTyp |] else propTyp
            let p = ProvidedProperty(NameUtils.nicePascalName field.Name, propTyp)
            let _, conv = Conversions.convertValue culture field.Name field.Optional baseTyp replacer
            p.GetterCode <- fun (Singleton row) -> let row = replacer.ToDesignTime row in conv <@@ Operations.AsOption((%%row:CsvRow).Columns.[index]) @@>
            objectTy.AddMember p

        objectTy