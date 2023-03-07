namespace rec FSharp.Data.Runtime.StructuralTypes

open System
open FSharp.Data.Runtime
open System.Diagnostics

// --------------------------------------------------------------------------------------
// Types that represent the result of the type inference
// --------------------------------------------------------------------------------------

/// <summary>A property of a record has a name and type and may be optional</summary>
/// <namespacedoc>
///   <summary>Types that represent the result of static type inference.</summary>
/// </namespacedoc>
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
type InferedProperty =
    { Name: string
      mutable Type: InferedType }
    override x.ToString() = sprintf "%A" x

/// For heterogeneous types (types that have multiple possible forms
/// such as differently named XML nodes or records and arrays mixed together)
/// this type represents the number of occurrences of individual forms
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
type InferedMultiplicity =
    | Single
    | OptionalSingle
    | Multiple

/// For heterogeneous types, this represents the tag that defines the form
/// (that is either primitive type, collection, named record etc.)
[<RequireQualifiedAccess>]
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
type InferedTypeTag =
    // Unknown type
    | Null
    // Primitive types
    | Number
    | Boolean
    | String
    /// Allow for support of embedded json in e.g. xml documents
    | Json
    | DateTime
    | TimeSpan
    | DateTimeOffset
    | Guid
    // Collections and sum types
    | Collection
    | Heterogeneous
    // Possibly named record
    | Record of string option

