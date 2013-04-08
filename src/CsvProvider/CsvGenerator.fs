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
        
      // Generate conversion according to the inferred field specification
      let typ, conv = Conversions.convertValue replacer culture field

      ProvidedProperty(field.Name, typ, GetterCode = fun (Singleton row) -> 
        let row = replacer.ToDesignTime row 
        conv <@@ Operations.AsOption((%%row:CsvRow).Columns.[index]) @@>) )