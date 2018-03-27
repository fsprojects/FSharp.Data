﻿namespace ProviderImplementation

open System.IO
open System.Xml.Linq
open FSharp.Core.CompilerServices
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProviderHelpers
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralTypes

// ----------------------------------------------------------------------------------------------

#nowarn "10001"

[<TypeProvider>]
type public XmlProvider(cfg:TypeProviderConfig) as this =
  inherit DisposableTypeProviderForNamespaces(cfg, assemblyReplacementMap=[ "FSharp.Data.DesignTime", "FSharp.Data" ])

  // Generate namespace and type 'FSharp.Data.XmlProvider'
  let asm, version = AssemblyResolver.init cfg (this :> TypeProviderForNamespaces)
  let ns = "FSharp.Data"
  let xmlProvTy = ProvidedTypeDefinition(asm, ns, "XmlProvider", None, hideObjectMethods=true, nonNullable=true)

  let cache = System.Collections.Concurrent.ConcurrentDictionary<string, ProvidedTypeDefinition>()

  let buildTypes (typeName:string) (args:obj[]) = 
   cache.GetOrAdd(typeName, fun typeName ->

    // Generate the required type
    let tpType = ProvidedTypeDefinition(asm, ns, typeName, None, hideObjectMethods=true, nonNullable=true)

    let sample = args.[0] :?> string
    let sampleIsList = args.[1] :?> bool
    let globalInference = args.[2] :?> bool
    let cultureStr = args.[3] :?> string
    let encodingStr = args.[4] :?> string
    let resolutionFolder = args.[5] :?> string
    let resource = args.[6] :?> string
    let inferTypesFromValues = args.[7] :?> bool
    let schema = args.[8] :?> string

    let cultureInfo = TextRuntime.GetCulture cultureStr
    let parseSingle _ value = XDocument.Parse(value).Root
    let parseList _ value = 
        XmlElement.CreateList(new StringReader(value))
        |> Array.map (fun doc -> doc.XElement)

    let getSpecFromSamples samples = 

      let inferedType = using (IO.logTime "Inference" sample) <| fun _ ->
        samples
        |> Array.ofSeq
        |> XmlInference.inferType inferTypesFromValues cultureInfo (*allowEmptyValues*)false globalInference
        |> Seq.fold (StructuralInference.subtypeInfered (*allowEmptyValues*)false) InferedType.Top

      using (IO.logTime "TypeGeneration" sample) <| fun _ ->

      let ctx = XmlGenerationContext.Create(cultureStr, tpType, globalInference)  
      let result = XmlTypeBuilder.generateXmlType ctx inferedType

      { GeneratedType = tpType
        RepresentationType = result.ConvertedType
        CreateFromTextReader = fun reader -> 
          result.Converter <@@ XmlElement.Create(%reader) @@>
        CreateFromTextReaderForSampleList = fun reader -> 
          result.Converter <@@ XmlElement.CreateList(%reader) @@> }

    let parseSingleSchema _ value = XsdParsing.parseSchema resolutionFolder value
    let parseListOfSchema _ _ = failwith "unexpected call to parseListOfSchema"

    let getSpecFromSchema schemaSet = 

      let inferedType = using (IO.logTime "Inference" sample) <| fun _ ->
          schemaSet 
          |> Seq.exactlyOne
          |> XsdParsing.getElements 
          |> List.ofSeq
          |> XsdInference.inferElements

      using (IO.logTime "TypeGeneration" sample) <| fun _ ->

      let ctx = XmlGenerationContext.Create(cultureStr, tpType, (*globalInference*)true)  
      let result = XmlTypeBuilder.generateXmlType ctx inferedType

      { GeneratedType = tpType
        RepresentationType = result.ConvertedType
        CreateFromTextReader = fun reader -> 
          result.Converter <@@ XmlElement.Create(%reader) @@>
        CreateFromTextReaderForSampleList = fun reader -> 
          result.Converter <@@ XmlElement.CreateList(%reader) @@> }

    let result =
        if schema = "" then
            generateType "XML" sample sampleIsList parseSingle parseList getSpecFromSamples 
                version this cfg encodingStr resolutionFolder resource typeName None
        else
            generateType "XML" schema false parseSingleSchema parseListOfSchema getSpecFromSchema 
                version this cfg "" resolutionFolder resource typeName None
    async { do! Async.Sleep (10000)
            if cache <> null then cache.TryRemove(typeName) |> ignore } |> Async.Start
    result
   )

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("Sample", typeof<string>, parameterDefaultValue = "")
      ProvidedStaticParameter("SampleIsList", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("Global", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "") 
      ProvidedStaticParameter("Encoding", typeof<string>, parameterDefaultValue = "") 
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")
      ProvidedStaticParameter("EmbeddedResource", typeof<string>, parameterDefaultValue = "")
      ProvidedStaticParameter("InferTypesFromValues", typeof<bool>, parameterDefaultValue = true) 
      ProvidedStaticParameter("Schema", typeof<string>, parameterDefaultValue = "") ]

  let helpText = 
    """<summary>Typed representation of a XML file.</summary>
       <param name='Sample'>Location of a XML sample file or a string containing a sample XML document.</param>
       <param name='SampleIsList'>If true, the children of the root in the sample document represent individual samples for the inference.</param>
       <param name='Global'>If true, the inference unifies all XML elements with the same name.</param>                     
       <param name='Culture'>The culture used for parsing numbers and dates. Defaults to the invariant culture.</param>
       <param name='Encoding'>The encoding used to read the sample. You can specify either the character set name or the codepage number. Defaults to UTF8 for files, and to ISO-8859-1 the for HTTP requests, unless `charset` is specified in the `Content-Type` response header.</param>
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution).</param>
       <param name='EmbeddedResource'>When specified, the type provider first attempts to load the sample from the specified resource 
          (e.g. 'MyCompany.MyAssembly, resource_name.xml'). This is useful when exposing types generated by the type provider.</param>
       <param name='InferTypesFromValues'>If true, turns on additional type inference from values. 
          (e.g. type inference infers string values such as "123" as ints and values constrained to 0 and 1 as booleans. The XmlProvider also infers string values as JSON.)</param>
       <param name='Schema'>Location of a schema file or a string containing xsd.</param>"""


  do xmlProvTy.AddXmlDoc helpText
  do xmlProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ xmlProvTy ])
