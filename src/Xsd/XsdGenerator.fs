﻿namespace ProviderImplementation
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

module XsdBuilder =
    type Particle = 
       Sequence of XmlSchemaSequence
       | Choice of XmlSchemaChoice
       | Empty
    type Extension = 
       Simple    of XmlSchemaSimpleContentExtension
       | Complex of XmlSchemaComplexContentExtension
    type Restriction = 
       Simple    of  XmlSchemaSimpleContentRestriction
       | Complex of XmlSchemaComplexContentRestriction
    type ComplexType =
        Extended of XmlSchemaComplexType * Extension
        | Restricted of XmlSchemaComplexType * Restriction
        | Basic of XmlSchemaComplexType
        member x.TypeDeclaration 
           with get() =
              match x with
               Extended (t,_)
               | Restricted (t,_)
               | Basic t -> t
        member x.QualifiedName  
            with get() =
               x.TypeDeclaration.QualifiedName
    type SimpleContent =
         Restriction of XmlSchemaSimpleTypeRestriction
         | Union of XmlSchemaSimpleTypeUnion
         | List of XmlSchemaSimpleTypeList
    type SimpleType =
        | Restricted of XmlSchemaSimpleType * SimpleContent
        | Basic of XmlSchemaSimpleType

         member x.TypeDeclaration 
           with get() =
              match x with
               | Restricted (t,_)
               | Basic t -> t
        member x.QualifiedName  
            with get() =
               x.TypeDeclaration.QualifiedName
                
    type XmlType = 
       Complex of ComplexType
       | Simple of SimpleType
    let qnToString (qn:System.Xml.XmlQualifiedName) =
        if String.IsNullOrWhiteSpace(qn.Namespace) then
            qn.Name
        else
            sprintf "{%s}%s" qn.Namespace qn.Name
    let (|Extension|ComplexRestriction|SimpleExtension|SimpleRestriction|NoContent|) (obj:XmlSchemaContent) =
       match obj with
       | null -> NoContent
       | :? XmlSchemaComplexContentExtension as c -> 
           Extension c
       | :? XmlSchemaComplexContentRestriction as c -> 
           ComplexRestriction c
       | :? XmlSchemaSimpleContentExtension as c -> 
           SimpleExtension c
       | :? XmlSchemaSimpleContentRestriction as c -> 
           SimpleRestriction c
       | _ -> failwith "Content unknown"
    let rec private getSchemaTypeName (t:XmlSchemaType) = 
           let name =
              match t.QualifiedName, t.Name with
              null, str when String.IsNullOrWhiteSpace str -> None
              | null, str -> Some str
              | qn, _ when String.IsNullOrWhiteSpace(qn.Namespace) -> 
                  if String.IsNullOrWhiteSpace(qn.Name) |> not then
                      qn.Name |> Some
                  else
                      None
              | qn, _ -> qn |> qnToString |> Some

           let name = 
               if name.IsNone || String.IsNullOrWhiteSpace(name.Value) then
                   match t with
                   :? XmlSchemaSimpleType as t when (t.Content :? XmlSchemaSimpleTypeRestriction) -> 
                          let restriction = t.Content :?> XmlSchemaSimpleTypeRestriction 
                          if restriction.BaseType <> null then 
                                restriction.BaseType |> getSchemaTypeName
                          else
                               restriction.BaseTypeName |> qnToString |> Some
                   | _ ->  name
                else
                    name
           assert (name.IsNone || name.Value |>  String.IsNullOrWhiteSpace |> not)
           name
    let (|Schema|Type|Element|Particle|Unknown|Attribute|) (obj:XmlSchemaObject)  =
       
       match obj with
       | :? XmlSchemaSimpleType as t -> 
           let content = 
                match t.Content with
                null -> Basic t
                | _ as model -> 
                    match model with
                    :? XmlSchemaSimpleTypeRestriction as restriction ->
                      SimpleType.Restricted(t, SimpleContent.Restriction(restriction))
                    | :? XmlSchemaSimpleTypeUnion as union -> 
                      SimpleType.Restricted(t, SimpleContent.Union(union))
                    | :? XmlSchemaSimpleTypeList as list -> 
                      SimpleType.Restricted(t, SimpleContent.List(list))
                    | _ -> failwithf "Unknkown content for simple type %A" model
           Type(Simple content)
       | :? XmlSchemaComplexType as t -> 
           let model =
              match t.ContentModel with
              null -> ComplexType.Basic t
              | _ as model-> 
                     match model.Content with
                     :? XmlSchemaComplexContentExtension as model -> 
                         ComplexType.Extended (t, Extension.Complex(model))
                     | :? XmlSchemaComplexContentRestriction as model -> 
                         ComplexType.Restricted (t, Restriction.Complex(model))
                     | :? XmlSchemaSimpleContentExtension as model -> 
                         ComplexType.Extended(t,Extension.Simple(model))
                     | :? XmlSchemaSimpleContentRestriction as model -> 
                         ComplexType.Restricted(t,Restriction.Simple(model))
                     | _ -> failwith "Invalid content model"
           Type(Complex (model))
       | :? XmlSchemaElement as e -> Element e
       | :? XmlSchemaSequence as sequence ->  Particle(Sequence(sequence))
       | :? XmlSchemaChoice as choice->  Particle(Choice(choice))
       | :? XmlSchemaSimpleTypeRestriction 
       | :? XmlSchemaComplexContentRestriction -> failwith "Restrictions not expected at this point"
       | :? XmlSchemaAttribute as attribute -> Attribute attribute
       | :? XmlSchema as schema -> Schema schema
       | _ as n -> Unknown n
    
    type ParsedSchema = {
         SchemaSet : XmlSchemaSet
         Items : XmlSchemaObject list
         Types : (string * XmlSchemaType) list
         Elements : XmlSchemaElement list
    }

    let rec private getItemsFromCollection (collection:seq<XmlSchemaObject>) = 
        [for item in collection do 
            yield item
            for i in getElementsFromObject item do
               yield i]

    and private getElementsFromObject (item:XmlSchemaObject) : XmlSchemaObject list =
        let fromParticle (particle:Particle) =
            match particle with
              Sequence sequence -> sequence.Items
              | Choice choice -> choice.Items
              | _ -> failwithf "Unsupported particle %A" particle
            |> Seq.cast<XmlSchemaObject>
        match item with
          Type(Complex t) ->
              match t with 
              ComplexType.Extended(t,ext) -> 
                 let extensions = 
                     match ext with
                        Extension.Complex(ext) -> 
                            ext.Attributes
                            |> Seq.cast<XmlSchemaObject>
                            |> Seq.append (
                                   ext.Particle 
                                   |> getElementsFromObject 
                            )
                        | Extension.Simple(ext) ->
                            ext.Attributes
                            |> Seq.cast<XmlSchemaObject>
                     |> List.ofSeq
                 match t.BaseXmlSchemaType with
                     null -> []
                     | _ as bt -> 
                         bt |> getElementsFromObject
                 |> Seq.append (match t.Particle with
                                | Particle(p) -> fromParticle p
                                | _ -> [] |> Seq.ofList)
                 |> Seq.append (t.Attributes |> Seq.cast<XmlSchemaObject>)
                 |> getItemsFromCollection 
                 |> List.append extensions
             | ComplexType.Basic(t) -> 
                 (match t.Particle with
                                | Particle(p) -> fromParticle p
                                | null -> Seq.empty
                                | _ -> failwithf "not a particle %A" t.Particle)
                 |> Seq.append (t.Attributes |> Seq.cast<XmlSchemaObject>)
                 |> getItemsFromCollection
             | ComplexType.Restricted(t,res) -> 
                 let getName a = 
                      match a with
                        Type (Complex (t)) -> t.QualifiedName
                        | Type (Simple (t)) ->  t.QualifiedName
                        | Element e -> e.QualifiedName
                        | Attribute a -> a.QualifiedName
                        | _ -> failwith "Unamed element"
                      |> qnToString
                 let restrictions = 
                     let attributeMapper (attrs:XmlSchemaObjectCollection) = 
                         attrs
                         |> Seq.cast<XmlSchemaAttribute>
                         |> Seq.map (fun a -> a.QualifiedName |> qnToString, a :> XmlSchemaObject)
                     match res with
                       Restriction.Complex (res) ->
                          res.Attributes
                          |> attributeMapper
                          |> Seq.append (
                             match res.Particle with
                             Particle(p) -> 
                                p |> fromParticle
                                |> Seq.map (fun a -> a |> getName , a)
                             | _ -> Seq.empty
                          )
                       | Restriction.Simple (res) ->
                          res.Attributes
                          |> attributeMapper
                     |> Map.ofSeq
                 t.BaseXmlSchemaType
                 |> getElementsFromObject
                 |> Seq.append (t.Attributes |> Seq.cast<XmlSchemaObject>)
                 |> Seq.filter (fun e -> (e |> getName |> restrictions.TryFind).IsNone)
                 |> Seq.cast<XmlSchemaObject>
                 |> getItemsFromCollection
          | Type(Simple _ ) ->
              []
          | Element element -> 
              match element.SchemaType with
              null -> []
              | t -> t |> getElementsFromObject
          | Attribute _ -> []
          | Particle (p) -> p |> fromParticle |> List.ofSeq
          | _ -> failwithf "Unsupported item %A" item

    let private getParentElement node =
          let rec _inner (node:System.Xml.Schema.XmlSchemaObject) = 
              match node.Parent with
              Element p ->  p :> XmlSchemaAnnotated |> Some
              | Attribute a -> a :> XmlSchemaAnnotated |> Some
              | null 
              | Schema _ ->  None
              |_ as p -> _inner p 
          _inner node
    let private getParentElements node = 
        let rec _inner acc (node:System.Xml.Schema.XmlSchemaObject)  =
             match node |> getParentElement with
             Some p -> _inner (p::acc) p
             | None -> acc
        _inner [] node
    let private getTypeName (xmlSchemaType:XmlSchemaType) = 
        let res = 
          match xmlSchemaType.QualifiedName with
          name when name = null || String.IsNullOrWhiteSpace(name.Name) ->
              if String.IsNullOrEmpty(xmlSchemaType.Name) then
                let p = xmlSchemaType |> getParentElement
                match p with
                Some p ->
                   "___" + match p with
                           :? XmlSchemaElement as p ->
                               p.Name
                           | :? XmlSchemaAttribute as a ->
                               a.Name
                           | _ -> failwith "Expected an element or an attribute"
                | None -> failwithf "No valid name for type %A" xmlSchemaType
              else 
                 xmlSchemaType.Name
          | name -> name |> qnToString 
        if res = "" then failwithf "Invalid  name for %A" xmlSchemaType
        res
    

    let private findType (map:Map<string, XmlSchemaType>) name =
        match map.TryFind name with
        None -> failwithf "Couldn't find a type named %s" name
        | Some t -> 
            t

    let read path = 
         use reader = new StreamReader(File.OpenRead(path))
         let xmlSchemaSet = new System.Xml.Schema.XmlSchemaSet()
         xmlSchemaSet.Add(System.Xml.Schema.XmlSchema.Read(reader, (fun o (e:ValidationEventArgs) -> failwith e.Message))) |> ignore
         xmlSchemaSet

    let parseSchema (xmlSchemaSet:XmlSchemaSet) = 
         let items = [for sch in xmlSchemaSet.Schemas() do
                        let sch = sch :?> XmlSchema
                        for item in sch.Items do yield item]
         let types =
             [for item in items do 
                  if item :? XmlSchemaType then 
                      let typ = item :?> XmlSchemaType
                      yield (typ |> getTypeName, typ)
                      for i in typ |> getElementsFromObject do
                        match i with
                        :? XmlSchemaType as typ ->
                            yield (typ |> getTypeName, typ)
                        | _ -> ()]
         let elements =
           [for i in items do 
              match i with
              Element e -> yield e
              | _ -> ()]
         {
             SchemaSet = xmlSchemaSet
             Items = items
             Types = types
             Elements = elements
         }

    let private XsdNamespace = "http://www.w3.org/2001/XMLSchema"
    let private NativeTypes = 
       [for (name,t)  in 
            [("string"      , typeof<string>);
             ("anyURI"      , typeof<string>);
             ("base64string", typeof<string>);
             ("hexBinary"   , typeof<string>);
             ("NMTOKEN"     , typeof<string>);
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
             ("http://www.w3.org/2001/XMLSchema}NMTOKEN"      , typeof<string>);
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
               do
                  yield (name, InferedType.Primitive(t,None,false))] |> Map.ofList
    let private getName (typ:XmlType) =
        let typeDeclaration, name  =
            match typ with
            Simple(typ) -> typ.TypeDeclaration :> XmlSchemaType, typ.TypeDeclaration |> getSchemaTypeName
            | Complex(t) -> 
                  t.TypeDeclaration  :> XmlSchemaType, t.TypeDeclaration |> getSchemaTypeName
        let name = 
            match name with
            | Some n -> 
                n
            | None ->
                typeDeclaration 
                |> getParentElements
                |> List.map (fun el -> (el :?> XmlSchemaElement).Name)
                |> String.Concat
        assert (name |>  String.IsNullOrWhiteSpace |> not)
        name

    let rec private createElements getTypeFromAnnotated (elements:XmlSchemaElement list) = 
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
                      let t = 
                          match el |> getTypeFromAnnotated with
                          InferedType.Record _  as t -> t
                          | t ->
                              InferedType.Record(Some "", [{Name = ""; Type = t}],false)
                      let name = el.QualifiedName |> qnToString 
                      multiplicity,t, name)
        |> List.map(fun (multiplicity,t,name) ->
                      assert not (String.IsNullOrWhiteSpace name )
                      let elemType =  
                            match t with
                            | InferedType.Record(Some "", p,o) ->
                                   InferedType.Record(Some name,p,o)
                            | t -> t
                      (InferedTypeTag.Record (Some name),(multiplicity, elemType)))

    and private getTypeFromAnnotated (_types:System.Collections.Generic.Dictionary<string,InferedType>) (el:XmlSchemaAnnotated) : InferedType =
         let getTypeFromAnnotated el = getTypeFromAnnotated _types el
         let createElements = createElements getTypeFromAnnotated
         let schemaType = 
              match el with
              Element  e -> 
                   e.ElementSchemaType
              | Attribute  e -> e.AttributeSchemaType :> XmlSchemaType
              | Type(t) ->
                  match t with
                  Complex  t -> t.TypeDeclaration :> XmlSchemaType
                  | Simple t -> t.TypeDeclaration :> XmlSchemaType
              |_ -> failwithf "Expected an element or an attribute but got %A" el 
         let typeName, typ = 
              let typ =
                  match schemaType with
                  | Type(s) ->
                       s
                  | _ -> failwithf "unknown type declaration for %A. Type declaration was %A" el schemaType
              typ |> getName, typ
         match _types.TryGetValue typeName with
         true,t -> t
         | _ ->
             let t = 
                 match typ with
                 Simple(SimpleType.Restricted(_,re)) ->
                     match re with
                     Restriction re ->
                         match re.BaseType with
                         null -> NativeTypes.[re.BaseTypeName |> qnToString]
                         | _ as baseType -> baseType |> getTypeFromAnnotated
                     | Union _ ->
                         failwith "unions not supported"
                     | List _ ->
                         failwith "lists not supported"
                 | Simple(SimpleType.Basic typeDeclaration) as t->
                      let name = t |> getName 
                      match name |> NativeTypes.TryFind with
                      None -> 
                          //if it's an undeclared XSD native type use string as the representation
                          if (typeDeclaration.QualifiedName.Namespace = XsdNamespace) then 
                             NativeTypes.["string"]
                          else
                             let t = 
                                 match typeDeclaration |> getTypeFromAnnotated with 
                                 InferedType.Primitive(_) as t -> t 
                                 | InferedType.Record(Some _,[{Name = "";Type = t}], _) -> t 
                                 | _ as t-> failwithf "Can't use %A for a simple type" t 
                             assert not (String.IsNullOrWhiteSpace typeName)
                             InferedType.Record( 
                                 Some typeName, 
                                   [{Name = ""; 
                                     Type =  t}],false) 
                     | Some t -> t
                 | Complex(t) ->
                     let objs = 
                        t.TypeDeclaration
                        |> getElementsFromObject 
                     let (elements, attributes) = 
                        objs
                        |> List.fold (fun (els,attrs) e -> 
                                        match e with
                                        Attribute a -> (els,a::attrs)
                                        | Element e -> (e::els,attrs)
                                        | _ ->  els,attrs) ([],[])
                        |> (fun (els,attrs) ->
                             els
                             |> createElements,
                              attrs 
                              |> List.map( 
                                  fun a ->
                                      let typ = a |> getTypeFromAnnotated
                                      let name = a.Name 
                                      let opt = a.Use = XmlSchemaUse.Optional 
                                      let t =
                                          match typ with
                                          InferedType.Record
                                                   (Some _,
                                                    [{Name = _;
                                                      Type = pt}], _) -> 
                                                          match pt with
                                                          InferedType.Primitive(t,u,_) -> InferedType.Primitive(t,u,opt)
                                                          | _ -> failwithf "Primitive type expected for attribute %A" pt      
                                          | InferedType.Primitive(_) as t -> t
                                          | _ as t -> failwithf "Unexpected type %A " t
                                      {Name = name;
                                       Type = t}))
                     assert not (String.IsNullOrWhiteSpace typeName)
                     InferedType.Record(
                              Some typeName,
                                {Name = "";
                                  Type = InferedType.Collection(elements |> List.map(fun (tag,_) -> tag) , elements |> Map.ofList)}::attributes,false)
             _types.Add(typeName,t) |> ignore
             t
    
    let generateType (schema:System.Xml.Schema.XmlSchemaSet) =

        schema.Compile()

        let getType el = getTypeFromAnnotated (new System.Collections.Generic.Dictionary<string,InferedType>()) el
        let createElements = createElements getType
        let parsedSchema = 
            schema 
            |> parseSchema
        
          
        //define all the types of the schema
        let types =
           parsedSchema.Types
           |> List.map (fun (_, t) -> t)
           |> List.map getType 
        //There can be zero or many elements defined as well as types
        //We'll essentially treat each as a type
        //failwith "post-compile"
        let elements =
           parsedSchema.Elements
           |> createElements
           |> List.zip parsedSchema.Elements
           //For the free elements we want them to be named after the element and not have the type name only
           //xmlSchemaType is essentially to have the extra Parse methods named after the elements
           //since the implementation relies on xmlSchemaType
           |> List.fold (fun st (e,(_,(_,typ)) as t) -> 
                          match typ with
                          | InferedType.Primitive (_) ->
                              st
                          | InferedType.Record(Some _, p, o) ->
                              let name = e.Name.Replace(":","__")
           
                              assert not (String.IsNullOrWhiteSpace name)
           
                              let typ = InferedType.Record(Some (name),p,o)
                              typ::st
                          | _ -> failwithf "Elements must be represented with a record %A" t) []
        
        types@elements
        |> List.filter (fun t -> 
                          match t with
                          | InferedType.Primitive (_) ->
                             false
                          | _ -> true
        )