/// Used to keep track of the original type of a primitive value (before inference).
/// This is primarily useful for the Json type provider,
/// to be able to serialize e.g an int inferred from a json string back into a json string instead of a json number.
[<RequireQualifiedAccess>]
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
type PrimitiveType =
    | String
    | Number
    | Bool

    /// Serialization function useful to bypass type provider limitations...
    /// ("Quotations provided by type providers can only contain simple constants")
    static member ToInt(x) =
        match x with
        | None -> 0
        | Some String -> 1
        | Some Number -> 2
        | Some Bool -> 3

    /// Deserialization function useful to bypass type provider limitations...
    /// ("Quotations provided by type providers can only contain simple constants")
    static member FromInt(x) =
        match x with
        | 0 -> None
        | 1 -> Some String
        | 2 -> Some Number
        | 3 -> Some Bool
        | _ -> failwith $"PrimitiveType value {x} is not mapped."

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
[<CustomEquality; NoComparison; RequireQualifiedAccess>]
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
[<DebuggerDisplay("{ToString(),nq}")>]
type InferedType =
    /// When shouldOverrideOnMerge is true, it means this type should win when merged with other primitive types during inference.
    /// This allows users to control inference by adding manual type hints that take priority.
    | Primitive of
        typ: Type *
        unit: option<System.Type> *
        optional: bool *
        shouldOverrideOnMerge: bool *
        originalType: PrimitiveType
    | Record of name: string option * fields: InferedProperty list * optional: bool
    | Json of typ: InferedType * optional: bool
    | Collection of order: InferedTypeTag list * types: Map<InferedTypeTag, InferedMultiplicity * InferedType>
    | Heterogeneous of types: Map<InferedTypeTag, InferedType> * containsOptional: bool
    | Null
    | Top

    member x.IsOptional =
        match x with
        | Primitive(optional = true)
        | Record(optional = true)
        | Json(optional = true) -> true
        | _ -> false

    static member CanHaveEmptyValues typ =
        typ = typeof<string> || typ = typeof<float>

    /// When allowEmptyValues is true, we allow "" and double.NaN, otherwise
    /// we make the type optional and use None instead.
    /// It's currently only true in CsvProvider when PreferOptionals is set to false
    member x.EnsuresHandlesMissingValues allowEmptyValues =
        match x with
        | Null
        | Heterogeneous(containsOptional = true)
        | Primitive(optional = true)
        | Record(optional = true)
        | Json(optional = true) -> x
        | Primitive (typ, _, false, _, _) when
            allowEmptyValues
            && InferedType.CanHaveEmptyValues typ
            ->
            x
        | Heterogeneous (map, false) -> Heterogeneous(map, true)
        | Primitive (typ, unit, false, overrideOnMerge, originalType) ->
            Primitive(typ, unit, true, overrideOnMerge, originalType)
        | Record (name, props, false) -> Record(name, props, true)
        | Json (typ, false) -> Json(typ, true)
        | Collection (order, types) ->
            let typesR =
                types
                |> Map.map (fun _ (mult, typ) -> (if mult = Single then OptionalSingle else mult), typ)

            Collection(order, typesR)
        | Top -> failwith "EnsuresHandlesMissingValues: unexpected InferedType.Top"

    member x.GetDropOptionality() =
        match x with
        | Primitive (typ, unit, true, overrideOnMerge, originalType) ->
            Primitive(typ, unit, false, overrideOnMerge, originalType), true
        | Record (name, props, true) -> Record(name, props, false), true
        | Json (typ, true) -> Json(typ, false), true
        | Heterogeneous (map, true) -> Heterogeneous(map, false), true
        | _ -> x, false

    member x.DropOptionality() = x.GetDropOptionality() |> fst

    // We need to implement custom equality that returns 'true' when
    // values reference the same object (to support recursive types)
    override x.GetHashCode() = -1

    override x.Equals(y: obj) =
        if y :? InferedType then
            match x, y :?> InferedType with
            | a, b when Object.ReferenceEquals(a, b) -> true
            | Primitive (t1, u1, b1, x1, ot1), Primitive (t2, u2, b2, x2, ot2) ->
                t1 = t2
                && u1 = u2
                && b1 = b2
                && x1 = x2
                && ot1 = ot2
            | Record (s1, pl1, b1), Record (s2, pl2, b2) -> s1 = s2 && pl1 = pl2 && b1 = b2
            | Json (t1, o1), Json (t2, o2) -> t1 = t2 && o1 = o2
            | Collection (o1, t1), Collection (o2, t2) -> o1 = o2 && t1 = t2
            | Heterogeneous (m1, o1), Heterogeneous (m2, o2) -> m1 = m2 && o1 = o2
            | Null, Null
            | Top, Top -> true
            | _ -> false
        else
            false

    override x.ToString() =
        let sb = System.Text.StringBuilder()
        let mutable indentation = -1
        let pushIndent () = indentation <- indentation + 1
        let popIndent () = indentation <- indentation - 1

        let indented str =
            sb.Append(new String(' ', 2 * indentation))
            |> ignore

            sb.AppendLine(str) |> ignore

        let rec walk t =
            pushIndent ()

            match t with
            | Top -> indented ("(Top)") |> ignore
            | Null -> indented ("(Null)") |> ignore
            | Primitive (typ, unit, opt, overrideOnMerge, originalType) ->
                indented (
                    $"(*Primitive* %A{typ}, Unit: %A{unit}, Optional: {opt}, OverrideOnMerge: {overrideOnMerge}, OriginalType: {originalType})"
                )
                |> ignore
            | Record (name, props, opt) ->
                indented ($"{{*Record* Name: {name}, Optional: {opt}")
                |> ignore

                pushIndent ()

                for p in props do
                    indented ($"- Property (Name: {p.Name})")
                    |> ignore

                    walk p.Type

                popIndent ()
                indented ("}") |> ignore
            | Json (iTyp, opt) ->
                indented ($"{{Json Optional: {opt}") |> ignore
                walk iTyp
                indented ("}") |> ignore
            | Collection (order, types) ->
                let inOrder order types =
                    types
                    |> Map.toList
                    |> List.sortBy (fun (tag, _) -> List.findIndex ((=) tag) order)

                indented ($"[*Array*") |> ignore
                pushIndent ()

                for (typTag, (multiplicity, inferedType)) in inOrder order types do
                    indented ($"- Item (Tag: {typTag}, Multiplicity: %A{multiplicity})")
                    |> ignore

                    walk inferedType

                popIndent ()
                indented ("]") |> ignore
            | Heterogeneous (inferedTypes, containsOptional) ->
                indented ($"{{[*Heterogeneous* ContainsOptional: {containsOptional}")
                |> ignore

                pushIndent ()

                for KeyValue (typTag, inferedType) in inferedTypes do
                    indented ($"- Type (Tag: {typTag})") |> ignore
                    walk inferedType

                popIndent ()
                indented ("]}") |> ignore

            popIndent ()

        walk x
        sb.ToString()

