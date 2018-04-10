// --------------------------------------------------------------------------------------
// XML type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Collections.Generic
open System.Xml.Linq
open FSharp.Quotations
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralTypes
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder

// --------------------------------------------------------------------------------------

#nowarn "10001"

/// Context that is used to generate the XML types.
type internal XmlGenerationContext =
    { CultureStr : string
      ProvidedType : ProvidedTypeDefinition
      // to nameclash type names
      UniqueNiceName : string -> string 
      UnifyGlobally : bool
      XmlTypeCache : Dictionary<string, XmlGenerationResult>
      JsonTypeCache : Dictionary<InferedType, ProvidedTypeDefinition> }
    static member Create(cultureStr, tpType, unifyGlobally) =
        let uniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName
        uniqueNiceName "XElement" |> ignore
        { CultureStr = cultureStr
          ProvidedType = tpType
          UniqueNiceName = uniqueNiceName
          UnifyGlobally = unifyGlobally
          XmlTypeCache = Dictionary()
          JsonTypeCache = Dictionary() }
    member x.ConvertValue prop =
        let typ, _, conv, _ = ConversionsGenerator.convertStringValue "" x.CultureStr prop
        typ, conv
    member x.ConvertValueBack prop =
        let typ, _, _, convBack = ConversionsGenerator.convertStringValue "" x.CultureStr prop
        typ, convBack
    member x.MakeOptionType(typ:Type) = 
        typedefof<option<_>>.MakeGenericType typ

and internal XmlGenerationResult = 
    { ConvertedType : Type
      Converter : Expr -> Expr }

