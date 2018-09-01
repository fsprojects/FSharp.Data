namespace ProviderImplementation

open System.IO
open System.Xml.Linq
open System.Xml.Schema
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
    let asm = AssemblyResolver.init cfg (this :> TypeProviderForNamespaces)
    let ns = "FSharp.Data"
    let xmlProvTy = ProvidedTypeDefinition(asm, ns, "XmlProvider", None, hideObjectMethods=true, nonNullable=true)
  
    let buildTypes (typeName:string) (args:obj[]) =
  
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
       
        if schema <> "" then
            if sample <> "" then
                failwith "When the Schema parameter is used, the Sample parameter cannot be used"
            if sampleIsList then
                failwith "When the Schema parameter is used, the SampleIsList parameter must be set to false"

        let getSpec _ value =
       
            let inferedType = 
       
                if schema <> "" then
       
                     let schemaSet = using (IO.logTime "Parsing" sample) <| fun _ ->
                          XsdParsing.parseSchema resolutionFolder value
       
                     using (IO.logTime "Inference" sample) <| fun _ ->
                        schemaSet
                        |> XsdParsing.getElements 
                        |> List.ofSeq
                        |> XsdInference.inferElements

                else
       
                    let samples = 
                        using (IO.logTime "Parsing" sample) <| fun _ ->
                            if sampleIsList then
                                XmlElement.CreateList(new StringReader(value))
                                |> Array.map (fun doc -> doc.XElement)
                            else
                                [| XDocument.Parse(value).Root |]
       
                    using (IO.logTime "Inference" sample) <| fun _ ->
                        samples
                        |> XmlInference.inferType inferTypesFromValues (TextRuntime.GetCulture cultureStr) (*allowEmptyValues*)false globalInference
                        |> Array.fold (StructuralInference.subtypeInfered (*allowEmptyValues*)false) InferedType.Top
        
            using (IO.logTime "TypeGeneration" sample) <| fun _ ->
          
                let ctx = XmlGenerationContext.Create(cultureStr, tpType, globalInference || schema <> "")  
                let result = XmlTypeBuilder.generateXmlType ctx inferedType
              
                { GeneratedType = tpType
                  RepresentationType = result.ConvertedType
                  CreateFromTextReader = fun reader -> 
                      result.Converter <@@ XmlElement.Create(%reader) @@>
                  CreateFromTextReaderForSampleList = fun reader -> 
                      result.Converter <@@ XmlElement.CreateList(%reader) @@> }
       
        let source = 
            if schema <> "" then
                Schema schema
            elif sampleIsList then
                SampleList sample
            else
                Sample sample
       
        generateType (if schema <> "" then "XSD" else "XML") source getSpec this cfg encodingStr resolutionFolder resource typeName None
  
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
  