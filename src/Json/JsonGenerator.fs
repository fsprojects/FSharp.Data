// --------------------------------------------------------------------------------------
// JSON type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Collections.Generic
open System.Reflection
open Microsoft.FSharp.Quotations
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open ProviderImplementation.JsonInference
open ProviderImplementation.JsonConversionsGenerator
open ProviderImplementation.ProvidedTypes

#nowarn "10001"

/// Context that is used to generate the JSON types.
type internal JsonGenerationContext =
  { CultureStr : string
    TypeProviderType : ProvidedTypeDefinition
    Replacer : AssemblyReplacer 
    // to nameclash type names
    UniqueNiceName : string -> string 
    IJsonDocumentType : Type
    JsonRuntimeType : Type
    // the type that is used to represent documents (JsonDocument or ApiaryDocument)
    Representation : Type
    TypeCache : Dictionary<InferedType, ProvidedTypeDefinition> }
  static member Create(cultureStr, tpType, replacer) =
    let uniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName
    JsonGenerationContext.Create(cultureStr, tpType, typeof<JsonDocument>, replacer, uniqueNiceName)
  static member internal Create(cultureStr, tpType, representation, replacer, uniqueNiceName) =
    { CultureStr = cultureStr
      TypeProviderType = tpType
      Replacer = replacer 
      UniqueNiceName = uniqueNiceName 
      IJsonDocumentType = replacer.ToRuntime typeof<IJsonDocument>
      JsonRuntimeType = replacer.ToRuntime typeof<JsonRuntime>
      Representation = replacer.ToRuntime representation
      TypeCache = Dictionary() }

type internal JsonGenerationResult = 
    { ConvertedType : Type
      Converter : (Expr -> Expr) option
      ConversionCallingType : JsonConversionCallingType }
    member x.GetConverter ctx = 
        defaultArg x.Converter ctx.Replacer.ToRuntime
    member x.ConverterFunc ctx =
      ReflectionHelpers.makeDelegate (x.GetConverter ctx) ctx.IJsonDocumentType
    member x.ConvertedTypeErased ctx =
      if x.ConvertedType.IsArray then
        match x.ConvertedType.GetElementType() with
        | :? ProvidedTypeDefinition -> ctx.IJsonDocumentType.MakeArrayType()
        | x when x = ctx.Representation -> ctx.IJsonDocumentType.MakeArrayType()
        | _ -> x.ConvertedType
      else
        match x.ConvertedType with
        | :? ProvidedTypeDefinition -> ctx.IJsonDocumentType
        | x when x = ctx.Representation -> ctx.IJsonDocumentType
        | _ -> x.ConvertedType

