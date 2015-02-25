// --------------------------------------------------------------------------------------
// HTML type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Collections.Generic
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralTypes

#nowarn "10001"

type internal HtmlGenerationContext =
    { CultureStr : string
      TypeProviderType : ProvidedTypeDefinition
      Replacer : AssemblyReplacer
      // to nameclash type names
      UniqueNiceName : string -> string
      HtmlTypeCache : Dictionary<string, HtmlGenerationResult> }
    static member Create(cultureStr, tpType, replacer) =
        let uniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName
        uniqueNiceName "Html" |> ignore
        { CultureStr = cultureStr
          TypeProviderType = tpType
          Replacer = replacer
          UniqueNiceName = uniqueNiceName
          HtmlTypeCache = Dictionary() }
    member x.ConvertValue prop =
        let typ, _, conv, _ = ConversionsGenerator.convertStringValue x.Replacer "" x.CultureStr prop
        typ, conv
    member x.ConvertValueBack prop =
        let typ, _, _, convBack = ConversionsGenerator.convertStringValue x.Replacer "" x.CultureStr prop
        typ, convBack
    member x.MakeOptionType(typ:Type) = 
        (x.Replacer.ToRuntime typedefof<option<_>>).MakeGenericType typ

and internal HtmlGenerationResult = 
    { ConvertedType : Type
      Converter : Expr -> Expr }

