namespace ProviderImplementation

open System
open System.IO
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProviderHelpers
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes

// ----------------------------------------------------------------------------------------------

#nowarn "10001"

[<TypeProvider>]
type public JsonProvider(cfg:TypeProviderConfig) as this =
  inherit DisposableTypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.JsonProvider'
  let asm, version, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let jsonProvTy = ProvidedTypeDefinition(asm, ns, "JsonProvider", Some typeof<obj>)

  let buildTypes (typeName:string) (args:obj[]) =

    // Generate the required type
    let tpType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)

    let sample = args.[0] :?> string
    let sampleIsList = args.[1] :?> bool
    let rootName = args.[2] :?> string
    let cultureStr = args.[3] :?> string
    let resolutionFolder = args.[4] :?> string

    let cultureInfo = TextRuntime.GetCulture cultureStr
    let parseSingle _ value = JsonValue.Parse(value, cultureInfo)
    let parseList _ value = 
        JsonDocument.CreateList(new StringReader(value), cultureStr)
        |> Seq.map (fun doc -> doc.JsonValue)
    
    let getSpecFromSamples samples = 

      let inferedType = 
        [ for sampleJson in samples -> JsonInference.inferType cultureInfo (*allowEmptyValues*)false (NameUtils.singularize rootName) sampleJson ]
        |> Seq.fold (StructuralInference.subtypeInfered (*allowEmptyValues*)false) StructuralTypes.Top
  
      let ctx = JsonGenerationContext.Create(cultureStr, tpType, replacer)
      let result = JsonTypeBuilder.generateJsonType ctx (*canPassAllConversionCallingTypes*)false (*optionalityHandledByParent*)false inferedType

      { GeneratedType = tpType
        RepresentationType = result.ConvertedType
        CreateFromTextReader = fun reader -> 
          result.GetConverter ctx <@@ JsonDocument.Create(%reader, cultureStr) @@>
        CreateFromTextReaderForSampleList = fun reader -> 
          result.GetConverter ctx <@@ JsonDocument.CreateList(%reader, cultureStr) @@> }

    generateConstructors "JSON" sample sampleIsList
                         parseSingle parseList getSpecFromSamples 
                         version this cfg replacer resolutionFolder false

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("Sample", typeof<string>)
      ProvidedStaticParameter("SampleIsList", typeof<bool>, parameterDefaultValue = false) 
      ProvidedStaticParameter("RootName", typeof<string>, parameterDefaultValue = "") 
      ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "") 
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") ]

  let helpText = 
    """<summary>Typed representation of a JSON document</summary>
       <param name='Sample'>Location of a JSON sample file or a string containing a sample JSON document</param>
       <param name='SampleIsList'>If true, sample should be a list of individual samples for the inference.</param>
       <param name='RootName'>The name to be used to the root type. Defaults to 'Root'.</param>
       <param name='Culture'>The culture used for parsing numbers and dates.</param>
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do jsonProvTy.AddXmlDoc helpText
  do jsonProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ jsonProvTy ])
