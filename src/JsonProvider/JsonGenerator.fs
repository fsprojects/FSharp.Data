// --------------------------------------------------------------------------------------
// JSON type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open Microsoft.FSharp.Quotations
open ProviderImplementation.JsonInference
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference
open FSharp.Data
open FSharp.Data.Json
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.StructuralTypes

/// Context that is used to generate the JSON types.
///
///  * `Representation` is the type that is used to represent documents
///    (here `JsonDocument`)
///
///  * `Unpacker` is a function that takes an expression representing whatever 
///    is used to represent the document (here `JsonDocument`) and obtains the
///    underlying `JsonValue`; `Packer` is the dual (wrap value in `JsonDocument`)
///
/// Both properties are needed for other uses of the provider 
/// (most notably in the Apiary provider)
///
#nowarn "10001"
type internal JsonGenerationContext =
  { Culture : string
    DomainTypesType : ProvidedTypeDefinition
    Replacer : AssemblyReplacer 
    UniqueNiceName : string -> string 
    // JsonDocument or ApiaryDocument
    Representation : Type
    // wraps a JsonValue in a Representation type
    Packer : Expr -> Expr
    // extracts the JsonValue from a Representation type
    Unpacker : Expr -> Expr
    UnpackerStayInDesignTime : Expr -> Expr }
  static member Create(culture, domainTy, replacer) =
    let packer e = <@@ JsonDocument.Create %%e @@>
    let unpacker e = <@@ ((%%e):JsonDocument).JsonValue @@>
    let uniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName
    JsonGenerationContext.Create(culture, domainTy, typeof<JsonDocument>, replacer, packer, unpacker, uniqueNiceName)
  static member internal Create(culture, domainTy, representation, replacer, packer, unpacker, uniqueNiceName) =
    { Culture = culture
      DomainTypesType = domainTy
      Replacer = replacer 
      Representation = replacer.ToRuntime representation
      Packer = replacer.ToDesignTime >> packer >> replacer.ToRuntime 
      Unpacker = replacer.ToDesignTime >> unpacker >> replacer.ToRuntime
      UnpackerStayInDesignTime = replacer.ToDesignTime >> unpacker
      UniqueNiceName = uniqueNiceName }

type internal JsonGenerationInput = 
    { ParentName : string
      Optional : bool
      // if true, output.Converter will get a JsonValue option, otherwise it will get a JsonDocument
      CanPassUnpackedOption : bool }

type internal JsonGenerationOutput = 
    { ConvertedType : Type
      Converter : Expr -> Expr
      ConverterRequiresUnpackedOption : bool }

