// --------------------------------------------------------------------------------------
// JSON type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open Microsoft.FSharp.Quotations
open FSharp.Data
open FSharp.Data.Json
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open ProviderImplementation.JsonInference
open ProviderImplementation.ProvidedTypes

#nowarn "10001"

/// Context that is used to generate the JSON types.
type internal JsonGenerationContext =
  { Culture : string
    TypeProviderType : ProvidedTypeDefinition
    Replacer : AssemblyReplacer 
    UniqueNiceName : string -> string 
    IJsonDocumentType : Type
    JsonRuntimeType : Type
    // the type that is used to represent documents (JsonDocument or ApiaryDocument)
    Representation : Type }
  static member Create(culture, tpType, replacer) =
    let uniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName
    JsonGenerationContext.Create(culture, tpType, typeof<JsonDocument>, replacer, uniqueNiceName)
  static member internal Create(culture, tpType, representation, replacer, uniqueNiceName) =
    { Culture = culture
      TypeProviderType = tpType
      Replacer = replacer 
      UniqueNiceName = uniqueNiceName 
      IJsonDocumentType = replacer.ToRuntime typeof<IJsonDocument>
      JsonRuntimeType = replacer.ToRuntime typeof<JsonRuntime>
      Representation = replacer.ToRuntime representation }

type internal JsonGenerationInput = 
    { ParentName : string
      Optional : bool
      // if true, output.Converter will get a JsonValue option, otherwise it will get a JsonDocument
      CanPassUnpackedOption : bool }

