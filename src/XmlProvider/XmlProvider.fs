namespace ProviderImplementation

open System
open System.IO
open System.Xml.Linq
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.ProviderFileSystem
open FSharp.Data.RuntimeImplementation.StructuralTypes

// ----------------------------------------------------------------------------------------------

[<TypeProvider>]
type public XmlProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.XmlProvider'
  let asm, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let xmlProvTy = ProvidedTypeDefinition(asm, ns, "XmlProvider", Some(typeof<obj>))

  let buildTypes (typeName:string) (args:obj[]) =

    // Generate the required type with empty constructor
    let resTy = ProvidedTypeDefinition(asm, ns, typeName, Some(typeof<obj>))
    ProvidedConstructor([], InvokeCode = fun _ -> <@@ new obj() @@>)
    |> resTy.AddMember

    // A type that is used to hide all generated domain types
    let domainTy = ProvidedTypeDefinition("DomainTypes", Some(typeof<obj>))
    resTy.AddMember domainTy

    let sample = args.[0] :?> string
    let globalInference = args.[1] :?> bool
    let sampleList = args.[2] :?> bool
    let culture = args.[3] :?> string
    let cultureInfo = Operations.GetCulture culture
    let resolutionFolder = args.[4] :?> string
    let isHostedExecution = cfg.IsHostedExecution
    let defaultResolutionFolder = cfg.ResolutionFolder

    let parse value = 
      if sampleList then
        try
          XDocument.Parse(value).Root.Descendants()
        with _  ->
          value.Split '\n'
          |> Seq.filter (not << String.IsNullOrWhiteSpace)
          |> Seq.map (fun value -> XDocument.Parse(value).Root)
      else
        XDocument.Parse(value).Root |> Seq.singleton

    // Infer the schema from a specified uri or inline text
    let sampleXml, sampleIsUri = 
      try
        match ProviderHelpers.tryGetUri sample with
        | Some uri ->
            use reader = ProviderHelpers.readTextAtDesignTime defaultResolutionFolder this.Invalidate resolutionFolder uri
            parse (reader.ReadToEnd()), true
        | None ->
            parse sample, false
      with e ->
        failwithf "Specified argument is neither a file, nor well-formed XML: %s" e.Message

    let inferedType = 
      sampleXml
      |> Seq.map (fun item -> XmlInference.inferType cultureInfo globalInference item)
      |> Seq.fold subtypeInfered Top

    let ctx = XmlGenerationContext.Create(domainTy, globalInference, replacer)
    let methResTy, methResConv = XmlTypeBuilder.generateXmlType culture ctx inferedType

    // Generate static Parse method
    let args = [ ProvidedParameter("text", typeof<string>) ]
    let m = ProvidedMethod("Parse", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton text) -> methResConv <@@ XmlElement(XDocument.Parse(%%text).Root) @@>
    resTy.AddMember m

    // Generate static Load stream method
    let args = [ ProvidedParameter("stream", typeof<Stream>) ]
    let m = ProvidedMethod("Load", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton stream) -> methResConv <@@ use reader = new StreamReader(%%stream:Stream)
                                                              XmlElement(XDocument.Parse(reader.ReadToEnd()).Root) @@>
    resTy.AddMember m

    // Generate static Load reader method
    let args = [ ProvidedParameter("reader", typeof<TextReader>) ]
    let m = ProvidedMethod("Load", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton reader) -> methResConv <@@ XmlElement(XDocument.Parse((%%reader:TextReader).ReadToEnd()).Root) @@>
    resTy.AddMember m

    // Generate static Load uri method
    let args = [ ProvidedParameter("uri", typeof<string>) ]
    let m = ProvidedMethod("Load", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton uri) -> methResConv <@@ use reader = readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder %%uri
                                                           XmlElement(XDocument.Parse(reader.ReadToEnd()).Root) @@>
    resTy.AddMember m

    if not sampleList then
      // Generate static GetSample method
      let m = ProvidedMethod("GetSample", [],  methResTy, IsStaticMethod = true)
      m.InvokeCode <- fun _ -> 
        (if sampleIsUri then
          <@@ use reader = readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder sample
              XmlElement(XDocument.Parse(reader.ReadToEnd()).Root) @@>
         else
          <@@ XmlElement(XDocument.Parse(sample).Root) @@>)
        |> methResConv
      resTy.AddMember m

    // Return the generated type
    resTy

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("Sample", typeof<string>)
      ProvidedStaticParameter("Global", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("SampleList", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "") 
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") ]

  let helpText = 
    """<summary>Typed representation of a XML file</summary>
       <param name='Sample'>Location of a XML sample file or a string containing a sample XML document</param>
       <param name='Global'>If true, the inference unifies all XML elements with the same name</param>                     
       <param name='Culture'>The culture used for parsing numbers and dates.</param>                     
       <param name='SampleList'>If true, the children of the root in the sample document represent individual samples for the inference.</param>
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do xmlProvTy.AddXmlDoc helpText
  do xmlProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ xmlProvTy ])