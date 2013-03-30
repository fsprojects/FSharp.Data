// --------------------------------------------------------------------------------------
// CSV type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open FSharp.Data.RuntimeImplementation
open ProviderImplementation.ProvidedTypes

module internal CsvTypeBuilder =

  /// Generate the properties for a CSV row
  let generateCsvRowProperties culture replacer fields =
    fields |> List.mapi (fun index field ->
        
      let typ, conv = Conversions.convertValue replacer culture field

      let p = ProvidedProperty(NameUtils.nicePascalName field.Name, typ)

      p.GetterCode <- fun (Singleton row) -> 
        let row = replacer.ToDesignTime row 
        conv <@@ Operations.AsOption((%%row:CsvRow).Columns.[index]) @@>

      p)