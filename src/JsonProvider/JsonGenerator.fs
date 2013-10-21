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
  { DomainTypesType : ProvidedTypeDefinition
    Replacer : AssemblyReplacer 
    UniqueNiceName : string -> string 
    // JsonDocument or ApiaryDocument
    Representation : Type
    // wraps a JsonValue in a Representation type
    Packer : Expr -> Expr
    // extracts the JsonValue from a Representation type
    Unpacker : Expr -> Expr
    UnpackerStayInDesignTime : Expr -> Expr }
  static member Create(domainTy, replacer) =
    let packer e = <@@ JsonDocument.Create(%%e) @@>
    let unpacker e = <@@ ((%%e):JsonDocument).JsonValue @@>
    JsonGenerationContext.Create(domainTy, typeof<JsonDocument>, replacer, packer, unpacker, NameUtils.uniqueGenerator NameUtils.nicePascalName)
  static member internal Create(domainTy, representation, replacer, packer, unpacker, uniqueNiceName) =
    { DomainTypesType = domainTy
      Replacer = replacer 
      Representation = replacer.ToRuntime representation
      Packer = replacer.ToDesignTime >> packer >> replacer.ToRuntime 
      Unpacker = replacer.ToDesignTime >> unpacker >> replacer.ToRuntime
      UnpackerStayInDesignTime = replacer.ToDesignTime >> unpacker
      UniqueNiceName = uniqueNiceName }

