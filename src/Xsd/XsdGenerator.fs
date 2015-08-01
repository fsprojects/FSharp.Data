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
//This and the following Simple and complex are simple wrappers
//for the types from System.Xml.SChema namespace that adds a little stricter typing
//an only exposes the properties required for the provider
type internal SchemaType(xmlSchemaType : System.Xml.Schema.XmlSchemaType, set:Schema, failOnUnsupported : bool) =
      
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
            match xmlSchemaType.QualifiedName with
            name when name = null || String.IsNullOrWhiteSpace(name.Name) ->
                if String.IsNullOrEmpty(xmlSchemaType.Name) then
                  let p = xmlSchemaType |> getParentElement
                  match p with
                  :? XmlSchemaElement as p ->
                      p.Name |> uniqueNiceName
                  | :? XmlSchemaAttribute as a ->
                      a.Name |> uniqueNiceName
                  | _ -> failwith "Expected an element or an attribute"
                else 
                   xmlSchemaType.Name
            | name -> sprintf "{%s}%s" name.Namespace name.Name)

   static member Create(xmlSchemaType : System.Xml.Schema.XmlSchemaType, set:Schema, failOnUnsupported:bool) =
        match xmlSchemaType with
        null -> failwith "Can't create anything from null" 
        | :? XmlSchemaSimpleType as t -> new simpleType(t, set, failOnUnsupported) :> SchemaType
        | :? XmlSchemaComplexType as t -> new complexType(t,set, failOnUnsupported) :> SchemaType
        | _ as typeDeclaration -> failwithf "Can't create type. Unknown type definition %s %s" (typeDeclaration.Name.ToString()) (typeDeclaration.GetType().Name)

   member x.Schema 
       with get() =
            let rec _inner (node:System.Xml.Schema.XmlSchemaObject) = 
                match node.Parent with
                | :? XmlSchema as s ->  s
                |_ as p -> _inner p 
            _inner xmlSchemaType

   member x.Name 
       with get() = x._typeName.Value
   member internal x.FindType = set.FindType

and internal complexType(xmlSchemaType : System.Xml.Schema.XmlSchemaComplexType, set : Schema, failOnUnsupported :bool) =
   inherit SchemaType(xmlSchemaType, set, failOnUnsupported)
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
         match xmlSchemaType.ContentModel with
         | null -> []
         | :? XmlSchemaComplexContent
         | :? XmlSchemaSimpleContent ->
             match xmlSchemaType.ContentModel.Content with
             | null -> []
             | :? XmlSchemaSimpleContentExtension ->
                 x.fail "ContentExtensions not supported" []
             | :? XmlSchemaComplexContentRestriction as restriction-> 
                 (restriction.Particle,restriction.Attributes,restriction.BaseTypeName.ToString()) |> mapper
             | :? XmlSchemaComplexContentExtension as extension ->
                 (extension.Particle,extension.Attributes,extension.BaseTypeName.ToString()) |> mapper
             | _ as content -> x.failf "Unexpected content type %A" content [] 
         | _ -> x.failf "Unsupported content model %A" xmlSchemaType.ContentModel []

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

            let elements = elementsFromParticle xmlSchemaType.Particle
            (x.contentElements fromComplexContent)@(elements)

   member x.Attributes
       with get() : XmlSchemaAttribute list =
           let attributes = fromAttributesCollection xmlSchemaType.Attributes
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

   member x.IsChoice with get() = xmlSchemaType.Particle :? XmlSchemaChoice 

and internal simpleType(xmlSchemaType : System.Xml.Schema.XmlSchemaSimpleType, set:Schema, failOnUnsupported:bool) =
   inherit SchemaType(xmlSchemaType,  set, failOnUnsupported )
   
   member x.Restrictions 
       with get () = 
            match xmlSchemaType.Content with
            :? XmlSchemaSimpleTypeRestriction as content -> 
                  match content.Facets with
                  null -> []
                  | content ->
                      [for restriction in content
                           do yield restriction]
            | _ -> x.failf "simple type definition not supported (%A)" (xmlSchemaType.LineNumber,xmlSchemaType.LinePosition) []
             
   member x.BaseTypeName 
       with get() = 
            match xmlSchemaType.BaseXmlSchemaType with
            null ->
                match xmlSchemaType.Content with
                :? XmlSchemaSimpleTypeRestriction as restriction -> 
                   restriction.BaseTypeName
                | :? XmlSchemaSimpleTypeUnion as extension ->
                    match [for t in extension.BaseTypes do yield t] with
                    [t] -> (t :?> XmlSchemaType).QualifiedName
                    | [] -> failwith "could not determine type"
                    | t::_ -> x.fail "Multiple base types not supported" (t :?> XmlSchemaType).QualifiedName
                | _ -> failwith "Type could not be determine"
            | t -> t.QualifiedName

