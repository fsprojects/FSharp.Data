// --------------------------------------------------------------------------------------
// CSV type provider
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProviderHelpers
open FSharp.Data.Csv
open FSharp.Data.Runtime
open FSharp.Data.Runtime.CsvInference
open ProviderImplementation.QuotationBuilder

// --------------------------------------------------------------------------------------

[<TypeProvider>]
type public CsvProvider(cfg:TypeProviderConfig) as this =
  inherit DisposableTypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.CsvProvider'
  let asm, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let csvProvTy = ProvidedTypeDefinition(asm, ns, "CsvProvider", Some typeof<obj>)

  let buildTypes (typeName:string) (args:obj[]) =

    let sample = args.[0] :?> string
    let separators = args.[1] :?> string
    let culture = args.[2] :?> string
    let cultureInfo = TextRuntime.GetCulture culture
    let inferRows = args.[3] :?> int
    let schema = args.[4] :?> string
    let hasHeaders = args.[5] :?> bool
    let ignoreErrors = args.[6] :?> bool
    let safeMode = args.[7] :?> bool
    let preferOptionals = args.[8] :?> bool
    let quote = args.[9] :?> char
    let missingValues = args.[10] :?> string
    let missingValuesList = missingValues.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
    let cacheRows = args.[11] :?> bool
    let resolutionFolder = args.[12] :?> string
    
    let parse (extension:string) value =
      let separators = 
        if String.IsNullOrEmpty separators && extension.ToLowerInvariant() = ".tsv"
        then "\t" else separators
      CsvFile.Parse(value, separators, quote, hasHeaders, ignoreErrors)

    let getSpecFromSamples samples = 
      
      use sampleCsv : CsvFile = Seq.head samples
      let separators = sampleCsv.Separators
  
      let inferredFields = 
        sampleCsv.InferColumnTypes(inferRows, missingValuesList, cultureInfo, schema,
                                   safeMode, preferOptionals, ProvidedMeasureBuilder.Default.SI)

      let csvType, csvErasedType, stringArrayToRow, rowToStringArray = 
        inferredFields 
        |> CsvTypeBuilder.generateTypes asm ns typeName (missingValues, culture) replacer 
  
      { GeneratedType = csvType
        RepresentationType = csvType
        CreateFromTextReader = fun reader ->
          let stringArrayToRowVar = Var("stringArrayToRow", stringArrayToRow.Type)
          let rowToStringArrayVar = Var("rowToStringArray", rowToStringArray.Type)
          let body = 
            csvErasedType?CreateNonReentrant () (Expr.Var stringArrayToRowVar, Expr.Var rowToStringArrayVar, replacer.ToRuntime reader, 
                                                 separators, quote, hasHeaders, ignoreErrors, cacheRows)
          Expr.Let(stringArrayToRowVar, stringArrayToRow, Expr.Let(rowToStringArrayVar, rowToStringArray, body))
        CreateFromTextReaderForSampleList = fun _ -> failwith "Not Applicable" }

    generateConstructors "CSV" sample (*sampleIsList*)false 
                         parse (fun _ _ -> failwith "Not Applicable") getSpecFromSamples
                         this cfg replacer resolutionFolder true

  let defaultMissingValues = String.Join(",", TextConversions.DefaultMissingValues)
  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("Sample", typeof<string>) 
      ProvidedStaticParameter("Separators", typeof<string>, parameterDefaultValue = "") 
      ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
      ProvidedStaticParameter("InferRows", typeof<int>, parameterDefaultValue = 1000)
      ProvidedStaticParameter("Schema", typeof<string>, parameterDefaultValue = "")
      ProvidedStaticParameter("HasHeaders", typeof<bool>, parameterDefaultValue = true)
      ProvidedStaticParameter("IgnoreErrors", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("SafeMode", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("PreferOptionals", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("Quote", typeof<char>, parameterDefaultValue = '"')
      ProvidedStaticParameter("MissingValues", typeof<string>, parameterDefaultValue = defaultMissingValues)
      ProvidedStaticParameter("CacheRows", typeof<bool>, parameterDefaultValue = true)
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") ]

  let helpText = 
    """<summary>Typed representation of a CSV file</summary>
       <param name='Sample'>Location of a CSV sample file or a string containing a sample CSV document</param>
       <param name='Separators'>Column delimiter(s). Defaults to ","</param>
       <param name='Culture'>The culture used for parsing numbers and dates. Defaults to the invariant culture</param>
       <param name='InferRows'>Number of rows to use for inference. Defaults to 1000. If this is zero, all rows are used</param>
       <param name='Schema'>Optional column types, in a comma separated list. Valid types are "int", "int64", "bool", "float", "decimal", "date", "guid", "string", "int?", "int64?", "bool?", "float?", "decimal?", "date?", "guid?", "int option", "int64 option", "bool option", "float option", "decimal option", "date option", "guid option" and "string option". You can also specify a unit and the name of the column like this: Name (type&lt;unit&gt;). You can also override only the name. If you don't want to specify all the columns, you can specify by name like this: 'ColumnName=type'</param>
       <param name='HasHeaders'>Whether the sample contains the names of the columns as its first line</param>
       <param name='IgnoreErrors'>Whether to ignore rows that have the wrong number of columns or which can't be parsed using the inferred or specified schema. Otherwise an exception is thrown when these rows are encountered</param>
       <param name='SafeMode'>When set to true, all columns will assume they can have missing values, even if in the provided sample all values are present. Defaults to false</param>
       <param name='PreferOptionals'>When set to true, inference will prefer to use the option type instead of nullable types, double.NaN or "" for missing values. Defaults to false</param>
       <param name='Quote'>The quotation mark (for surrounding values containing the delimiter). Defaults to "</param>
       <param name='MissingValues'>The set of strings recogized as missing values. Defaults to """ + "\"" + defaultMissingValues + "\"" + """</param>
       <param name='CacheRows'>Whether the rows should be caches so they can be iterated multiple times. Defaults to true. Disable for large datasets</param>
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do csvProvTy.AddXmlDoc helpText
  do csvProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ csvProvTy ])
