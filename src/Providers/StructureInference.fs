// --------------------------------------------------------------------------------------
// Implements type inference for unstructured documents like XML or JSON
// --------------------------------------------------------------------------------------

module ProviderImplementation.StructureInference

open System
open System.Diagnostics
open System.Collections.Generic
open System.Globalization
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.TypeInference

[<RequireQualifiedAccess>]
type InferenceOptions =
| None
| UseNaNforOptionalDecimal

/// Merge two sequences by pairing elements for which
/// the specified predicate returns the same key
///
/// (If the inputs contain the same keys, then the order
/// of the elements is preserved.)
let internal pairBy f first second = 
  let vals1 = [ for o in first -> f o, o ]
  let vals2 = [ for o in second -> f o, o ]
  let d1, d2 = dict vals1, dict vals2
  let k1, k2 = set d1.Keys, set d2.Keys
  let keys = List.map fst vals1 @ (List.ofSeq (k2 - k1))
  let asOption = function true, v -> Some v | _ -> None
  [ for k in keys -> 
      k, asOption (d1.TryGetValue(k)), asOption (d2.TryGetValue(k)) ]
  
// ------------------------------------------------------------------------------------------------

/// List of primitive types that can be returned as a result of the inference
/// (with names that are returned for heterogeneous types)
let primitiveTypes =
  [ typeof<int>; typeof<int64>; typeof<float>; 
    typeof<decimal>; typeof<bool>; typeof<string>; typeof<DateTime> ]

/// Checks whether a value is a value type (and cannot have null as a value)
let isValueType = function
  | Primitive(typ, _) -> typ <> typeof<string>
  | _ -> false

let hasNullOrNaN = function
  | Primitive(typ, _) as t -> typ = typeof<float> || not (isValueType t)
  | _ -> false

/// Returns a tag of a type - a tag represents a 'kind' of type 
/// (essentially it describes the different bottom types we have)
let typeTag = function
  | Record(n, _)-> InferedTypeTag.Record n
  | Collection _ -> InferedTypeTag.Collection
  | Null | Top -> InferedTypeTag.Null
  | Heterogeneous _ -> InferedTypeTag.Heterogeneous
  | Primitive(typ, _) ->
      if typ = typeof<int> || typ = typeof<int64> || typ = typeof<float> || typ = typeof<decimal> 
        then InferedTypeTag.Number
      elif typ = typeof<bool> then InferedTypeTag.Boolean
      elif typ = typeof<string> then InferedTypeTag.String
      elif typ = typeof<DateTime> then InferedTypeTag.DateTime
      else failwith "inferCollectionType: Unknown primitive type"

/// Find common subtype of two primitive types or `Bottom` if there is no such type.
/// The numeric types are ordered as below, other types are not related in any way.
///
///   float :> decimal :> int64 :> int
///
/// This means that e.g. `int` is a subtype of `decimal` and so all `int` values
/// are also `decimal` (and `float`) values, but not the other way round.
let subtypePrimitives typ1 typ2 = 
  Debug.Assert(Seq.exists ((=) typ1) primitiveTypes)
  Debug.Assert(Seq.exists ((=) typ2) primitiveTypes)
    
  let convertibleTo typ source = 
    if typ = typeof<int64> then 
      source = typeof<int64> || source = typeof<int>
    elif typ = typeof<decimal> then
      source = typeof<decimal> || source = typeof<int64> || source = typeof<int>
    elif typ = typeof<float> then 
      source = typeof<float> || source = typeof<decimal> || source = typeof<int64> || source = typeof<int>
    else failwith "convertibleTo: Incorrect argument"

  // If both types are the same, then that's good
  if typ1 = typ2 then Some typ1 
  // If both are convertible to int64, decimal and float, respectively
  elif convertibleTo typeof<int64> typ1 && convertibleTo typeof<int64> typ2 then
    Some typeof<int64>
  elif convertibleTo typeof<decimal> typ1 && convertibleTo typeof<decimal> typ2 then
    Some typeof<decimal>
  elif convertibleTo typeof<float> typ1 && convertibleTo typeof<float> typ2 then
    Some typeof<float>
  // Otherwise there is no common subtype
  else None

