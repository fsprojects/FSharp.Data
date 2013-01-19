namespace ProviderImplementation

open System
open System.IO
open System.Linq.Expressions
open System.Reflection
open System.Globalization
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open System.Collections.Generic
open ProviderImplementation.ProvidedTypes

open FSharp.Net
open FSharp.Data.Json
open FSharp.Data.Json.JsonReader
open ProviderImplementation

// ----------------------------------------------------------------------------------------------

[<TypeProvider>]
type public JsonProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.JsonProvider'
  let asm = System.Reflection.Assembly.GetExecutingAssembly()
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


    // Infer the schema from a specified file or URI sample
    let sample = 
      try 
        let sample = args.[0] :?> string
        let resolutionFolder = args.[2] :?> string
        use reader = ProviderHelpers.readTextAtDesignTime cfg this.Invalidate resolutionFolder sample
        JsonValue.Parse(reader.ReadToEnd())
      with _ ->
        try JsonValue.Parse(args.[0] :?> string) 
        with _ -> failwith "Specified argument is neither a file, nor well-formed JSON."

    let infered = 
      let sampleList = args.[1] :?> bool
      if not sampleList then
        JsonInference.inferType sample
      else
        [ for itm in sample -> JsonInference.inferType itm ]
        |> Seq.fold StructureInference.subtypeInfered StructureInference.Top

    let ctx = JsonGenerationContext.Create(domainTy)
    let methResTy, methResConv = JsonTypeBuilder.generateJsonType ctx infered
    
    // Generate static Parse method
    let args =  [ ProvidedParameter("source", typeof<string>) ]
    let m = ProvidedMethod("Parse", args, methResTy)
    m.IsStaticMethod <- true
    m.InvokeCode <- fun (Singleton source) -> methResConv <@@ JsonDocument.Create(JsonValue.Parse (%%source)) @@>
    resTy.AddMember(m)

    // Generate static Load method
    let args =  [ ProvidedParameter("path", typeof<string>) ]
    let m = ProvidedMethod("Load", args, methResTy)
    m.IsStaticMethod <- true
    m.InvokeCode <- fun (Singleton source) -> methResConv <@@ JsonDocument.Create(JsonValue.Parse (File.ReadAllText(%%source))) @@>
    resTy.AddMember(m)

    // Return the generated type
    resTy

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("Sample", typeof<string>)
      ProvidedStaticParameter("SampleList", typeof<bool>, parameterDefaultValue = false) 
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = null) ]

  let helpText = 
    """<summary>Typed representation of a JSON document</summary>
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>
       <param name='Sample'>Location of a JSON sample file or a string containing sample JSON document</param>
       <param name='SampleList'>If true, sample should be a list of individual samples for the inference.</param>"""

  do jsonProvTy.AddXmlDoc helpText
  do jsonProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ jsonProvTy ])
