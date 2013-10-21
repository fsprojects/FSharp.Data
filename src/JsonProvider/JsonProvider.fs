namespace ProviderImplementation

open System.IO
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProviderHelpers
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open FSharp.Data.RuntimeImplementation

// ----------------------------------------------------------------------------------------------

#nowarn "10001"

[<TypeProvider>]
type public JsonProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

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

    let cultureInfo = Operations.GetCulture culture
    let parseSingle _ value = JsonValue.Parse(value, cultureInfo)
    let parseList _ value = JsonValue.Parse(value, cultureInfo).AsArray() :> seq<_>
    
    let getSpecFromSamples samples = 

      let inferedType = 
        [ for sampleJson in samples -> JsonInference.inferType cultureInfo (*allowNulls*)true sampleJson ]
        |> Seq.fold (StructureInference.subtypeInfered (*allowNulls*)true) StructuralTypes.Top
  
      let ctx = JsonGenerationContext.Create(domainTy, replacer)
      let resTy, resTypConv = JsonTypeBuilder.generateJsonType culture ctx (NameUtils.singularize rootName) inferedType

      { GeneratedType = tpType
        RepresentationType = resTy
        CreateFromTextReader = fun reader -> 
          resTypConv <@@ JsonDocument.Create(%reader, culture) @@>
        AsyncCreateFromTextReader = fun readerAsync -> 
          resTypConv <@@ JsonDocument.AsyncCreate(%readerAsync, culture) @@>
        CreateFromTextReaderForSampleList = fun reader -> 
          resTypConv <@@ JsonDocument.CreateList(%reader, culture) @@>
        AsyncCreateFromTextReaderForSampleList = fun readerAsync -> 
          resTypConv <@@ JsonDocument.AsyncCreateList(%readerAsync, culture) @@> }

    generateConstructors "JSON" sample sampleIsList
                         parseSingle parseList getSpecFromSamples 
                         this cfg replacer resolutionFolder

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
