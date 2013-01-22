namespace FSharp.Data.RuntimeImplementation.TypeInference

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
