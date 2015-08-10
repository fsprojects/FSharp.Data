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
        let types = ref  StructuralTypes.InferedType.Top
        // Generate the required type
        let tpType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
        
        let sample = args.[0] :?> string
        let resolutionFolder = 
            match args.[1] :?> string with
            "" -> cfg.ResolutionFolder
            | _ as str -> str
        let parseSingle _ value = XDocument.Parse(value).Root
        let parseList _ value = XDocument.Parse(value).Root.Elements()
        
        let getTypes sample =
          match !types with
          StructuralTypes.InferedType.Top ->
            let read (reader:TextReader) = 
                let schema = XmlSchema.Read(reader,(fun o (e:ValidationEventArgs) -> failwith e.Message))
                reader.Dispose()
                schema
            let ts =
              let path,reader = 
                 if File.Exists(sample) then
                    sample,new StreamReader(File.OpenRead(sample)) :> TextReader
                 else
                     try
                           XDocument.Parse(sample) |> ignore
                           Path.Combine(resolutionFolder, "temp.xsd"),new StringReader(sample) :> TextReader
                     with e ->
                           let exists,path = 
                               try
                                  let p = Path.Combine(resolutionFolder,sample)
                                  File.Exists(p),p
                               with e ->
                                  false, ""
                           if  exists then
                              path, new StreamReader(File.OpenRead(path)) :> TextReader
                           else
                              failwithf "Could not find a file at %s and could not interprete as valid XML either" (Path.Combine(resolutionFolder,sample))
              let schema = read reader
              schema.SourceUri <- path
              let schemaSet = new XmlSchemaSet()
              schemaSet.Add(schema) |> ignore
              elements := [for e in schema.Elements 
                            do if e:? XmlSchemaElement then yield e:?>XmlSchemaElement]
              let xsdtypes = schemaSet |> XsdBuilder.generateType
              let consolidatedTypes = xsdtypes |> List.fold (StructuralInference.subtypeInfered (*allowNulls*)true) StructuralTypes.InferedType.Top 
              consolidatedTypes
            types := ts
            ts
          | _ -> !types
        
        let getTypesFromSchema (schema:string) = 
          let samples = schema |> getTypes
              
          let inferedType =
            match samples with
            StructuralTypes.InferedType.Record(_) as t -> t
            | StructuralTypes.InferedType.Heterogeneous cases ->
                 let t = StructuralTypes.InferedType.Collection(cases |> Map.map (fun _ v -> (StructuralTypes.InferedMultiplicity.Single, v)))
                 //If there's no top level type then wrap the types in a parent, this type won't be used
                 //We'll create a parse method for each individual type that can be used
                 StructuralTypes.InferedType.Record(Some "Schema",[{Name = "";
                                                                    Type = t}],false)
            | _ as t -> t
        
          let ctx = XmlGenerationContext.Create(System.Globalization.CultureInfo.CurrentCulture.Name, tpType, true, replacer)  
          XmlTypeBuilder.generateXmlType ctx inferedType
        
        let result = getTypesFromSchema sample
        
        let getSpec _ = 
          { GeneratedType = tpType
            RepresentationType = result.ConvertedType
            CreateFromTextReader = fun reader -> 
              result.Converter <@@ XmlElement.Create(%reader) @@>
            CreateFromTextReaderForSampleList = fun reader -> 
              result.Converter <@@ XmlElement.CreateList(%reader) @@> }
        
        let providedType =
            generateType "XSD" sample false
                                 parseSingle parseList getSpec
                                 version this cfg replacer resolutionFolder typeName
        
        let inferedType = getTypes sample
        
        let typeList = 
          match inferedType with
            StructuralTypes.InferedType.Heterogeneous types ->
                types |>  Map.fold(fun st _ t -> t::st) []
          | StructuralTypes.InferedType.Record _ -> [inferedType]
          | _ as t -> failwithf "Did not expect %A" t
        for t in typeList do
              match t with
              StructuralTypes.InferedType.Record(Some _, [{Name = ""; Type = StructuralTypes.InferedType.Primitive(_)}],_) ->
                  ()
              | StructuralTypes.InferedType.Record(Some qn, _,_)  ->
                //For each top level type create a method to parse that type
                let n = match qn.Split('}') with 
                        | [|n|] | [|_;n|] -> NameUtils.nicePascalName  n
                        | _ -> failwithf "can't parse name %s" qn
                let res = providedType.GetMember(n) 
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
          
        providedType
    

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("SchemaFile", typeof<string>)
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")]

  let helpText = 
    """<summary>Typed representation of a XML file based on a xml schema definition (XSD)</summary>
       <param name='SchemaFile'>Location of a XSD file or a string containing the XSD</param>                    
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do xmlProvTy.AddXmlDoc helpText
  do xmlProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ xmlProvTy ])