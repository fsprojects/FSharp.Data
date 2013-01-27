// --------------------------------------------------------------------------------------
// JSON type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open Microsoft.FSharp.Quotations
open ProviderImplementation.JsonInference
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder
open ProviderImplementation.StructureInference
open FSharp.Data.Json
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.TypeInference

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
type internal JsonGenerationContext =
  { DomainType : ProvidedTypeDefinition
    Replacer : AssemblyReplacer 
    UniqueNiceName : string -> string 
    Representation : Type
    Packer : Expr -> Expr
    Unpacker : Expr -> Expr
    UnpackerStayInDesignTime : Expr -> Expr }
  static member Create(domainTy, replacer) =
    let packer e = <@@ JsonDocument(%%e) @@>
    let unpacker e = <@@ ((%%e):JsonDocument).JsonValue @@>
    { DomainType = domainTy
      Replacer = replacer 
      Representation = replacer.ToRuntime typeof<JsonDocument>
      Packer = replacer.ToDesignTime >> packer >> replacer.ToRuntime 
      Unpacker = replacer.ToDesignTime >> unpacker >> replacer.ToRuntime
      UnpackerStayInDesignTime = replacer.ToDesignTime >> unpacker
      UniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName }

module JsonTypeBuilder = 
  
  /// Common code that is shared by code generators that generate 
  /// "Choice" type. This is parameterized by the types (choices) to generate,
  /// by functions that get the multiplicity and the type tag for each option
  /// and also by function that generates the actual code.
  let rec internal generateMultipleChoiceType culture ctx types codeGenerator =
    // Generate new type for the heterogeneous type
    let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName "Choice", Some(ctx.Replacer.ToRuntime typeof<JsonDocument>), HideObjectMethods = true)
    ctx.DomainType.AddMember(objectTy)
        
    // Generate GetXyz(s) method for every different case
    // (but skip all Null nodes - we simply ingore them)
    let gen = NameUtils.uniqueGenerator NameUtils.nicePascalName
    for (KeyValue(kind, (multiplicity, typ))) in types do
      if kind <> InferedTypeTag.Null then
        let valTy, valConv = generateJsonType culture ctx typ
        let kindCode = kind.Code

        // If it occurs at most once, then generate property (which may 
        // be optional). For multiple occurrences, generate method
        match multiplicity with 
        | InferedMultiplicity.OptionalSingle ->
            let p = ProvidedProperty(gen kind.NiceName, typedefof<option<_>>.MakeGenericType [| valTy |])
            p.GetterCode <- codeGenerator (multiplicity, typ) valConv kindCode
            objectTy.AddMember(p)          
        | InferedMultiplicity.Single ->
            let p = ProvidedProperty(gen kind.NiceName, valTy)
            p.GetterCode <- codeGenerator (multiplicity, typ) valConv kindCode
            objectTy.AddMember(p)          
        | InferedMultiplicity.Multiple ->
            let p = ProvidedMethod(gen ("Get" + NameUtils.pluralize kind.NiceName), [], valTy.MakeArrayType())
            p.InvokeCode <- codeGenerator (multiplicity, typ) valConv kindCode
            objectTy.AddMember(p)          

    objectTy :> Type, fun (json:Expr) -> ctx.Replacer.ToRuntime json


  /// Recursively walks over inferred type information and 
  /// generates types for read-only access to the document
  and internal generateJsonType culture ctx = function
    | Primitive(typ, _) -> 

        // Return the JSON value as one of the supported primitive types
        let conv = 
          if typ = typeof<int> then fun json -> <@@ JsonOperations.GetInteger(%%json, culture) @@>
          elif typ = typeof<int64> then fun json -> <@@ JsonOperations.GetInteger64(%%json, culture) @@>
          elif typ = typeof<decimal> then fun json -> <@@ JsonOperations.GetDecimal(%%json, culture) @@>
          elif typ = typeof<float> then fun json -> <@@ JsonOperations.GetFloat(%%json, culture) @@>
          elif typ = typeof<string> then fun json -> <@@ JsonOperations.GetString(%%json) @@>
          elif typ = typeof<bool> then fun json -> <@@ JsonOperations.GetBoolean(%%json) @@>
          elif typ = typeof<DateTime> then fun json -> <@@ JsonOperations.GetDateTime(%%json) @@>
          else failwith "generateJsonType: Unsupported primitive type"
        let conv = ctx.UnpackerStayInDesignTime >> conv >> ctx.Replacer.ToRuntime
        typ, conv

    | Top | Null -> 
        // Return the underlying JsonDocument without change
        ctx.Representation, fun (json:Expr) -> ctx.Replacer.ToRuntime json

    | Collection (SingletonMap(_, (_, typ))) -> 
        let elementTy, elementConv = generateJsonType culture ctx typ

        // Build a function `mapper = fun x -> %%(elementConv x)`
        let convTyp, convFunc = ReflectionHelpers.makeDelegate elementConv ctx.Representation
        // Build a function `packer = fun x -> %%(ctx.Packer x)`
        let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)

        // Call `ConvertArray<Representation, 'TRes>(json, packer, mapper)`
        let conv = fun json -> 
          let operationsTyp = ctx.Replacer.ToRuntime typeof<JsonOperations>
          operationsTyp?ConvertArray (ctx.Representation, convTyp) (ctx.Unpacker json, packFunc, convFunc)
        
        elementTy.MakeArrayType(), conv

    | Record(_, props) -> 
        // Generate new type for the record (for JSON, we do not try to unify them)
        let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName "Entity", Some(ctx.Representation), HideObjectMethods = true)
        ctx.DomainType.AddMember(objectTy)

        // Add all record fields as properties
        for prop in props do
          let propName = prop.Name
          let propTy, getter =
            if not prop.Optional then 
              // If it is not optional, then we simply return the property
              let valTy, valConv = generateJsonType culture ctx prop.Type
              valTy, fun (Singleton json) -> 
                valConv (ctx.Packer <@@ JsonOperations.GetProperty(%%(ctx.UnpackerStayInDesignTime json), propName) @@>)
            else
              // If it is optional, then we generate code similar to arrays
              let valTy, valConv = generateJsonType culture ctx prop.Type
              let optValTy = typedefof<option<_>>.MakeGenericType [| valTy |]

              // Construct function arguments & call `ConvertOptionalProperty` 
              let convTyp, convFunc = ReflectionHelpers.makeDelegate valConv ctx.Representation
              let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)
              let conv = fun (Singleton json) -> 
                let operationsTyp = ctx.Replacer.ToRuntime typeof<JsonOperations>
                operationsTyp?ConvertOptionalProperty (ctx.Representation, convTyp) (ctx.Unpacker json, propName, packFunc, convFunc)
              optValTy, conv

          // Add property with PascalCased name
          let p = ProvidedProperty(NameUtils.nicePascalName prop.Name, propTy)
          p.GetterCode <- getter
          objectTy.AddMember(p)          

        objectTy :> Type, ctx.Replacer.ToRuntime

    | Collection types -> 
        // Generate a choice type that calls either `GetArrayChildrenByTypeTag`
        // or `GetArrayChildByTypeTag`, depending on the multiplicity of the item
        generateMultipleChoiceType culture ctx types (fun info valConv kindCode ->
          match info with
          | InferedMultiplicity.Single, _ -> fun (Singleton json) -> 
              // Generate method that calls `GetArrayChildByTypeTag`
              valConv (ctx.Packer <@@ JsonOperations.GetArrayChildByTypeTag(%%(ctx.UnpackerStayInDesignTime json), kindCode) @@>)
          
          | InferedMultiplicity.Multiple, _ -> 
              // Generate method that calls `GetArrayChildrenByTypeTag` 
              // (unlike the previous easy case, this needs to call conversion function
              // from the runtime similarly to options and arrays) 
              let convTyp, convFunc = ReflectionHelpers.makeDelegate valConv ctx.Representation
              let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)
              fun (Singleton json) -> 
                let operationsTyp = ctx.Replacer.ToRuntime typeof<JsonOperations>
                operationsTyp?GetArrayChildrenByTypeTag (ctx.Representation, convTyp) (ctx.Unpacker json, kindCode, packFunc, convFunc)
          
          | InferedMultiplicity.OptionalSingle, _ -> 
              // Similar to the previous case, but call `TryGetArrayChildByTypeTag` 
              let convTyp, convFunc = ReflectionHelpers.makeDelegate valConv ctx.Representation
              let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)
              fun (Singleton json) -> 
                let operationsTyp = ctx.Replacer.ToRuntime typeof<JsonOperations>
                operationsTyp?TryGetArrayChildByTypeTag (ctx.Representation, convTyp) (ctx.Unpacker json, kindCode, packFunc, convFunc))

    | Heterogeneous types ->
        // Generate a choice type that always calls `GetValueByTypeTag` to 
        let types = types |> Map.map (fun _ v -> InferedMultiplicity.OptionalSingle, v)
        generateMultipleChoiceType culture ctx types (fun info valConv kindCode ->
          // Similar to the previous case, but call `TryGetArrayChildByTypeTag` 
          let convTyp, convFunc = ReflectionHelpers.makeDelegate valConv ctx.Representation
          let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)
          fun (Singleton json) -> 
            let operationsTyp = ctx.Replacer.ToRuntime typeof<JsonOperations>
            operationsTyp?TryGetValueByTypeTag (ctx.Representation, convTyp) (ctx.Unpacker json, kindCode, packFunc, convFunc))
