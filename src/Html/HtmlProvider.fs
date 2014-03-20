// --------------------------------------------------------------------------------------
// HTML type provider
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProviderHelpers
open ProviderImplementation.ProvidedTypes
open FSharp.Data
open FSharp.Data.Runtime

#nowarn "10001"

[<TypeProvider>]
type public HtmlProvider(cfg:TypeProviderConfig) as this =
    inherit DisposableTypeProviderForNamespaces()
    
    // Generate namespace and type 'FSharp.Data.HtmlProvider'
    let asm, version, replacer = AssemblyResolver.init cfg
    let ns = "FSharp.Data"
    let htmlProvTy = ProvidedTypeDefinition(asm, ns, "HtmlProvider", Some typeof<obj>)
    
    let buildTypes (typeName:string) (args:obj[]) =

        let sample = args.[0] :?> string
        let preferOptionals = args.[1] :?> bool
        let missingValues = args.[2] :?> string
        let missingValuesList = missingValues.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
        let cultureStr = args.[3] :?> string
        let cultureInfo = TextRuntime.GetCulture cultureStr
        let resolutionFolder = args.[4] :?> string

        let getSpecFromSamples samples = 
      
            let tables : HtmlTable list = Seq.exactlyOne samples

            let htmlType = using (IO.logTime "Inference" sample) <| fun _ ->
                tables 
                |> List.filter (fun table -> table.Headers.Length > 0)
                |> List.map (fun table -> table.Name, HtmlInference.inferColumns preferOptionals missingValuesList cultureInfo table.Headers table.Rows)
                |> HtmlGenerator.generateTypes asm ns typeName (missingValues, cultureStr) replacer

            using (IO.logTime "TypeGeneration" sample) <| fun _ ->

            { GeneratedType = htmlType
              RepresentationType = htmlType
              CreateFromTextReader = fun reader ->  replacer.ToRuntime <@@ TypedHtmlDocument.Create(%reader) @@>                    
              CreateFromTextReaderForSampleList = fun _ -> failwith "Not Applicable" }

        generateType "HTML" sample (*sampleIsList*)false (fun _ -> HtmlDocument.Parse >> HtmlRuntime.getTables) (fun _ _ -> failwith "Not Applicable")
                     getSpecFromSamples version this cfg replacer resolutionFolder typeName

    let defaultMissingValues = String.Join(",", TextConversions.DefaultMissingValues)

    // Add static parameter that specifies the API we want to get (compile-time) 
    let parameters = 
        [ ProvidedStaticParameter("Sample", typeof<string>, parameterDefaultValue = "")           
          ProvidedStaticParameter("PreferOptionals", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("MissingValues", typeof<string>, parameterDefaultValue = defaultMissingValues)
          ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") ]
  
    let helpText = 
        """<summary>Typed representation of an HTML file</summary>
           <param name='Sample'>Location of an HTML sample file or a string containing a sample HTML document</param>
           <param name='PreferOptionals'>When set to true, inference will prefer to use the option type instead of nullable types, double.NaN or "" for missing values. Defaults to false</param>
           <param name='MissingValues'>The set of strings recogized as missing values. Defaults to """ + "\"" + defaultMissingValues + "\"" + """</param>
           <param name='Culture'>The culture used for parsing numbers and dates. Defaults to the invariant culture</param>
           <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""
  
    do htmlProvTy.AddXmlDoc helpText
    do htmlProvTy.DefineStaticParameters(parameters, buildTypes)
  
    // Register the main type with F# compiler
    do this.AddNamespace(ns, [ htmlProvTy ])
