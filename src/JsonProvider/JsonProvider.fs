namespace ProviderImplementation

open System.IO
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open FSharp.Data
open FSharp.Data.Json
open FSharp.Data.Json.JsonReader

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
    let resolutionFolder = args.[2] :?> string

    // Infer the schema from a specified file or URI sample    
    let sample = 
      try         
        let firstChar = sample.Trim().[0]
        if firstChar = '{' || firstChar = '[' then
            JsonValue.Parse(sample) 
        else
            use reader = ProviderHelpers.readTextAtDesignTime cfg this.Invalidate resolutionFolder sample
            JsonValue.Parse(reader.ReadToEnd())
      with _ ->
        try JsonValue.Parse(sample) 
        with _ -> failwith "Specified argument is neither a file, nor well-formed JSON."

    let infered = 
      if not sampleList then
        JsonInference.inferType sample
      else
        [ for itm in sample -> JsonInference.inferType itm ]
        |> Seq.fold StructureInference.subtypeInfered StructureInference.Top

    let ctx = JsonGenerationContext.Create(domainTy, replacer)
    let methResTy, methResConv = JsonTypeBuilder.generateJsonType ctx infered

    let (|Singleton|) = function Singleton s -> replacer.ToDesignTime s
    
    // Generate static Parse method
    let args = [ ProvidedParameter("source", typeof<string>) ]
    let m = ProvidedMethod("Parse", args, methResTy)
    m.IsStaticMethod <- true
    m.InvokeCode <- fun (Singleton source) -> methResConv <@@ JsonDocument.Create(JsonValue.Parse (%%source)) @@>
    resTy.AddMember(m)

    // Generate static Load stream method
    let args = [ ProvidedParameter("stream", typeof<Stream>) ]
    let m = ProvidedMethod("Load", args, methResTy)
    m.IsStaticMethod <- true
    m.InvokeCode <- fun (Singleton stream) -> methResConv <@@ JsonDocument.Create(JsonValue.Load (%%stream)) @@>
    resTy.AddMember(m)

    // Generate static Load location method
    let args = [ ProvidedParameter("location", typeof<string>) ]
    let m = ProvidedMethod("Load", args, methResTy)
    m.IsStaticMethod <- true
    let isHostedExecution = cfg.IsHostedExecution
    let defaultResolutionFolder = cfg.ResolutionFolder
    m.InvokeCode <- fun (Singleton location) -> methResConv <@@ use reader = Importing.readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder %%location
                                                                JsonDocument.Create(JsonValue.Parse(reader.ReadToEnd())) @@>
    resTy.AddMember(m)

    // Return the generated type
    resTy

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("Sample", typeof<string>)
      ProvidedStaticParameter("SampleList", typeof<bool>, parameterDefaultValue = false) 
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") ]

  let helpText = 
    """<summary>Typed representation of a JSON document</summary>
       <param name='Sample'>Location of a JSON sample file or a string containing sample JSON document</param>
       <param name='SampleList'>If true, sample should be a list of individual samples for the inference.</param>
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do jsonProvTy.AddXmlDoc helpText
  do jsonProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ jsonProvTy ])
