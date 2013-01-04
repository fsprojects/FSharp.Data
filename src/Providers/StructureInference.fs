// --------------------------------------------------------------------------------------
// Implements type inference for unstructured documents like XML or JSON
// --------------------------------------------------------------------------------------

module ProviderImplementation.StructureInference

open System
open System.Diagnostics
open System.Collections.Generic

// --------------------------------------------------------------------------------------
// Types that represent the result of the type inference
// --------------------------------------------------------------------------------------

/// A property of a record has a name and type and may be optional
type InferedProperty =
  { Name : string
    Optional : bool
    mutable Type : InferedType }

/// For heterogeneous types (types that have multiple possible forms
/// such as differently named XML nodes or records and arrays mixed together)
/// this type represents the number of occurrences of individual forms
and InferedMultiplicity = 
  | Single
  | OptionalSingle
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
  | DateTime
  // Collections and sum types
  | Collection 
  | Heterogeneous
  // Possibly named record
  | Record of string option

/// Represents inferred structural type. A type may be either primitive type
/// (one of those listed by `primitiveTypes`) or it can be collection, 
/// (named) record and heterogeneous type. We also have `Null` type (which is
/// a subtype of all non-primitive types) and universal `Top` type.
///
///  * For collection, we infer the types of different things that appear in 
///    the collection and how many times they do.
///
///  * A heterogeneous type (sum type) is simply a choice containing one
///    of multiple different possibilities
///
/// Why is collection not simply a list of Heterogeneous types? If we used that
/// we would lose information about multiplicity and so we would not be able
/// to generate nicer types!
and [<CustomEquality; NoComparison>] InferedType =
  | Primitive of System.Type * option<System.Type>
  | Record of string option * InferedProperty list
  | Collection of Map<InferedTypeTag, InferedMultiplicity * InferedType>
  | Heterogeneous of Map<InferedTypeTag, InferedType>
  | Null
  | Top

  // We need to implement custom equality that returns 'true' when 
  // values reference the same object (to support recursive types)
  override x.GetHashCode() = 
    failwith "InferedType.GetHashCode: Not implemented"

  override x.Equals(y:obj) = 
    if y :? InferedType then 
      match x, y :?> InferedType with
      | a, b when Object.ReferenceEquals(a, b) -> true
      | Primitive(t1, ot1), Primitive(t2, ot2) -> t1 = t2 && ot1 = ot2
      | Record(s1, pl1), Record(s2, pl2) -> s1 = s2 && pl1 = pl2
      | Collection(m1), Collection(m2) -> m1 = m2
      | Heterogeneous(m1), Heterogeneous(m2) -> m1 = m2
      | Null, Null | Top, Top -> true
      | _ -> false
    else false

// ------------------------------------------------------------------------------------------------
// Additional operations for working with the inferred representation

type InferedTypeTag with
  member x.NiceName = 
    match x with
    | Null -> failwith "Null nodes should be skipped."
    | Number -> "Number"
    | Boolean -> "Boolean"
    | String -> "String"
    | DateTime -> "DateTime"
    | Collection -> "Array"
    | Heterogeneous -> "Choice"
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
    | s when s.StartsWith("Record@") -> Record(Some(s.Substring("Record@".Length)))
    | "Record" -> Record None
    | "Number" -> Number 
    | "Boolean" -> Boolean
    | "String" -> String 
    | "DateTime" -> DateTime
    | "Array" -> Collection
    | "Choice" -> Heterogeneous
    | _ -> failwith "Invalid InferredTypeTag code"

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
let rec subtypeInfered ot1 ot2 =
  match ot1, ot2 with
  // Subtype of matching types or one of equal types
  | SubtypePrimitives t -> Primitive t
  | Record(n1, t1), Record(n2, t2) when n1 = n2 -> Record(n1, unionRecordTypes t1 t2)
  | Heterogeneous t1, Heterogeneous t2 -> Heterogeneous(unionHeterogeneousTypes t1 t2)
  | Collection t1, Collection t2 -> Collection(unionCollectionTypes t1 t2)
  | Null, Null -> Null
  
  // Top type can be merged with else
  | t, Top | Top, t -> t
  // Null type can be merged with non-value types
  | t, Null | Null, t when not (isValueType t) -> t
  // Heterogeneous can be merged with any type
  | Heterogeneous h, other 
  | other, Heterogeneous h ->
      // Add the other type as another option. We should never add
      // heterogenous type as an option of other heterogeneous type.
      assert (typeTag other <> InferedTypeTag.Heterogeneous)
      Heterogeneous(unionHeterogeneousTypes h (Map.ofSeq [typeTag other, other]))
    
  // Otherwise the types are incompatible so we build a new heterogeneous type
  | t1, t2 -> 
      let h1, h2 = Map.ofSeq [typeTag t1, t1], Map.ofSeq [typeTag t2, t2]
      Heterogeneous(unionHeterogeneousTypes h1 h2)