module internal HtmlTypeBuilder = 
    


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
    
    let getTypesForPrimitives (ctx:HtmlGenerationContext) forceOptional (primitives:_ list) = [

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
                let conv = fun xml -> conv <@ HtmlRuntime.TryGetValue(%%xml) @>
                
                typ, name, ctx.Replacer.ToDesignTime >> conv, optionalJustBecauseThereAreMultiple
            
            | _ -> failwithf "generatePropertiesForValue: Primitive expected: %A" primitive
    ]

    let rec generateHtmlType (ctx:HtmlGenerationContext) inferedType = 
    
        match inferedType with
       
        // If we already generated object for this type, return it
        | InferedType.Record(Some nameWithNs, _, false) when ctx.HtmlTypeCache.ContainsKey nameWithNs -> 
            ctx.HtmlTypeCache.[nameWithNs]
        
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
            let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName "Choice", Some(ctx.Replacer.ToRuntime typeof<HtmlElement>), HideObjectMethods = true, NonNullable = true)
            ctx.TypeProviderType.AddMember objectTy
       
            // to nameclash property names
            let makeUnique = NameUtils.uniqueGenerator NameUtils.nicePascalName
            makeUnique "Html" |> ignore
       
            // For each case, add property of optional type
            let members = 
                [ for name, case in cases ->
                
                    let result = generateHtmlType ctx case
                    let convFunc = ReflectionHelpers.makeDelegate result.Converter (ctx.Replacer.ToRuntime typeof<HtmlElement>)                            
                    let name = makeUnique name

                    ProvidedProperty(name, ctx.MakeOptionType result.ConvertedType, GetterCode = fun (Singleton xml) ->               
                        // HtmlRuntime.ConvertAsName checks that the name of the current element
                        // has the required name and returns Some/None
                        let HtmlRuntime = ctx.Replacer.ToRuntime typeof<HtmlRuntime>
                        HtmlRuntime?ConvertAsName (result.ConvertedType) (xml, name, convFunc)), 
                    ((if result.ConvertedType :? ProvidedTypeDefinition then "" else name),
                     ProvidedParameter(NameUtils.niceCamelName name, result.ConvertedType)) ]

            let properties, parameters = List.unzip members            
            objectTy.AddMembers properties

            let cultureStr = ctx.CultureStr

            for nameWithNS, param in parameters do
                let ctor = ProvidedConstructor([param], InvokeCode = fun (Singleton arg) ->
                    if nameWithNS = "" then
                        arg
                    else
                        let arg = Expr.Coerce(arg, typeof<obj>)
                        <@@ HtmlRuntime.CreateValue(nameWithNS, %%arg, cultureStr) @@> |> ctx.Replacer.ToRuntime)
                objectTy.AddMember ctor

            objectTy.AddMember <| 
              ProvidedConstructor(
                  [ProvidedParameter("Html",ctx.Replacer.ToRuntime typeof<HtmlNode>)], 
                  InvokeCode = fun (Singleton arg) -> 
                      let arg = ctx.Replacer.ToDesignTime arg
                      <@@ HtmlElement.Create(%%arg) @@> |> ctx.Replacer.ToRuntime)

            { ConvertedType = objectTy
              Converter = ctx.Replacer.ToRuntime }
       
        // If the element is more complicated, then we generate a type to represent it properly
        | InferedType.Record(Some name, props, false) -> 
            let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName name,
                                                  Some(ctx.Replacer.ToRuntime typeof<HtmlElement>), 
                                                  HideObjectMethods = true, NonNullable = true)
            ctx.TypeProviderType.AddMember objectTy
                       
            // Split the properties into attributes and a 
            // special property representing the content
            let attrs, content =
                props |> List.partition (fun prop -> prop.Name <> "")
       
            // to nameclash property names
            let makeUnique = NameUtils.uniqueGenerator NameUtils.nicePascalName
            makeUnique "Html" |> ignore
       
            // Generate properties for all XML attributes
            let attributeResults = 
                [ for attr in attrs ->
       
                    let name = attr.Name
                
                    let createMember typ conv =
                        name,
                        ProvidedProperty(makeUnique name, typ, GetterCode = fun (Singleton xml) -> 
                            let xml = ctx.Replacer.ToDesignTime xml
                            conv <@ HtmlRuntime.TryGetAttribute(%%xml, name) @>),
                        ProvidedParameter(NameUtils.niceCamelName name, typ)

                    let createPrimitiveMember typ unit (optional:bool) =            
                        let typ, conv = ctx.ConvertValue <| PrimitiveInferedProperty.Create("Attribute " + name, typ, optional, unit)
                        createMember typ conv
                
                    match attr.Type with 
                    | InferedType.Heterogeneous types ->
                
                        // If the attribute has multiple possible type (e.g. "bool|int") then we generate
                        // a choice type that is erased to 'option<string>' (for simplicity, assuming that
                        // the attribute is always optional)
                        let choiceTy = ProvidedTypeDefinition(ctx.UniqueNiceName (name + "Choice"), Some(ctx.Replacer.ToRuntime typeof<option<string>>), HideObjectMethods = true, NonNullable = true)
                        ctx.TypeProviderType.AddMember choiceTy
                
                        for KeyValue(tag, typ) in types do 
                      
                            if typ.IsOptional then
                                failwithf "generateXmlType: Type shouldn't be optional: %A" typ
                      
                            match typ with
                            | InferedType.Primitive(primTyp, unit, false) ->
                        
                                let typ, conv = ctx.ConvertValue <| PrimitiveInferedProperty.Create(tag.NiceName, primTyp, true, unit)
                                choiceTy.AddMember <|
                                    ProvidedProperty(tag.NiceName, typ, GetterCode = fun (Singleton attrVal) -> 
                                        attrVal |> ctx.Replacer.ToDesignTime |> Expr.Cast |> conv)

                                let typ, convBack = ctx.ConvertValueBack <| PrimitiveInferedProperty.Create(tag.NiceName, primTyp, false, unit)
                                choiceTy.AddMember <|
                                    let parameter = ProvidedParameter("value", typ)
                                    ProvidedConstructor([parameter], InvokeCode = fun (Singleton arg) -> 
                                        arg |> convBack |> ProviderHelpers.some typeof<string> |> ctx.Replacer.ToRuntime)

                            | _ -> failwithf "generateXmlType: A choice type of an attribute can only contain primitive types, got %A" typ

                        let defaultCtor = ProvidedConstructor([], InvokeCode = fun _ -> ctx.Replacer.ToRuntime <@@ option<string>.None @@>)
                        choiceTy.AddMember defaultCtor

                        createMember choiceTy ctx.Replacer.ToRuntime
                
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
                            ProvidedProperty(name, typ, GetterCode = fun (Singleton xml) -> conv xml),
                            ProvidedParameter(NameUtils.niceCamelName name, nonOptionalType) ]
       
                    // For every possible child element, generate a getter property
                    let childResults =
                        [ for child in children ->

                            let isListName parentName childName =
                                parentName = NameUtils.pluralize childName || parentName = childName + "Array" || parentName = childName + "List"

                            let child = 
                                match child with
                                | InferedTypeTag.Record(Some parentNameWithNS),
                                  (InferedMultiplicity.Single, 
                                   InferedType.Record(Some parentNameWithNS2,
                                                      [ { Type = InferedType.Collection (_, SingletonMap (InferedTypeTag.Record (Some childNameWithNS), 
                                                                                                         (_, InferedType.Record(Some childNameWithNS2, _, false) as multiplicityAndType))) } ], false)) 
                                   when parentNameWithNS = parentNameWithNS2 && childNameWithNS = childNameWithNS2 && isListName parentNameWithNS childNameWithNS
                                  -> 
                                      
                                      InferedTypeTag.Record (Some parentNameWithNS), multiplicityAndType
                                | x -> x

                            match child with
                            | InferedTypeTag.Record(Some nameWithNS), (multiplicity, typ) ->

                                let names = nameWithNS
                                let result = generateHtmlType ctx typ 
       
                                match multiplicity with
                                | InferedMultiplicity.Single ->
                                    let name = makeUnique names
                                    nameWithNS,
                                    ProvidedProperty(name, result.ConvertedType, GetterCode = fun (Singleton xml) -> 
                                        let xml = ctx.Replacer.ToDesignTime xml
                                        result.Converter <@@ HtmlRuntime.GetChild(%%xml, nameWithNS) @@>),
                                    ProvidedParameter(NameUtils.niceCamelName name, result.ConvertedType)
       
                                // For options and arrays, we need to generate call to ConvertArray or ConvertOption
                                // (because the child may be represented as primitive type - so we cannot just
                                // return array of HtmlNode - it might be for example int[])
                                | InferedMultiplicity.Multiple ->
                                    let convFunc = ReflectionHelpers.makeDelegate result.Converter (ctx.Replacer.ToRuntime typeof<HtmlElement>)
                                    let name = makeUnique (NameUtils.pluralize names)
                                    let typ = result.ConvertedType.MakeArrayType()
                                    nameWithNS,
                                    ProvidedProperty(name, typ, GetterCode = fun (Singleton xml) -> 
                                        let HtmlRuntime = ctx.Replacer.ToRuntime typeof<HtmlRuntime>
                                        HtmlRuntime?ConvertArray (result.ConvertedType) (xml, nameWithNS, convFunc)),
                                    ProvidedParameter(NameUtils.niceCamelName name, typ)

                                | InferedMultiplicity.OptionalSingle ->
                                    let convFunc = ReflectionHelpers.makeDelegate result.Converter (ctx.Replacer.ToRuntime typeof<HtmlElement>)
                                    let name = makeUnique names
                                    if result.ConvertedType.Name.StartsWith "FSharpOption`1" then                                      
                                        nameWithNS,
                                        ProvidedProperty(name, result.ConvertedType, GetterCode = fun (Singleton xml) -> 
                                            let HtmlRuntime = ctx.Replacer.ToRuntime typeof<HtmlRuntime>
                                            HtmlRuntime?ConvertOptional2 (result.ConvertedType.GenericTypeArguments.[0]) (xml, nameWithNS, convFunc)),
                                        ProvidedParameter(NameUtils.niceCamelName name, result.ConvertedType)
                                    else
                                        let typ = ctx.MakeOptionType result.ConvertedType
                                        nameWithNS,
                                        ProvidedProperty(name,  typ, GetterCode = fun (Singleton xml) -> 
                                            let HtmlRuntime = ctx.Replacer.ToRuntime typeof<HtmlRuntime>
                                            HtmlRuntime?ConvertOptional (result.ConvertedType) (xml, nameWithNS, convFunc)),
                                        ProvidedParameter(NameUtils.niceCamelName name, typ)
                            | _ -> failwithf "generateHtmlType: Child elements should be named record types, got %A" child ]

                    primitiveResults, childResults

                | [_] -> failwithf "generateHtmlType: Children should be collection or heterogeneous: %A" content
                | _::_ -> failwithf "generateHtmlType: Only one child collection expected: %A" content
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
                    ProvidedConstructor(parameters, InvokeCode = fun args -> 
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
                        <@@ HtmlRuntime.CreateRecord(name, %%attributes, %%elements, cultureStr) @@>
                        |> ctx.Replacer.ToRuntime)
            
            if primitiveElemParameters.Length = 0 then
                createConstrutor None
            else
                for primitiveParam in primitiveElemParameters do
                    createConstrutor (Some primitiveParam)

            objectTy.AddMember <| 
              ProvidedConstructor(
                  [ProvidedParameter("Html", ctx.Replacer.ToRuntime typeof<HtmlNode>)], 
                  InvokeCode = fun (Singleton arg) -> 
                      let arg = ctx.Replacer.ToDesignTime arg
                      <@@ HtmlElement.Create(%%arg) @@> |> ctx.Replacer.ToRuntime)

            { ConvertedType = objectTy 
              Converter = ctx.Replacer.ToRuntime }
       
        | _ -> failwithf "generateHtmlType: Infered type should be record type: %A" inferedType

