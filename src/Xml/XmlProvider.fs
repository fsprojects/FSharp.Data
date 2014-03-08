namespace ProviderImplementation

open System.IO
open System.Xml.Linq
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProviderHelpers
open FSharp.Data.Runtime

// ----------------------------------------------------------------------------------------------

#nowarn "10001"

[<TypeProvider>]
type public XmlProvider(cfg:TypeProviderConfig) as this =
  inherit DisposableTypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.XmlProvider'
  let asm, version, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let xmlProvTy = ProvidedTypeDefinition(asm, ns, "XmlProvider", Some typeof<obj>)

  let buildTypes (typeName:string) (args:obj[]) =

    // Generate the required type
    let tpType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)

    let sample = args.[0] :?> string
    let sampleIsList = args.[1] :?> bool
    let globalInference = args.[2] :?> bool
    let cultureStr = args.[3] :?> string
    let resolutionFolder = args.[4] :?> string

    let cultureInfo = TextRuntime.GetCulture cultureStr
    let parseSingle _ value = XDocument.Parse(value).Root
    let parseList _ value = 
        XmlElement.CreateList(new StringReader(value))
        |> Seq.map (fun doc -> doc.XElement)

    let getSpecFromSamples samples = 

      let inferedType = using (IO.logTime "Inference" sample) <| fun _ ->
        samples
        |> Seq.map (fun sampleXml -> XmlInference.inferType cultureInfo (*allowEmptyValues*)false globalInference sampleXml)
        |> Seq.fold (StructuralInference.subtypeInfered (*allowEmptyValues*)false) StructuralTypes.Top

      using (IO.logTime "TypeGeneration" sample) <| fun _ ->

      let ctx = XmlGenerationContext.Create(cultureStr, tpType, globalInference, replacer)  
      let result = XmlTypeBuilder.generateXmlType ctx inferedType

      { GeneratedType = tpType
        RepresentationType = result.ConvertedType
        CreateFromTextReader = fun reader -> 
          result.Converter <@@ XmlElement.Create(%reader) @@>
        CreateFromTextReaderForSampleList = fun reader -> 
          result.Converter <@@ XmlElement.CreateList(%reader) @@> }

    generateConstructors "XML" sample sampleIsList parseSingle parseList
                         getSpecFromSamples version this cfg replacer resolutionFolder

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("Sample", typeof<string>)
      ProvidedStaticParameter("SampleIsList", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("Global", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "") 
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") ]

  let helpText = 
    """<summary>Typed representation of a XML file</summary>
       <param name='Sample'>Location of a XML sample file or a string containing a sample XML document</param>
       <param name='SampleIsList'>If true, the children of the root in the sample document represent individual samples for the inference.</param>
       <param name='Global'>If true, the inference unifies all XML elements with the same name</param>                     
       <param name='Culture'>The culture used for parsing numbers and dates.</param>                     
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do xmlProvTy.AddXmlDoc helpText
  do xmlProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ xmlProvTy ])
