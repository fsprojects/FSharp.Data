// --------------------------------------------------------------------------------------
// Implements type inference for unstructured documents like XML or JSON
// --------------------------------------------------------------------------------------

module ProviderImplementation.StructureInference

open System
open System.Diagnostics
open System.Collections.Generic

// --------------------------------------------------------------------------------------
// Helpers

module Seq = 
  /// Merge two sequences by pairing elements for which
  /// the specified predicate returns the same key
  let pairBy f first second = 
    let d1 = dict [ for o in first -> f o, o ]
    let d2 = dict [ for o in second -> f o, o ]
    let keys = set (Seq.concat [ d1.Keys; d2.Keys ])
    let asOption = function true, v -> Some v | _ -> None
    [ for k in keys -> 
        k, asOption (d1.TryGetValue(k)), asOption (d2.TryGetValue(k)) ]
        
// --------------------------------------------------------------------------------------
// Types that represent the result of the type inference
// --------------------------------------------------------------------------------------

/// A property of a record has a name and type and may be optional
type InferedProperty =
  { Name : string
    Optional : bool
    Type : InferedType }

/// For heterogeneous types (types that have multiple possible forms
/// such as differently named XML nodes or records and arrays mixed together)
/// this type represents the number of occurrences of individual forms
and InferedMultiplicity = 
  | Single
  | Multiple 

/// For heterogeneous types, this represents the tag that defines the form
/// (that is either primitive type, collection, named record etc.)
and [<RequireQualifiedAccess>] InferedTypeTag = 
  // Unknown type
  | Null
  // Primitive types
  | Number 
  | Boolean
  | String
  // Collections
  | Collection 
  // Possibly named record
  | Record of string option

/// Represents inferred structural type. A type may be either primitive type
/// (one of those listed by `primitiveTypes`) or it can be collection, 
/// (named) record and heterogeneous type. We also have `Null` type (which is
/// a subtype of all non-primitive types) and universal `Top` type.
and InferedType =
  | Primitive of System.Type
  | Record of string option * InferedProperty list
  | Collection of InferedType
  | Heterogeneous of Map<InferedTypeTag, InferedMultiplicity * InferedType>
  | Null
  | Top

// ------------------------------------------------------------------------------------------------
// Additional operations for working with the inferred representation

type InferedTypeTag with
  member x.NiceName = 
    match x with
    | Null -> failwith "Null nodes should be skipped."
    | Number -> "Number"
    | Boolean -> "Boolean"
    | String -> "String"
    | Collection -> "Array"
    | Record None -> "Record"
    | Record (Some name) -> name
  
  /// Converts tag to string code that can be passed to generated code
  member x.Code = 
    match x with
    | Record (Some name) -> "Record@" + name
    | _ -> x.NiceName
  /// Parses code returned by 'Code' member (to be used in provided code)
  static member ParseCode(str:string) =
    match str with
    | s when s.StartsWith("Record@") -> Record(Some (s.Substring("Record@".Length)))
    | "Record" -> Record None
    | "Number" -> Number 
    | "Boolean" -> Boolean
    | "String" -> String 
    | "Array" -> Collection
    | _ -> failwith "Invalid InferredTypeTag code"

// ------------------------------------------------------------------------------------------------

/// List of primitive types that can be returned as a result of the inference
/// (with names that are returned for heterogeneous types)
let primitiveTypes =
  [ typeof<int>; typeof<int64>; typeof<float>; 
    typeof<decimal>; typeof<bool>; typeof<string> ]

/// Checks whether a value is a value type (and cannot have null as a value)
let isValueType = function
  | Primitive typ -> typ <> typeof<string>
  | _ -> true

/// Find common subtype of two primitive types or `Bottom` if there is no such type.
/// The numeric types are ordered as below, other types are not related in any way.
///
///   double :> decimal :> int64 :> int
///
/// This means that e.g. `int` is a subtype of `decimal` and so all `int` values
/// are also `decimal` (and `double`) values, but not the other way round.
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
    Some typeof<decimal>
  elif convertibleTo typeof<decimal> typ1 && convertibleTo typeof<decimal> typ2 then
    Some typeof<decimal>
  elif convertibleTo typeof<float> typ1 && convertibleTo typeof<float> typ2 then
    Some typeof<float>
  // Otherwise there is no common subtype
  else None

