namespace ProviderImplementation
open System.Xml.Schema
open System
open System.IO
open System.Collections.Generic
open System.Xml.Schema
open Microsoft.FSharp.Quotations
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open ProviderImplementation.JsonInference
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder
open System.Linq

//This and the following SIMple and complex are simple wrappers
//for the types from System.Xml.SChema namespace that adds a little stricter typing
//an only exposes the properties required for the provider
type internal SchemaType(this : System.Xml.Schema.XmlSchemaType) = 
   let uniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName
   let getParentElement node =
       let rec _inner (node:System.Xml.Schema.XmlSchemaObject) = 
           match node.Parent with
           :? XmlSchemaElement as p -> p :> XmlSchemaAnnotated
           | :? XmlSchemaAttribute as a -> a :> XmlSchemaAnnotated
           | null 
           | :? XmlSchema ->  failwithf "Couldn't find a parent for %A" node
           |_ as p -> _inner p 
       _inner node

   member private x._typeName = 
       new Lazy<string>( fun () ->
            match this.QualifiedName with
            name when name = null || String.IsNullOrWhiteSpace(name.Name) ->
                if String.IsNullOrEmpty(this.Name) then
                  let p = this |> getParentElement
                  match p with
                  :? XmlSchemaElement as p ->
                      p.Name + "Type" |> uniqueNiceName
                  | :? XmlSchemaAttribute as a ->
                      a.Name + "Type" |> uniqueNiceName
                  | _ -> failwith "Expected an element or an attribute"
                else 
                   this.Name
            | name -> name.ToString())

   static member Create(this : System.Xml.Schema.XmlSchemaType) =
        match this with
        :? XmlSchemaSimpleType as t -> new simpleType(t) :> SchemaType
        | :? XmlSchemaComplexType as t -> new complexType(t) :> SchemaType
        | _ -> failwith "unknown type definition"

   member x.Schema 
       with get() =
            let rec _inner (node:System.Xml.Schema.XmlSchemaObject) = 
                match node.Parent with
                | :? XmlSchema as s ->  s
                |_ as p -> _inner p 
            _inner this

   member x.Name 
       with get() = x._typeName.Value
            

and internal complexType(this : System.Xml.Schema.XmlSchemaComplexType) =
   inherit SchemaType(this)
   member x.Elements 
       with get() = 
            match this.Particle with
            :? XmlSchemaGroupBase as group-> 
                 let elements = group.Items
                 [for e in elements 
                      do 
                         if e :? XmlSchemaElement then
                            let el = e:?> XmlSchemaElement
                            if el.Name |> String.IsNullOrWhiteSpace |> not then
                                yield el]
            | _ -> []
   member x.Attributes
       with get() =
           [ for a in this.Attributes
               do 
                  if a :? XmlSchemaAttribute then
                     yield a :?> XmlSchemaAttribute]

   member x.IsChoice with get() = this.Particle :? XmlSchemaChoice 

and internal simpleType(this : System.Xml.Schema.XmlSchemaSimpleType) =
   inherit SchemaType(this)
   
   member x.Restrictions 
       with get () = 
            match this.Content with
            :? XmlSchemaSimpleTypeRestriction as content -> 
                  match content.Facets with
                  null -> []
                  | content ->
                      [for restriction in content
                           do yield restriction]
            | _ -> failwithf "simple type definition not supported (%d,%d)" this.LineNumber this.LinePosition
             
   member x.BaseTypeName 
       with get() = 
            match this.BaseXmlSchemaType with
            null ->
                match this.Content with
                :? XmlSchemaSimpleTypeRestriction as restriction -> 
                   restriction.BaseTypeName
                | :? XmlSchemaSimpleTypeUnion as extension ->
                    match [for t in extension.BaseTypes do yield t] with
                    [t] -> (t :?> XmlSchemaType).QualifiedName
                    | [] -> failwith "could not determine type"
                    | x::xs -> failwith "Multiple base types not supported"
                | _ -> failwith "Type could not be determine"
            | t -> t.QualifiedName

   