module JsonTypeBuilder = 
  
  let (?) = QuotationBuilder.(?)

  // check if a type was already created for the inferedType before creating a new one
  let internal getOrCreateType ctx inferedType createType =
    
    // normalize properties of the inferedType which don't affect code generation
    let rec normalize topLevel = function
    | InferedType.Heterogeneous map -> 
        map 
        |> Map.map (fun _ inferedType -> normalize false inferedType) 
        |> InferedType.Heterogeneous
    | InferedType.Collection map -> 
        map 
        |> Map.map (fun _ (multiplicity, inferedType) -> multiplicity, normalize false inferedType) 
        |> InferedType.Collection
    | InferedType.Record (_, props, optional) -> 
        let props = 
          props
          |> List.map (fun { Name = name; Type = inferedType } -> { Name = name; Type = normalize false inferedType })
        // optional only affects the parent, so at top level always set to true regardless of the actual value
        InferedType.Record (None, props, optional || topLevel)
    | InferedType.Primitive (typ, unit, optional) when typ = typeof<Bit0> || typ = typeof<Bit1> -> InferedType.Primitive (typeof<int>, unit, optional)
    | InferedType.Primitive (typ, unit, optional) when typ = typeof<Bit> -> InferedType.Primitive (typeof<bool>, unit, optional)
    | x -> x

    let inferedType = normalize true inferedType
    let typ = 
      match ctx.TypeCache.TryGetValue inferedType with
      | true, typ -> typ
      | _ -> 
        let typ = createType()
        ctx.TypeCache.Add(inferedType, typ)
        typ

    { ConvertedType = typ
      Converter = None
      ConversionCallingType = JsonDocument }

  /// Common code that is shared by code generators that generate 
  /// "Choice" type. This is parameterized by the types (choices) to generate,
  /// by functions that get the multiplicity and the type tag for each option
  /// and also by function that generates the actual code.
  let rec internal generateMultipleChoiceType ctx types codeGenerator =

    let types = 
      types
      |> Seq.map (fun (KeyValue(tag, (multiplicity, inferedType))) -> tag, multiplicity, inferedType)
      |> Seq.sortBy (fun (tag, _, _) -> tag)
      |> Seq.toArray

    if types.Length <= 1 then failwithf "generateMultipleChoiceType: Invalid choice type: %A" types
    
    for _, _, inferedType in types do
        match inferedType with
        | InferedType.Null | InferedType.Top | InferedType.Heterogeneous _ -> 
            failwithf "generateMultipleChoiceType: Unsupported type: %A" inferedType
        | x when x.IsOptional -> 
            failwithf "generateMultipleChoiceType: Type shouldn't be optional: %A" inferedType
        | _ -> ()

    let getTypeName (tag:InferedTypeTag, multiplicity, inferedType)  =
      match multiplicity with
      | InferedMultiplicity.Multiple -> NameUtils.pluralize tag.NiceName
      | InferedMultiplicity.OptionalSingle | InferedMultiplicity.Single -> 
          match inferedType with
          | InferedType.Primitive(typ, _, _) ->
              if typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then "Int"
              elif typ = typeof<int64> then "Int64"
              elif typ = typeof<decimal> then "Decimal"
              elif typ = typeof<float> then "Float"
              else tag.NiceName
          | _ -> tag.NiceName

    let typeName = 
        types 
        |> Array.map getTypeName
        |> String.concat "Or"
        |> ctx.UniqueNiceName

    // Generate new type for the heterogeneous type
    let objectTy = ProvidedTypeDefinition(typeName, Some(ctx.IJsonDocumentType), HideObjectMethods = true)
    ctx.TypeProviderType.AddMember objectTy

    // to nameclash property names
    let makeUnique = NameUtils.uniqueGenerator NameUtils.nicePascalName
    makeUnique "JsonValue" |> ignore

    objectTy.AddMembers
      [ for tag, multiplicity, inferedType in types ->

          let result = generateJsonType ctx (*canPassAllConversionCallingTypes*)false (*optionalityHandledByParent*)false inferedType
          
          let propName =
              match tag with
              | InferedTypeTag.Record _ -> "Record"
              | _ -> tag.NiceName
          
          // If it occurs at most once, then generate property (which may 
          // be optional). For multiple occurrences, generate method
          match multiplicity with 
          | InferedMultiplicity.OptionalSingle ->
              ProvidedProperty(makeUnique propName, 
                               typedefof<option<_>>.MakeGenericType [| result.ConvertedType |], 
                               GetterCode = codeGenerator multiplicity result tag.Code)
          | InferedMultiplicity.Single ->
              ProvidedProperty(makeUnique propName, 
                               result.ConvertedType, 
                               GetterCode = codeGenerator multiplicity result tag.Code)
          | InferedMultiplicity.Multiple ->
              ProvidedProperty(makeUnique (NameUtils.pluralize tag.NiceName),
                               result.ConvertedType.MakeArrayType(), 
                               GetterCode = codeGenerator multiplicity result tag.Code)
      ]

    objectTy

  /// Recursively walks over inferred type information and 
  /// generates types for read-only access to the document
  and internal generateJsonType ctx canPassAllConversionCallingTypes optionalityHandledByParent inferedType =

    let inferedType = 
      match inferedType with
      | InferedType.Collection types ->
          types 
          |> Map.remove InferedTypeTag.Null 
          |> InferedType.Collection 
      | x -> x

    match inferedType with

    | InferedType.Primitive(inferedType, unit, optional) ->

        let typ, conv, conversionCallingType = 
            PrimitiveInferedProperty.Create("", inferedType, optional, unit)
            |> convertJsonValue ctx.Replacer "" ctx.CultureStr canPassAllConversionCallingTypes

        { ConvertedType = typ
          Converter = Some (ctx.Replacer.ToDesignTime >> conv)
          ConversionCallingType = conversionCallingType }

    | InferedType.Top 
    | InferedType.Null -> 

        // Return the underlying JsonDocument without change
        { ConvertedType = ctx.IJsonDocumentType
          Converter = None
          ConversionCallingType = JsonDocument }

    | InferedType.Collection (SingletonMap(_, (_, typ)))
    | InferedType.Collection (EmptyMap InferedType.Top typ) -> 

        let elementResult = generateJsonType ctx (*canPassAllConversionCallingTypes*)false (*optionalityHandledByParent*)false typ

        let conv = fun (jDoc:Expr) -> 
          ctx.JsonRuntimeType?ConvertArray (elementResult.ConvertedTypeErased ctx) (ctx.Replacer.ToRuntime jDoc, elementResult.ConverterFunc ctx)
        
        { ConvertedType = elementResult.ConvertedType.MakeArrayType()
          Converter = Some conv
          ConversionCallingType = JsonDocument }

    | InferedType.Record(name, props, optional) -> getOrCreateType ctx inferedType <| fun () ->
        
        if optional && not optionalityHandledByParent then
          failwith "generateJsonType: optionality not handled for %A" inferedType

        // Generate new type for the record
        let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName (match name with Some name -> name | _ -> "Root"), Some(ctx.IJsonDocumentType), HideObjectMethods = true)
        ctx.TypeProviderType.AddMember(objectTy)

        // to nameclash property names
        let makeUnique = NameUtils.uniqueGenerator NameUtils.nicePascalName
        makeUnique "JsonValue" |> ignore

        // Add all record fields as properties
        for prop in props do
  
          let propResult = generateJsonType ctx (*canPassAllConversionCallingTypes*)true (*optionalityHandledByParent*)true prop.Type
          let propName = prop.Name
          let optionalityHandledByProperty = propResult.ConversionCallingType <> JsonDocument

          let getter = fun (Singleton jDoc) -> 

            if optionalityHandledByProperty then 

              let jDoc = ctx.Replacer.ToDesignTime jDoc
              propResult.GetConverter ctx <|
                if propResult.ConversionCallingType = JsonValueOptionAndPath then
                  <@@ JsonRuntime.TryGetPropertyUnpackedWithPath(%%jDoc, propName) @@>
                else
                  <@@ JsonRuntime.TryGetPropertyUnpacked(%%jDoc, propName) @@>
          
            elif prop.Type.IsOptional then
              
              match propResult.Converter with
              | Some _ ->
                  //TODO: not covered in tests
                  ctx.JsonRuntimeType?ConvertOptionalProperty (propResult.ConvertedTypeErased ctx) (jDoc, propName, propResult.ConverterFunc ctx) :> Expr

              | None ->
                  let jDoc = ctx.Replacer.ToDesignTime jDoc
                  ctx.Replacer.ToRuntime <@@ JsonRuntime.TryGetPropertyPacked(%%jDoc, propName) @@>
          
            else

              let jDoc = ctx.Replacer.ToDesignTime jDoc
              propResult.GetConverter ctx <|
                match prop.Type with
                | InferedType.Collection _ 
                | InferedType.Heterogeneous _ 
                | InferedType.Top 
                | InferedType.Null -> <@@ JsonRuntime.GetPropertyPackedOrNull(%%jDoc, propName) @@>
                | _ -> <@@ JsonRuntime.GetPropertyPacked(%%jDoc, propName) @@>

          let convertedType = 
            if prop.Type.IsOptional && not optionalityHandledByProperty 
            then typedefof<option<_>>.MakeGenericType propResult.ConvertedType
            else propResult.ConvertedType

          objectTy.AddMember <| ProvidedProperty(makeUnique prop.Name, convertedType, GetterCode = getter)
        
        objectTy

    | InferedType.Collection types -> getOrCreateType ctx inferedType <| fun () ->

        // Generate a choice type that calls either `GetArrayChildrenByTypeTag`
        // or `GetArrayChildByTypeTag`, depending on the multiplicity of the item
        generateMultipleChoiceType ctx types (fun multiplicity result tagCode ->
          match multiplicity with
          | InferedMultiplicity.Single -> fun (Singleton jDoc) -> 
              // Generate method that calls `GetArrayChildByTypeTag`
              let jDoc = ctx.Replacer.ToDesignTime jDoc
              let cultureStr = ctx.CultureStr
              result.GetConverter ctx <@@ JsonRuntime.GetArrayChildByTypeTag(%%jDoc, cultureStr, tagCode) @@>
          
          | InferedMultiplicity.Multiple -> fun (Singleton jDoc) -> 
              // Generate method that calls `GetArrayChildrenByTypeTag` 
              // (unlike the previous easy case, this needs to call conversion function
              // from the runtime similarly to options and arrays)
              let cultureStr = ctx.CultureStr
              ctx.JsonRuntimeType?GetArrayChildrenByTypeTag (result.ConvertedTypeErased ctx) (jDoc, cultureStr, tagCode, result.ConverterFunc ctx)
          
          | InferedMultiplicity.OptionalSingle -> fun (Singleton jDoc) -> 
              // Similar to the previous case, but call `TryGetArrayChildByTypeTag`
              let cultureStr = ctx.CultureStr
              ctx.JsonRuntimeType?TryGetArrayChildByTypeTag (result.ConvertedTypeErased ctx) (jDoc, cultureStr, tagCode, result.ConverterFunc ctx))

    | InferedType.Heterogeneous types -> getOrCreateType ctx inferedType <| fun () ->

        // Generate a choice type that always calls `TryGetValueByTypeTag`
        let types = types |> Map.map (fun _ v -> InferedMultiplicity.OptionalSingle, v)
        generateMultipleChoiceType ctx types (fun multiplicity result tagCode -> fun (Singleton jDoc) -> 
          assert (multiplicity = InferedMultiplicity.OptionalSingle)
          let cultureStr = ctx.CultureStr
          ctx.JsonRuntimeType?TryGetValueByTypeTag (result.ConvertedTypeErased ctx) (jDoc, cultureStr, tagCode, result.ConverterFunc ctx))
