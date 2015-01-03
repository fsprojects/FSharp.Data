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

module debug = 
    let print = System.Diagnostics.Debug.Print
//This and the following SIMple and complex are simple wrappers
//for the types from System.Xml.SChema namespace that adds a little stricter typing
//an only exposes the properties required for the provider
type internal SchemaType(this : System.Xml.Schema.XmlSchemaType, failOnUnsupported : bool) =
      
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
   member internal x.failf msg args def = 
       let msg = sprintf msg args
       x.fail msg def
              
   member internal x.fail msg def = 
       if failOnUnsupported then
              failwith msg 
       else 
            debug.print msg
       def
   member private x._typeName = 
       new Lazy<string>( fun () ->
            match this.QualifiedName with
            name when name = null || String.IsNullOrWhiteSpace(name.Name) ->
                if String.IsNullOrEmpty(this.Name) then
                  let p = this |> getParentElement
                  match p with
                  :? XmlSchemaElement as p ->
                      p.Name |> uniqueNiceName
                  | :? XmlSchemaAttribute as a ->
                      a.Name |> uniqueNiceName
                  | _ -> failwith "Expected an element or an attribute"
                else 
                   this.Name
            | name -> name.ToString())

   static member Create(this : System.Xml.Schema.XmlSchemaType, failOnUnsupported:bool) =
        match this with
        null -> failwith "Can't create anything from null" 
        | :? XmlSchemaSimpleType as t -> new simpleType(t, failOnUnsupported) :> SchemaType
        | :? XmlSchemaComplexType as t -> new complexType(t,failOnUnsupported) :> SchemaType
        | _ as typeDeclaration -> failwithf "Can't create type. Unknown type definition %s %s" (typeDeclaration.Name.ToString()) (typeDeclaration.GetType().Name)

   member x.Schema 
       with get() =
            let rec _inner (node:System.Xml.Schema.XmlSchemaObject) = 
                match node.Parent with
                | :? XmlSchema as s ->  s
                |_ as p -> _inner p 
            _inner this

   member x.Name 
       with get() = x._typeName.Value
   member internal x.FindType = Schema(x.Schema, failOnUnsupported).FindType

and internal complexType(this : System.Xml.Schema.XmlSchemaComplexType, failOnUnsupported :bool) =
   inherit SchemaType(this, failOnUnsupported)
   let fromAttributesCollection (collection:XmlSchemaObjectCollection) = 
               [ for a in collection
                   do 
                      if a :? XmlSchemaAttribute then
                         yield a :?> XmlSchemaAttribute]

   let elementsFromParticle (particle:XmlSchemaParticle) =
                          match particle with
                          :? XmlSchemaGroupBase as group-> 
                               let elements = group.Items
                               [for e in elements 
                                    do 
                                       if e :? XmlSchemaElement then
                                          let el = e:?> XmlSchemaElement
                                          if el.Name |> String.IsNullOrWhiteSpace |> not then
                                              yield el]
                          | _ -> []
   member private x.contentElements mapper =
         match this.ContentModel with
         | null -> []
         | :? XmlSchemaComplexContent
         | :? XmlSchemaSimpleContent ->
             match this.ContentModel.Content with
             | null -> []
             | :? XmlSchemaSimpleContentExtension ->
                 x.fail "ContentExtensions not supported" []
             | :? XmlSchemaComplexContentRestriction as restriction-> 
                 (restriction.Particle,restriction.Attributes,restriction.BaseTypeName.ToString()) |> mapper
             | :? XmlSchemaComplexContentExtension as extension ->
                 (extension.Particle,extension.Attributes,extension.BaseTypeName.ToString()) |> mapper
             | _ as content -> x.failf "Unexpected content type %A" content [] 
         | _ -> x.failf "Unsupported content model %A" this.ContentModel []

   member  x.Elements 
       with get() : XmlSchemaElement list = 
            let fromComplexContent (particle,_,baseTypeName) =
                let t:SchemaType = baseTypeName |> x.FindType
                let fromExtension = elementsFromParticle particle
                match t with
                :? complexType as t -> 
                   let inheritedElements = 
                         //Only use the most recent definition of an element
                          t.Elements       
                          |> List.filter( fun e -> fromExtension.Any(fun c -> c.Name = e.Name) |> not)
                   inheritedElements@fromExtension
                | _ -> x.fail "can't extend a simple type" fromExtension

            let elements = elementsFromParticle this.Particle
            (x.contentElements fromComplexContent)@(elements)

   member x.Attributes
       with get() : XmlSchemaAttribute list =
           let attributes = fromAttributesCollection this.Attributes
           let fromComplexContent (_,attributes,baseName) =
                      let t:SchemaType = baseName |> x.FindType
                      let fromExtension =  fromAttributesCollection attributes
                      match t with
                      :? complexType as t -> 
                         let inheritedElements = 
                               //Only use the most recent definition of an element
                                t.Attributes       
                                |> List.filter( fun e -> fromExtension.Any(fun c -> c.Name = e.Name) |> not)
                         inheritedElements@fromExtension
                      | _ -> x.fail "can't extend a simple type" fromExtension

           ((x.contentElements fromComplexContent))@(attributes)

   member x.IsChoice with get() = this.Particle :? XmlSchemaChoice 