and internal Schema(this:System.Xml.Schema.XmlSchema) =
    static member Read path = 
             use reader = new StreamReader(File.OpenRead(path))
             new Schema(System.Xml.Schema.XmlSchema.Read(reader, (fun o (e:ValidationEventArgs) -> failwith e.Message)))

    static member Namespace = "http://www.w3.org/2001/XMLSchema"
    static member NativeTypes 
       with get() = [("string"      , typeof<string>);
                     ("anyURI"      , typeof<string>);
                     ("base64string", typeof<string>);
                     ("hexBinary"   , typeof<string>);
                     ("integer"     , typeof<int>);
                     ("int"         , typeof<int>);
                     ("byte"        , typeof<System.Int16>);
                     ("double"      , typeof<System.Double>);
                     ("decimal"     , typeof<System.Decimal>);
                     ("float"       , typeof<System.Single>);
                     ("dateTime"    , typeof<DateTime>);
                     ("time"        , typeof<DateTime>);
                     ("date"        , typeof<DateTime>);
                     ("duration"    , typeof<TimeSpan>);
                     ("boolean"     , typeof<bool>);
                     ] 
    
    member private x.Read (external:XmlSchemaExternal) =
        try
            let root = Path.GetDirectoryName(this.SourceUri)
            let path = Path.Combine(root,external.SchemaLocation)    
            Schema.Read path
        with e ->
            failwith (this.SourceUri)

    member x.Types 
        with get() = 
             if this = null then failwith "The schema can't be null"
             let types =
               match this.Items with
               null -> []
               | items ->
                 [for t in items 
                     do 
                       if t :? XmlSchemaType then yield SchemaType.Create(t :?> XmlSchemaType)
                 ]
             match this.Includes with
             null -> types
             | includes -> 
               types@[for i in includes 
                          do
                            match i with
                            :? XmlSchemaInclude as incl ->
                                  for t in (incl |> x.Read).Types do yield t
                            | _ -> ()]

    member x.ImportedTypes 
        with get() = 
             [for imported  in this.Includes
                 do
                    match imported with
                    :? XmlSchemaImport as imported ->
                         for t in (imported |> x.Read).Types do yield t
                    | _ -> ()]

