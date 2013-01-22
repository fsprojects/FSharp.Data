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
    resTy.AddMember(domainTy)

    let sample = args.[0] :?> string
    let globalInference = args.[1] :?> bool
    let sampleList = args.[2] :?> bool
    let culture = args.[3] :?> string
    let resolutionFolder = args.[4] :?> string

    // Infer the schema from a specified file or URI sample
    let sample = 
      try 
        let firstChar = sample.Trim().[0]
        if firstChar = '<' then
            XDocument.Parse(sample) 
        else
            use reader = ProviderHelpers.readTextAtDesignTime cfg this.Invalidate resolutionFolder sample
            XDocument.Parse(reader.ReadToEnd())
      with _ ->
        try XDocument.Parse(sample) 
        with _ -> failwith "Specified argument is neither a file, nor well-formed XML."

    let infered = 
      if not sampleList then
        XmlInference.inferType globalInference sample.Root
      else
        [ for itm in sample.Root.Descendants() -> XmlInference.inferType globalInference itm ]
        |> Seq.fold subtypeInfered Top

    let ctx = XmlGenerationContext.Create(domainTy, globalInference, replacer)
    let methResTy, methResConv = XmlTypeBuilder.generateXmlType culture ctx infered

    let (|Singleton|) = function Singleton s -> replacer.ToDesignTime s

    // Generate static Parse method
    let args =  [ ProvidedParameter("source", typeof<string>) ]
    let m = ProvidedMethod("Parse", args, methResTy)
    m.IsStaticMethod <- true
    m.InvokeCode <- fun (Singleton source) -> methResConv <@@ XmlElement.Create(XDocument.Parse(%%source).Root) @@>
    resTy.AddMember(m)

    // Generate static Load stream method
    let args =  [ ProvidedParameter("stream", typeof<Stream>) ]
    let m = ProvidedMethod("Load", args, methResTy)
    m.IsStaticMethod <- true
    m.InvokeCode <- fun (Singleton stream) -> methResConv <@@ use reader = new StreamReader(%%stream:Stream)
                                                              XmlElement.Create(XDocument.Parse(reader.ReadToEnd()).Root) @@>
    resTy.AddMember(m)

    // Generate static Load location method
    let args = [ ProvidedParameter("location", typeof<string>) ]
    let m = ProvidedMethod("Load", args, methResTy)
    m.IsStaticMethod <- true
    let isHostedExecution = cfg.IsHostedExecution
    let defaultResolutionFolder = cfg.ResolutionFolder
    m.InvokeCode <- fun (Singleton location) -> methResConv <@@ use reader = readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder %%location
                                                                XmlElement.Create(XDocument.Parse(reader.ReadToEnd()).Root) @@>
    resTy.AddMember(m)

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
       <param name='Sample'>Location of a XML sample file or a string containing sample XML document</param>
       <param name='Global'>If true, the inference unifies all XML elements with the same name</param>                     
       <param name='Culture'>The culture used for parsing numbers and dates.</param>                     
       <param name='SampleList'>If true, the children of the root in the sample document represent individual samples for the inference.</param>
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do xmlProvTy.AddXmlDoc helpText
  do xmlProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ xmlProvTy ])