module JsonTypeBuilder = 
  
  /// Common code that is shared by code generators that generate 
  /// "Choice" type. This is parameterized by the types (choices) to generate,
  /// by functions that get the multiplicity and the type tag for each option
  /// and also by function that generates the actual code.
  let rec internal generateMultipleChoiceType culture ctx parentName types codeGenerator =
    // Generate new type for the heterogeneous type
    let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName (parentName + "Choice"), Some(ctx.Representation), HideObjectMethods = true)
    ctx.DomainTypesType.AddMember(objectTy)
        
    // Generate GetXyz(s) method for every different case
    // (but skip all Null nodes - we simply ingore them)
    let gen = NameUtils.uniqueGenerator NameUtils.nicePascalName
    let types = 
      types 
      |> Seq.map (fun (KeyValue(kind, (multiplicity, typ))) -> kind, multiplicity, typ)
      |> Seq.filter (fun (kind, _, _) -> kind <> InferedTypeTag.Null)
      |> Seq.mapi (fun index (kind, multiplicity, typ) -> index + 1, kind, multiplicity, typ)
      |> Seq.toArray
    for index, kind, multiplicity, typ in types do
      let valTy, valConv = generateJsonType culture ctx (sprintf "%sChoice%dof%d" parentName index types.Length) typ
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
  and internal generateJsonType culture ctx parentName = function
    | Primitive(inferedTyp, _) -> 

        let inferedProp = PrimitiveInferedProperty.Create("", inferedTyp)
        let typ, runtimeTyp = inferedProp.InferedType, inferedProp.RuntimeType

        // Return the JSON value as one of the supported primitive types
        let conv = 
          if typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then fun json -> <@@ JsonOperations.GetInteger(%%json, culture) @@>
          elif typ = typeof<int64> then fun json -> <@@ JsonOperations.GetInteger64(%%json, culture) @@>
          elif typ = typeof<decimal> then fun json -> <@@ JsonOperations.GetDecimal(%%json, culture) @@>
          elif typ = typeof<float> then fun json -> <@@ JsonOperations.GetFloat(%%json, culture) @@>
          elif typ = typeof<string> then fun json -> <@@ JsonOperations.GetString(%%json) @@>
          elif typ = typeof<bool> || typ = typeof<Bit> then fun json -> <@@ JsonOperations.GetBoolean(%%json) @@>
          elif typ = typeof<Guid> then fun json -> <@@ JsonOperations.GetGuid(%%json) @@>
          elif typ = typeof<DateTime> then fun json -> <@@ JsonOperations.GetDateTime(%%json, culture) @@>
          else failwith "generateJsonType: Unsupported primitive type"
        let conv = ctx.UnpackerStayInDesignTime >> conv >> ctx.Replacer.ToRuntime
        runtimeTyp, conv

    | Top | Null -> 
        // Return the underlying JsonDocument without change
        ctx.Representation, fun (json:Expr) -> ctx.Replacer.ToRuntime json

    | Collection (SingletonMap(_, (_, typ))) -> 
        let elementTy, elementConv = generateJsonType culture ctx (NameUtils.singularize parentName) typ

        // Build a function `mapper = fun x -> %%(elementConv x)`
        let convTyp, convFunc = ReflectionHelpers.makeDelegate elementConv ctx.Representation
        // Build a function `packer = fun x -> %%(ctx.Packer x)`
        let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)
        // Build a function `unpacker = fun x -> %%(ctx.Unpacker x)`
        let _, unpackFunc = ReflectionHelpers.makeDelegate ctx.Unpacker (ctx.Replacer.ToRuntime ctx.Representation)

        // Call `ConvertArray<Representation, 'TRes>(jDoc, unpacker, packer, mapper)`
        // or `AsyncConvertArray<Representation, 'TRes>(jDoc, unpacker, packer, mapper)`
        // the async version is only used when the top level element returned by Parse/Load is an array
        let conv = fun (jDoc:Expr)-> 
          let operationsTyp = ctx.Replacer.ToRuntime typeof<JsonOperations>
          let isAsync = jDoc.Type.Name.StartsWith "FSharpAsync`1"
          if isAsync then
            operationsTyp?AsyncConvertArray (ctx.Representation, convTyp) (ctx.Replacer.ToRuntime jDoc, unpackFunc, packFunc, convFunc)
          else
            operationsTyp?ConvertArray (ctx.Representation, convTyp) (ctx.Replacer.ToRuntime jDoc, unpackFunc, packFunc, convFunc)
        
        elementTy.MakeArrayType(), conv

    | Record(_, props) -> 
        // Generate new type for the record (for JSON, we do not try to unify them)
        let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName (if parentName = "" then "Entity" else parentName), Some(ctx.Representation), HideObjectMethods = true)
        ctx.DomainTypesType.AddMember(objectTy)

        let gen = NameUtils.uniqueGenerator NameUtils.nicePascalName

        // Add all record fields as properties
        for prop in props do
          let propName = prop.Name
          let propTy, getter =
            if not prop.Optional then 
              // If it is not optional, then we simply return the property
              let valTy, valConv = generateJsonType culture ctx propName prop.Type
              valTy, fun (Singleton json) -> 
                valConv (ctx.Packer <@@ JsonOperations.GetProperty(%%(ctx.UnpackerStayInDesignTime json), propName) @@>)
            else
              // If it is optional, then we generate code similar to arrays
              let valTy, valConv = generateJsonType culture ctx propName prop.Type
              let optValTy = typedefof<option<_>>.MakeGenericType [| valTy |]

              // Construct function arguments & call `ConvertOptionalProperty` 
              let convTyp, convFunc = ReflectionHelpers.makeDelegate valConv ctx.Representation
              let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)
              let conv = fun (Singleton json) -> 
                let operationsTyp = ctx.Replacer.ToRuntime typeof<JsonOperations>
                operationsTyp?ConvertOptionalProperty (ctx.Representation, convTyp) (ctx.Unpacker json, propName, packFunc, convFunc)
              optValTy, conv

          // Add property with PascalCased name
          let p = ProvidedProperty(gen prop.Name, propTy)
          p.GetterCode <- getter
          objectTy.AddMember(p)          

        objectTy :> Type, ctx.Replacer.ToRuntime

    | Collection types -> 
        // Generate a choice type that calls either `GetArrayChildrenByTypeTag`
        // or `GetArrayChildByTypeTag`, depending on the multiplicity of the item
        generateMultipleChoiceType culture ctx parentName types (fun info valConv kindCode ->
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
        generateMultipleChoiceType culture ctx parentName types (fun info valConv kindCode ->
          // Similar to the previous case, but call `TryGetArrayChildByTypeTag` 
          let convTyp, convFunc = ReflectionHelpers.makeDelegate valConv ctx.Representation
          let _, packFunc = ReflectionHelpers.makeDelegate ctx.Packer (ctx.Replacer.ToRuntime typeof<JsonValue>)
          fun (Singleton json) -> 
            let operationsTyp = ctx.Replacer.ToRuntime typeof<JsonOperations>
            operationsTyp?TryGetValueByTypeTag (ctx.Representation, convTyp) (ctx.Unpacker json, kindCode, packFunc, convFunc))
