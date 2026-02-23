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
let rec internal inferType unitsOfMeasureProvider inferenceMode cultureInfo parentName json =
    let inline inRangeDecimal lo hi (v: decimal) : bool = (v >= decimal lo) && (v <= decimal hi)
    let inline isIntegerDecimal (v: decimal) : bool = Math.Round v = v

    let shouldInferNonStringFromValue =
        match inferenceMode with
        | InferenceMode'.NoInference -> false
        | InferenceMode'.ValuesOnly -> true
        | InferenceMode'.ValuesAndInlineSchemasHints -> true
        | InferenceMode'.ValuesAndInlineSchemasOverrides -> true

    match json with
    // Null and primitives without subtyping hierarchies
    | JsonValue.Null -> InferedType.Null
    | JsonValue.Boolean _ -> InferedType.Primitive(typeof<bool>, None, false, false)
    | JsonValue.String s ->
        StructuralInference.getInferedTypeFromString unitsOfMeasureProvider inferenceMode cultureInfo s None
    // For numbers, we test if it is integer and if it fits in smaller range
    | JsonValue.Number 0M when shouldInferNonStringFromValue -> InferedType.Primitive(typeof<Bit0>, None, false, false)
    | JsonValue.Number 1M when shouldInferNonStringFromValue -> InferedType.Primitive(typeof<Bit1>, None, false, false)
    | JsonValue.Number n when
        shouldInferNonStringFromValue
        && inRangeDecimal Int32.MinValue Int32.MaxValue n
        && isIntegerDecimal n
        ->
        InferedType.Primitive(typeof<int>, None, false, false)
    | JsonValue.Number n when
        shouldInferNonStringFromValue
        && inRangeDecimal Int64.MinValue Int64.MaxValue n
        && isIntegerDecimal n
        ->
        InferedType.Primitive(typeof<int64>, None, false, false)
    | JsonValue.Number _ -> InferedType.Primitive(typeof<decimal>, None, false, false)
    // JsonValue.Float is produced when the JSON number uses exponential notation (e.g. 0.1e1, 2.34E5)
    // because TextConversions.AsDecimal uses NumberStyles.Currency which excludes AllowExponent.
    // Such values are always inferred as float regardless of whether the value happens to be a whole
    // number, so that e.g. [0.1e1, 0.2e1] is inferred as float[] not int[]. See issue #1221.
    | JsonValue.Float _ -> InferedType.Primitive(typeof<float>, None, false, false)
    // More interesting types
    | JsonValue.Array ar ->
        StructuralInference.inferCollectionType
            false
            (Seq.map (inferType unitsOfMeasureProvider inferenceMode cultureInfo (NameUtils.singularize parentName)) ar)
    | JsonValue.Record properties ->
        let name =
            if String.IsNullOrEmpty parentName then
                None
            else
                Some parentName

        let props =
            [ for propName, value in properties ->
                  let t = inferType unitsOfMeasureProvider inferenceMode cultureInfo propName value
                  { Name = propName; Type = t } ]

        InferedType.Record(name, props, false)
