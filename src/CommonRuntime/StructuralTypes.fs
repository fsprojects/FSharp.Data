namespace FSharp.Data.Runtime.StructuralTypes

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
  override x.ToString() = sprintf "%A" x

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
  | Guid
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
and [<CustomEquality; NoComparison; RequireQualifiedAccess>] InferedType =
  | Primitive of System.Type * (*unit*)option<System.Type>
  | Record of string option * InferedProperty list
  | Collection of Map<InferedTypeTag, InferedMultiplicity * InferedType>
  | Heterogeneous of Map<InferedTypeTag, InferedType>
  | Null
  | Top

  // We need to implement custom equality that returns 'true' when 
  // values reference the same object (to support recursive types)
  override x.GetHashCode() = -1

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

  override x.ToString() = sprintf "%A" x

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
    | Guid -> "Guid"
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
    | "Guid" -> Guid
    | "Array" -> Collection
    | "Choice" -> Heterogeneous
    | _ -> failwith "Invalid InferredTypeTag code"

/// Dummy type to represent that only "0" was found.
/// Will be generated as 'int', unless it's converted to Bit.
type Bit0 = Bit0

/// Dummy type to represent that only "1" was found
/// Will be generated as 'int', unless it's converted to Bit
type Bit1 = Bit1

/// Dummy type to represent that only one of "0" and "1" were found
/// Will be generated as a 'bool', unless it's converted to another numerical type
type Bit = Bit

// ------------------------------------------------------------------------------------------------

/// Represents type information about a primitive property (used mainly in the CSV provider)
/// This type captures the type, unit of measure and handling of missing values (if we
/// infer that the value may be missing, we can generate option<T> or nullable<T>)
type PrimitiveInferedProperty =
  { Name : string
    InferedType : Type
    RuntimeType : Type
    UnitOfMeasure : Type option
    TypeWrapper : TypeWrapper }
  static member Create(name, typ, ?typWrapper, ?unit) =
    let runtimeTyp = 
      if typ = typeof<Bit> then typeof<bool>
      elif typ = typeof<Bit0> || typ = typeof<Bit1> then typeof<int>
      else typ
    { Name = name
      InferedType = typ
      RuntimeType = runtimeTyp
      UnitOfMeasure = unit
      TypeWrapper = defaultArg typWrapper TypeWrapper.None }
  static member Create(name, typ, optional) =
    PrimitiveInferedProperty.Create(name, typ, (if optional then TypeWrapper.Option else TypeWrapper.None), ?unit=None)

and     
    [<RequireQualifiedAccess>] 
    /// Represents a transformation of a type
    TypeWrapper = 
    /// No transformation will be made to the type
    | None 
    /// The type T will be converter to type T option
    | Option 
    /// The type T will be converter to type Nullable<T>
    | Nullable