/// Active pattern that calls `subtypePrimitives` on two primitive types
let (|SubtypePrimitives|_|) = function
  | Primitive(t1, u1), Primitive(t2, u2) -> 
      // Re-annotate with the unit, if it is the same one
      match subtypePrimitives t1 t2 with
      | Some(t) when u1 = u2 -> Some(t, u1)
      | Some(t) -> Some(t, None)
      | _ -> None
  | _ -> None    

/// Find common subtype of two infered types:
/// 
///  * If the types are both primitive, then we find common subtype of the primitive types
///  * If the types are both records, then we union their fields (and mark some as optional)
///  * If the types are both collections, then we take subtype of their elements
///    (note we do not generate heterogeneous types in this case!)
///  * If one type is the Top type, then we return the other without checking
///  * If one of the types is the Null type and the other is not a value type
///    (numbers or booleans, but not string) then we return the other type.
///    Otherwise, we return bottom.
///
/// The contract that should hold about the function is that given two types with the
/// same `InferedTypeTag`, the result also has the same `InferedTypeTag`. 
///
let rec subtypeInfered opt ot1 ot2 =
  match ot1, ot2 with
  // Subtype of matching types or one of equal types
  | SubtypePrimitives t -> Primitive t
  | Record(n1, t1), Record(n2, t2) when n1 = n2 -> Record(n1, unionRecordTypes opt t1 t2)
  | Heterogeneous t1, Heterogeneous t2 -> Heterogeneous(unionHeterogeneousTypes opt t1 t2)
  | Collection t1, Collection t2 -> Collection(unionCollectionTypes opt t1 t2)
  | Null, Null -> Null
  
  // Top type can be merged with else
  | t, Top | Top, t -> t
  
  // Null type can be merged with non-value types
  | t, Null | Null, t when hasNullOrNaN t -> t
  
  // Heterogeneous can be merged with any type
  | Heterogeneous h, other 
  | other, Heterogeneous h ->
      // Add the other type as another option. We should never add
      // heterogenous type as an option of other heterogeneous type.
      assert (typeTag other <> InferedTypeTag.Heterogeneous)
      Heterogeneous(unionHeterogeneousTypes opt h (Map.ofSeq [typeTag other, other]))
    
  // Otherwise the types are incompatible so we build a new heterogeneous type
  | t1, t2 -> 
      let h1, h2 = Map.ofSeq [typeTag t1, t1], Map.ofSeq [typeTag t2, t2]
      Heterogeneous(unionHeterogeneousTypes opt h1 h2)

/// Given two heterogeneous types, get a single type that can represent all the
/// types that the two heterogeneous types can. For every tag, 
and unionHeterogeneousTypes opt cases1 cases2 =
  pairBy (fun (KeyValue(k, _)) -> k) cases1 cases2
  |> Seq.map (function
      | tag, Some (KeyValue(_, t)), None 
      | tag, None, Some (KeyValue(_, t)) -> tag, t
      | tag, Some (KeyValue(_, t1)), Some (KeyValue(_, t2)) -> tag, subtypeInfered opt t1 t2 
      | _ -> failwith "unionHeterogeneousTypes: pairBy returned None, None")
  |> Map.ofSeq

/// A collection can contain multiple types - in that case, we do keep 
/// the multiplicity for each different type tag to generate better types
/// (this is essentially the same as `unionHeterogeneousTypes`, but 
/// it also handles the multiplicity)
and unionCollectionTypes opt cases1 cases2 = 
  pairBy (fun (KeyValue(k, _)) -> k) cases1 cases2 
  |> Seq.map (function
      | tag, Some (KeyValue(_, (m, t))), None 
      | tag, None, Some (KeyValue(_, (m, t))) -> 
          // If one collection contains thing exactly once
          // but the other does not contain it, then it is optional
          tag, ((if m = Single then OptionalSingle else m), t)
      | tag, Some (KeyValue(_, (m1, t1))), Some (KeyValue(_, (m2, t2))) -> 
          let m = if m1 = Multiple || m2 = Multiple then Multiple else Single
          tag, (m, subtypeInfered opt t1 t2)
      | _ -> failwith "unionHeterogeneousTypes: pairBy returned None, None")
  |> Map.ofSeq