type internal JsonGenerationOutput = 
    { ConvertedType : Type
      Converter : Expr -> Expr
      ConverterRequiresUnpackedOption : bool }
    member x.ConverterFunc ctx =
      ReflectionHelpers.makeDelegate x.Converter ctx.IJsonDocumentType
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
  
  open FSharp.Data.Json.Extensions
  let (?) = QuotationBuilder.(?)

  /// Common code that is shared by code generators that generate 
  /// "Choice" type. This is parameterized by the types (choices) to generate,
  /// by functions that get the multiplicity and the type tag for each option
  /// and also by function that generates the actual code.
  let rec internal generateMultipleChoiceType ctx parentName types codeGenerator =
    // Generate new type for the heterogeneous type
    let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName (parentName + "Choice"), Some(ctx.IJsonDocumentType), HideObjectMethods = true)
    ctx.TypeProviderType.AddMember(objectTy)
        
    let makeUnique = NameUtils.uniqueGenerator NameUtils.nicePascalName
    makeUnique "JsonValue" |> ignore

    // Generate GetXyz(s) method for every different case
    // (but skip all Null nodes - we simply ignore them)
    let types = 
      types 
      |> Seq.map (fun (KeyValue(kind, (multiplicity, typ))) -> kind, multiplicity, typ)
      |> Seq.filter (fun (kind, _, _) -> kind <> InferedTypeTag.Null)
      |> Seq.mapi (fun index (kind, multiplicity, typ) -> index + 1, kind, multiplicity, typ)
      |> Seq.toArray
    for index, kind, multiplicity, typ in types do
      let input = { ParentName = sprintf "%sChoice%dof%d" parentName index types.Length
                    CanPassUnpackedOption = false
                    Optional = false }
      let output = generateJsonType ctx input typ
      let kindCode = kind.Code

      // If it occurs at most once, then generate property (which may 
      // be optional). For multiple occurrences, generate method
      match multiplicity with 
      | InferedMultiplicity.OptionalSingle ->
          let p = ProvidedProperty(makeUnique kind.NiceName, typedefof<option<_>>.MakeGenericType [| output.ConvertedType |])
          p.GetterCode <- codeGenerator (multiplicity, typ) output kindCode
          objectTy.AddMember(p)          
      | InferedMultiplicity.Single ->
          let p = ProvidedProperty(makeUnique kind.NiceName, output.ConvertedType)
          p.GetterCode <- codeGenerator (multiplicity, typ) output kindCode
          objectTy.AddMember(p)          
      | InferedMultiplicity.Multiple ->
          let p = ProvidedMethod(makeUnique ("Get" + NameUtils.pluralize kind.NiceName), [], output.ConvertedType.MakeArrayType())
          p.InvokeCode <- codeGenerator (multiplicity, typ) output kindCode
          objectTy.AddMember(p)          

    { ConvertedType = objectTy
      Converter = ctx.Replacer.ToRuntime
      ConverterRequiresUnpackedOption = false }

  /// Recursively walks over inferred type information and 
  /// generates types for read-only access to the document
  and internal generateJsonType ctx input = function
    | InferedType.Primitive(inferedTyp, _) ->
        let typ, conv = 
            PrimitiveInferedProperty.Create(input.ParentName, inferedTyp, input.Optional)
            |> JsonConversionsGenerator.convertJsonValue ctx.Replacer "" ctx.Culture
        let jDocToJsonOpt = fun x -> <@ Some (%%x:IJsonDocument).JsonValue @>
        { ConvertedType = typ
          Converter = if input.CanPassUnpackedOption
                      then ctx.Replacer.ToDesignTime >> Expr.Cast >> conv
                      else ctx.Replacer.ToDesignTime >> jDocToJsonOpt >> conv
          ConverterRequiresUnpackedOption = input.CanPassUnpackedOption }

    | InferedType.Top | InferedType.Null -> 

        // Return the underlying JsonDocument without change
        { ConvertedType = ctx.Representation
          Converter = ctx.Replacer.ToRuntime
          ConverterRequiresUnpackedOption = false }

    | InferedType.Collection (SingletonMap(_, (_, typ))) -> 

        // TODO: handle input.Optional

        let input = { ParentName = NameUtils.singularize input.ParentName
                      CanPassUnpackedOption = false
                      Optional = false }
        let output = generateJsonType ctx input typ

        // the async version is only used when the top level element returned by Parse/Load is an array
        let conv = fun (jDoc:Expr)-> 
          let isAsync = jDoc.Type.Name.StartsWith "FSharpAsync`1"
          // TODO: use the same as in ApiaryGenerationHelper.AsyncMap
          if isAsync then
            ctx.JsonRuntimeType?AsyncConvertArray (output.ConvertedTypeErased ctx) (ctx.Replacer.ToRuntime jDoc, output.ConverterFunc ctx)
          else
            ctx.JsonRuntimeType?ConvertArray (output.ConvertedTypeErased ctx) (ctx.Replacer.ToRuntime jDoc, output.ConverterFunc ctx)
        
        { ConvertedType = output.ConvertedType.MakeArrayType()
          Converter = conv
          ConverterRequiresUnpackedOption = false }

    | InferedType.Record(_, props) -> 
        // Generate new type for the record (for JSON, we do not try to unify them)
        let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName (if input.ParentName = "" then "Entity" else input.ParentName), Some(ctx.IJsonDocumentType), HideObjectMethods = true)
        ctx.TypeProviderType.AddMember(objectTy)

        //TODO: handle input.Optional (#163)

        let makeUnique = NameUtils.uniqueGenerator NameUtils.nicePascalName
        makeUnique "JsonValue" |> ignore

        // Add all record fields as properties
        for prop in props do
  
          let propName = prop.Name
            
          let propType, getter =

            let propInput = { ParentName = propName
                              CanPassUnpackedOption = true
                              Optional = prop.Optional }
            let propOutput = generateJsonType ctx propInput prop.Type
              
            if propOutput.ConverterRequiresUnpackedOption then
            
              let getter = fun (Singleton jDoc) -> 
                let jDoc = ctx.Replacer.ToDesignTime jDoc
                <@@ JsonRuntime.TryGetPropertyUnpacked(%%jDoc, propName) @@>
                |> propOutput.Converter
              
              propOutput.ConvertedType, getter
            
            else if prop.Optional then
            
              let optionType = typedefof<option<_>>.MakeGenericType propOutput.ConvertedType

              // Construct function arguments & call `ConvertOptionalProperty` 
              let getter = fun (Singleton jDoc) -> 
                ctx.JsonRuntimeType?ConvertOptionalProperty (propOutput.ConvertedTypeErased ctx) (jDoc, propName, propOutput.ConverterFunc ctx) :> Expr

              optionType, getter
            
            else

              // Construct function arguments & call `ConvertProperty` 
              let getter = fun (Singleton jDoc) -> 
                let jDoc = ctx.Replacer.ToDesignTime jDoc
                <@@ JsonRuntime.GetPropertyPacked(%%jDoc, propName) @@>
                |> propOutput.Converter

              propOutput.ConvertedType, getter
  
          // Add property with PascalCased name
          let p = ProvidedProperty(makeUnique propName, propType)
          p.GetterCode <- getter
          objectTy.AddMember(p)          
        
        { ConvertedType = objectTy
          Converter = ctx.Replacer.ToRuntime
          ConverterRequiresUnpackedOption = false }

    | InferedType.Collection types -> 

        //TODO: handle input.Optional

        // Generate a choice type that calls either `GetArrayChildrenByTypeTag`
        // or `GetArrayChildByTypeTag`, depending on the multiplicity of the item
        generateMultipleChoiceType ctx input.ParentName types (fun info output kindCode ->
          match info with
          | InferedMultiplicity.Single, _ -> fun (Singleton jDoc) -> 
              // Generate method that calls `GetArrayChildByTypeTag`
              let jDoc = ctx.Replacer.ToDesignTime jDoc
              output.Converter <@@ JsonRuntime.GetArrayChildByTypeTag(%%jDoc, kindCode) @@>
          
          | InferedMultiplicity.Multiple, _ -> 
              // Generate method that calls `GetArrayChildrenByTypeTag` 
              // (unlike the previous easy case, this needs to call conversion function
              // from the runtime similarly to options and arrays) 
              fun (Singleton jDoc) -> 
                ctx.JsonRuntimeType?GetArrayChildrenByTypeTag (output.ConvertedTypeErased ctx) (jDoc, kindCode, output.ConverterFunc ctx)
          
          | InferedMultiplicity.OptionalSingle, _ -> 
              // Similar to the previous case, but call `TryGetArrayChildByTypeTag` 
              fun (Singleton jDoc) -> 
                ctx.JsonRuntimeType?TryGetArrayChildByTypeTag (output.ConvertedTypeErased ctx) (jDoc, kindCode, output.ConverterFunc ctx))

    | InferedType.Heterogeneous types ->

        //TODO: handle input.Optional

        // Generate a choice type that always calls `TryGetValueByTypeTag`
        let types = types |> Map.map (fun _ v -> InferedMultiplicity.OptionalSingle, v)
        generateMultipleChoiceType ctx input.ParentName types (fun info output kindCode ->
          fun (Singleton jDoc) -> 
            let jsonRuntime = ctx.Replacer.ToRuntime typeof<JsonRuntime>
            jsonRuntime?TryGetValueByTypeTag (output.ConvertedTypeErased ctx) (jDoc, kindCode, output.ConverterFunc ctx))
