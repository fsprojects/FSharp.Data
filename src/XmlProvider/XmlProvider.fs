namespace ProviderImplementation

open System.IO
open System.Xml.Linq
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.DataLoading
open FSharp.Data.RuntimeImplementation.TypeInference

// ----------------------------------------------------------------------------------------------

[<TypeProvider>]
type public XmlProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.XmlProvider'
  let asm, isPortable, replacer = AssemblyResolver.init cfg
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
    let resolutionFolder = args.[4] :?> string
    let isHostedExecution = cfg.IsHostedExecution
    let defaultResolutionFolder = cfg.ResolutionFolder

    // Infer the schema from a specified file or URI sample
    let sampleXml, sampleIsUri = 
      try
        let firstChar = sample.Trim().[0]
        if firstChar = '<' then
            XDocument.Parse(sample), false
        else
            try
                use reader = ProviderHelpers.readTextAtDesignTime defaultResolutionFolder this.Invalidate resolutionFolder sample
                XDocument.Parse(reader.ReadToEnd()), true
            with _ ->
                XDocument.Parse(sample), false
      with _ ->
        failwith "Specified argument is neither a file, nor well-formed XML."

    let inferedType = 
      if not sampleList then
        XmlInference.inferType globalInference sampleXml.Root
      else
        [ for itm in sampleXml.Root.Descendants() -> XmlInference.inferType globalInference itm ]
        |> Seq.fold subtypeInfered Top

    let ctx = XmlGenerationContext.Create(domainTy, globalInference, replacer)
    let methResTy, methResConv = XmlTypeBuilder.generateXmlType culture ctx inferedType

    let (|Singleton|) = function Singleton s -> replacer.ToDesignTime s

    // Generate static Parse method
    let args =
        if not sampleIsUri then
            [ ProvidedParameter("text", typeof<string>, optionalValue = sample) ]
        else
            [ ProvidedParameter("text", typeof<string>) ]
    let m = ProvidedMethod("Parse", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton text) -> methResConv <@@ XmlElement(XDocument.Parse(%%text).Root) @@>
    resTy.AddMember m

    // Generate static Load stream method
    let args = [ ProvidedParameter("stream", typeof<Stream>) ]
    let m = ProvidedMethod("Load", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton stream) -> methResConv <@@ use reader = new StreamReader(%%stream:Stream)
                                                              XmlElement(XDocument.Parse(reader.ReadToEnd()).Root) @@>
    resTy.AddMember m

    // Generate static Load uri method
    let args =
        if sampleIsUri then
            // TODO: if the uri is a file and we're compiling for a portable library, don't make the uri optional, as it won't work at runtime
            [ ProvidedParameter("uri", typeof<string>, optionalValue = sample) ]
        else
            [ ProvidedParameter("uri", typeof<string>) ]
    let m = ProvidedMethod("Load", args, methResTy, IsStaticMethod = true)
    m.InvokeCode <- fun (Singleton uri) -> methResConv <@@ use reader = readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder %%uri
                                                           XmlElement(XDocument.Parse(reader.ReadToEnd()).Root) @@>
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