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
let rec inferType cultureInfo allowNulls parentName json = 
  let inline inrange lo hi v = (v >= decimal lo) && (v <= decimal hi)
  let inline integer v = Math.Round(v:decimal) = v

  match json with
  // Null and primitives without subtyping hiearchies
  | JsonValue.Null -> InferedType.Null
  | JsonValue.Boolean _ -> InferedType.Primitive(typeof<bool>, None)
  | JsonValue.String s -> StructuralInference.inferPrimitiveType cultureInfo s None 
  // For numbers, we test if it is integer and if it fits in smaller range
  | JsonValue.Number 0M -> InferedType.Primitive(typeof<Bit0>, None)
  | JsonValue.Number 1M -> InferedType.Primitive(typeof<Bit1>, None)
  | JsonValue.Number n when inrange Int32.MinValue Int32.MaxValue n && integer n -> InferedType.Primitive(typeof<int>, None)
  | JsonValue.Number n when inrange Int64.MinValue Int64.MaxValue n && integer n -> InferedType.Primitive(typeof<int64>, None)
  | JsonValue.Number _ -> InferedType.Primitive(typeof<decimal>, None)
  | JsonValue.Float _ -> InferedType.Primitive(typeof<float>, None)
  // More interesting types 
  | JsonValue.Array ar -> StructuralInference.inferCollectionType allowNulls (Seq.map (inferType cultureInfo allowNulls (NameUtils.singularize parentName)) ar)
  | JsonValue.Object o ->
      let name = 
        if String.IsNullOrEmpty parentName 
        then None 
        else Some parentName
      let props = 
        [ for propName, value in o |> Map.toArray |> Array.sortBy fst -> 
            let t = inferType cultureInfo allowNulls propName value
            { Name = propName
              Optional = false
              Type = t } ]
      InferedType.Record(name, props)
