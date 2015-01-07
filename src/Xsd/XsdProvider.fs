namespace ProviderImplementation

open System.IO
open System.Linq
open System.Xml.Linq
open System.Xml.Schema
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProviderHelpers
open FSharp.Data.Runtime

// ----------------------------------------------------------------------------------------------

#nowarn "10001"

[<TypeProvider>]
type public XsdProvider(cfg:TypeProviderConfig) as this =
  inherit DisposableTypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.XmlProvider'
  let asm, version, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let xmlProvTy = ProvidedTypeDefinition(asm, ns, "XsdProvider", Some typeof<obj>)

  let buildTypes (typeName:string) (args:obj[]) =
      let elements = ref []
      // Generate the required type
      let tpType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
      
      let sample = args.[0] :?> string
      let resolutionFolder = args.[1] :?> string
      let includeMetadata = args.[2] :?> bool
      let failOnUnsupported = args.[3] :?>  bool
      
      let read (reader:TextReader) = 
          let schema = XmlSchema.Read(reader,(fun o (e:ValidationEventArgs) -> failwith e.Message))
          reader.Dispose()
          schema

      let parseSingle _ (value: string) = 
          use sr = new StringReader(value)
          read sr
      let parseList _ _ = failwith "Never pass sampleIsList=true"
      
      let addTopLevelItems inferedType result = 
        match inferedType with
        StructuralTypes.InferedType.Heterogeneous types ->
          for (_,t) in types |> Map.toList do
            match t with
            StructuralTypes.InferedType.Record(Some _, [{Name = ""; Type = StructuralTypes.InferedType.Primitive(_)}],_) ->
                ()
            | StructuralTypes.InferedType.Record(Some n, _,_)  ->
              //For each top level type create a method to parse that type
              let n = NameUtils.nicePascalName n
              let res = tpType.GetMember(n) 
              match res with
                [||] -> 
                    failwithf "Could not find a provided type for %s" n
                | [|res|] when (res :? ProvidedTypeDefinition) ->
                    let resultType = res :?> ProvidedTypeDefinition
                    let args = [ ProvidedParameter("text", typeof<string>) ]
                    let m = ProvidedMethod("Parse" + n, args, resultType, IsStaticMethod = true)
                    let nlower = n.ToLower()
                    m.InvokeCode <- fun (Singleton text) -> 
                      
                      <@ 
                          let t = %%text
                          let doc = XDocument.Parse(t)
                          let t = 
                            if doc.Root.Name.LocalName.ToLower() = nlower then
                               //wrap the XML in a new root to make the XMLRuntime pick the right child elements
                               let newRoot = new XElement(doc.Root.Name.Namespace.GetName("root__"))
                               newRoot.Add(doc.Root)
                               let newDoc = new XDocument(doc.Declaration)
                               newDoc.Add(newRoot)
                               doc.ToString()
                            else
                               //Assume that the XML is already wrapped
                               t
                          new StringReader(t) :> TextReader
                      @>
                      |> fun reader -> result.Converter <@@ XmlElement.Create(%reader) @@>
                    m.AddXmlDoc <| sprintf "Parses the specified XML string as a %s" n
                    tpType.AddMember m
                | [|res|] -> failwithf "%s is not a provided type but a " res.Name (res.GetType().Name)
                | _ as res -> failwithf "Found several nested types (%A) with the name %s" res n
            | _ -> ()
        | _ as t -> failwithf "Did not expect %A" t

      let getTypes (schema : XmlSchema) =
        schema.SourceUri <- Path.Combine(resolutionFolder, "temp.xsd")
        elements := [for e in schema.Elements do yield e:?>XmlSchemaElement]
        schema |> XsdBuilder.generateType <| includeMetadata <| failOnUnsupported |> List.fold (StructuralInference.subtypeInfered (*allowNulls*)true) StructuralTypes.Top 
      
      let getTypesFromSchema (schema:XmlSchema) = 
        let inferredType = schema |> getTypes
            
        let wrappedType =
          match inferredType with
          StructuralTypes.InferedType.Record(_) as t -> t
          | StructuralTypes.InferedType.Heterogeneous cases ->
               let t = StructuralTypes.InferedType.Collection(cases |> Map.toList |> List.map fst, cases |> Map.map (fun _ v -> (StructuralTypes.InferedMultiplicity.Single, v)))
               //If there's no top level type then wrap the types in a parent, this type won't be used
               //We'll create a parse method for each individual type that can be used
               StructuralTypes.InferedType.Record(Some "Schema",[{Name = "";
                                                                  Type = t}],false)
          | _ as t -> t
      
        let ctx = XmlGenerationContext.Create(System.Globalization.CultureInfo.CurrentCulture.Name, tpType, true, replacer)  
        XmlTypeBuilder.generateXmlType ctx wrappedType, inferredType
      
      let getSpec (schema : XmlSchema seq) = 
        let result, inferredType = schema |> Seq.exactlyOne |> getTypesFromSchema 

        addTopLevelItems inferredType result

        { GeneratedType = tpType
          RepresentationType = result.ConvertedType
          CreateFromTextReader = fun reader -> 
            result.Converter <@@ XmlElement.Create(%reader) @@>
          CreateFromTextReaderForSampleList = fun reader -> 
            result.Converter <@@ XmlElement.CreateList(%reader) @@> }
      
      generateType "XSD" sample false
                               parseSingle parseList getSpec
                               version this cfg replacer resolutionFolder typeName
    

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("SchemaFile", typeof<string>)
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")
      ProvidedStaticParameter("IncludeMetadata", typeof<bool>, parameterDefaultValue = false)
      ProvidedStaticParameter("FailOnUnsupported", typeof<bool>, parameterDefaultValue = true)]

  let helpText = 
    """<summary>Typed representation of a XML file based on a xml schema definition (XSD)</summary>
       <param name='SchemaFile'>Location of a XSD file or a string containing the XSD</param>                    
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>
       <param name='IncludeMetadata'>If true XSD metadata such as target namespace will be included and accessible from each generated type</param>
       <param name='FailOnUnsupported'>If false then the provider will ignore unsupported features and do it's best to generate a type hierachie</param>"""

  do xmlProvTy.AddXmlDoc helpText
  do xmlProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ xmlProvTy ])