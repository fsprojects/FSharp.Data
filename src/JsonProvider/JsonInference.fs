﻿// --------------------------------------------------------------------------------------
// Implements type inference for JSON
// --------------------------------------------------------------------------------------

module ProviderImplementation.JsonInference

open System
open FSharp.Data.Json
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.StructuralTypes
open ProviderImplementation.StructureInference

/// Infer type of a JSON value - this is simple function because most of the
/// functionality is handled in `StructureInference` (most notably, by
/// `inferCollectionType` and various functions to find common subtype), so
/// here we just need to infer types of primitive JSON values.
let rec inferType culture allowNulls json = 
  let inline inrange lo hi v = (v >= decimal lo) && (v <= decimal hi)
  let inline integer v = Math.Round(v:decimal) = v

  match json with
  // Null and primitives without subtyping hiearchies
  | JsonValue.Null -> Null
  | JsonValue.Boolean _ -> Primitive(typeof<bool>, None)
  | JsonValue.String s -> inferPrimitiveType culture s None 
  // For numbers, we test if it is integer and if it fits in smaller range
  | JsonValue.Number 0M -> Primitive(typeof<Bit0>, None)
  | JsonValue.Number 1M -> Primitive(typeof<Bit1>, None)
  | JsonValue.Number n when inrange Int32.MinValue Int32.MaxValue n && integer n -> Primitive(typeof<int>, None)
  | JsonValue.Number n when inrange Int64.MinValue Int64.MaxValue n && integer n -> Primitive(typeof<int64>, None)
  | JsonValue.Number _ -> Primitive(typeof<decimal>, None)
  | JsonValue.Float _ -> Primitive(typeof<float>, None)
  // More interesting types 
  | JsonValue.Array ar -> inferCollectionType allowNulls (Seq.map (inferType culture allowNulls) ar)
  | JsonValue.Object o ->
      let props = 
        [ for KeyValue(k, v) in o -> 
            { Name = k; Optional = false; Type = inferType culture allowNulls v } ]
      Record(None, props)