module internal HtmlGenerator =

    let private getPropertyName = NameUtils.capitalizeFirstLetter
    
    let private typeNameGenerator() =
        NameUtils.uniqueGenerator <| fun s ->
            Utils.invalidTypeNameRegex.Value.Replace(s, " ")
            |> NameUtils.nicePascalName
  
    let private createType (replacer:AssemblyReplacer) (inferenceParameters:HtmlDom.TableInferenceParameters, _, cultureStr) tableType (hobj:HtmlDom.HtmlObject) = 
               
        let name, hasHeaders, headers = 
            match hobj with
            | HtmlDom.Table t -> t.Name, t.HasHeaders, (t.HeaderNamesAndUnits |> Array.map fst)
            | _ -> hobj.Name, false, [||]
            
        let htmlElement = hobj.ToHtmlElement(hasHeaders, headers)
        let inferedType = HtmlInference.inferType true inferenceParameters.CultureInfo true htmlElement
        let ctx = HtmlGenerationContext.Create(cultureStr,tableType, replacer)
        let result = HtmlTypeBuilder.generateHtmlType ctx inferedType
        let runtimeTypeWrapper = replacer.ToRuntime (typeof<HtmlRuntimeWrapper>)
        
        let create (htmlDoc:Expr) =
            runtimeTypeWrapper?Create () (htmlDoc, name, hasHeaders, headers)

        (fun doc -> create doc |> result.Converter), result.ConvertedType


    let generateHtmlTypes asm ns typeName parameters (replacer:AssemblyReplacer) (htmlObjects:HtmlDom.HtmlObject list) =

        let htmlType = ProvidedTypeDefinition(asm, ns, typeName, Some (replacer.ToRuntime typeof<HtmlDocument>), HideObjectMethods = true)
        
        let containerTypes = Dictionary<string, ProvidedTypeDefinition>()

        let getTableTypeName = typeNameGenerator()

        let getOrCreateContainer name = 
            match containerTypes.TryGetValue(name) with
            | true, t -> t
            | false, _ ->
                let containerType = ProvidedTypeDefinition(name + "Container", Some (replacer.ToRuntime typeof<HtmlDocument>), HideObjectMethods = true)
                htmlType.AddMember <| ProvidedProperty(name, containerType, GetterCode = fun (Singleton doc) -> doc)
                htmlType.AddMember containerType
                containerTypes.Add(name, containerType)
                containerType

        for htmlObj in htmlObjects do
            let containerType =
                match htmlObj with
                | HtmlDom.Table _ ->getOrCreateContainer "Tables"   
                | HtmlDom.List _ -> getOrCreateContainer "Lists"
                | HtmlDom.DefinitionList _ -> getOrCreateContainer "DefinitionLists"
               
            let typ = ProvidedTypeDefinition(getTableTypeName htmlObj.Name, Some typeof<obj>)
            htmlType.AddMember typ
            let create, tableType = createType replacer parameters typ htmlObj
            containerType.AddMember <| ProvidedProperty(getPropertyName htmlObj.Name, tableType, GetterCode = fun (Singleton doc) -> create doc)

        htmlType
