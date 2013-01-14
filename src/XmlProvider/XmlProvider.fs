namespace ProviderImplementation

open System
open System.IO
open System.Xml.Linq
open System.Linq.Expressions
open System.Reflection
open System.Globalization
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open System.Collections.Generic
open ProviderImplementation.ProvidedTypes

open FSharp.Net
open ProviderImplementation

// ----------------------------------------------------------------------------------------------

[<TypeProvider>]
type public XmlProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.JsonProvider'
  let asm = System.Reflection.Assembly.GetExecutingAssembly()
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

    // Infer the schema from a specified file or URI sample
    let sample = 
      try 
        let text = ProviderHelpers.readFileInProvider cfg (args.[0] :?> string) 
        XDocument.Parse(text)
      with _ ->
        try XDocument.Parse(args.[0] :?> string) 
        with _ -> failwith "Specified argument is neither a file, nor well-formed XML."

    // Use global inference (unify elements in different locations)
    let globalInference = args.[1] :?> bool
    let culture = args.[3] :?> string

    let infered = 
      let sampleList = args.[2] :?> bool
      if not sampleList then
        XmlInference.inferType globalInference sample.Root
      else
        [ for itm in sample.Root.Descendants() -> XmlInference.inferType globalInference itm ]
        |> Seq.fold StructureInference.subtypeInfered StructureInference.Top

    let ctx = XmlGenerationContext.Create(domainTy, globalInference)
    let culture = Conversions.Operations.GetCulture culture
    let methResTy, methResConv = XmlTypeBuilder.generateXmlType culture ctx infered
    
    // Generate static Parse method
    let args =  [ ProvidedParameter("source", typeof<string>) ]
    let m = ProvidedMethod("Parse", args, methResTy)
    m.IsStaticMethod <- true
    m.InvokeCode <- fun (Singleton source) -> methResConv <@@ XmlElement.Create(XDocument.Parse(%%source).Root) @@>
    resTy.AddMember(m)

    // Generate static Load method
    let args =  [ ProvidedParameter("path", typeof<string>) ]
    let m = ProvidedMethod("Load", args, methResTy)
    m.IsStaticMethod <- true
    m.InvokeCode <- fun (Singleton source) -> methResConv <@@ XmlElement.Create(XDocument.Parse(File.ReadAllText(%%source)).Root) @@>
    resTy.AddMember(m)

    // Return the generated type
    resTy

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("Sample", typeof<string>)
      ProvidedStaticParameter("Global", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("SampleList", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("Culture", typeof<string>, "") ]

  let helpText = 
    """<summary>Typed representation of a XML file</summary>
       <param name='Sample'>Location of a XML sample file or a string containing sample XML document</param>
       <param name='Culture'>The culture used for parsing numbers and dates.</param>                     
       <param name='Global'>If true, the inference unifies all XML elements with the same name</param>                     
       <param name='SampleList'>If true, the children of the root in the sample document represent individual samples for the inference.</param>"""

  do xmlProvTy.AddXmlDoc helpText
  do xmlProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ xmlProvTy ])