// --------------------------------------------------------------------------------------
// Implements type inference for JSON
// --------------------------------------------------------------------------------------

module ProviderImplementation.JsonInference

open System
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes

/// Infer type of a JSON value - this is simple function because most of the
/// functionality is handled in `StructureInference` (most notably, by
/// `inferCollectionType` and various functions to find common subtype), so
/// here we just need to infer types of primitive JSON values.
let rec inferType inferTypesFromValues cultureInfo parentName json =
  let inline inrange lo hi v = (v >= decimal lo) && (v <= decimal hi)
  let inline integer v = Math.Round(v:decimal) = v

  match json with
  // Null and primitives without subtyping hiearchies
  | JsonValue.Null -> InferedType.Null
  | JsonValue.Boolean _ -> InferedType.Primitive(typeof<bool>, None, false)
  | JsonValue.String s when inferTypesFromValues -> StructuralInference.getInferedTypeFromString cultureInfo s None
  | JsonValue.String _ -> InferedType.Primitive(typeof<string>, None, false)
  // For numbers, we test if it is integer and if it fits in smaller range
  | JsonValue.Number 0M when inferTypesFromValues -> InferedType.Primitive(typeof<Bit0>, None, false)
  | JsonValue.Number 1M when inferTypesFromValues -> InferedType.Primitive(typeof<Bit1>, None, false)
  | JsonValue.Number n when inferTypesFromValues && inrange Int32.MinValue Int32.MaxValue n && integer n -> InferedType.Primitive(typeof<int>, None, false)
  | JsonValue.Number n when inferTypesFromValues && inrange Int64.MinValue Int64.MaxValue n && integer n -> InferedType.Primitive(typeof<int64>, None, false)
  | JsonValue.Number _ -> InferedType.Primitive(typeof<decimal>, None, false)
  | JsonValue.Float _ -> InferedType.Primitive(typeof<float>, None, false)
  // More interesting types 
  | JsonValue.Array ar -> StructuralInference.inferCollectionType (*allowEmptyValues*)false (Seq.map (inferType inferTypesFromValues cultureInfo (NameUtils.singularize parentName)) ar)
  | JsonValue.Record properties ->
      let name = 
        if String.IsNullOrEmpty parentName 
        then None 
        else Some parentName
      let props = 
        [ for propName, value in properties -> 
            let t = inferType inferTypesFromValues cultureInfo propName value
            { Name = propName
              Type = t } ]
      InferedType.Record(name, props, false)