module internal XmlTypeBuilder = 

    /// Recognizes different valid infered types of content:
    ///
    ///  - `Primitive` means that the content is a value and there are no children
    ///  - `Collection` means that there are always just children but no value
    ///  - `Heterogeneous` means that there may be either children or value(s)
    ///
    /// We return a list with all possible primitive types and all possible
    /// children types (both may be empty)
    let (|ContentType|_|) inferedProp = 

        let inOrder order types = 
            types |> Map.toList |> List.sortBy (fun (tag, _) -> List.findIndex ((=) tag) order)

        match inferedProp with 
        | { Type = (InferedType.Primitive _ | InferedType.Json _) as typ } -> Some([typ], [])
        | { Type = InferedType.Collection (order, types) } -> Some([], inOrder order types)
        | { Type = InferedType.Heterogeneous cases } ->
              let collections, others = Map.toList cases |> List.partition (fst >> (=) InferedTypeTag.Collection)
              match collections with
              | [InferedTypeTag.Collection, InferedType.Collection (order, types)] -> Some(List.map snd others, inOrder order types)
              | [] -> Some(List.map snd others, [])
              | _ -> failwith "(|ContentType|_|): Only one collection type expected"
        // an empty element
        | { Type = InferedType.Top } -> Some([], [])
        | _ -> None
    
    /// Succeeds when type is a heterogeneous type containing recors
    /// If the type is heterogeneous, but contains other things, exception
    /// is thrown (this is unexpected, because XML elements are always records)
    let (|HeterogeneousRecords|_|) inferedType =
        match inferedType with
        | InferedType.Heterogeneous cases ->
            let records = 
              cases 
              |> List.ofSeq
              |> List.choose (function 
                  | KeyValue(InferedTypeTag.Record (Some name), v) -> Some(name, v) 
                  | _ -> None)
            if cases.Count = records.Length then Some records
            else failwith "HeterogeneousRecords: Unexpected mix of records and other type kinds"
        | _ -> None
    
    // For every possible primitive type add '<Tag>Value' property that 
    // returns it converted to the right type (or an option)  
    let getTypesForPrimitives (ctx:XmlGenerationContext) forceOptional (primitives:_ list) = [

        for primitive in primitives ->
        
            let name = 
                if primitives.Length = 1 
                then "Value" 
                else (StructuralInference.typeTag primitive).NiceName
            
            match primitive with 
            | InferedType.Primitive(typ, unit, optional) -> 
            
                let optional = optional || forceOptional
                let optionalJustBecauseThereAreMultiple = primitives.Length > 1 && not optional
                let optional = optional || primitives.Length > 1

                let typ, conv = ctx.ConvertValue <| PrimitiveInferedProperty.Create("Value", typ, optional, unit)
                let conv = fun xml -> conv <@ XmlRuntime.TryGetValue(%%xml) @>
                
                typ, name, conv, optionalJustBecauseThereAreMultiple
            
            | InferedType.Json(typ, optional) -> 

                let cultureStr = ctx.CultureStr
                let ctx = JsonGenerationContext.Create(cultureStr, ctx.ProvidedType, ctx.UniqueNiceName, ctx.JsonTypeCache)
                let result = JsonTypeBuilder.generateJsonType ctx (*canPassAllConversionCallingTypes*)false (*optionalityHandledByParent*)true "" typ          
                
                let optional = optional || forceOptional
                let optionalJustBecauseThereAreMultiple = primitives.Length > 1 && not optional
                let optional = optional || primitives.Length > 1

                let typ = 
                    if optional
                    then ctx.MakeOptionType result.ConvertedType
                    else result.ConvertedType

                let conv = fun xml ->
                    if optional
                    then <@@ XmlRuntime.TryGetJsonValue(%%xml) @@>
                    else <@@ XmlRuntime.GetJsonValue(%%xml) @@>
                    |> result.Convert

                typ, name, conv, optionalJustBecauseThereAreMultiple
            
            | _ -> failwithf "generatePropertiesForValue: Primitive or Json type expected: %A" primitive
    ]
    
    /// Recursively walks over inferred type information and 
    /// generates types for read-only access to the document
    let rec generateXmlType ctx inferedType = 
    
        match inferedType with
       
        // If we already generated object for this type, return it
        | InferedType.Record(Some nameWithNs, _, false) when ctx.XmlTypeCache.ContainsKey nameWithNs -> 
            ctx.XmlTypeCache.[nameWithNs]
        
        // If the element does not have any children and always contains only primitive type
        // then we turn it into a primitive value of type such as int/string/etc.
        | InferedType.Record(Some _, [{ Name = ""
                                        Type = (InferedType.Primitive _ | InferedType.Json _) as primitive }], false) ->
       
            let typ, _, conv, _ = getTypesForPrimitives ctx false [ primitive ] |> Seq.exactlyOne
            { ConvertedType = typ
              Converter = conv }
       
        // If the element is a heterogeneous type containing records, generate type with multiple
        // optional properties (this can only happen when using sample list with multiple root
        // elements of different names). Otherwise, heterogeneous types appear only as child elements
        // of an element (handled similarly below)
        | HeterogeneousRecords cases ->
       
            // Generate new choice type for the element
            let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName "Choice", Some typeof<XmlElement>, hideObjectMethods = true, nonNullable = true)
            ctx.ProvidedType.AddMember objectTy
       
            // to nameclash property names
            let makeUnique = NameUtils.uniqueGenerator NameUtils.nicePascalName
            makeUnique "XElement" |> ignore
       
            // For each case, add property of optional type
            let members = 
                [ for nameWithNS, case in cases ->
                
                    let result = generateXmlType ctx case
                    let convFunc = ReflectionHelpers.makeDelegate result.Converter typeof<XmlElement>
                    let name = makeUnique (XName.Get(nameWithNS).LocalName)

                    ProvidedProperty(name, ctx.MakeOptionType result.ConvertedType, getterCode = fun (Singleton xml) ->               
                        // XmlRuntime.ConvertAsName checks that the name of the current element
                        // has the required name and returns Some/None
                        let xmlRuntime = typeof<XmlRuntime>
                        (xmlRuntime?ConvertAsName (result.ConvertedType) (xml, nameWithNS, convFunc) : Expr)
                       ), 
                    ((if result.ConvertedType :? ProvidedTypeDefinition then "" else nameWithNS),
                     ProvidedParameter(NameUtils.niceCamelName name, result.ConvertedType)) ]

            let properties, parameters = List.unzip members            
            objectTy.AddMembers properties

            let cultureStr = ctx.CultureStr

            for nameWithNS, param in parameters do
                let ctor = ProvidedConstructor([param], invokeCode = fun (Singleton arg) ->
                    if nameWithNS = "" then
                        arg
                    else
                        let arg = Expr.Coerce(arg, typeof<obj>)
                        <@@ XmlRuntime.CreateValue(nameWithNS, %%arg, cultureStr) @@>)
                objectTy.AddMember ctor

            objectTy.AddMember <| 
              ProvidedConstructor(
                  [ProvidedParameter("xElement",typeof<XElement>)], 
                  invokeCode = fun (Singleton arg) -> <@@ XmlElement.Create(%%arg:XElement) @@>)

            { ConvertedType = objectTy
              Converter = id }
       
        // If the element is more complicated, then we generate a type to represent it properly
        | InferedType.Record(Some nameWithNS, props, false) -> 
       
            let names = nameWithNS.Split [| '|' |] |> Array.map (fun nameWithNS -> XName.Get(nameWithNS).LocalName)

            let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName names.[0],
                                                                 Some typeof<XmlElement>, 
                                                                 hideObjectMethods = true, nonNullable = true)
            ctx.ProvidedType.AddMember objectTy
       
            // If we unify types globally, then save type for this record
            if ctx.UnifyGlobally then
                ctx.XmlTypeCache.Add(nameWithNS, { ConvertedType = objectTy 
                                                   Converter = id })
                
            // Split the properties into attributes and a 
            // special property representing the content
            let attrs, content =
                props |> List.partition (fun prop -> prop.Name <> "")
       
            // to nameclash property names
            let makeUnique = NameUtils.uniqueGenerator NameUtils.nicePascalName
            makeUnique "XElement" |> ignore
       
            // Generate properties for all XML attributes
            let attributeResults = 
                [ for attr in attrs ->
       
                    let nameWithNS = attr.Name
                    let name = XName.Get(nameWithNS).LocalName
                
                    let createMember (typ: Type) (conv : _ -> Expr) =
                        nameWithNS,
                        ProvidedProperty(makeUnique name, typ, getterCode = fun (Singleton xml) -> 
                            conv <@ XmlRuntime.TryGetAttribute(%%xml, nameWithNS) @> ),
                        ProvidedParameter(NameUtils.niceCamelName name, typ)

                    let createPrimitiveMember typ unit (optional:bool) =            
                        let typ, conv = ctx.ConvertValue <| PrimitiveInferedProperty.Create("Attribute " + name, typ, optional, unit)
                        createMember typ conv
                
                    match attr.Type with 
                    | InferedType.Heterogeneous types ->
                
                        // If the attribute has multiple possible type (e.g. "bool|int") then we generate
                        // a choice type that is erased to 'option<string>' (for simplicity, assuming that
                        // the attribute is always optional)
                        let choiceTy = ProvidedTypeDefinition(ctx.UniqueNiceName (name + "Choice"), Some typeof<option<string>>, hideObjectMethods = true, nonNullable = true)
                        ctx.ProvidedType.AddMember choiceTy
                
                        for KeyValue(tag, typ) in types do 
                      
                            if typ.IsOptional then
                                failwithf "generateXmlType: Type shouldn't be optional: %A" typ
                      
                            match typ with
                            | InferedType.Primitive(primTyp, unit, false) ->
                        
                                let typ, conv = ctx.ConvertValue <| PrimitiveInferedProperty.Create(tag.NiceName, primTyp, true, unit)
                                choiceTy.AddMember <|
                                    ProvidedProperty(tag.NiceName, typ , getterCode = fun (Singleton attrVal) -> 
                                        attrVal |> Expr.Cast |> conv)

                                let typ, convBack = ctx.ConvertValueBack <| PrimitiveInferedProperty.Create(tag.NiceName, primTyp, false, unit)
                                choiceTy.AddMember <|
                                    let parameter = ProvidedParameter("value", typ)
                                    ProvidedConstructor([parameter], invokeCode = fun (Singleton arg) -> 
                                        arg |> convBack |> ProviderHelpers.some typeof<string> )

                            | _ -> failwithf "generateXmlType: A choice type of an attribute can only contain primitive types, got %A" typ

                        let defaultCtor = ProvidedConstructor([], invokeCode = fun _ -> <@@ option<string>.None @@>)
                        choiceTy.AddMember defaultCtor

                        createMember choiceTy (fun x -> x :> Expr)
                
                    | InferedType.Primitive(typ, unit, optional) -> createPrimitiveMember typ unit optional
                    | InferedType.Null -> createPrimitiveMember typeof<string> None false 
                
                    | _ -> failwithf "generateXmlType: Expected Primitive or Choice type, got %A" attr.Type]                 

            // Add properties that can be used to access content of the element
            // (either child elements or primitive values if the element contains only simple values)
            let primitiveResults, childResults = 
                match content with 
                | [ContentType(primitives, children)] ->
       
                    // If there may be other children, make it optional
                    let forceOptional = children.Length > 0
       
                    let primitiveResults =
                        [ for typ, name, conv, optionalJustBecauseThereAreMultiple in getTypesForPrimitives ctx forceOptional primitives ->
                            let nonOptionalType = if optionalJustBecauseThereAreMultiple && typ.IsGenericType then typ.GetGenericArguments().[0] else typ                                
                            let name = makeUnique name
                            ProvidedProperty(name, typ, getterCode = fun (Singleton xml) -> conv xml),
                            ProvidedParameter(NameUtils.niceCamelName name, nonOptionalType) ]
       
                    // For every possible child element, generate a getter property
                    let childResults =
                        [ for child in children ->

                            let isCollectionName parentName childName =
                                parentName = NameUtils.pluralize childName || parentName.StartsWith childName

                            let child = 
                                match child with
                                | InferedTypeTag.Record(Some parentNameWithNS),
                                  (InferedMultiplicity.Single, 
                                   InferedType.Record(Some parentNameWithNS2,
                                                      [ { Type = InferedType.Collection (_, SingletonMap (InferedTypeTag.Record (Some childNameWithNS), 
                                                                                                         (_, InferedType.Record(Some childNameWithNS2, _, false) as multiplicityAndType))) } ], false)) 
                                  when parentNameWithNS = parentNameWithNS2 && childNameWithNS = childNameWithNS2 && isCollectionName (XName.Get(parentNameWithNS).LocalName) (XName.Get(childNameWithNS).LocalName) -> 
                                      let combinedName = Some (parentNameWithNS + "|" + childNameWithNS)
                                      InferedTypeTag.Record combinedName, multiplicityAndType
                                | x -> x

                            match child with
                            | InferedTypeTag.Record(Some nameWithNS), (multiplicity, typ) ->

                                let names = nameWithNS.Split [| '|' |] |> Array.map (fun nameWithNS -> XName.Get(nameWithNS).LocalName)
                                let result = generateXmlType ctx typ 
       
                                match multiplicity with
                                | InferedMultiplicity.Single ->
                                    let name = makeUnique names.[names.Length - 1]
                                    nameWithNS,
                                    ProvidedProperty(name, result.ConvertedType, getterCode = fun (Singleton xml) -> 
                                        result.Converter <@@ XmlRuntime.GetChild(%%xml, nameWithNS) @@> 
                                       ),
                                    ProvidedParameter(NameUtils.niceCamelName name, result.ConvertedType)
       
                                // For options and arrays, we need to generate call to ConvertArray or ConvertOption
                                // (because the child may be represented as primitive type - so we cannot just
                                // return array of XmlElement - it might be for example int[])
                                | InferedMultiplicity.Multiple ->
                                    let convFunc = ReflectionHelpers.makeDelegate result.Converter typeof<XmlElement>
                                    let isCollectionName = names.[0].EndsWith "List" || names.[0].EndsWith "Array" || names.[0].EndsWith "Collection"
                                    let name = makeUnique (if isCollectionName then names.[0] else NameUtils.pluralize names.[0])
                                    let typ = result.ConvertedType.MakeArrayType()
                                    nameWithNS,
                                    ProvidedProperty(name, typ, getterCode = fun (Singleton xml) -> 
                                        let xmlRuntime = typeof<XmlRuntime>
                                        xmlRuntime?ConvertArray (result.ConvertedType) (xml, nameWithNS, convFunc)),
                                    ProvidedParameter(NameUtils.niceCamelName name, typ)

                                | InferedMultiplicity.OptionalSingle ->
                                    let convFunc = ReflectionHelpers.makeDelegate result.Converter typeof<XmlElement>
                                    let name = makeUnique names.[names.Length - 1]
                                    if result.ConvertedType.Name.StartsWith "FSharpOption`1" then                                      
                                        nameWithNS,
                                        ProvidedProperty(name, result.ConvertedType, getterCode = fun (Singleton xml) -> 
                                            let xmlRuntime = typeof<XmlRuntime>
                                            xmlRuntime?ConvertOptional2 (result.ConvertedType.GenericTypeArguments.[0]) (xml, nameWithNS, convFunc)
                                           ),
                                        ProvidedParameter(NameUtils.niceCamelName name, result.ConvertedType)
                                    else
                                        let typ = ctx.MakeOptionType result.ConvertedType
                                        nameWithNS,
                                        ProvidedProperty(name,  typ, getterCode = fun (Singleton xml) -> 
                                            let xmlRuntime = typeof<XmlRuntime>
                                            xmlRuntime?ConvertOptional (result.ConvertedType) (xml, nameWithNS, convFunc)
                                           ),
                                        ProvidedParameter(NameUtils.niceCamelName name, typ)
       
                            | _ -> failwithf "generateXmlType: Child elements should be named record types, got %A" child ]

                    primitiveResults, childResults

                | [_] -> failwithf "generateXmlType: Children should be collection or heterogeneous: %A" content
                | _::_ -> failwithf "generateXmlType: Only one child collection expected: %A" content
                | [] -> [], []
                
            let attrNames, attrProperties, attrParameters = List.unzip3 attributeResults
            let primitiveElemProperties, primitiveElemParameters = List.unzip primitiveResults
            let childElemNames, childElemProperties, childElemParameters = List.unzip3 childResults

            objectTy.AddMembers (attrProperties @ primitiveElemProperties @ childElemProperties)
            
            let createConstrutor primitiveParam = 
                let parameters = match primitiveParam with
                                 | Some primitiveParam -> attrParameters @ [primitiveParam] @ childElemParameters
                                 | None -> attrParameters @ childElemParameters
                objectTy.AddMember <|                
                    ProvidedConstructor(parameters, invokeCode = fun args -> 
                        let attributes = 
                            Expr.NewArray(typeof<string * obj>, 
                                          args 
                                          |> Seq.take attrParameters.Length
                                          |> Seq.toList
                                          |> List.mapi (fun i a -> Expr.NewTuple [ Expr.Value attrNames.[i]
                                                                                   Expr.Coerce(a, typeof<obj>) ]))
                        let elements =
                            args 
                            |> Seq.skip (attrParameters.Length + (match primitiveParam with Some _ -> 1 | None -> 0))
                            |> Seq.toList
                            |> List.mapi (fun i a -> Expr.NewTuple [ Expr.Value childElemNames.[i]
                                                                     Expr.Coerce(a, typeof<obj>) ])                    
                        let elements = 
                            match primitiveParam with
                            | Some _ ->
                                Expr.NewTuple [ Expr.Value ""
                                                Expr.Coerce (args.[attrParameters.Length], typeof<obj>) ] :: elements
                            | None -> elements

                        let elements = Expr.NewArray(typeof<string * obj>, elements)

                        let cultureStr = ctx.CultureStr
                        <@@ XmlRuntime.CreateRecord(nameWithNS, %%attributes, %%elements, cultureStr) @@>
                       )
            
            if primitiveElemParameters.Length = 0 then
                createConstrutor None
            else
                for primitiveParam in primitiveElemParameters do
                    createConstrutor (Some primitiveParam)

            objectTy.AddMember <| 
              ProvidedConstructor(
                  [ProvidedParameter("xElement", typeof<XElement>)], 
                  invokeCode = fun (Singleton arg) -> 
                      <@@ XmlElement.Create(%%arg:XElement) @@>)

            { ConvertedType = objectTy 
              Converter = id }
       
        | _ -> failwithf "generateXmlType: Infered type should be record type: %A" inferedType
