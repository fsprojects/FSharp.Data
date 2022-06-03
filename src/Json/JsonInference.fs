// --------------------------------------------------------------------------------------
// Implements type inference for JSON
// --------------------------------------------------------------------------------------

module ProviderImplementation.JsonInference

open System
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference

/// Infer type of a JSON value - this is a simple function because most of the
/// functionality is handled in `StructureInference` (most notably, by
/// `inferCollectionType` and various functions to find common subtype), so
/// here we just need to infer types of primitive JSON values.
let rec inferType inferenceMode cultureInfo parentName json =
    let inline inRangeDecimal lo hi (v: decimal) : bool = (v >= decimal lo) && (v <= decimal hi)
    let inline inRangeFloat lo hi (v: float) : bool = (v >= float lo) && (v <= float hi)
    let inline isIntegerDecimal (v: decimal) : bool = Math.Round v = v
    let inline isIntegerFloat (v: float) : bool = Math.Round v = v

    let shouldInferNonStringFromValue =
        match inferenceMode with
        | InferenceMode'.NoInference -> false
        | InferenceMode'.ValuesOnly -> true
        | InferenceMode'.ValuesAndInlineSchemasHints -> true
        | InferenceMode'.ValuesAndInlineSchemasOverrides -> true

    match json with
    // Null and primitives without subtyping hierarchies
    | JsonValue.Null -> InferedType.Null
    | JsonValue.Boolean _ -> InferedType.Primitive(typeof<bool>, None, false)
    | JsonValue.String s ->
        StructuralInference.getInferedTypeFromString inferenceMode cultureInfo s None
    // For numbers, we test if it is integer and if it fits in smaller range
    | JsonValue.Number 0M when shouldInferNonStringFromValue -> InferedType.Primitive(typeof<Bit0>, None, false)
    | JsonValue.Number 1M when shouldInferNonStringFromValue -> InferedType.Primitive(typeof<Bit1>, None, false)
    | JsonValue.Number n when
        shouldInferNonStringFromValue
        && inRangeDecimal Int32.MinValue Int32.MaxValue n
        && isIntegerDecimal n
        ->
        InferedType.Primitive(typeof<int>, None, false)
    | JsonValue.Number n when
        shouldInferNonStringFromValue
        && inRangeDecimal Int64.MinValue Int64.MaxValue n
        && isIntegerDecimal n
        ->
        InferedType.Primitive(typeof<int64>, None, false)
    | JsonValue.Number _ -> InferedType.Primitive(typeof<decimal>, None, false)
    | JsonValue.Float f when
        shouldInferNonStringFromValue
        && inRangeFloat Int32.MinValue Int32.MaxValue f
        && isIntegerFloat f
        ->
        InferedType.Primitive(typeof<int>, None, false)
    | JsonValue.Float f when
        shouldInferNonStringFromValue
        && inRangeFloat Int64.MinValue Int64.MaxValue f
        && isIntegerFloat f
        ->
        InferedType.Primitive(typeof<int64>, None, false)
    | JsonValue.Float _ -> InferedType.Primitive(typeof<float>, None, false)
    // More interesting types
    | JsonValue.Array ar ->
        StructuralInference.inferCollectionType
            false
            (Seq.map (inferType inferenceMode cultureInfo (NameUtils.singularize parentName)) ar)
    | JsonValue.Record properties ->
        let name =
            if String.IsNullOrEmpty parentName then
                None
            else
                Some parentName

        let props =
            [ for propName, value in properties ->
                  let t = inferType inferenceMode cultureInfo propName value
                  { Name = propName; Type = t } ]

        InferedType.Record(name, props, false)
