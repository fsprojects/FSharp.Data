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

type internal JsonGenerationInput = 
    { Optional : bool
      CanPassAllConversionCallingTypes : bool }

type internal JsonGenerationOutput = 
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
  
  open FSharp.Data.JsonExtensions
  let (?) = QuotationBuilder.(?)

  // check if a type was already created for the inferedType before creating a new one
  let internal getOrCreateType ctx inferedType createType =
    
    // normalize properties of the inferedType which don't affect code generation
    let rec normalize = function
    | InferedType.Heterogeneous map -> 
        map 
        |> Map.remove InferedTypeTag.Null 
        |> Map.map (fun _ inferedType -> normalize inferedType) 
        |> InferedType.Heterogeneous
    | InferedType.Collection map -> 
        map 
        |> Map.remove InferedTypeTag.Null 
        |> Map.map (fun _ (multiplicity, inferedType) -> multiplicity, normalize inferedType) 
        |> InferedType.Collection
    | InferedType.Record (_, props) -> 
        let props = 
          props
          |> List.map (fun { Name = name; Optional = optional; Type = inferedType } -> { Name = name; Optional = optional; Type = normalize inferedType })
        InferedType.Record (None, props)
    | InferedType.Primitive (typ, unit) when typ = typeof<Bit0> || typ = typeof<Bit1> -> InferedType.Primitive (typeof<int>, unit)
    | InferedType.Primitive (typ, unit) when typ = typeof<Bit> -> InferedType.Primitive (typeof<bool>, unit)
    | x -> x

    let inferedType = normalize inferedType
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

    // Generate GetXyz(s) method for every different case
    // (but skip all Null nodes - we simply ignore them)
    let types = 
      types
      |> Seq.map (fun (KeyValue(tag, (multiplicity, inferedType))) -> tag, multiplicity, inferedType)
      |> Seq.filter (fun (tag, _, _) -> tag <> InferedTypeTag.Null)
      |> Seq.sortBy (fun (tag, _, _) -> tag)
      |> Seq.toArray

    let getTypeName (tag:InferedTypeTag, multiplicity, inferedType)  =
      match multiplicity with
      | InferedMultiplicity.Multiple -> NameUtils.pluralize tag.NiceName
      | InferedMultiplicity.OptionalSingle | InferedMultiplicity.Single -> 
          match inferedType with
          | InferedType.Primitive(typ, _) ->
              if typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then "Int"
              elif typ = typeof<int64> then "Int64"
              elif typ = typeof<decimal> then "Decimal"
              elif typ = typeof<float> then "Float"
              else tag.NiceName
          | _ -> tag.NiceName

    let typeName = 
        if types.Length = 1
        then getTypeName types.[0] + "Choice"
        else
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
        let input = { CanPassAllConversionCallingTypes = false
                      Optional = false }
        let output = generateJsonType ctx input inferedType
      
        let propName =
            match tag with
            | InferedTypeTag.Record _ -> "Record"
            | _ -> tag.NiceName

        // If it occurs at most once, then generate property (which may 
        // be optional). For multiple occurrences, generate method
        match multiplicity with 
        | InferedMultiplicity.OptionalSingle ->
            ProvidedProperty(makeUnique propName, 
                             typedefof<option<_>>.MakeGenericType [| output.ConvertedType |], 
                             GetterCode = codeGenerator multiplicity output tag.Code)
        | InferedMultiplicity.Single ->
            ProvidedProperty(makeUnique propName, 
                             output.ConvertedType, 
                             GetterCode = codeGenerator multiplicity output tag.Code)
        | InferedMultiplicity.Multiple ->
            ProvidedProperty(makeUnique (NameUtils.pluralize tag.NiceName),
                             output.ConvertedType.MakeArrayType(), 
                             GetterCode = codeGenerator multiplicity output tag.Code)]
    objectTy

  /// Recursively walks over inferred type information and 
  /// generates types for read-only access to the document
  and internal generateJsonType ctx input = function

    | InferedType.Primitive(inferedType, _) ->

        let typ, conv, conversionCallingType = 
            PrimitiveInferedProperty.Create("", inferedType, input.Optional)
            |> convertJsonValue ctx.Replacer "" ctx.CultureStr input.CanPassAllConversionCallingTypes

        { ConvertedType = typ
          Converter = Some (ctx.Replacer.ToDesignTime >> conv)
          ConversionCallingType = conversionCallingType }

    | InferedType.Top | InferedType.Null | InferedType.Heterogeneous EmptyMap | InferedType.Collection EmptyMap -> 

        // Return the underlying JsonDocument without change
        { ConvertedType = ctx.IJsonDocumentType
          Converter = None
          ConversionCallingType = JsonDocument }

    | InferedType.Collection (SingletonMap(_, (_, typ))) -> 

        // TODO: handle input.Optional

        let elementInput = { CanPassAllConversionCallingTypes = false
                             Optional = false }
        let elementOutput = generateJsonType ctx elementInput typ

        // the async version is only used when the top level element returned by Parse/Load is an array
        let conv = fun (jDoc:Expr) -> 
          ctx.JsonRuntimeType?ConvertArray (elementOutput.ConvertedTypeErased ctx) (ctx.Replacer.ToRuntime jDoc, elementOutput.ConverterFunc ctx)
        
        { ConvertedType = elementOutput.ConvertedType.MakeArrayType()
          Converter = Some conv
          ConversionCallingType = JsonDocument }

    | InferedType.Record(name, props) as inferedType -> getOrCreateType ctx inferedType <| fun () ->

        // Generate new type for the record
        let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName (match name with Some name -> name | _ -> "Root"), Some(ctx.IJsonDocumentType), HideObjectMethods = true)
        ctx.TypeProviderType.AddMember(objectTy)

        //TODO: handle input.Optional (#163)

        // to nameclash property names
        let makeUnique = NameUtils.uniqueGenerator NameUtils.nicePascalName
        makeUnique "JsonValue" |> ignore

        // Add all record fields as properties
        for prop in props do
  
          let propInput = { CanPassAllConversionCallingTypes = true
                            Optional = prop.Optional }
          let propOutput = generateJsonType ctx propInput prop.Type

          let getter = fun (Singleton jDoc) -> 

            let propName = prop.Name

            if propOutput.ConversionCallingType <> JsonDocument then

              let jDoc = ctx.Replacer.ToDesignTime jDoc
              propOutput.GetConverter ctx <|
                if propOutput.ConversionCallingType = JsonValueOptionAndPath then
                  <@@ JsonRuntime.TryGetPropertyUnpackedWithPath(%%jDoc, propName) @@>
                else
                  <@@ JsonRuntime.TryGetPropertyUnpacked(%%jDoc, propName) @@>                
          
            elif prop.Optional then
              
              match propOutput.Converter with
              | Some _ ->
                  ctx.JsonRuntimeType?ConvertOptionalProperty (propOutput.ConvertedTypeErased ctx) (jDoc, propName, propOutput.ConverterFunc ctx) :> Expr

              | None -> 
                  let jDoc = ctx.Replacer.ToDesignTime jDoc
                  ctx.Replacer.ToRuntime <@@ JsonRuntime.TryGetPropertyPacked(%%jDoc, propName) @@>
          
            else
          
              let jDoc = ctx.Replacer.ToDesignTime jDoc
              propOutput.GetConverter ctx <@@ JsonRuntime.GetPropertyPacked(%%jDoc, propName) @@>

          let optionalityAlreadyHandled = propOutput.ConversionCallingType <> JsonDocument          
          let convertedType = 
            if prop.Optional && not optionalityAlreadyHandled 
            then typedefof<option<_>>.MakeGenericType propOutput.ConvertedType
            else propOutput.ConvertedType

          objectTy.AddMember <| ProvidedProperty(makeUnique prop.Name, convertedType, GetterCode = getter)
        
        objectTy

    | InferedType.Collection types as inferedType -> getOrCreateType ctx inferedType <| fun () ->

        //TODO: handle input.Optional

        // Generate a choice type that calls either `GetArrayChildrenByTypeTag`
        // or `GetArrayChildByTypeTag`, depending on the multiplicity of the item
        generateMultipleChoiceType ctx types (fun multiplicity output tagCode ->
          match multiplicity with
          | InferedMultiplicity.Single -> fun (Singleton jDoc) -> 
              // Generate method that calls `GetArrayChildByTypeTag`
              let jDoc = ctx.Replacer.ToDesignTime jDoc
              let cultureStr = ctx.CultureStr
              output.GetConverter ctx <@@ JsonRuntime.GetArrayChildByTypeTag(%%jDoc, cultureStr, tagCode) @@>
          
          | InferedMultiplicity.Multiple -> 
              // Generate method that calls `GetArrayChildrenByTypeTag` 
              // (unlike the previous easy case, this needs to call conversion function
              // from the runtime similarly to options and arrays) 
              fun (Singleton jDoc) -> 
                let cultureStr = ctx.CultureStr
                ctx.JsonRuntimeType?GetArrayChildrenByTypeTag (output.ConvertedTypeErased ctx) (jDoc, cultureStr, tagCode, output.ConverterFunc ctx)
          
          | InferedMultiplicity.OptionalSingle -> 
              // Similar to the previous case, but call `TryGetArrayChildByTypeTag` 
              fun (Singleton jDoc) -> 
                let cultureStr = ctx.CultureStr
                ctx.JsonRuntimeType?TryGetArrayChildByTypeTag (output.ConvertedTypeErased ctx) (jDoc, cultureStr, tagCode, output.ConverterFunc ctx))

    | InferedType.Heterogeneous types as inferedType -> getOrCreateType ctx inferedType <| fun () ->

        //TODO: handle input.Optional

        // Generate a choice type that always calls `TryGetValueByTypeTag`
        let types = types |> Map.map (fun _ v -> InferedMultiplicity.OptionalSingle, v)
        generateMultipleChoiceType ctx types (fun multiplicity output tagCode ->
          assert (multiplicity = InferedMultiplicity.OptionalSingle)
          fun (Singleton jDoc) -> 
            let cultureStr = ctx.CultureStr
            ctx.JsonRuntimeType?TryGetValueByTypeTag (output.ConvertedTypeErased ctx) (jDoc, cultureStr, tagCode, output.ConverterFunc ctx))
