namespace ProviderImplementation

open System.IO
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProviderHelpers
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open FSharp.Data.Runtime

// ----------------------------------------------------------------------------------------------

#nowarn "10001"

[<TypeProvider>]
type public JsonProvider(cfg:TypeProviderConfig) as this =
  inherit DisposableTypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.JsonProvider'
  let asm, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let jsonProvTy = ProvidedTypeDefinition(asm, ns, "JsonProvider", Some typeof<obj>)

  let buildTypes (typeName:string) (args:obj[]) =

    // Generate the required type
    let tpType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)

    // A type that is used to hide all generated domain types
    let domainTy = ProvidedTypeDefinition("DomainTypes", Some typeof<obj>)
    tpType.AddMember(domainTy)

    let sample = args.[0] :?> string
    let sampleIsList = args.[1] :?> bool
    let rootName = args.[2] :?> string
    let culture = args.[3] :?> string
    let resolutionFolder = args.[4] :?> string

    let cultureInfo = TextRuntime.GetCulture culture
    let parseSingle _ value = JsonValue.Parse(value, cultureInfo)
    let parseList _ value = JsonValue.Parse(value, cultureInfo).AsArray() :> seq<_>
    
    let getSpecFromSamples samples = 

      let inferedType = 
        [ for sampleJson in samples -> JsonInference.inferType cultureInfo (*allowNulls*)true sampleJson ]
        |> Seq.fold (StructuralInference.subtypeInfered (*allowNulls*)true) StructuralTypes.Top
  
      let ctx = JsonGenerationContext.Create(culture, domainTy, replacer)
      let input = { ParentName = NameUtils.singularize rootName
                    CanPassUnpackedOption = false
                    Optional = false }
      let output = JsonTypeBuilder.generateJsonType ctx input inferedType

      { GeneratedType = tpType
        RepresentationType = output.ConvertedType
        CreateFromTextReader = fun reader -> 
          <@@ JsonDocument.Create(%reader, culture) @@> |> output.Converter
        AsyncCreateFromTextReader = fun readerAsync -> 
          <@@ JsonDocument.AsyncCreate(%readerAsync, culture) @@> |> output.Converter
        CreateFromTextReaderForSampleList = fun reader -> 
          <@@ JsonDocument.CreateList(%reader, culture) @@> |> output.Converter
        AsyncCreateFromTextReaderForSampleList = fun readerAsync -> 
          <@@ JsonDocument.AsyncCreateList(%readerAsync, culture) @@> |> output.Converter }

    generateConstructors "JSON" sample sampleIsList
                         parseSingle parseList getSpecFromSamples 
                         this cfg replacer resolutionFolder false

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
       <param name='RootName'>The name to be used to the root type. Defaults to 'Entity'.</param>
       <param name='Culture'>The culture used for parsing numbers and dates.</param>
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do jsonProvTy.AddXmlDoc helpText
  do jsonProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ jsonProvTy ])
