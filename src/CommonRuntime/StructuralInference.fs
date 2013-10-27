/// Implements type inference for unstructured documents like XML or JSON
module FSharp.Data.Runtime.StructuralInference

open System
open System.Diagnostics
open System.Collections.Generic
open System.Globalization
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes

module Seq = 
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

let private numericTypes = [ typeof<Bit0>; typeof<Bit1>; typeof<int>; typeof<int64>; typeof<decimal>; typeof<float>]

/// List of primitive types that can be returned as a result of the inference
let private primitiveTypes = [typeof<string>; typeof<DateTime>; typeof<Guid>; typeof<bool>; typeof<Bit>] @ numericTypes

/// Checks whether a type can have null as a value
let private supportsNull = function
  | InferedType.Primitive(typ, _) -> typ = typeof<string>
  | _ -> true

/// Checks whether a type supports unit of measure
let supportsUnitsOfMeasure typ =    
  List.exists ((=) typ) numericTypes

/// Returns a tag of a type - a tag represents a 'kind' of type 
/// (essentially it describes the different bottom types we have)
let typeTag = function
  | InferedType.Record(n, _)-> InferedTypeTag.Record n
  | InferedType.Collection _ -> InferedTypeTag.Collection
  | InferedType.Null | InferedType.Top -> InferedTypeTag.Null
  | InferedType.Heterogeneous _ -> InferedTypeTag.Heterogeneous
  | InferedType.Primitive(typ, _) ->
      if typ = typeof<Bit> || List.exists ((=) typ) numericTypes then InferedTypeTag.Number
      elif typ = typeof<bool> then InferedTypeTag.Boolean
      elif typ = typeof<string> then InferedTypeTag.String
      elif typ = typeof<DateTime> then InferedTypeTag.DateTime
      elif typ = typeof<Guid> then InferedTypeTag.Guid
      else failwith "inferCollectionType: Unknown primitive type"

/// Find common subtype of two primitive types or `Bottom` if there is no such type.
/// The numeric types are ordered as below, other types are not related in any way.
///
///   float :> decimal :> int64 :> int :> bit :> bit0
///   float :> decimal :> int64 :> int :> bit :> bit1
///   bool :> bit :> bit0
///   bool :> bit :> bit1
///
/// This means that e.g. `int` is a subtype of `decimal` and so all `int` values
/// are also `decimal` (and `float`) values, but not the other way round.

let private conversionTable =
    [ typeof<Bit>,     [ typeof<Bit0>; typeof<Bit1>]
      typeof<bool>,    [ typeof<Bit0>; typeof<Bit1>; typeof<Bit>]
      typeof<int>,     [ typeof<Bit0>; typeof<Bit1>; typeof<Bit>]
      typeof<int64>,   [ typeof<Bit0>; typeof<Bit1>; typeof<Bit>; typeof<int>]
      typeof<decimal>, [ typeof<Bit0>; typeof<Bit1>; typeof<Bit>; typeof<int>; typeof<int64>]
      typeof<float>,   [ typeof<Bit0>; typeof<Bit1>; typeof<Bit>; typeof<int>; typeof<int64>; typeof<decimal>] ]

let private subtypePrimitives typ1 typ2 = 
  Debug.Assert(List.exists ((=) typ1) primitiveTypes)
  Debug.Assert(List.exists ((=) typ2) primitiveTypes)
    
  let convertibleTo typ source = 
    typ = source ||
    conversionTable |> List.find (fst >> (=) typ) |> snd |> List.exists ((=) source)

  // If both types are the same, then that's good
  if typ1 = typ2 then Some typ1   
  else
    // try to find the smaller type that both types are convertible to
    conversionTable
    |> List.map fst
    |> List.tryPick (fun superType -> 
        if convertibleTo superType typ1 && convertibleTo superType typ2 
        then Some superType
        else None)

/// Active pattern that calls `subtypePrimitives` on two primitive types
let private (|SubtypePrimitives|_|) = function
  | InferedType.Primitive(t1, u1), InferedType.Primitive(t2, u2) -> 
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
let rec subtypeInfered allowNulls ot1 ot2 =
  match ot1, ot2 with
  // Subtype of matching types or one of equal types
  | SubtypePrimitives t -> InferedType.Primitive t
  | InferedType.Record(n1, t1), InferedType.Record(n2, t2) when n1 = n2 -> InferedType.Record(n1, unionRecordTypes allowNulls t1 t2)
  | InferedType.Heterogeneous t1, InferedType.Heterogeneous t2 -> InferedType.Heterogeneous(unionHeterogeneousTypes allowNulls t1 t2)
  | InferedType.Collection t1, InferedType.Collection t2 -> InferedType.Collection(unionCollectionTypes allowNulls t1 t2)
  | InferedType.Null, InferedType.Null -> InferedType.Null
  
  // Top type can be merged with else
  | t, InferedType.Top | InferedType.Top, t -> t
  // Null type can be merged with non-value types
  | t, InferedType.Null | InferedType.Null, t when allowNulls && supportsNull t -> t
  // Heterogeneous can be merged with any type
  | InferedType.Heterogeneous h, other 
  | other, InferedType.Heterogeneous h ->
      // Add the other type as another option. We should never add
      // heterogenous type as an option of other heterogeneous type.
      assert (typeTag other <> InferedTypeTag.Heterogeneous)
      InferedType.Heterogeneous(unionHeterogeneousTypes allowNulls h (Map.ofSeq [typeTag other, other]))
    
  // Otherwise the types are incompatible so we build a new heterogeneous type
  | t1, t2 -> 
      let h1, h2 = Map.ofSeq [typeTag t1, t1], Map.ofSeq [typeTag t2, t2]
      InferedType.Heterogeneous(unionHeterogeneousTypes allowNulls h1 h2)