and internal Schema(xmlSchemaSet:System.Xml.Schema.XmlSchemaSet, failOnUnsupported : bool) =
    static member Read (un,path)  = 
             use reader = new StreamReader(File.OpenRead(path))
             let s = new System.Xml.Schema.XmlSchemaSet()
             s.Add(System.Xml.Schema.XmlSchema.Read(reader, (fun o (e:ValidationEventArgs) -> failwith e.Message))) |> ignore
             new Schema(s ,un)

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
                     ("{http://www.w3.org/2001/XMLSchema}string"      , typeof<string>);
                     ("{http://www.w3.org/2001/XMLSchema}anyURI"      , typeof<string>);
                     ("{http://www.w3.org/2001/XMLSchema}base64string", typeof<string>);
                     ("{http://www.w3.org/2001/XMLSchema}hexBinary"   , typeof<string>);
                     ("{http://www.w3.org/2001/XMLSchema}integer"     , typeof<int>);
                     ("{http://www.w3.org/2001/XMLSchema}int"         , typeof<int>);
                     ("{http://www.w3.org/2001/XMLSchema}byte"        , typeof<System.Int16>);
                     ("{http://www.w3.org/2001/XMLSchema}double"      , typeof<System.Double>);
                     ("{http://www.w3.org/2001/XMLSchema}decimal"     , typeof<System.Decimal>);
                     ("{http://www.w3.org/2001/XMLSchema}float"       , typeof<System.Single>);
                     ("{http://www.w3.org/2001/XMLSchema}dateTime"    , typeof<DateTime>);
                     ("{http://www.w3.org/2001/XMLSchema}time"        , typeof<DateTime>);
                     ("{http://www.w3.org/2001/XMLSchema}date"        , typeof<DateTime>);
                     ("{http://www.w3.org/2001/XMLSchema}duration"    , typeof<TimeSpan>);
                     ("{http://www.w3.org/2001/XMLSchema}boolean"     , typeof<bool>);
                     ] 

    member internal x.FindType (name:string) =
        //xmlSchemaType implementation does not work across namespaces
        match x.Types |> List.filter (fun t-> 
                                       match name.Contains ":" with
                                       true -> t.Name = name || ((name.Split(':').Last() = t.Name) && t.Name.Contains(":") |> not)
                                       | false -> t.Name.Split(':').Last() = name) with
        [t] -> t
        | [] -> failwithf "Type '%s' not found %A" name x.Types
        | _ -> failwithf "multiple types with the name '%s' found" name
    member private x.Items
        with get() =
           [for sch in xmlSchemaSet.Schemas() do
               if sch:? XmlSchema then
                   let sch = sch :?> XmlSchema
                   for i in sch.Items do yield i]

    member x.Elements
        with get() =
           [for i in x.Items do if i :? XmlSchemaElement then yield i :?> XmlSchemaElement]
   

    member x.Types
        with get() : SchemaType list = 
             if xmlSchemaSet = null then failwith "The schema can't be null"
             [for t in x.Items 
                   do 
                     if t :? XmlSchemaType then yield SchemaType.Create(t :?> XmlSchemaType, x, failOnUnsupported)
               ]