// ------------------------------------------------------------------------------------------------
// Additional operations for working with the inferred representation

type internal InferedTypeTag with
    member x.NiceName =
        match x with
        | Null -> failwith "Null nodes should be skipped"
        | Number -> "Number"
        | Boolean -> "Boolean"
        | String -> "String"
        | DateTime -> "DateTime"
        | TimeSpan -> "TimeSpan"
        | DateTimeOffset -> "DateTimeOffset"
        | Guid -> "Guid"
        | Collection -> "Array"
        | Heterogeneous -> "Choice"
        | Record None -> "Record"
        | Record (Some name) -> NameUtils.nicePascalName name
        | Json _ -> "Json"

    /// Converts tag to string code that can be passed to generated code
    member x.Code =
        match x with
        | Record (Some name) -> "Record@" + name
        | _ -> x.NiceName

    /// Parses code returned by 'Code' member (to be used in provided code)
    static member ParseCode(str: string) =
        match str with
        | s when s.StartsWith("Record@") -> Record(Some(s.Substring("Record@".Length)))
        | "Record" -> Record None
        | "Json" -> Json
        | "Number" -> Number
        | "Boolean" -> Boolean
        | "String" -> String
        | "DateTime" -> DateTime
        | "TimeSpan" -> TimeSpan
        | "DateTimeOffset" -> DateTimeOffset
        | "Guid" -> Guid
        | "Array" -> Collection
        | "Choice" -> Heterogeneous
        | "Null" -> failwith "Null nodes should be skipped"
        | _ -> failwith "Invalid InferredTypeTag code"

/// Dummy type to represent that only "0" was found.
/// Will be generated as 'int', unless it's converted to Bit.
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
type Bit0 = Bit0

/// Dummy type to represent that only "1" was found
/// Will be generated as 'int', unless it's converted to Bit
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
type Bit1 = Bit1

/// Dummy type to represent that only one of "0" and "1" were found
/// Will be generated as a 'bool', unless it's converted to another numerical type
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
type Bit = Bit

// ------------------------------------------------------------------------------------------------

/// Represents a transformation of a type
[<RequireQualifiedAccess>]
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
type TypeWrapper =
    /// No transformation will be made to the type
    | None
    /// The type T will be converter to type T option
    | Option
    /// The type T will be converter to type Nullable<T>
    | Nullable
    static member FromOption optional =
        if optional then TypeWrapper.Option else TypeWrapper.None

/// Represents type information about a primitive value (used mainly in the CSV provider)
/// This type captures the type, unit of measure and handling of missing values (if we
/// infer that the value may be missing, we can generate option<T> or nullable<T>)
type internal PrimitiveInferedValue =
    { InferedType: Type
      RuntimeType: Type
      UnitOfMeasure: Type option
      TypeWrapper: TypeWrapper }

    static member Create(typ, typWrapper, unit) =
        let runtimeTyp =
            if typ = typeof<Bit> then
                typeof<bool>
            elif typ = typeof<Bit0> || typ = typeof<Bit1> then
                typeof<int>
            else
                typ

        { InferedType = typ
          RuntimeType = runtimeTyp
          UnitOfMeasure = unit
          TypeWrapper = typWrapper }

    static member Create(typ, optional, unit) =
        PrimitiveInferedValue.Create(typ, TypeWrapper.FromOption optional, unit)

/// Represents type information about a primitive property (used mainly in the CSV provider)
/// This type captures the type, unit of measure and handling of missing values (if we
/// infer that the value may be missing, we can generate option<T> or nullable<T>)
type internal PrimitiveInferedProperty =
    { Name: string
      Value: PrimitiveInferedValue }

    static member Create(name, typ, (typWrapper: TypeWrapper), unit) =
        { Name = name
          Value = PrimitiveInferedValue.Create(typ, typWrapper, unit) }

    static member Create(name, typ, optional, unit) =
        PrimitiveInferedProperty.Create(name, typ, TypeWrapper.FromOption optional, unit)