and internal simpleType(this : System.Xml.Schema.XmlSchemaSimpleType, failOnUnsupported:bool) =
   inherit SchemaType(this,  failOnUnsupported )
   
   member x.Restrictions 
       with get () = 
            match this.Content with
            :? XmlSchemaSimpleTypeRestriction as content -> 
                  match content.Facets with
                  null -> []
                  | content ->
                      [for restriction in content
                           do yield restriction]
            | _ -> x.failf "simple type definition not supported (%A)" (this.LineNumber,this.LinePosition) []
             
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
                    | t::_ -> x.fail "Multiple base types not supported" (t :?> XmlSchemaType).QualifiedName
                | _ -> failwith "Type could not be determine"
            | t -> t.QualifiedName

and internal Schema(this:System.Xml.Schema.XmlSchema, failOnUnsupported : bool) =
    static member Read (un,path)  = 
             use reader = new StreamReader(File.OpenRead(path))
             new Schema(System.Xml.Schema.XmlSchema.Read(reader, (fun o (e:ValidationEventArgs) -> failwith e.Message)),un)

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
    
    member x.TargetNamespace = this.TargetNamespace
    member x.HasTargetNamespace = String.IsNullOrEmpty(x.TargetNamespace) |> not

    member private x.Read (external:XmlSchemaExternal) =
        try
            let root = Path.GetDirectoryName(this.SourceUri)
            let path = Path.Combine(root,external.SchemaLocation)    
            Schema.Read (failOnUnsupported,path)
        with e ->
            failwith (this.SourceUri)

    member internal x.FindType (name:string) =
        //this implementation does not work across namespaces
        match x.Types |> List.filter (fun t-> 
                                       match name.Contains ":" with
                                       true -> t.Name = name || ((name.Split(':').Last() = t.Name) && t.Name.Contains(":") |> not)
                                       | false -> t.Name.Split(':').Last() = name) with
        [t] -> t
        | [] -> failwithf "Type '%s' not found %A" name x.Types
        | _ -> failwithf "multiple types with the name '%s' found" name
    member X.Elements
        with get() =
           [for i in this.Items do if i :? XmlSchemaElement then yield i :?> XmlSchemaElement]
    member x.Types
        with get() : SchemaType list = 
             if this = null then failwith "The schema can't be null"
             let types =
               match this.Items with
               null -> []
               | items ->
                 [for t in items 
                     do 
                       if t :? XmlSchemaType then yield SchemaType.Create(t :?> XmlSchemaType, failOnUnsupported)
                 ]
             match this.Includes with
             null -> types
             | includes -> 
               types@[for i in includes 
                          do
                            match i with
                            :? XmlSchemaInclude as incl ->
                                  for t in x.Read(incl).Types do yield t
                            | _ -> ()]

    member x.ImportedTypes 
        with get() = 
             [for imported  in this.Includes
                 do
                    match imported with
                    :? XmlSchemaImport as imported ->
                         for t in x.Read(imported).Types do yield t
                    | _ -> ()]

