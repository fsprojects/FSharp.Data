// --------------------------------------------------------------------------------------
// JSON type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Collections.Generic
open FSharp.Quotations
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralTypes
open ProviderImplementation
open ProviderImplementation.JsonConversionsGenerator
open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.StructuralInference

#nowarn "10001"

/// Context that is used to generate the JSON types.
type internal JsonGenerationContext =
    { CultureStr: string
      TypeProviderType: ProvidedTypeDefinition
      // to nameclash type names
      UniqueNiceName: string -> string
      IJsonDocumentType: Type
      JsonValueType: Type
      JsonRuntimeType: Type
      TypeCache: Dictionary<InferedType, ProvidedTypeDefinition>
      PreferDictionaries: bool
      GenerateConstructors: bool
      InferenceMode: InferenceMode'
      UnitsOfMeasureProvider: IUnitsOfMeasureProvider
      UseOriginalNames: bool
      OmitNullFields: bool
      ExceptionIfMissing: bool }

    static member Create
        (
            cultureStr,
            tpType,
            unitsOfMeasureProvider,
            inferenceMode,
            ?uniqueNiceName,
            ?typeCache,
            ?preferDictionaries,
            ?useOriginalNames,
            ?omitNullFields,
            ?exceptionIfMissing
        ) =
        let useOriginalNames = defaultArg useOriginalNames false

        let uniqueNiceName =
            defaultArg
                uniqueNiceName
                (NameUtils.uniqueGenerator (if useOriginalNames then id else NameUtils.nicePascalName))

        let typeCache = defaultArg typeCache (Dictionary())
        let preferDictionaries = defaultArg preferDictionaries false
        let omitNullFields = defaultArg omitNullFields false
        let exceptionIfMissing = defaultArg exceptionIfMissing false

        JsonGenerationContext.Create(
            cultureStr,
            tpType,
            uniqueNiceName,
            typeCache,
            preferDictionaries,
            true,
            inferenceMode,
            unitsOfMeasureProvider,
            useOriginalNames,
            omitNullFields,
            exceptionIfMissing
        )

    static member Create
        (
            cultureStr,
            tpType,
            uniqueNiceName,
            typeCache,
            preferDictionaries,
            generateConstructors,
            inferenceMode,
            unitsOfMeasureProvider,
            useOriginalNames,
            omitNullFields,
            exceptionIfMissing
        ) =
        { CultureStr = cultureStr
          TypeProviderType = tpType
          UniqueNiceName = uniqueNiceName
          IJsonDocumentType = typeof<IJsonDocument>
          JsonValueType = typeof<JsonValue>
          JsonRuntimeType = typeof<JsonRuntime>
          TypeCache = typeCache
          PreferDictionaries = preferDictionaries
          GenerateConstructors = generateConstructors
          InferenceMode = inferenceMode
          UnitsOfMeasureProvider = unitsOfMeasureProvider
          UseOriginalNames = useOriginalNames
          OmitNullFields = omitNullFields
          ExceptionIfMissing = exceptionIfMissing }

    static member Create
        (
            cultureStr,
            tpType,
            uniqueNiceName,
            typeCache,
            preferDictionaries,
            generateConstructors,
            inferenceMode,
            unitsOfMeasureProvider,
            useOriginalNames
        ) =
        JsonGenerationContext.Create(
            cultureStr,
            tpType,
            uniqueNiceName,
            typeCache,
            preferDictionaries,
            generateConstructors,
            inferenceMode,
            unitsOfMeasureProvider,
            useOriginalNames,
            false,
            false
        )

    member x.MakeOptionType(typ: Type) =
        typedefof<option<_>>.MakeGenericType typ

type internal JsonGenerationResult =
    { ConvertedType: Type
      OptionalConverter: (Expr -> Expr) option
      ConversionCallingType: JsonConversionCallingType }

    member x.Convert = defaultArg x.OptionalConverter id

    member x.ConverterFunc ctx =
        ReflectionHelpers.makeDelegate x.Convert ctx.IJsonDocumentType

    member x.ConvertedTypeErased ctx =
        if x.ConvertedType.IsArray then
            match x.ConvertedType.GetElementType() with
            | :? ProvidedTypeDefinition -> ctx.IJsonDocumentType.MakeArrayType()
            | _ -> x.ConvertedType
        else
            match x.ConvertedType with
            | :? ProvidedTypeDefinition -> ctx.IJsonDocumentType
            | _ -> x.ConvertedType

