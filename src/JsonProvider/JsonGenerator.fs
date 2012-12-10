// --------------------------------------------------------------------------------------
// JSON type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open FSharp.Web
open FSharp.Web.JsonReader
open ProviderImplementation.JsonInference
open ProviderImplementation.StructureInference

// --------------------------------------------------------------------------------------
// Runtime components used by the generated JSON types
// --------------------------------------------------------------------------------------

type JsonDocument private (json:JsonValue) =
  member x.JsonValue = json
  static member Create(json:JsonValue) =
    JsonDocument(json)

type JsonOperations = 
  // Trivial operations that return primitive values
  static member GetString(value:JsonValue) = value.AsString
  static member GetBoolean(value:JsonValue) = value.AsBoolean
  static member GetFloat(value:JsonValue) = value.AsFloat
  static member GetDecimal(value:JsonValue) = value.AsDecimal
  static member GetInteger(value:JsonValue) = value.AsInteger
  static member GetInteger64(value:JsonValue) = value.AsInteger64
  static member GetProperty(doc:JsonValue, name) = (?) doc name

  /// Converts JSON array to array of target types
  /// The `packer` function rebuilds representation type (such as
  /// `JsonDocument`) which is then passed to projection function `f`.
  static member ConvertArray<'P, 'R>
      (value:JsonValue, packer:JsonValue -> 'P, f:'P -> 'R) : 'R[] = 
    [| for v in value -> f (packer (v)) |]

  /// Get optional property of a specified type
  static member ConvertOptionalProperty<'P, 'R>
      (doc:JsonValue, name, packer:JsonValue -> 'P, f:'P -> 'R) : 'R option = 
    match doc with 
    | JsonValue.Object o -> 
        match o.TryFind name with
        | None | Some JsonValue.Null -> None
        | Some it -> Some (f (packer it))
    | _ -> None

  /// Returns all nodes that match the specified tag
  /// (Follows the same pattern as ConvertXyz functions above)
  static member GetMultipleByTypeTag(doc:JsonValue, tag, pack, f) = 
    let tag = InferedTypeTag.ParseCode tag
    let matchesTag = function
      | JsonValue.Null -> false
      | JsonValue.Boolean _ -> tag = InferedTypeTag.Boolean
      | JsonValue.Number _ -> tag = InferedTypeTag.Number
      | JsonValue.BigNumber _ -> tag = InferedTypeTag.Number
      | JsonValue.Array _ -> tag = InferedTypeTag.Collection
      | JsonValue.Object _ -> tag = InferedTypeTag.Record None
      | JsonValue.String _ -> tag = InferedTypeTag.String
    match doc with
    | JsonValue.Array ar ->
        ar 
        |> List.filter matchesTag 
        |> Array.ofList
        |> Array.map (pack >> f)
    | _ -> failwith "JSON mismatch: Expected Array node"

  /// Returns a single node that matches the specified tag
  /// (either directly this element, or if this is an array, searches)
  static member GetSingleByTypeTag(value:JsonValue, tag) = 
    // The function may be used both when there is just a single value
    // (property of a record) or when there is a collection (array)
    let valueArray =
      match value with 
      | JsonValue.Array _ -> value | _ -> JsonValue.Array [value]
    match JsonOperations.GetMultipleByTypeTag(valueArray, tag, id, id) with
    | [| the |] -> the
    | _ -> failwith "JSON mismatch: Expected single value, but found multiple."

// --------------------------------------------------------------------------------------
// Compile-time components that are used to generate JSON types
// --------------------------------------------------------------------------------------

open System
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes

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
    UniqueNiceName : string -> string 
    Representation : Type
    Packer : Expr -> Expr
    Unpacker : Expr -> Expr }
  static member Create(domainTy) =
    { DomainType = domainTy
      Representation = typeof<JsonDocument>
      Packer = fun e -> <@@ JsonDocument.Create(%%e) @@>
      Unpacker = fun e -> <@@ ((%%e):JsonDocument).JsonValue @@>
      UniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName }

module internal JsonTypeBuilder = 
  
  /// Recursively walks over inferred type information and 
  /// generates types for read-only access to the document
  let rec generateJsonType ctx = function
    | InferedType.Primitive typ -> 
        let conv = 
          if typ = typeof<int> then fun json -> <@@ JsonOperations.GetInteger(%%(ctx.Unpacker json)) @@>
          elif typ = typeof<int64> then fun json -> <@@ JsonOperations.GetInteger64(%%(ctx.Unpacker json)) @@>
          elif typ = typeof<decimal> then fun json -> <@@ JsonOperations.GetDecimal(%%(ctx.Unpacker json)) @@>
          elif typ = typeof<float> then fun json -> <@@ JsonOperations.GetFloat(%%(ctx.Unpacker json)) @@>
          elif typ = typeof<string> then fun json -> <@@ JsonOperations.GetString(%%(ctx.Unpacker json)) @@>
          elif typ = typeof<bool> then fun json -> <@@ JsonOperations.GetBoolean(%%(ctx.Unpacker json)) @@>
          else failwith "generateJsonType: Unsupported primitive type"
        typ, conv

    | InferedType.Top | InferedType.Null -> 
        ctx.Representation, fun json -> <@@ (%%json:JsonDocument) @@>

    | InferedType.Collection typ -> 
        let elementTy, elementConv = generateJsonType ctx typ

        // Build a function `mapper = fun x -> %%(elementConv x)`
        let convTyp, convFunc = ReflectionHelpers.makeFunc elementConv ctx.Representation
        // Build a function `packer = fun x -> %%(ctx.Packer x)`
        let _, packFunc = ReflectionHelpers.makeFunc ctx.Packer typeof<JsonValue>
        // Call `ConvertArray<Representation, 'TRes>(json, packer, mapper)`
        let conv = fun json -> 
          ReflectionHelpers.makeMethodCall 
            typeof<JsonOperations> "ConvertArray"
            [ ctx.Representation; convTyp ] [ ctx.Unpacker json; packFunc; convFunc ]
        elementTy.MakeArrayType(), conv

    | InferedType.Record(_, props) -> 
        // Generate new type for the record (for JSON, we do not try to unify them)
        let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName "Entity", Some(ctx.Representation))
        ctx.DomainType.AddMember(objectTy)

        // Add all record fields as properties
        for prop in props do
          let propName = prop.Name
          let propTy, getter =
            if not prop.Optional || (prop.Optional && prop.Type = Primitive typeof<string>) then 
              // If it is not optional, then we simply return the property
              let valTy, valConv = generateJsonType ctx prop.Type
              valTy, fun (Singleton json) -> 
                valConv (ctx.Packer <@@ JsonOperations.GetProperty(%%(ctx.Unpacker json), propName) @@>)
            else
              // If it is optional, then we generate code similar to arrays
              let valTy, valConv = generateJsonType ctx prop.Type
              let optValTy = typedefof<option<_>>.MakeGenericType [| valTy |]

              // Construct function arguments & call `ConvertOptionalProperty` 
              let convTyp, convFunc = ReflectionHelpers.makeFunc valConv ctx.Representation
              let _, packFunc = ReflectionHelpers.makeFunc ctx.Packer typeof<JsonValue> 
              let conv = fun (Singleton json) -> 
                ReflectionHelpers.makeMethodCall typeof<JsonOperations> "ConvertOptionalProperty"
                  [ ctx.Representation; convTyp ] [ctx.Unpacker json; Expr.Value propName; packFunc; convFunc]
              optValTy, conv

          // Add property with PascalCased name
          let p = ProvidedProperty(NameUtils.nicePascalName prop.Name, propTy)
          p.GetterCode <- getter
          objectTy.AddMember(p)          

        objectTy :> Type, fun json -> json

    | InferedType.Heterogeneous types ->
        // Generate new type for the heterogeneous type
        let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName "Choice", Some(typeof<JsonDocument>))
        ctx.DomainType.AddMember(objectTy)
        
        // Generate GetXyz(s) method for every different case
        // (but skip all Null nodes - we simply ingore them)
        let gen = NameUtils.uniqueGenerator NameUtils.nicePascalName
        for (KeyValue(kind, (multiplicity, typ))) in types do
          if kind <> InferedTypeTag.Null then
            let valTy, valConv = generateJsonType ctx typ
            let kindCode = kind.Code
            match multiplicity with 
            | InferedMultiplicity.Single ->
                // Generate method that calls `GetSingleByTypeTag`
                let p = ProvidedMethod(gen ("Get" + kind.NiceName), [], valTy)
                p.InvokeCode <- fun (Singleton json) -> 
                  valConv (ctx.Packer <@@ JsonOperations.GetSingleByTypeTag(%%(ctx.Unpacker json), kindCode) @@>)
                objectTy.AddMember(p)          

            | InferedMultiplicity.Multiple ->
                // Generate method that calls `GetMultipleByTypeTag` 
                // (unlike the previous easy case, this needs to call conversion function
                // from the runtime similarly to options and arrays) 
                let p = ProvidedMethod(gen ("Get" + NameUtils.pluralize kind.NiceName), [], valTy.MakeArrayType())

                // Construct function arguments & call `GetMultipleByTypeTag` 
                let convTyp, convFunc = ReflectionHelpers.makeFunc valConv ctx.Representation
                let _, packFunc = ReflectionHelpers.makeFunc ctx.Packer typeof<JsonValue> 
                p.InvokeCode <- fun (Singleton json) -> 
                  ReflectionHelpers.makeMethodCall typeof<JsonOperations> "GetMultipleByTypeTag"
                    [ ctx.Representation; convTyp ] [ctx.Unpacker json; Expr.Value kindCode; packFunc; convFunc]
                objectTy.AddMember(p)          

        objectTy :> Type, fun json -> json