/// Given two heterogeneous types, get a single type that can represent all the
/// types that the two heterogeneous types can. For every tag, 
and unionHeterogeneousTypes cases1 cases2 =
  Seq.pairBy (fun (KeyValue(k, _)) -> k) cases1 cases2
  |> Seq.map (function
      | tag, Some (KeyValue(_, t)), None 
      | tag, None, Some (KeyValue(_, t)) -> tag, t
      | tag, Some (KeyValue(_, t1)), Some (KeyValue(_, t2)) -> tag, subtypeInfered t1 t2 
      | _ -> failwith "unionHeterogeneousTypes: pairBy returned None, None")
  |> Map.ofSeq

/// A collection can contain multiple types - in that case, we do keep 
/// the multiplicity for each different type tag to generate better types
/// (this is essentially the same as `unionHeterogeneousTypes`, but 
/// it also handles the multiplicity)
and unionCollectionTypes cases1 cases2 = 
  Seq.pairBy (fun (KeyValue(k, _)) -> k) cases1 cases2 
  |> Seq.map (function
      | tag, Some (KeyValue(_, (m, t))), None 
      | tag, None, Some (KeyValue(_, (m, t))) -> 
          // If one collection contains thing exactly once
          // but the other does not contain it, then it is optional
          tag, ((if m = Single then OptionalSingle else m), t)
      | tag, Some (KeyValue(_, (m1, t1))), Some (KeyValue(_, (m2, t2))) -> 
          let m = if m1 = Multiple || m2 = Multiple then Multiple else Single
          tag, (m, subtypeInfered t1 t2)
      | _ -> failwith "unionHeterogeneousTypes: pairBy returned None, None")
  |> Map.ofSeq

/// Get the union of record types (merge their properties)
/// This matches the corresponding members and marks them as `Optional`
/// if one may be missing. It also returns subtype of their types.
and unionRecordTypes t1 t2 =
  Seq.pairBy (fun p -> p.Name) t1 t2
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
          { Name = name; Optional = p1.Optional || p2.Optional
            Type = subtypeInfered p1.Type p2.Type }
      | _ -> failwith "unionRecordTypes: pairBy returned None, None")
  |> List.ofSeq

/// Infer the type of the collection based on multiple sample types
/// (group the types by tag, count their multiplicity)
let inferCollectionType types = 
  types 
  |> Seq.groupBy typeTag
  |> Seq.map (fun (tag, types) ->
      let multiple = if Seq.length types > 1 then Multiple else Single
      tag, (multiple, Seq.fold subtypeInfered Top types) )
  |> Map.ofSeq |> Collection

/// Infers the type of a simple string value (this is either
/// the value inside a node or value of an attribute)
let inferPrimitiveType value unit =
  match value with 
  | StringEquals "true" | StringEquals "false"
  | StringEquals "yes" | StringEquals "no" -> Primitive(typeof<bool>, unit)
  | Parse Int32.TryParse _ -> Primitive(typeof<int>, unit)
  | Parse Int64.TryParse _ -> Primitive(typeof<int64>, unit)
  | Parse Decimal.TryParse _ -> Primitive(typeof<decimal>, unit)
  | Parse Double.TryParse _ -> Primitive(typeof<float>, unit)
  | Parse DateTime.TryParse _ -> Primitive(typeof<DateTime>, unit)
  | _ -> Primitive(typeof<string>, unit)