module XsdBuilder = 
  let generateType (schema:System.Xml.Schema.XmlSchema) includeMetadata  =
    let schema = Schema(schema)
    let _types = new System.Collections.Generic.Dictionary<string, InferedType>()

    //add XSD types
    for (name,t) 
      in Schema.NativeTypes
      do
         let p = InferedType.Record(Some "", [{Name ="";
                                               Type = InferedType.Primitive(t,None,false)}],false)
                                
         _types.Add(name,p)
         let qualified = Schema.Namespace + ":" + name
         _types.Add(qualified.ToString(),p)
         
    //this implementation does not work across namespaces
    let findType (name:string) =
        match schema.Types |> List.filter (fun t-> 
           match name.Contains ":" with
           true -> t.Name = name || ((name.Split(':').Last() = t.Name) && t.Name.Contains(":") |> not)
           | false -> t.Name.Split(':').Last() = name) with
        [t] -> t
        | [] -> failwithf "Type '%s' not found %A" name _types
        | _ -> failwithf "multiple types with the name '%s' found" name

    let rec getType typeName =
        if String.IsNullOrWhiteSpace(typeName) then
            None
        else 
            match _types.TryGetValue(typeName) with
            true,t -> 
               Some(t)
            | false,_ -> 
                //might violate namespaces
                match (_types.Keys |> Seq.filter (fun key -> key.Split(':').Last() = typeName)).SingleOrDefault() with
                null -> 
                        //If it's a XSD data type but not found default to string
                        if typeName.IndexOf(Schema.Namespace,StringComparison.InvariantCultureIgnoreCase) >= 0 || (typeName.Contains(":") |> not) then
                           getType (Schema.Namespace + ":string")
                        else
                           None
                | key -> Some(_types.[key])

    let rec createType (typeDeclaration:SchemaType) =
      let n = typeDeclaration.Name

      let getTypeFromAnnotated (el:XmlSchemaAnnotated) =
         let schemaTypeName,schemaType = 
              match el with
              :? XmlSchemaElement as e -> e.SchemaTypeName,e.SchemaType
              | :? XmlSchemaAttribute as e -> e.SchemaTypeName,e.SchemaType :> XmlSchemaType
              |_ -> failwithf "Expected an element or an attribute but got %A" el
         let typeName =  
             schemaTypeName.ToString()
         match typeName |> getType with
         None -> 
            if schemaType <> null then
               //The element/attribute has an anonymous type
               schemaType 
               |> SchemaType.Create 
               |> createType
            else
               let typeName = schemaTypeName.ToString()
               //The SchemaType has not been visited yet, so 
               //Find the type element in the schema and create the type
               typeName |> findType |> createType
         | Some(t) -> t

      if _types.ContainsKey (n) then _types.[n]
      else
          let t = 
               match typeDeclaration with
                 _ when _types.ContainsKey (n) -> _types.[n]
                 | :? simpleType as simple -> 
                      let typeName = simple.BaseTypeName.ToString()
                      let t = 
                          let t =
                              match typeName |> getType with
                              None ->
                                 typeName |> findType |> createType
                              | Some(t) -> t
                          match t with
                          InferedType.Primitive(_) -> t
                          | InferedType.Record(Some _,[{Name = "";Type = t}], _) -> t
                          | _ -> failwithf "Can't use %A for a simple type" t
                      InferedType.Record(
                          Some n,
                            [{Name = "";
                              Type =  t}],false)
                 | :? complexType as typeDeclaration -> 
                      let isChoice = typeDeclaration.IsChoice
                      let elements = 
                         typeDeclaration.Elements 
                         |> List.map (fun el -> 
                                       let multiplicity =
                                          match el.MaxOccurs,el.MinOccurs with
                                          1m,0m -> InferedMultiplicity.OptionalSingle
                                          | 1m,_ -> if isChoice then
                                                       //choice elements are always optional
                                                       InferedMultiplicity.OptionalSingle
                                                    else
                                                       InferedMultiplicity.Single
                                          | _,_ -> InferedMultiplicity.Multiple
                                       let elemType =  
                                             match el |> getTypeFromAnnotated  with
                                             InferedType.Record(Some "", p,o) ->
                                                InferedType.Record(Some el.Name,p,o)
                                             | _ as t -> t
                                       (InferedTypeTag.Record (Some el.Name),(multiplicity, elemType)))
                      let elements =
                         if includeMetadata then 
                             let name = "TargetNamespace"
                             (InferedTypeTag.Record(Some name),(InferedMultiplicity.Single,InferedType.Constant(name,typeof<string>,typeDeclaration.Schema.TargetNamespace)))::elements
                         else
                              elements
                      let attributes = 
                            typeDeclaration.Attributes 
                            |> List.map (fun a -> 
                                 let t = 
                                     match a |> getTypeFromAnnotated with
                                     InferedType.Record
                                         (Some _,
                                          [{Name = _;
                                            Type = pt}], _) -> 
                                                match pt with
                                                InferedType.Primitive(t,u,_) -> InferedType.Primitive(t,u,a.Use = XmlSchemaUse.Optional)
                                                | _ -> failwithf "Primitive type expected for attribute %A" pt

                                     | _ as t -> failwithf "Unexpected type %A " t
                                 {Name = a.Name;
                                  Type = t})

                      InferedType.Record(
                          Some n,
                            {Name = "";
                              Type = InferedType.Collection(elements |> Map.ofList)}::attributes,false)
                      
                 | _ -> failwith "unknown type definition %s" (typeDeclaration.ToString())
          try
            _types.Add(n,t)
          with e ->
            match e with
            :? ArgumentException -> failwithf "duplication of type '%s'" n
            | _ -> raise e
          t

    //Make all the imported types available.
    //They will all be added to _types but only those used will be provided
    schema.ImportedTypes |>
    List.iter (fun t -> _types.Add(t.Name,createType t)) 

    //define all the types of the schema
    schema.Types 
    |> List.map createType