module XsdBuilder = 
  let generateType (schema:System.Xml.Schema.XmlSchema) includeMetadata failOnUnsupported =
    let elementFormIsQualified (element: XmlSchemaElement) = 
        match element.Form, schema.ElementFormDefault with 
        | XmlSchemaForm.None, XmlSchemaForm.Qualified -> true
        | XmlSchemaForm.Qualified, _ -> true
        | _ -> false
        
    let schema = Schema(schema, failOnUnsupported)
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
         
    let findType = schema.FindType
    let rec getTypeFromAnnotated (el:XmlSchemaAnnotated) =
         let schemaTypeName,schemaType = 
              match el with
              :? XmlSchemaElement as e -> 
                   e.SchemaTypeName,e.SchemaType
              | :? XmlSchemaAttribute as e -> e.SchemaTypeName,e.SchemaType :> XmlSchemaType
              |_ -> failwithf "Expected an element or an attribute but got %A" el
         let typeName =  
             schemaTypeName.ToString()
         let t = 
             typeName |> getType 
         
         match t,schemaType with
         None, null ->
             //This can happen if it's a restriction that doesn't change the type
             match el.Parent with
             :? XmlSchemaSimpleContentRestriction
             | :? XmlSchemaComplexContentRestriction ->
                  None 
             | _ -> 
                 failwithf "Couldn't find type %s. node was %A and parent %A" typeName el (el.Parent)
         | None, _ -> 
             //The element/attribute has an anonymous type 
             SchemaType.Create(schemaType, failOnUnsupported)
             |> createType |> Some
         | Some(t), _ -> Some(t)

    and createElements (elements:XmlSchemaElement list) = 
           elements
           |> List.map (fun el ->
                         let isChoice = el.Parent :? XmlSchemaChoice
                         let multiplicity =
                             match el.MaxOccurs,el.MinOccurs with
                             1m,0m -> InferedMultiplicity.OptionalSingle
                             | 1m,_ -> if isChoice then
                                          //choice elements are always optional
                                          InferedMultiplicity.OptionalSingle
                                       else
                                          InferedMultiplicity.Single
                             | _,_ -> InferedMultiplicity.Multiple
                         multiplicity,el |> getTypeFromAnnotated,el.Name, elementFormIsQualified el)
           |> List.filter(fun (_,t,_,_) -> t.IsSome)
           |> List.map(fun (multiplicity,t,name,qualifiedForm) ->
                         let elemType =  
                               match t with
                               Some(InferedType.Record(Some "", p,o)) ->
                                      InferedType.Record(Some name,p,o)
                               | Some(t) -> t
                               | _ -> failwith "Filter failed"
                          // TODO top-level elements vs nested
                         let nameWithNs = if qualifiedForm && schema.HasTargetNamespace then sprintf "{%s}%s" schema.TargetNamespace name else name
                         (InferedTypeTag.Record (Some nameWithNs),(multiplicity, elemType)))

    and qualifySchemaType typeName =if schema.HasTargetNamespace then schema.TargetNamespace + ":" + typeName else typeName
               
    and getType typeName =
        if String.IsNullOrWhiteSpace(typeName) then
            None
        else 
            //lookup already build types and XSD native types
            match _types.TryGetValue(typeName) with
            | true,t -> Some(t)
            | false,_ -> 
                //It's not native and it's not created already
                match schema.Types |> List.filter (fun t -> qualifySchemaType t.Name = typeName) with
                //we found one matching without namespaec
                t::_ -> Some(t |> createType)
                | _  when typeName.IndexOf(Schema.Namespace,StringComparison.InvariantCultureIgnoreCase) >= 0 -> 
                    //we've already searched for build in types so default to string
                    getType (Schema.Namespace + ":string")
                | _ -> failwithf "Unknown type %s %A" typeName (schema.Types |> List.map(fun e -> qualifySchemaType e.Name))             
                
    and createType (typeDeclaration:SchemaType) =
      let n = typeDeclaration.Name

      if _types.ContainsKey (qualifySchemaType n) then _types.[qualifySchemaType n]
      else
          let t = 
               match typeDeclaration with
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
                      let elements = 
                         typeDeclaration.Elements 
                         |> createElements
                         
                      let elements =
                         if includeMetadata then 
                             let createConstant (name,value) = 
                                  (InferedTypeTag.Record (Some name),
                                              (InferedMultiplicity.Single,
                                               InferedType.Record
                                                 (Some name,
                                                  [{Name = "";
                                                    Type = InferedType.Constant(name,typeof<string>,value)}],
                                                  false)))

                             createConstant("TargetNamespace",typeDeclaration.Schema.TargetNamespace)
                             ::createConstant("TypeName", typeDeclaration.Name)
                             ::elements
                         else
                             elements
                      let attributes = 
                            typeDeclaration.Attributes 
                            |> List.map( fun a ->
                                          a,a |> getTypeFromAnnotated)
                            |> List.filter ( fun (_,t) -> t.IsSome)
                            |> List.map (fun (a,t) -> 
                                 let t = 
                                     match t with
                                     Some(InferedType.Record
                                              (Some _,
                                               [{Name = _;
                                                 Type = pt}], _)) -> 
                                                     match pt with
                                                     InferedType.Primitive(t,u,_) -> InferedType.Primitive(t,u,a.Use = XmlSchemaUse.Optional)
                                                     | _ -> failwithf "Primitive type expected for attribute %A" pt      
                                     | _ as t -> failwithf "Unexpected type %A " t
                                 {Name = a.Name;
                                  Type = t})

                      InferedType.Record(
                          Some n,
                            {Name = "";
                              Type = InferedType.Collection(elements |> List.map fst, elements |> Map.ofList)}::attributes,false)
                      
                 | _ -> failwithf "unknown type definition %s %s" (typeDeclaration.Name.ToString()) (typeDeclaration.GetType().Name)
          try
            _types.Add(qualifySchemaType n,t)
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
    let types = schema.Types 
                |> List.map createType
    //The can be zero or many elements defined as well as types
    //We'll essentially treat each as a type
    let elements = schema.Elements
                   |> createElements
                   |> List.zip schema.Elements
                   //For the free elements we want them to be named after the element and not have the type name only
                   //This is essentially to have the xtra Parse methods named after the elements
                   //since the implementation relies on this
                   |> List.map (fun (e,(_,(_,typ)) as t) -> 
                                  match typ with
                                  InferedType.Record(Some _, p, o) ->
                                      InferedType.Record(Some (e.Name),p,o)
                                  | _ -> failwithf "Elements must be represented with a record %A" t)
    types@elements
    