module JsonTypeBuilder = 
  
  open FSharp.Data.Json.Extensions
  let (?) = QuotationBuilder.(?)

  /// Common code that is shared by code generators that generate 
  /// "Choice" type. This is parameterized by the types (choices) to generate,
  /// by functions that get the multiplicity and the type tag for each option
  /// and also by function that generates the actual code.
  let rec internal generateMultipleChoiceType ctx parentName types codeGenerator =
    // Generate new type for the heterogeneous type
    let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName (parentName + "Choice"), Some(ctx.Representation), HideObjectMethods = true)
    ctx.DomainTypesType.AddMember(objectTy)
        
    let makeUnique = NameUtils.uniqueGenerator NameUtils.nicePascalName
    makeUnique "JsonValue" |> ignore

    // Generate GetXyz(s) method for every different case
    // (but skip all Null nodes - we simply ingore them)
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
          p.GetterCode <- codeGenerator (multiplicity, typ) output.Converter kindCode
          objectTy.AddMember(p)          
      | InferedMultiplicity.Single ->
          let p = ProvidedProperty(makeUnique kind.NiceName, output.ConvertedType)
          p.GetterCode <- codeGenerator (multiplicity, typ) output.Converter kindCode
          objectTy.AddMember(p)          
      | InferedMultiplicity.Multiple ->
          let p = ProvidedMethod(makeUnique ("Get" + NameUtils.pluralize kind.NiceName), [], output.ConvertedType.MakeArrayType())
          p.InvokeCode <- codeGenerator (multiplicity, typ) output.Converter kindCode
          objectTy.AddMember(p)          

    { ConvertedType = objectTy
      Converter = ctx.Replacer.ToRuntime
      ConverterRequiresUnpackedOption = false }

  /// Recursively walks over inferred type information and 
  /// generates types for read-only access to the document
  and internal generateJsonType ctx input = function
    | Primitive(inferedTyp, _) ->
        let typ, conv = 
            PrimitiveInferedProperty.Create(input.ParentName, inferedTyp, input.Optional)
            |> ConversionsGenerator.convertJsonValue ctx.Replacer "" ctx.Culture
        let toOption = fun x -> <@ Some (%%x:JsonValue) @>
        let asOption = fun x -> <@ %%x:JsonValue option @> 
        { ConvertedType = typ
          Converter = if input.CanPassUnpackedOption
                      then asOption >> conv
                      else ctx.UnpackerStayInDesignTime >> toOption >> conv
          ConverterRequiresUnpackedOption = input.CanPassUnpackedOption }

    | Top | Null -> 
        // Return the underlying JsonDocument without change
        { ConvertedType = ctx.Representation
          Converter = ctx.Replacer.ToRuntime
          ConverterRequiresUnpackedOption = false }

    | Collection (SingletonMap(_, (_, typ))) -> 

        // TODO: handle input.Optional

        let input = { ParentName = NameUtils.singularize input.ParentName
                      CanPassUnpackedOption = false
                      Optional = false }
        let output = generateJsonType ctx input typ

        // Build a function `mapper = fun x -> %%(elementConv x)`
        let convTyp, convFunc = ReflectionHelpers.makeDelegate output.Converter ctx.Representation
        // Build a function `packer = fun x -> %%(ctx.Packer x)`
        let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)
        // Build a function `unpacker = fun x -> %%(ctx.Unpacker x)`
        let _, unpackFunc = ReflectionHelpers.makeDelegate ctx.Unpacker (ctx.Replacer.ToRuntime ctx.Representation)

        // Call `ConvertArray<'Representation, 'T>(jDoc, unpacker, packer, mapper)`
        // or `AsyncConvertArray<'Representation, 'T>(jDoc, unpacker, packer, mapper)`
        // the async version is only used when the top level element returned by Parse/Load is an array
        let conv = fun (jDoc:Expr)-> 
          let runtimeType = ctx.Replacer.ToRuntime typeof<JsonRuntime>
          let isAsync = jDoc.Type.Name.StartsWith "FSharpAsync`1"
          // TODO: use the same as in ApiaryGenerationHelper.AsyncMap
          if isAsync then
            runtimeType?AsyncConvertArray (ctx.Representation, convTyp) (ctx.Replacer.ToRuntime jDoc, unpackFunc, packFunc, convFunc)
          else
            runtimeType?ConvertArray (ctx.Representation, convTyp) (ctx.Replacer.ToRuntime jDoc, unpackFunc, packFunc, convFunc)
        
        { ConvertedType = output.ConvertedType.MakeArrayType()
          Converter = conv
          ConverterRequiresUnpackedOption = false }

    | Record(_, props) -> 
        // Generate new type for the record (for JSON, we do not try to unify them)
        let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName (if input.ParentName = "" then "Entity" else input.ParentName), Some(ctx.Representation), HideObjectMethods = true)
        ctx.DomainTypesType.AddMember(objectTy)

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
            
              let getter = fun (Singleton json) -> 
                <@@ (%%(ctx.UnpackerStayInDesignTime json):JsonValue).TryGetProperty propName @@>
                |> propOutput.Converter
              
              propOutput.ConvertedType, getter
            
            else if prop.Optional then
            
              let optionType = typedefof<option<_>>.MakeGenericType propOutput.ConvertedType

              // Construct function arguments & call `ConvertOptionalProperty` 
              let convTyp, convFunc = ReflectionHelpers.makeDelegate propOutput.Converter ctx.Representation
              let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)
              let getter = fun (Singleton json) -> 
                let runtimeTyp = ctx.Replacer.ToRuntime typeof<JsonRuntime>
                runtimeTyp?ConvertOptionalProperty (ctx.Representation, convTyp) (ctx.Unpacker json, propName, packFunc, convFunc) :> Expr
            
              optionType, getter
            
            else

              let getter = fun (Singleton json) -> 
                <@@ (%%(ctx.UnpackerStayInDesignTime json):JsonValue).GetProperty propName @@>
                |> ctx.Packer
                |> propOutput.Converter

              propOutput.ConvertedType, getter
  
          // Add property with PascalCased name
          let p = ProvidedProperty(makeUnique propName, propType)
          p.GetterCode <- getter
          objectTy.AddMember(p)          
        
        { ConvertedType = objectTy
          Converter = ctx.Replacer.ToRuntime
          ConverterRequiresUnpackedOption = false }

    | Collection types -> 

        //TODO: handle input.Optional

        // Generate a choice type that calls either `GetArrayChildrenByTypeTag`
        // or `GetArrayChildByTypeTag`, depending on the multiplicity of the item
        generateMultipleChoiceType ctx input.ParentName types (fun info valConv kindCode ->
          match info with
          | InferedMultiplicity.Single, _ -> fun (Singleton json) -> 
              // Generate method that calls `GetArrayChildByTypeTag`
              valConv (ctx.Packer <@@ JsonRuntime.GetArrayChildByTypeTag(%%(ctx.UnpackerStayInDesignTime json), kindCode) @@>)
          
          | InferedMultiplicity.Multiple, _ -> 
              // Generate method that calls `GetArrayChildrenByTypeTag` 
              // (unlike the previous easy case, this needs to call conversion function
              // from the runtime similarly to options and arrays) 
              let convTyp, convFunc = ReflectionHelpers.makeDelegate valConv ctx.Representation
              let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)
              fun (Singleton json) -> 
                let runtimeType = ctx.Replacer.ToRuntime typeof<JsonRuntime>
                runtimeType?GetArrayChildrenByTypeTag (ctx.Representation, convTyp) (ctx.Unpacker json, kindCode, packFunc, convFunc)
          
          | InferedMultiplicity.OptionalSingle, _ -> 
              // Similar to the previous case, but call `TryGetArrayChildByTypeTag` 
              let convTyp, convFunc = ReflectionHelpers.makeDelegate valConv ctx.Representation
              let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)
              fun (Singleton json) -> 
                let runtimeType = ctx.Replacer.ToRuntime typeof<JsonRuntime>
                runtimeType?TryGetArrayChildByTypeTag (ctx.Representation, convTyp) (ctx.Unpacker json, kindCode, packFunc, convFunc))

    | Heterogeneous types ->

        //TODO: handle input.Optional

        // Generate a choice type that always calls `GetValueByTypeTag` to 
        let types = types |> Map.map (fun _ v -> InferedMultiplicity.OptionalSingle, v)
        generateMultipleChoiceType ctx input.ParentName types (fun info valConv kindCode ->
          // Similar to the previous case, but call `TryGetArrayChildByTypeTag` 
          let convTyp, convFunc = ReflectionHelpers.makeDelegate valConv ctx.Representation
          let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)
          fun (Singleton json) -> 
            let runtimeType = ctx.Replacer.ToRuntime typeof<JsonRuntime>
            runtimeType?TryGetValueByTypeTag (ctx.Representation, convTyp) (ctx.Unpacker json, kindCode, packFunc, convFunc))
