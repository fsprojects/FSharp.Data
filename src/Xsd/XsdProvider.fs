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
  let asm, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let xmlProvTy = ProvidedTypeDefinition(asm, ns, "XsdProvider", Some typeof<obj>)

  let buildTypes (typeName:string) (args:obj[]) =
    try
        let types = ref  StructuralTypes.InferedType.Top
        // Generate the required type
        let tpType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
        
        let sample = args.[0] :?> string
        let resolutionFolder = args.[1] :?> string
        
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
                     let p = Path.Combine(resolutionFolder,sample)
                     if File.Exists(p) then
                        p,new StreamReader(File.OpenRead(p)) :> TextReader
                     else
                        try
                           XDocument.Parse(sample) |> ignore
                           Path.Combine(resolutionFolder, "temp.xsd"),new StringReader(sample) :> TextReader
                        with e ->
                           failwith "Could not find a file and could not interprete as valid XML either"
                        
              let schema = read reader
              schema.SourceUri <- path
              schema |> XsdBuilder.generateType |> List.fold (StructuralInference.subtypeInfered (*allowNulls*)true) StructuralTypes.Top 
            types := ts
            ts
          | _ -> !types
        
        let getTypesFromSchema (schema:string) = 
          let samples = schema |> getTypes
              
          let inferedType =
            match samples with
            StructuralTypes.InferedType.Record(_) as t -> t
            | StructuralTypes.InferedType.Heterogeneous cases as t ->
                 let t = StructuralTypes.InferedType.Collection(cases |> Map.map (fun k v -> (StructuralTypes.InferedMultiplicity.Single, v)))
                 //If there's no top level type then wrap the types in a parent, this type won't be used
                 //We'll create a parse method for each individual type that can be used
                 StructuralTypes.InferedType.Record(Some "Schema",[{Name = "";
                                                                    Optional = false;
                                                                    Type = t}])
            | _ as t -> t
        
          let ctx = XmlGenerationContext.Create(System.Globalization.CultureInfo.CurrentCulture.Name, tpType, true, replacer)  
          XmlTypeBuilder.generateXmlType ctx inferedType
        
        let resTy, resTypConv = getTypesFromSchema sample
        
        let getSpec _ = 
          { GeneratedType = tpType
            RepresentationType = resTy
            CreateFromTextReader = fun reader -> 
              resTypConv <@@ XsdSchema.Create(%reader) @@>
            CreateFromTextReaderForSampleList = fun reader -> 
              resTypConv <@@ XsdSchema.CreateList(%reader) @@> }
        
        let providedType =
            generateConstructors "XSD" sample false
                                 parseSingle parseList getSpec
                                 this cfg replacer resolutionFolder false
        
        let inferedType = getTypes sample
        
        match inferedType with
          StructuralTypes.InferedType.Heterogeneous types ->
            for (_,t) in types |> Map.toList do
              match t with
              StructuralTypes.InferedType.Record(Some n, [{Name = "";Optional = _ ; Type = StructuralTypes.InferedType.Primitive(t,o)}]) ->
                  ()
              | StructuralTypes.InferedType.Record(Some n, _) as t ->
                //For each top level type create a method to parse that type
                let n = NameUtils.nicePascalName n
                let res = providedType.GetMember(n) 
                match res with
                  [||] -> 
                      failwithf "Could not find a provided type for %s" n
                  | [|res|] when (res :? ProvidedTypeDefinition) ->
                      let res = res :?> ProvidedTypeDefinition
                      let args = [ ProvidedParameter("text", typeof<string>) ]
                      let m = ProvidedMethod("Parse" + n, args, res, IsStaticMethod = true)
                      m.InvokeCode <- fun (Singleton text) -> 
                        <@ 
                            if XDocument.Parse(%%text).Root.Name.LocalName = n.ToLower() then
                                 //wrap the XML in a new root to make the XMLRuntime pick the right child elements
                                 new StringReader(sprintf "<root__>%s</root__>" %%text) :> TextReader
                            else
                                 //Assume that the XML is already wrapped
                                 new StringReader(%%text) :> TextReader
                        @>
                        |> fun reader -> resTypConv <@@ XsdSchema.Create(%reader) @@>
                      m.AddXmlDoc <| sprintf "Parses the specified XML string as a %s" n
                      tpType.AddMember m
                  | [|res|] -> failwithf "%s is not a provided type but a " res.Name (res.GetType().Name)
                  | _ as res -> failwithf "Found several nested types (%A) with the name %s" res n
              | _ -> ()
          | _ as t -> failwithf "Did not expect %A" t
        providedType
    with e ->
       failwith (e.ToString())
    

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("SchemaFile", typeof<string>)
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") ]

  let helpText = 
    """<summary>Typed representation of a XML file based on a xml schema definition (XSD)</summary>
       <param name='SchemaFile'>Location of a XSD file or a string containing the XSD</param>                    
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do xmlProvTy.AddXmlDoc helpText
  do xmlProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ xmlProvTy ])