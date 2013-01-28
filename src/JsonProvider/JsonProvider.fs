namespace ProviderImplementation

open System.IO
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.DataLoading
open FSharp.Data.RuntimeImplementation.TypeInference

// ----------------------------------------------------------------------------------------------

[<TypeProvider>]
type public JsonProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.JsonProvider'
  let asm, isPortable, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let jsonProvTy = ProvidedTypeDefinition(asm, ns, "JsonProvider", Some(typeof<obj>))

  let buildTypes (typeName:string) (args:obj[]) =

    // Generate the required type with empty constructor
    let resTy = ProvidedTypeDefinition(asm, ns, typeName, Some(typeof<obj>))
    ProvidedConstructor([], InvokeCode = fun _ -> <@@ new obj() @@>)
    |> resTy.AddMember

    // A type that is used to hide all generated domain types
    let domainTy = ProvidedTypeDefinition("DomainTypes", Some(typeof<obj>))
    resTy.AddMember(domainTy)

    let sample = args.[0] :?> string
    let sampleList = args.[1] :?> bool
    let culture = args.[2] :?> string
    let cultureInfo = Operations.GetCulture culture
    let resolutionFolder = args.[3] :?> string
    let isHostedExecution = cfg.IsHostedExecution
    let defaultResolutionFolder = cfg.ResolutionFolder

    // Infer the schema from a specified file or URI sampleTextOrUri
    let sampleJson, sampleIsUri = 
      try
        let firstChar = sample.Trim().[0]
        if firstChar = '{' || firstChar = '[' then
            JsonValue.Parse(sample, cultureInfo), false
        else
            try
                use reader = ProviderHelpers.readTextAtDesignTime defaultResolutionFolder this.Invalidate resolutionFolder sample
                JsonValue.Parse(reader.ReadToEnd(), cultureInfo), true
            with _ ->
                JsonValue.Parse(sample, cultureInfo), false
      with _ ->
        failwith "Specified argument is neither a file, nor well-formed JSON."

    let inferedType = 
      if not sampleList then
        JsonInference.inferType cultureInfo sampleJson
      else
        [ for itm in sampleJson -> JsonInference.inferType cultureInfo itm ]
        |> Seq.fold subtypeInfered Top

    let ctx = JsonGenerationContext.Create(domainTy, replacer)
    let methResTy, methResConv = JsonTypeBuilder.generateJsonType culture ctx inferedType

    let (|Singleton|) = function Singleton s -> replacer.ToDesignTime s

    // Generate static Parse method
    let args = [ ProvidedParameter("text", typeof<string>) ]
    let m = ProvidedMethod("Parse", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton text) -> methResConv <@@ JsonDocument(JsonValue.Parse(%%text, Operations.GetCulture(culture))) @@>
    resTy.AddMember m

    // Generate static Load stream method
    let args = [ ProvidedParameter("stream", typeof<Stream>) ]
    let m = ProvidedMethod("Load", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton stream) -> methResConv <@@ JsonDocument(JsonValue.Load(%%stream, Operations.GetCulture(culture))) @@>
    resTy.AddMember m

    // Generate static Load uri method
    let args = [ ProvidedParameter("uri", typeof<string>) ]
    let m = ProvidedMethod("Load", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton uri) -> methResConv <@@ use reader = readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder %%uri
                                                           JsonDocument(JsonValue.Parse(reader.ReadToEnd(), Operations.GetCulture(culture))) @@>
    resTy.AddMember m

    if not sampleList then
        // Generate static GetSample method
        let m = ProvidedMethod("GetSample", [], methResTy, IsStaticMethod = true)
        m.InvokeCode <- fun _ -> 
            if sampleIsUri then
                <@@ use reader = readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder sample
                    JsonDocument(JsonValue.Parse(reader.ReadToEnd(), Operations.GetCulture(culture))) @@>
            else
                <@@ JsonDocument(JsonValue.Parse(sample, Operations.GetCulture(culture))) @@>
            |> methResConv
        resTy.AddMember m

    // Return the generated type
    resTy

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("Sample", typeof<string>)
      ProvidedStaticParameter("SampleList", typeof<bool>, parameterDefaultValue = false) 
      ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "") 
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") ]

  let helpText = 
    """<summary>Typed representation of a JSON document</summary>
       <param name='Sample'>Location of a JSON sample file or a string containing a sample JSON document</param>
       <param name='SampleList'>If true, sample should be a list of individual samples for the inference.</param>
       <param name='Culture'>The culture used for parsing numbers and dates.</param>
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do jsonProvTy.AddXmlDoc helpText
  do jsonProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ jsonProvTy ])