module JsonTypeBuilder =

    let (?) = QuotationBuilder.(?)

    // check if a type was already created for the inferedType before creating a new one
    let internal getOrCreateType ctx inferedType createType =

        // normalize properties of the inferedType which don't affect code generation
        let rec normalize topLevel =
            function
            | InferedType.Heterogeneous(map, _) ->
                map
                |> Map.map (fun _ inferedType -> normalize false inferedType)
                |> (fun x -> InferedType.Heterogeneous(x, false))
            | InferedType.Collection(order, types) ->
                InferedType.Collection(
                    order,
                    Map.map (fun _ (multiplicity, inferedType) -> multiplicity, normalize false inferedType) types
                )
            | InferedType.Record(_, props, optional) ->
                let props =
                    props
                    |> List.map (fun { Name = name; Type = inferedType } ->
                        { Name = name
                          Type = normalize false inferedType })
                // optional only affects the parent, so at top level always set to true regardless of the actual value
                InferedType.Record(None, props, optional || topLevel)
            | InferedType.Primitive(typ, unit, optional, shouldOverrideOnMerge) when
                typ = typeof<Bit0> || typ = typeof<Bit1>
                ->
                InferedType.Primitive(typeof<int>, unit, optional, shouldOverrideOnMerge)
            | InferedType.Primitive(typ, unit, optional, shouldOverrideOnMerge) when typ = typeof<Bit> ->
                InferedType.Primitive(typeof<bool>, unit, optional, shouldOverrideOnMerge)
            | x -> x

        let inferedType = normalize true inferedType

        let typ =
            match ctx.TypeCache.TryGetValue inferedType with
            | true, typ -> typ
            | _ ->
                let typ = createType ()
                ctx.TypeCache.Add(inferedType, typ)
                typ

        { ConvertedType = typ
          OptionalConverter = None
          ConversionCallingType = JsonDocument }

    let internal replaceJDocWithJValue (ctx: JsonGenerationContext) (typ: Type) =
        if typ = ctx.IJsonDocumentType then
            ctx.JsonValueType
        elif typ.IsArray && typ.GetElementType() = ctx.IJsonDocumentType then
            ctx.JsonValueType.MakeArrayType()
        elif typ.IsGenericType && typ.GetGenericArguments() = [| ctx.IJsonDocumentType |] then
            typ.GetGenericTypeDefinition().MakeGenericType ctx.JsonValueType
        else
            typ

    /// Common code that is shared by code generators that generate
    /// "Choice" type. This is parameterized by the types (choices) to generate,
    /// by functions that get the multiplicity and the type tag for each option
    /// and also by function that generates the actual code.
    let rec internal generateMultipleChoiceType
        ctx
        types
        forCollection
        nameOverride
        (codeGenerator: _ -> _ -> _ -> _ -> Expr)
        =

        let types =
            types
            |> Seq.map (fun (KeyValue(tag, (multiplicity, inferedType))) -> tag, multiplicity, inferedType)
            |> Seq.sortBy (fun (tag, _, _) -> tag)
            |> Seq.toArray

        if types.Length <= 1 then
            failwithf "generateMultipleChoiceType: Invalid choice type: %A" types

        for _, _, inferedType in types do
            match inferedType with
            | InferedType.Null
            | InferedType.Top
            | InferedType.Heterogeneous _ -> failwithf "generateMultipleChoiceType: Unsupported type: %A" inferedType
            | x when x.IsOptional -> failwithf "generateMultipleChoiceType: Type shouldn't be optional: %A" inferedType
            | _ -> ()

        let typeName =
            if not (String.IsNullOrEmpty nameOverride) then
                nameOverride
            else
                let getTypeName (tag: InferedTypeTag, multiplicity, inferedType) =
                    match multiplicity with
                    | InferedMultiplicity.Multiple -> NameUtils.pluralize tag.NiceName
                    | InferedMultiplicity.OptionalSingle
                    | InferedMultiplicity.Single ->
                        match inferedType with
                        | InferedType.Primitive(typ, _, _, _) ->
                            if typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then
                                "Int"
                            elif typ = typeof<int64> then
                                "Int64"
                            elif typ = typeof<decimal> then
                                "Decimal"
                            elif typ = typeof<float> then
                                "Float"
                            else
                                tag.NiceName
                        | _ -> tag.NiceName

                types |> Array.map getTypeName |> String.concat "Or"
            |> ctx.UniqueNiceName

        // Generate new type for the heterogeneous type
        let objectTy =
            ProvidedTypeDefinition(typeName, Some ctx.IJsonDocumentType, hideObjectMethods = true, nonNullable = true)

        ctx.TypeProviderType.AddMember objectTy

        // to nameclash property names
        let makeUnique = NameUtils.uniqueGenerator NameUtils.nicePascalName
        makeUnique "JsonValue" |> ignore

        let members =
            [ for tag, multiplicity, inferedType in types ->

                  let result = generateJsonType ctx false false "" inferedType

                  let propName =
                      match tag with
                      | InferedTypeTag.Record _ -> "Record"
                      | _ -> tag.NiceName

                  let name, typ, constructorType =
                      match multiplicity with
                      | InferedMultiplicity.OptionalSingle ->
                          makeUnique propName,
                          ctx.MakeOptionType result.ConvertedType,
                          if forCollection then
                              ctx.MakeOptionType(replaceJDocWithJValue ctx result.ConvertedType)
                          else
                              replaceJDocWithJValue ctx result.ConvertedType
                      | InferedMultiplicity.Single ->
                          makeUnique propName, result.ConvertedType, replaceJDocWithJValue ctx result.ConvertedType
                      | InferedMultiplicity.Multiple ->
                          makeUnique (NameUtils.pluralize tag.NiceName),
                          result.ConvertedType.MakeArrayType(),
                          (replaceJDocWithJValue ctx result.ConvertedType).MakeArrayType()

                  ProvidedProperty(name, typ, getterCode = codeGenerator multiplicity result tag.Code),
                  ProvidedParameter(NameUtils.niceCamelName name, constructorType) ]

        let properties, parameters = List.unzip members
        objectTy.AddMembers properties

        if ctx.GenerateConstructors then

            let cultureStr = ctx.CultureStr

            if forCollection then
                let ctorCode (args: Expr list) =
                    let elements =
                        Expr.NewArray(typeof<obj>, args |> List.map (fun a -> Expr.Coerce(a, typeof<obj>)))

                    let cultureStr = ctx.CultureStr
                    <@@ JsonRuntime.CreateArray(%%elements, cultureStr) @@>

                let ctor = ProvidedConstructor(parameters, invokeCode = ctorCode)
                objectTy.AddMember ctor
            else
                for param in parameters do
                    let ctorCode (Singleton arg: Expr list) =
                        let arg = Expr.Coerce(arg, typeof<obj>)
                        <@@ JsonRuntime.CreateValue((%%arg: obj), cultureStr) @@>

                    let ctor = ProvidedConstructor([ param ], invokeCode = ctorCode)
                    objectTy.AddMember ctor

                let defaultCtor =
                    let ctorCode _ =
                        <@@ JsonRuntime.CreateValue(null :> obj, cultureStr) @@>

                    ProvidedConstructor([], invokeCode = ctorCode)

                objectTy.AddMember defaultCtor

            let ctorCode (Singleton arg) =
                <@@ JsonDocument.Create((%%arg: JsonValue), "") @@>

            let ctor =
                ProvidedConstructor([ ProvidedParameter("jsonValue", ctx.JsonValueType) ], invokeCode = ctorCode)

            objectTy.AddMember ctor

        objectTy

    /// Recursively walks over inferred type information and
    /// generates types for read-only access to the document
    and internal generateJsonType
        ctx
        canPassAllConversionCallingTypes
        optionalityHandledByParent
        nameOverride
        inferedType
        =

        let inferedType =
            match inferedType with
            | InferedType.Collection(order, types) ->
                InferedType.Collection(
                    List.filter ((<>) InferedTypeTag.Null) order,
                    Map.remove InferedTypeTag.Null types
                )
            | x -> x

        match inferedType with

        | InferedType.Primitive(inferedType, unit, optional, _) ->

            let typ, conv, conversionCallingType =
                PrimitiveInferedValue.Create(inferedType, optional, unit)
                |> convertJsonValue "" ctx.CultureStr canPassAllConversionCallingTypes ctx.ExceptionIfMissing

            { ConvertedType = typ
              OptionalConverter = Some conv
              ConversionCallingType = conversionCallingType }

        | InferedType.Top
        | InferedType.Null ->

            // Return the underlying JsonDocument without change
            { ConvertedType = ctx.IJsonDocumentType
              OptionalConverter = None
              ConversionCallingType = JsonDocument }

        | InferedType.Collection(_, SingletonMap(_, (_, typ)))
        | InferedType.Collection(_, EmptyMap InferedType.Top typ) ->

            let elementResult = generateJsonType ctx false false nameOverride typ

            let conv =
                fun (jDoc: Expr) ->
                    ctx.JsonRuntimeType?(nameof (JsonRuntime.ConvertArray))
                        (elementResult.ConvertedTypeErased ctx)
                        (jDoc, elementResult.ConverterFunc ctx)

            { ConvertedType = elementResult.ConvertedType.MakeArrayType()
              OptionalConverter = Some conv
              ConversionCallingType = JsonDocument }

        | InferedType.Record(name, props, optional) ->
            getOrCreateType ctx inferedType (fun () ->

                if optional && not optionalityHandledByParent then
                    failwithf "generateJsonType: optionality not handled for %A" inferedType

                let name =
                    if String.IsNullOrEmpty nameOverride then
                        match name with
                        | Some name -> name
                        | _ -> "Record"
                    else
                        nameOverride
                    |> ctx.UniqueNiceName

                // Generate new type for the record
                let objectTy =
                    ProvidedTypeDefinition(
                        name,
                        Some ctx.IJsonDocumentType,
                        hideObjectMethods = true,
                        nonNullable = true
                    )

                ctx.TypeProviderType.AddMember(objectTy)

                // to nameclash property names
                let nicePropertyName =
                    if ctx.UseOriginalNames then
                        id
                    else
                        NameUtils.nicePascalName

                let makeUnique = NameUtils.uniqueGenerator nicePropertyName
                makeUnique "JsonValue" |> ignore

                let inferedKeyValueType =
                    let aggr = List.fold (StructuralInference.subtypeInfered false) InferedType.Top

                    let dropRecordsNames infType =

                        let dropRecordName infType =
                            match infType with
                            | InferedType.Record(_, fields, opt) -> InferedType.Record(None, fields, opt)
                            | _ -> infType

                        let dropTagName tag =
                            match tag with
                            | InferedTypeTag.Record(Some _) -> InferedTypeTag.Record None
                            | _ -> tag

                        let infType = dropRecordName infType

                        match infType with
                        | InferedType.Collection(order, types) ->
                            // Records in collections have the parent property as name.
                            // We drop it too so they can be merged into a unified type.
                            let order = order |> List.map dropTagName

                            let types =
                                types
                                |> Map.toSeq
                                |> Seq.map (fun (tag, (multiplicity, typ)) ->
                                    let tag = dropTagName tag
                                    let typ = dropRecordName typ
                                    tag, (multiplicity, typ))
                                |> Map.ofSeq

                            InferedType.Collection(order, types)
                        | _ -> infType

                    if not ctx.PreferDictionaries then
                        None
                    else
                        let infKeyType =
                            [ for prop in props ->
                                  StructuralInference.getInferedTypeFromString
                                      ctx.UnitsOfMeasureProvider
                                      ctx.InferenceMode
                                      (TextRuntime.GetCulture ctx.CultureStr)
                                      prop.Name
                                      None ]
                            |> aggr

                        match infKeyType with
                        | InferedType.Primitive(typ = typ) when typ <> typeof<string> ->
                            let inferValueType =
                                ([ for prop in props -> prop.Type |> dropRecordsNames ] |> aggr)
                                    // Optional properties in the initial record should translate
                                    // to simply missing values in the dictionary, not an optional type.
                                    .DropOptionality()

                            Some(infKeyType, inferValueType)
                        | _ -> None

                match inferedKeyValueType with
                | Some(inferedKeyType, inferedValueType) ->
                    // Add all record fields as dictionary items
                    let valueName = name + "Value"

                    let keyResult = generateJsonType ctx false true "" inferedKeyType
                    let valueResult = generateJsonType ctx false true valueName inferedValueType
                    let valueConvertedTypeErased = valueResult.ConvertedTypeErased ctx

                    let tupleType =
                        Microsoft.FSharp.Reflection.FSharpType.MakeTupleType(
                            [| keyResult.ConvertedType; valueResult.ConvertedType |]
                        )

                    let itemsSeqType = typedefof<_ seq>.MakeGenericType([| tupleType |])

                    let itemsGetter (Singleton jDoc) =
                        ctx.JsonRuntimeType?(nameof (JsonRuntime.ConvertRecordToDictionary))
                            (keyResult.ConvertedType, valueConvertedTypeErased)
                            (jDoc, keyResult.ConverterFunc ctx, valueResult.ConverterFunc ctx)

                    let keysGetter (Singleton jDoc) =
                        ctx.JsonRuntimeType?(nameof (JsonRuntime.GetKeysFromInferedDictionary))
                            (keyResult.ConvertedType)
                            (jDoc, keyResult.ConverterFunc ctx)

                    let valuesGetter (Singleton jDoc) =
                        ctx.JsonRuntimeType?(nameof (JsonRuntime.GetValuesFromInferedDictionary))
                            (valueConvertedTypeErased)
                            (jDoc, valueResult.ConverterFunc ctx)

                    let (|Doubleton|) =
                        function
                        | [ f; s ] -> f, s
                        | _ -> failwith "Parameter mismatch"

                    let itemGetter (Doubleton(jDoc, key)) =
                        ctx.JsonRuntimeType?(nameof (JsonRuntime.GetValueByKeyFromInferedDictionary))
                            (keyResult.ConvertedType, valueConvertedTypeErased)
                            (jDoc, keyResult.ConverterFunc ctx, valueResult.ConverterFunc ctx, key)

                    let tryFindCode (Doubleton(jDoc, key)) =
                        ctx.JsonRuntimeType?(nameof (JsonRuntime.TryGetValueByKeyFromInferedDictionary))
                            (keyResult.ConvertedType, valueConvertedTypeErased)
                            (jDoc, keyResult.ConverterFunc ctx, valueResult.ConverterFunc ctx, key)

                    let containsKeyCode (Doubleton(jDoc, key)) =
                        ctx.JsonRuntimeType?(nameof (JsonRuntime.InferedDictionaryContainsKey))
                            (keyResult.ConvertedType)
                            (jDoc, keyResult.ConverterFunc ctx, key)

                    let countGetter (Singleton jDoc) =
                        <@@ JsonRuntime.GetRecordProperties(%%jDoc).Length @@>

                    let isEmptyGetter (Singleton jDoc) =
                        <@@ JsonRuntime.GetRecordProperties(%%jDoc).Length = 0 @@>

                    [ ProvidedProperty("Items", itemsSeqType, getterCode = itemsGetter)
                      ProvidedProperty("Keys", keyResult.ConvertedType.MakeArrayType(), getterCode = keysGetter)
                      ProvidedProperty("Values", valueResult.ConvertedType.MakeArrayType(), getterCode = valuesGetter)
                      ProvidedProperty(
                          "Item",
                          valueResult.ConvertedType,
                          getterCode = itemGetter,
                          indexParameters = [ ProvidedParameter("key", keyResult.ConvertedType) ]
                      )
                      ProvidedProperty("Count", typeof<int>, getterCode = countGetter)
                      ProvidedProperty("IsEmpty", typeof<bool>, getterCode = isEmptyGetter) ]
                    |> objectTy.AddMembers

                    [ ProvidedMethod(
                          "TryFind",
                          [ ProvidedParameter("key", keyResult.ConvertedType) ],
                          valueResult.ConvertedType |> ctx.MakeOptionType,
                          tryFindCode
                      )
                      ProvidedMethod(
                          "ContainsKey",
                          [ ProvidedParameter("key", keyResult.ConvertedType) ],
                          typeof<bool>,
                          containsKeyCode
                      ) ]
                    |> objectTy.AddMembers

                    if ctx.GenerateConstructors then
                        let conv (value: Expr) =
                            let value = ProviderHelpers.some keyResult.ConvertedType value

                            ConversionsGenerator.getBackConversionQuotation
                                ""
                                ctx.CultureStr
                                keyResult.ConvertedType
                                value
                            :> Expr

                        let ctorCode (args: Expr list) =
                            let kvSeq = args.Head
                            let convFunc = ReflectionHelpers.makeDelegate conv keyResult.ConvertedType

                            let cultureStr = ctx.CultureStr

                            ctx.JsonRuntimeType?(nameof (JsonRuntime.CreateRecordFromDictionary))
                                (keyResult.ConvertedType, valueConvertedTypeErased)
                                (kvSeq, cultureStr, convFunc)

                        let ctor =
                            ProvidedConstructor([ ProvidedParameter("items", itemsSeqType) ], ctorCode)

                        objectTy.AddMember ctor

                    ()
                | None ->
                    // Add all record fields as properties
                    let members =
                        [ for prop in props ->

                              let propResult = generateJsonType ctx true true "" prop.Type
                              let propName = prop.Name
                              let optionalityHandledByProperty = propResult.ConversionCallingType <> JsonDocument

                              let getter (Singleton jDoc) =

                                  if optionalityHandledByProperty then

                                      propResult.Convert
                                      <| if propResult.ConversionCallingType = JsonValueOptionAndPath then
                                             <@@ JsonRuntime.TryGetPropertyUnpackedWithPath(%%jDoc, propName) @@>
                                         else
                                             <@@ JsonRuntime.TryGetPropertyUnpacked(%%jDoc, propName) @@>

                                  elif prop.Type.IsOptional then

                                      match propResult.OptionalConverter with
                                      | Some _ ->
                                          //TODO: not covered in tests
                                          ctx.JsonRuntimeType?(nameof (JsonRuntime.ConvertOptionalProperty))
                                              (propResult.ConvertedTypeErased ctx)
                                              (jDoc, propName, propResult.ConverterFunc ctx)

                                      | None -> <@@ JsonRuntime.TryGetPropertyPacked(%%jDoc, propName) @@>

                                  else

                                      propResult.Convert
                                      <| match prop.Type with
                                         | InferedType.Collection _
                                         | InferedType.Heterogeneous _
                                         | InferedType.Top
                                         | InferedType.Null ->
                                             <@@ JsonRuntime.GetPropertyPackedOrNull(%%jDoc, propName) @@>
                                         | _ -> <@@ JsonRuntime.GetPropertyPacked(%%jDoc, propName) @@>

                              let convertedType =
                                  if prop.Type.IsOptional && not optionalityHandledByProperty then
                                      ctx.MakeOptionType propResult.ConvertedType
                                  else
                                      propResult.ConvertedType

                              let name = makeUnique prop.Name

                              (prop.Name, name),
                              [ ProvidedProperty(name, convertedType, getterCode = getter) ],
                              ProvidedParameter(
                                  (if ctx.UseOriginalNames then
                                       name
                                   else
                                       NameUtils.niceCamelName name),
                                  replaceJDocWithJValue ctx convertedType
                              ) ]

                    let namePairs, properties, parameters = List.unzip3 members
                    let names = namePairs |> List.map fst
                    let properties = properties |> List.concat
                    objectTy.AddMembers properties

                    if ctx.GenerateConstructors then
                        let ctorCode (args: Expr list) =
                            let properties =
                                Expr.NewArray(
                                    typeof<string * obj>,
                                    args
                                    |> List.mapi (fun i a ->
                                        Expr.NewTuple [ Expr.Value names.[i]; Expr.Coerce(a, typeof<obj>) ])
                                )

                            let cultureStr = ctx.CultureStr

                            if ctx.OmitNullFields then
                                <@@ JsonRuntime.CreateRecordOmitNulls(%%properties, cultureStr) @@>
                            else
                                <@@ JsonRuntime.CreateRecord(%%properties, cultureStr) @@>

                        let ctor = ProvidedConstructor(parameters, invokeCode = ctorCode)
                        objectTy.AddMember ctor

                        // Add With* methods: WithPropName(newValue) returns a new record with one field updated
                        for i, (rawName, displayName) in List.indexed namePairs do
                            let param = parameters.[i]
                            let cultureStr = ctx.CultureStr

                            let withMethod =
                                ProvidedMethod(
                                    "With" + displayName,
                                    [ ProvidedParameter(param.Name, param.ParameterType) ],
                                    objectTy,
                                    invokeCode =
                                        fun args ->
                                            let jDoc = args.[0]
                                            let newVal = args.[1]
                                            let newValObj = Expr.Coerce(newVal, typeof<obj>)

                                            <@@
                                                JsonRuntime.WithRecordProperty(
                                                    (%%jDoc: IJsonDocument),
                                                    rawName,
                                                    %%newValObj,
                                                    cultureStr
                                                )
                                            @@>
                                )

                            objectTy.AddMember withMethod

                    ()

                if ctx.GenerateConstructors then
                    let ctorCode (Singleton arg: Expr list) =
                        <@@ JsonDocument.Create((%%arg: JsonValue), "") @@>

                    let ctorParams = [ ProvidedParameter("jsonValue", ctx.JsonValueType) ]
                    let ctor = ProvidedConstructor(ctorParams, ctorCode)
                    objectTy.AddMember ctor

                objectTy)

        | InferedType.Collection(_, types) ->
            getOrCreateType ctx inferedType (fun () ->

                // Generate a choice type that calls either `GetArrayChildrenByTypeTag`
                // or `GetArrayChildByTypeTag`, depending on the multiplicity of the item
                generateMultipleChoiceType ctx types true nameOverride (fun multiplicity result tagCode ->
                    match multiplicity with
                    | InferedMultiplicity.Single ->
                        fun (Singleton jDoc) ->
                            // Generate method that calls `GetArrayChildByTypeTag`
                            let cultureStr = ctx.CultureStr
                            result.Convert <@@ JsonRuntime.GetArrayChildByTypeTag(%%jDoc, cultureStr, tagCode) @@>

                    | InferedMultiplicity.Multiple ->
                        fun (Singleton jDoc) ->
                            // Generate method that calls `GetArrayChildrenByTypeTag`
                            // (unlike the previous easy case, this needs to call conversion function
                            // from the runtime similarly to options and arrays)
                            let cultureStr = ctx.CultureStr

                            ctx.JsonRuntimeType?(nameof (JsonRuntime.GetArrayChildrenByTypeTag))
                                (result.ConvertedTypeErased ctx)
                                (jDoc, cultureStr, tagCode, result.ConverterFunc ctx)

                    | InferedMultiplicity.OptionalSingle ->
                        fun (Singleton jDoc) ->
                            // Similar to the previous case, but call `TryGetArrayChildByTypeTag`
                            let cultureStr = ctx.CultureStr

                            ctx.JsonRuntimeType?(nameof (JsonRuntime.TryGetArrayChildByTypeTag))
                                (result.ConvertedTypeErased ctx)
                                (jDoc, cultureStr, tagCode, result.ConverterFunc ctx)))

        | InferedType.Heterogeneous(types, _) ->
            getOrCreateType ctx inferedType (fun () ->

                // Generate a choice type that always calls `TryGetValueByTypeTag`
                let types = types |> Map.map (fun _ v -> InferedMultiplicity.OptionalSingle, v)

                generateMultipleChoiceType ctx types false nameOverride (fun multiplicity result tagCode ->
                    fun (Singleton jDoc) ->
                        assert (multiplicity = InferedMultiplicity.OptionalSingle)
                        let cultureStr = ctx.CultureStr

                        ctx.JsonRuntimeType?(nameof (JsonRuntime.TryGetValueByTypeTag))
                            (result.ConvertedTypeErased ctx)
                            (jDoc, cultureStr, tagCode, result.ConverterFunc ctx)))

        | InferedType.Json _ -> failwith "Json type not supported"
