// --------------------------------------------------------------------------------------
// HTML type provider
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open FSharp.Core.CompilerServices
open ProviderImplementation.ProviderHelpers
open ProviderImplementation.ProvidedTypes
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes

#nowarn "10001"

[<TypeProvider>]
type public HtmlProvider(cfg:TypeProviderConfig) as this =
    inherit DisposableTypeProviderForNamespaces(cfg, assemblyReplacementMap=[ "FSharp.Data.DesignTime", "FSharp.Data" ])
    
    // Generate namespace and type 'FSharp.Data.HtmlProvider'
    do AssemblyResolver.init ()
    let asm = System.Reflection.Assembly.GetExecutingAssembly()
    let ns = "FSharp.Data"
    let htmlProvTy = ProvidedTypeDefinition(asm, ns, "HtmlProvider", None, hideObjectMethods=true, nonNullable=true)
    
    let buildTypes (typeName:string) (args:obj[]) =

        let sample = args.[0] :?> string
        let preferOptionals = args.[1] :?> bool
        let includeLayoutTables = args.[2] :?> bool
        let missingValuesStr = args.[3] :?> string
        let cultureStr = args.[4] :?> string
        let encodingStr = args.[5] :?> string
        let resolutionFolder = args.[6] :?> string
        let resource = args.[7] :?> string

        let getSpec _ value = 

            let doc = using (IO.logTime "Parsing" sample) <| fun _ ->
                HtmlDocument.Parse value

            let htmlType = using (IO.logTime "Inference" sample) <| fun _ ->
                let inferenceParameters : HtmlInference.Parameters = 
                    { MissingValues = TextRuntime.GetMissingValues missingValuesStr
                      CultureInfo = TextRuntime.GetCulture cultureStr
                      UnitsOfMeasureProvider = ProviderHelpers.unitsOfMeasureProvider
                      PreferOptionals  = preferOptionals }
                doc
                |> HtmlRuntime.getHtmlObjects (Some inferenceParameters) includeLayoutTables
                |> HtmlGenerator.generateTypes asm ns typeName (inferenceParameters, missingValuesStr, cultureStr)

            using (IO.logTime "TypeGeneration" sample) <| fun _ ->

            { GeneratedType = htmlType
              RepresentationType = htmlType
              CreateFromTextReader = fun reader -> <@@ HtmlDocument.Create(includeLayoutTables, %reader) @@>                    
              CreateListFromTextReader = None
              CreateFromTextReaderForSampleList = fun _ -> failwith "Not Applicable" }

        generateType "HTML" (Sample sample) getSpec this cfg encodingStr resolutionFolder resource typeName (*maxNumberOfRows*)None

    // Add static parameter that specifies the API we want to get (compile-time) 
    let parameters = 
        [ ProvidedStaticParameter("Sample", typeof<string>, parameterDefaultValue = "")           
          ProvidedStaticParameter("PreferOptionals", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("IncludeLayoutTables", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("MissingValues", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("Encoding", typeof<string>, parameterDefaultValue = "") 
          ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") 
          ProvidedStaticParameter("EmbeddedResource", typeof<string>, parameterDefaultValue = "") ]
  
    let helpText = 
        """<summary>Typed representation of an HTML file.</summary>
           <param name='Sample'>Location of an HTML sample file or a string containing a sample HTML document.</param>
           <param name='PreferOptionals'>When set to true, inference will prefer to use the option type instead of nullable types, `double.NaN` or `""` for missing values. Defaults to false.</param>
           <param name='IncludeLayoutTables'>Includes tables that are potentially layout tables (with cellpadding=0 and cellspacing=0 attributes)</param>
           <param name='MissingValues'>The set of strings recogized as missing values. Defaults to `""" + String.Join(",", TextConversions.DefaultMissingValues) + """`.</param>
           <param name='Culture'>The culture used for parsing numbers and dates. Defaults to the invariant culture.</param>
           <param name='Encoding'>The encoding used to read the sample. You can specify either the character set name or the codepage number. Defaults to UTF8 for files, and to ISO-8859-1 the for HTTP requests, unless `charset` is specified in the `Content-Type` response header.</param>
           <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution).</param>
           <param name='EmbeddedResource'>When specified, the type provider first attempts to load the sample from the specified resource 
              (e.g. 'MyCompany.MyAssembly, resource_name.html'). This is useful when exposing types generated by the type provider.</param>"""
  
    do htmlProvTy.AddXmlDoc helpText
    do htmlProvTy.DefineStaticParameters(parameters, buildTypes)
  
    // Register the main type with F# compiler
    do this.AddNamespace(ns, [ htmlProvTy ])
