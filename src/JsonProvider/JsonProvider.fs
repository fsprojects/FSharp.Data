namespace ProviderImplementation

open System.IO
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.ProviderFileSystem
open FSharp.Data.RuntimeImplementation.StructuralTypes

// ----------------------------------------------------------------------------------------------

[<TypeProvider>]
type public JsonProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.JsonProvider'
  let asm, replacer = AssemblyResolver.init cfg
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

    // Infer the schema from a specified uri or inline text
    let sampleJson, sampleIsUri = 
      try
        match ProviderHelpers.tryGetUri sample with
        | Some uri ->
            use reader = ProviderHelpers.readTextAtDesignTime defaultResolutionFolder this.Invalidate resolutionFolder uri
            JsonValue.Parse(reader.ReadToEnd(), cultureInfo), true
        | None ->
            JsonValue.Parse(sample, cultureInfo), false
      with e ->
        failwithf "Specified argument is neither a file, nor well-formed JSON: %s" e.Message

    let inferedType = 
      if not sampleList then
        JsonInference.inferType cultureInfo sampleJson
      else
        [ for itm in sampleJson -> JsonInference.inferType cultureInfo itm ]
        |> Seq.fold subtypeInfered Top

    let ctx = JsonGenerationContext.Create(domainTy, replacer)
    let methResTy, methResConv = JsonTypeBuilder.generateJsonType culture ctx inferedType

    // Generate static Parse method
    let args = [ ProvidedParameter("text", typeof<string>) ]
    let m = ProvidedMethod("Parse", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton text) -> methResConv <@@ { JsonValue = JsonValue.Parse(%%text, Operations.GetCulture(culture)) } @@>
    m.AddXmlDoc "Parses the specified JSON string"
    resTy.AddMember m

    // Generate static Load stream method
    let args = [ ProvidedParameter("stream", typeof<Stream>) ]
    let m = ProvidedMethod("Load", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton stream) -> methResConv <@@ { JsonValue = JsonValue.Load((%%stream:Stream), Operations.GetCulture(culture)) } @@>
    m.AddXmlDoc "Loads JSON from the specified stream"
    resTy.AddMember m

    // Generate static Load reader method
    let args = [ ProvidedParameter("reader", typeof<TextReader>) ]
    let m = ProvidedMethod("Load", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton reader) -> methResConv <@@ { JsonValue = JsonValue.Load((%%reader:TextReader), Operations.GetCulture(culture)) } @@>
    m.AddXmlDoc "Loads JSON from the specified reader"
    resTy.AddMember m

    // Generate static Load uri method
    let args = [ ProvidedParameter("uri", typeof<string>) ]
    let m = ProvidedMethod("Load", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton uri) -> methResConv <@@ use reader = readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder %%uri
                                                           { JsonValue = JsonValue.Parse(reader.ReadToEnd(), Operations.GetCulture(culture)) } @@>
    m.AddXmlDoc "Loads JSON from the specified uri"
    resTy.AddMember m

    if not sampleList then
      // Generate static GetSample method
      let m = ProvidedMethod("GetSample", [], methResTy, IsStaticMethod = true)
      m.InvokeCode <- fun _ -> 
        (if sampleIsUri then
          <@@ use reader = readTextAtRunTimeWithDesignTimeOptions defaultResolutionFolder resolutionFolder sample
              { JsonValue = JsonValue.Parse(reader.ReadToEnd(), Operations.GetCulture(culture)) } @@>
         else
          <@@ { JsonValue = JsonValue.Parse(sample, Operations.GetCulture(culture)) } @@>)
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