module XsdBuilder = 
  let generateType (schema:System.Xml.Schema.XmlSchemaSet) includeMetadata failOnUnsupported =
    schema.Compile()
    let schema = Schema(schema, failOnUnsupported)
    let _types = new System.Collections.Generic.Dictionary<string, InferedType>()

    //add XSD types
    for (name,t) 
      in Schema.NativeTypes
      do
         let p = InferedType.Record(Some "", [{Name ="";
                                               Type = InferedType.Primitive(t,None,false)}],false)
                                
         _types.Add(name,p)
         
    let findType = schema.FindType
    let rec getTypeFromAnnotated (el:XmlSchemaAnnotated) =
         let schemaTypeName,schemaType = 
              match el with
              :? XmlSchemaElement as e -> 
                   e.SchemaTypeName,e.SchemaType
              | :? XmlSchemaAttribute as e -> e.SchemaTypeName,e.SchemaType :> XmlSchemaType
              |_ -> failwithf "Expected an element or an attribute but got %A" el 

         if schemaTypeName.Name = "" then
                 match schemaType with
                 :? XmlSchemaSimpleType as simpleT ->
                         SchemaType.Create(simpleT, schema, failOnUnsupported)
                         |> createType
                         |> Some
                 | :? XmlSchemaComplexType as complexT ->
                         SchemaType.Create(complexT, schema, failOnUnsupported)
                         |> createType
                         |> Some
                 | _ ->
                     if failOnUnsupported then
                          failwithf "Couldn't find type for %A" el
                     else
                         None
         else
            let t = sprintf "{%s}%s" (schemaTypeName.Namespace) (schemaTypeName.Name)
                    |> getType
         
            match t, schemaType with
            None, null ->
                //xmlSchemaType can happen if it's a restriction that doesn't change the type
                match el.Parent with
                :? XmlSchemaSimpleContentRestriction
                | :? XmlSchemaComplexContentRestriction ->
                     None 
                | _ -> 
                    failwithf "Couldn't find type. node was %A and parent %A"  el (el.Parent)
            | None, _ -> 
                //The element/attribute has an anonymous type 
                SchemaType.Create(schemaType,schema, failOnUnsupported)
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
                         multiplicity,el |> getTypeFromAnnotated,el.Name)
           |> List.filter(fun (_,t,_) -> t.IsSome)
           |> List.map(fun (multiplicity,t,name) ->
                         let elemType =  
                               match t with
                               Some(InferedType.Record(Some "", p,o)) ->
                                      InferedType.Record(Some name,p,o)
                               | Some(t) -> t
                               | _ -> failwith "Filter failed"
                         (InferedTypeTag.Record (Some name),(multiplicity, elemType)))
           
    and getType typeName =
        if String.IsNullOrWhiteSpace(typeName) then
            None
        else 
            let filter (t:SchemaType) = t.Name = typeName
            match _types.TryGetValue(typeName) with
            true,t -> 
               Some(t)
            | false,_ -> 
                //might violate namespaces
                //lookup already build types and XSD native types
                match _types.TryGetValue typeName with
                false,_ -> 
                    //It's not native and it's not created already
                    let typeDecl = schema.Types |> List.filter filter

                    match typeDecl with
                    //we found one matching without namespace
                    t::_ -> Some(t |> createType)
                    | [] ->
                        match schema.Types |> List.filter filter with
                        t::_ -> Some(t |> createType)
                        | [] -> 
                           if failOnUnsupported then
                               failwithf "unknown type %s" typeName
                           else
                               Some(_types.[sprintf "{%s}string" Schema.Namespace])
                | true,t -> 
                    Some(t)

    and createType (typeDeclaration:SchemaType) =
      let n = typeDeclaration.Name

      if _types.ContainsKey (n) then _types.[n]
      else
          let t = 
               match typeDeclaration with
               | :? simpleType as simple -> 
                      let typeName = sprintf "{%s}%s" (simple.BaseTypeName.Namespace) (simple.BaseTypeName.Name)
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
                      let els = 
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
                             ::els 
                             |> List.map (
                                  fun element -> 
                                    match element with
                                    (InferedTypeTag.Record(Some name),
                                         (InferedMultiplicity.Single,
                                            (InferedType.Record(Some n,
                                                                  [{Name = ""
                                                                    Type = InferedType.Collection(props)}],o)))) ->
                                       (InferedTypeTag.Record(Some name),
                                         (InferedMultiplicity.Single,
                                            (InferedType.Record(Some n,
                                                                  [{Name = ""
                                                                    Type = InferedType.Collection(props)}],o)))) 
                                    | e -> e    )
                         else
                              els
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
                              Type = InferedType.Collection(elements |> Map.ofList)}::attributes,false)
                      
               | _ -> failwithf "unknown type definition %s %s" (typeDeclaration.Name.ToString()) (typeDeclaration.GetType().Name)
          try
            _types.Add(n,t)
          with e ->
            match e with
            :? ArgumentException -> failwithf "duplication of type '%s'" n
            | _ -> raise e
          t

    //define all the types of the schema
    let types = schema.Types 
                |> List.map createType
    //The can be zero or many elements defined as well as types
    //We'll essentially treat each as a type
    let elements = schema.Elements
                   |> createElements
                   |> List.zip schema.Elements
                   //For the free elements we want them to be named after the element and not have the type name only
                   //xmlSchemaType is essentially to have the extra Parse methods named after the elements
                   //since the implementation relies on xmlSchemaType
                   |> List.map (fun (e,(_,(_,typ)) as t) -> 
                                  match typ with
                                  InferedType.Record(Some _, p, o) ->
                                      InferedType.Record(Some (e.Name.Replace(":","__")),p,o)
                                  | _ -> failwithf "Elements must be represented with a record %A" t)
    types@elements
    