/// Active pattern that calls `subtypePrimitives` on two primitive types
let (|SubtypePrimitives|_|) = function
  | Primitive t1, Primitive t2 -> subtypePrimitives t1 t2
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
let rec subtypeInfered ot1 ot2 =
  match ot1, ot2 with
  // Subtype of matching types or one of equal types
  | SubtypePrimitives t -> Primitive t
  | Record(n1, t1), Record(n2, t2) when n1 = n2 -> Record(n1, unionRecordTypes t1 t2)
  | Collection t1, Collection t2 -> inferCollectionType [t1; t2]
  | Null, Null -> Null
  
  // Heterogeneous can be merged with any type
  // Collections are handled specially because they are merged
  // into a single heterogeneous option (with multiplicity=Multiple).
  | Heterogeneous h, Collection other
  | Collection other, Heterogeneous h 
  | Heterogeneous h, other 
  | other, Heterogeneous h ->
      // We reconstruct a list that matches the heterogeneous type,
      // add the new one and then merge them
      [ yield other
        for (KeyValue(k, (multi, typ))) in h do
          yield typ
          if multi = Multiple then yield typ ]
      |> inferCollectionType
    
  // Top type can be merged with else
  | t, Top | Top, t -> t
  // Null type can be merged with non-value types
  | t, Null | Null, t when not (isValueType t) -> t

  // Otherwise the types are incompatible
  | t1, t2 -> 
      match inferCollectionType [t1; t2] with
      | (Heterogeneous _) as res -> res
      | _ -> failwith "subtypeInfered: Expected heterogeneous type."

/// A collection is either `Collection` if all elements have common subtype
/// or it is `Heterogeneous` when there is no common subtype. We do not return a 
/// collection of `Heterogeneous` or `Heterogeneous` of collections, because
/// `Heterogeneous` can automtaically contain collections of things.
and inferCollectionType (types:seq<_>) = 
  let typeTag = function
    | Record(n, _)-> InferedTypeTag.Record n
    | Collection _ -> InferedTypeTag.Collection
    | Null | Top -> InferedTypeTag.Null
    | Heterogeneous _ -> 
        failwith "inferCollectionType: Unexpected heterogeneous argument"
    | Primitive typ ->
        if typ = typeof<int> || typ = typeof<int64> || typ = typeof<float> || typ = typeof<decimal> 
          then InferedTypeTag.Number
        elif typ = typeof<bool> then InferedTypeTag.Boolean
        elif typ = typeof<string> then InferedTypeTag.String
        else failwith "inferCollectionType: Unknown primitive type"

  // Group types by their tag (essentially a bottom type)
  // and create a heterogeneous type 
  let heterogeneousTypes =
    types
    |> Seq.groupBy typeTag
    |> Seq.map (fun (tag, group) -> 
        match List.ofSeq group with
        | [single] -> tag, (Single, single)
        | multiple -> tag, (Multiple, group |> Seq.fold subtypeInfered Top))
    |> List.ofSeq
  match heterogeneousTypes with
  | [] -> Top
  | [_, (_, single)] -> Collection single
  | types -> 
      // We remove all Null types as post processing which means
      // that we may return heterogeneous type with just a single case
      // if there are nulls to be skipped
      types 
      |> Map.ofSeq |> Heterogeneous


/// Get the union of record types (merge their properties)
/// This matches the corresponding members and marks them as `Optional`
/// if one may be missing. It also returns subtype of their types.
and unionRecordTypes t1 t2 =
  // If we return heterogeneous type as a result of a filed
  // then it will never contain multiple items (this only happens for arrays)
  let singularize = function
    | Heterogeneous cases -> 
        [ for (KeyValue(k, (_, t))) in cases -> k, (Single, t) ]
        |> Map.ofSeq |> Heterogeneous 
    | t -> t

  Seq.pairBy (fun p -> p.Name) t1 t2
  |> Seq.map (fun (name, fst, snd) ->
      match fst, snd with
      // If one is missing, return the other, but optional
      | Some p, None | None, Some p -> { p with Optional = true }
      // If both are available, we get their subtype
      | Some p1, Some p2 -> 
          { Name = name; Optional = p1.Optional || p2.Optional
            Type = singularize (subtypeInfered p1.Type p2.Type) }
      | _ -> failwith "unionRecordTypes: pairBy returned None, None")
  |> List.ofSeq