/// Get the union of record types (merge their properties)
/// This matches the corresponding members and marks them as `Optional`
/// if one may be missing. It also returns subtype of their types.
and unionRecordTypes opt t1 t2 =
  pairBy (fun p -> p.Name) t1 t2
  |> Seq.map (fun (name, fst, snd) ->
      
      match fst, snd with
      
      // If one is missing, return the other, but optional
      | Some p, None | None, Some p -> { p with Optional = true }
      
      // If both reference the same object, we return one
      // (This is needed to support recursive type structures)
      | Some p1, Some p2 when Object.ReferenceEquals(p1, p2) -> p1

      // If both are available, we get their subtype
      | Some p1, Some p2 -> 

          let typ, optional =
              if p1.Type = Null && p2.Type <> Null then p2.Type, true
              elif p2.Type = Null && p1.Type <> Null then p1.Type, true
              else subtypeInfered opt p1.Type p2.Type, p1.Optional || p2.Optional

          // prefer floats instead of optional decimals
          let optional, typ = 
              match optional, typ with
              | true, Primitive(typ, unit) when 
                opt = InferenceOptions.UseNaNforOptionalDecimal && typ = typeof<decimal> -> false, Primitive(typeof<float>, unit)
              | pair -> pair

          // doesn't make sense to have options of nullable types
          let optional = optional && not (hasNullOrNaN typ)

          { Name = name; Optional = optional; Type = typ }

      | _ -> failwith "unionRecordTypes: pairBy returned None, None")

  |> List.ofSeq

/// Infer the type of the collection based on multiple sample types
/// (group the types by tag, count their multiplicity)
let inferCollectionType opt types = 
  types 
  |> Seq.groupBy typeTag
  |> Seq.map (fun (tag, types) ->
      let multiple = if Seq.length types > 1 then Multiple else Single
      tag, (multiple, Seq.fold (subtypeInfered opt) Top types) )
  |> Map.ofSeq |> Collection

let private (|StringEquals|_|) (s1:string) s2 = 
    if s1.Equals(s2, StringComparison.OrdinalIgnoreCase) 
    then Some () else None

let private (|Parse|_|) func value = 
    match func value with
    | true, v -> Some v
    | _ -> None

/// Infers the type of a simple string value (this is either
/// the value inside a node or value of an attribute)
let inferPrimitiveType culture (value : string) unit =

    let int32TryParse value = Int32.TryParse(value, NumberStyles.Integer, culture)
    let int64TryParse value = Int64.TryParse(value, NumberStyles.Integer, culture)
    let decimalTryParse value = Decimal.TryParse(value, NumberStyles.Number, culture)
    let doubleTryParse value = Double.TryParse(value, NumberStyles.Float, culture)
    let dateTimeTryParse value = DateTime.TryParse(value, culture, DateTimeStyles.None)

    if String.IsNullOrEmpty value then 
        Null
    else   
        match value.Trim() with 
        | StringEquals "true" | StringEquals "false" | StringEquals "yes" | StringEquals "no" -> 
            Primitive(typeof<bool>, unit)
        | StringEquals "#N/A" -> Primitive(typeof<float>, unit)
        | Parse int32TryParse _ -> Primitive(typeof<int>, unit)
        | Parse int64TryParse _ -> Primitive(typeof<int64>, unit)
        | Parse decimalTryParse _ -> Primitive(typeof<decimal>, unit)
        | Parse doubleTryParse _ -> Primitive(typeof<float>, unit)
        | Parse dateTimeTryParse _ -> Primitive(typeof<DateTime>, unit)
        | _ -> Primitive(typeof<string>, unit)