/// Given two heterogeneous types, get a single type that can represent all the
/// types that the two heterogeneous types can. For every tag, 
and private unionHeterogeneousTypes allowNulls cases1 cases2 =
  Seq.pairBy (fun (KeyValue(k, _)) -> k) cases1 cases2
  |> Seq.map (function
      | tag, Some (KeyValue(_, t)), None 
      | tag, None, Some (KeyValue(_, t)) -> tag, t
      | tag, Some (KeyValue(_, t1)), Some (KeyValue(_, t2)) -> tag, subtypeInfered allowNulls t1 t2
      | _ -> failwith "unionHeterogeneousTypes: pairBy returned None, None")
  |> Map.ofSeq

/// A collection can contain multiple types - in that case, we do keep 
/// the multiplicity for each different type tag to generate better types
/// (this is essentially the same as `unionHeterogeneousTypes`, but 
/// it also handles the multiplicity)
and private unionCollectionTypes allowNulls cases1 cases2 = 
  Seq.pairBy (fun (KeyValue(k, _)) -> k) cases1 cases2 
  |> Seq.map (function
      | tag, Some (KeyValue(_, (m, t))), None 
      | tag, None, Some (KeyValue(_, (m, t))) -> 
          // If one collection contains thing exactly once
          // but the other does not contain it, then it is optional
          tag, ((if m = Single then OptionalSingle else m), t)
      | tag, Some (KeyValue(_, (m1, t1))), Some (KeyValue(_, (m2, t2))) -> 
          let m = if m1 = Multiple || m2 = Multiple then Multiple else Single
          tag, (m, subtypeInfered allowNulls t1 t2)
      | _ -> failwith "unionHeterogeneousTypes: pairBy returned None, None")
  |> Map.ofSeq

/// Get the union of record types (merge their properties)
/// This matches the corresponding members and marks them as `Optional`
/// if one may be missing. It also returns subtype of their types.
and unionRecordTypes allowNulls t1 t2 =
  Seq.pairBy (fun (p:InferedProperty) -> p.Name) t1 t2
  |> Seq.map (fun (name, fst, snd) ->
      match fst, snd with
      // If one is missing, return the other, but optional
      | Some p, None | None, Some p -> { p with Optional = true }
      // If both reference the same object, we return one
      // (This is needed to support recursive type structures)
      | Some p1, Some p2 when Object.ReferenceEquals(p1, p2) ->
          p1
      // If both are available, we get their subtype
      | Some p1, Some p2 -> 
          { InferedProperty.Name = name
            Optional = p1.Optional || p2.Optional
            Type = subtypeInfered allowNulls p1.Type p2.Type }
      | _ -> failwith "unionRecordTypes: pairBy returned None, None")
  |> List.ofSeq

/// Infer the type of the collection based on multiple sample types
/// (group the types by tag, count their multiplicity)
let inferCollectionType allowNulls types = 
  types 
  |> Seq.groupBy typeTag
  |> Seq.map (fun (tag, types) ->
      let multiple = if Seq.length types > 1 then Multiple else Single
      tag, (multiple, Seq.fold (subtypeInfered allowNulls) InferedType.Top types) )
  |> Map.ofSeq |> InferedType.Collection

/// Infers the type of a simple string value (this is either
/// the value inside a node or value of an attribute)
let inferPrimitiveType culture (value : string) unit =

  // Helper for calling TextConversions.AsXyz functions
  let (|Parse|_|) func value = func culture value

  let asGuid _ value = TextConversions.AsGuid value

  match value with
  | "0" -> InferedType.Primitive(typeof<Bit0>, unit)
  | "1" -> InferedType.Primitive(typeof<Bit1>, unit)
  | Parse TextConversions.AsBoolean _ -> InferedType.Primitive(typeof<bool>, unit)
  | Parse TextConversions.AsInteger _ -> InferedType.Primitive(typeof<int>, unit)
  | Parse TextConversions.AsInteger64 _ -> InferedType.Primitive(typeof<int64>, unit)
  | Parse TextConversions.AsDecimal _ -> InferedType.Primitive(typeof<decimal>, unit)
  | Parse (TextConversions.AsFloat [| |] (*useNoneForMissingValues*)false) _ -> InferedType.Primitive(typeof<float>, unit)
  | Parse asGuid _ -> InferedType.Primitive(typeof<Guid>, unit)
  | Parse TextConversions.AsDateTime _ 
        // If this can be considered a decimal under the invariant culture, 
        // it's a safer bet to consider it a string than a DateTime
        when TextConversions.AsDecimal CultureInfo.InvariantCulture value = None -> 
      InferedType.Primitive(typeof<DateTime>, unit)
  | _ -> InferedType.Primitive(typeof<string>, unit)
