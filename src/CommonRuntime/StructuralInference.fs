/// Implements type inference for unstructured documents like XML or JSON
module FSharp.Data.Runtime.StructuralInference

open System
open System.Diagnostics
open System.Collections.Generic
open System.Globalization
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open System.Text.RegularExpressions

/// This is the public inference mode enum with backward compatibility.
type InferenceMode =
    /// Used as a default value for backward compatibility with the legacy InferTypesFromValues boolean static parameter.
    /// The actual behaviour will depend on whether InferTypesFromValues is set to true (default) or false.
    | BackwardCompatible = 0
    /// Type everything as strings
    /// (or the most basic type possible for the value when it's not string, e.g. for json numbers or booleans).
    | NoInference = 1
    /// Infer types from values only. Inline schemas are disabled.
    | ValuesOnly = 2
    /// Inline schemas types have the same weight as value infered types.
    | ValuesAndInlineSchemasHints = 3
    /// Inline schemas types override value infered types. (Value infered types are ignored if an inline schema is present)
    | ValuesAndInlineSchemasOverrides = 4

/// This is the internal DU representing all the valid cases we support, mapped from the public InferenceMode.
type InferenceMode' =
    | NoInference
    /// Backward compatible mode.
    | ValuesOnly
    | ValuesAndInlineSchemasHints
    | ValuesAndInlineSchemasOverrides
    /// Converts from the public api enum with backward compat to the internal representation with only valid cases.
    /// If the user sets InferenceMode manually (to a value other than BackwardCompatible)
    /// then the legacy InferTypesFromValues is ignored.
    /// Otherwise (when set to BackwardCompatible), inference mode is set to a compatible value.
    static member FromPublicApi(inferenceMode: InferenceMode, ?legacyInferTypesFromValues: bool) =
        match inferenceMode with
        | InferenceMode.BackwardCompatible ->
            let legacyInferTypesFromValues = defaultArg legacyInferTypesFromValues true

            match legacyInferTypesFromValues with
            | true -> InferenceMode'.ValuesOnly
            | false -> InferenceMode'.NoInference
        | InferenceMode.NoInference -> InferenceMode'.NoInference
        | InferenceMode.ValuesOnly -> InferenceMode'.ValuesOnly
        | InferenceMode.ValuesAndInlineSchemasHints -> InferenceMode'.ValuesAndInlineSchemasHints
        | InferenceMode.ValuesAndInlineSchemasOverrides -> InferenceMode'.ValuesAndInlineSchemasOverrides
        | _ -> failwithf "Unexpected inference mode value %A" inferenceMode

let asOption =
    function
    | true, x -> Some x
    | false, _ -> None
        
/// <exclude />
module internal List =
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

        [ for k in keys -> k, asOption (d1.TryGetValue(k)), asOption (d2.TryGetValue(k)) ]

// ------------------------------------------------------------------------------------------------

let private numericTypes =
    [ typeof<Bit0>
      typeof<Bit1>
      typeof<int>
      typeof<int64>
      typeof<decimal>
      typeof<float> ]

/// List of primitive types that can be returned as a result of the inference
let private primitiveTypes =
    [ typeof<string>
      typeof<DateTime>
      typeof<DateTimeOffset>
      typeof<TimeSpan>
      typeof<Guid>
      typeof<bool>
      typeof<Bit> ]
    @ numericTypes

/// Checks whether a type supports unit of measure
let supportsUnitsOfMeasure typ = List.exists ((=) typ) numericTypes

/// Returns a tag of a type - a tag represents a 'kind' of type
/// (essentially it describes the different bottom types we have)
let typeTag =
    function
    | InferedType.Record (name = n) -> InferedTypeTag.Record n
    | InferedType.Collection _ -> InferedTypeTag.Collection
    | InferedType.Null
    | InferedType.Top -> InferedTypeTag.Null
    | InferedType.Heterogeneous _ -> InferedTypeTag.Heterogeneous
    | InferedType.Primitive (typ = typ) ->
        if typ = typeof<Bit>
           || List.exists ((=) typ) numericTypes then
            InferedTypeTag.Number
        elif typ = typeof<bool> then
            InferedTypeTag.Boolean
        elif typ = typeof<string> then
            InferedTypeTag.String
        elif typ = typeof<DateTime>
             || typ = typeof<DateTimeOffset> then
            InferedTypeTag.DateTime
        elif typ = typeof<TimeSpan> then
            InferedTypeTag.TimeSpan
        elif typ = typeof<Guid> then
            InferedTypeTag.Guid
        else
            failwith "typeTag: Unknown primitive type"
    | InferedType.Json _ -> InferedTypeTag.Json

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
    [ typeof<Bit>, [ typeof<Bit0>; typeof<Bit1> ]
      typeof<bool>, [ typeof<Bit0>; typeof<Bit1>; typeof<Bit> ]
      typeof<int>, [ typeof<Bit0>; typeof<Bit1>; typeof<Bit> ]
      typeof<int64>, [ typeof<Bit0>; typeof<Bit1>; typeof<Bit>; typeof<int> ]
      typeof<decimal>, [ typeof<Bit0>; typeof<Bit1>; typeof<Bit>; typeof<int>; typeof<int64> ]
      typeof<float>,
      [ typeof<Bit0>
        typeof<Bit1>
        typeof<Bit>
        typeof<int>
        typeof<int64>
        typeof<decimal> ]
      typeof<DateTime>, [ typeof<DateTimeOffset> ] ]

let private subtypePrimitives typ1 typ2 =
    Debug.Assert(List.exists ((=) typ1) primitiveTypes)
    Debug.Assert(List.exists ((=) typ2) primitiveTypes)

    let convertibleTo typ source =
        typ = source
        || conversionTable
           |> List.find (fst >> (=) typ)
           |> snd
           |> List.exists ((=) source)

    // If both types are the same, then that's good
    if typ1 = typ2 then
        Some typ1
    else
        // try to find the smaller type that both types are convertible to
        conversionTable
        |> List.map fst
        |> List.tryPick (fun superType ->
            if convertibleTo superType typ1
               && convertibleTo superType typ2 then
                Some superType
            else
                None)

/// Active pattern that calls `subtypePrimitives` on two primitive types
let private (|SubtypePrimitives|_|) allowEmptyValues =
    function
    | InferedType.Primitive (t1, u1, o1), InferedType.Primitive (t2, u2, o2) ->
        // Re-annotate with the unit, if it is the same one
        match subtypePrimitives t1 t2 with
        | Some t ->
            let unit = if u1 = u2 then u1 else None

            let optional =
                (o1 || o2)
                && not (
                    allowEmptyValues
                    && InferedType.CanHaveEmptyValues t
                )

            Some(t, unit, optional)
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
let rec subtypeInfered allowEmptyValues ot1 ot2 =
    match ot1, ot2 with
    // Subtype of matching types or one of equal types
    | SubtypePrimitives allowEmptyValues t -> InferedType.Primitive t
    | InferedType.Record (n1, t1, o1), InferedType.Record (n2, t2, o2) when n1 = n2 ->
        InferedType.Record(n1, unionRecordTypes allowEmptyValues t1 t2, o1 || o2)
    | InferedType.Json (t1, o1), InferedType.Json (t2, o2) ->
        InferedType.Json(subtypeInfered allowEmptyValues t1 t2, o1 || o2)
    | InferedType.Heterogeneous t1, InferedType.Heterogeneous t2 ->
        InferedType.Heterogeneous(unionHeterogeneousTypes allowEmptyValues t1 t2)
    | InferedType.Collection (o1, t1), InferedType.Collection (o2, t2) ->
        InferedType.Collection(unionCollectionOrder o1 o2, unionCollectionTypes allowEmptyValues t1 t2)

    // Top type can be merged with anything else
    | t, InferedType.Top
    | InferedType.Top, t -> t
    // Merging with Null type will make a type optional if it's not already
    | t, InferedType.Null
    | InferedType.Null, t -> t.EnsuresHandlesMissingValues allowEmptyValues
    // Heterogeneous can be merged with any type
    | InferedType.Heterogeneous h, other
    | other, InferedType.Heterogeneous h ->
        // Add the other type as another option. We should never add
        // heterogeneous type as an option of other heterogeneous type.
        assert (typeTag other <> InferedTypeTag.Heterogeneous)
        InferedType.Heterogeneous(unionHeterogeneousTypes allowEmptyValues h (Map.ofSeq [ typeTag other, other ]))

    // Otherwise the types are incompatible so we build a new heterogeneous type
    | t1, t2 ->
        let h1, h2 = Map.ofSeq [ typeTag t1, t1 ], Map.ofSeq [ typeTag t2, t2 ]
        InferedType.Heterogeneous(unionHeterogeneousTypes allowEmptyValues h1 h2)

/// Given two heterogeneous types, get a single type that can represent all the
/// types that the two heterogeneous types can.
/// Heterogeneous types already handle optionality on their own, so we drop
/// optionality from all its inner types
and private unionHeterogeneousTypes allowEmptyValues cases1 cases2 =
    List.pairBy (fun (KeyValue (k, _)) -> k) cases1 cases2
    |> List.map (fun (tag, fst, snd) ->
        match tag, fst, snd with
        | tag, Some (KeyValue (_, t)), None
        | tag, None, Some (KeyValue (_, t)) -> tag, t.DropOptionality()
        | tag, Some (KeyValue (_, t1)), Some (KeyValue (_, t2)) ->
            tag, (subtypeInfered allowEmptyValues t1 t2).DropOptionality()
        | _ -> failwith "unionHeterogeneousTypes: pairBy returned None, None")
    |> Map.ofList

/// A collection can contain multiple types - in that case, we do keep
/// the multiplicity for each different type tag to generate better types
/// (this is essentially the same as `unionHeterogeneousTypes`, but
/// it also handles the multiplicity)
and private unionCollectionTypes allowEmptyValues cases1 cases2 =
    List.pairBy (fun (KeyValue (k, _)) -> k) cases1 cases2
    |> List.map (fun (tag, fst, snd) ->
        match tag, fst, snd with
        | tag, Some (KeyValue (_, (m, t))), None
        | tag, None, Some (KeyValue (_, (m, t))) ->
            // If one collection contains something exactly once
            // but the other does not contain it, then it is optional
            let m = if m = Single then OptionalSingle else m
            let t = if m <> Single then t.DropOptionality() else t
            tag, (m, t)
        | tag, Some (KeyValue (_, (m1, t1))), Some (KeyValue (_, (m2, t2))) ->
            let m =
                match m1, m2 with
                | Multiple, _
                | _, Multiple -> Multiple
                | OptionalSingle, _
                | _, OptionalSingle -> OptionalSingle
                | Single, Single -> Single

            let t = subtypeInfered allowEmptyValues t1 t2
            let t = if m <> Single then t.DropOptionality() else t
            tag, (m, t)
        | _ -> failwith "unionHeterogeneousTypes: pairBy returned None, None")
    |> Map.ofList

and unionCollectionOrder order1 order2 =
    order1
    @ (order2
       |> List.filter (fun x -> not (List.exists ((=) x) order1)))

/// Get the union of record types (merge their properties)
/// This matches the corresponding members and marks them as `Optional`
/// if one may be missing. It also returns subtype of their types.
and unionRecordTypes allowEmptyValues t1 t2 =
    List.pairBy (fun (p: InferedProperty) -> p.Name) t1 t2
    |> List.map (fun (name, fst, snd) ->
        match fst, snd with
        // If one is missing, return the other, but optional
        | Some p, None
        | None, Some p -> { p with Type = subtypeInfered allowEmptyValues p.Type InferedType.Null }
        // If both reference the same object, we return one
        // (This is needed to support recursive type structures)
        | Some p1, Some p2 when Object.ReferenceEquals(p1, p2) -> p1
        // If both are available, we get their subtype
        | Some p1, Some p2 ->
            { InferedProperty.Name = name
              Type = subtypeInfered allowEmptyValues p1.Type p2.Type }
        | _ -> failwith "unionRecordTypes: pairBy returned None, None")

/// Infer the type of the collection based on multiple sample types
/// (group the types by tag, count their multiplicity)
let inferCollectionType allowEmptyValues types =
    let groupedTypes =
        types
        |> Seq.groupBy typeTag
        |> Seq.map (fun (tag, types) ->
            let multiple = if Seq.length types > 1 then Multiple else Single
            tag, (multiple, Seq.fold (subtypeInfered allowEmptyValues) InferedType.Top types))
        |> Seq.toList

    InferedType.Collection(List.map fst groupedTypes, Map.ofList groupedTypes)

type IUnitsOfMeasureProvider =
    abstract SI: str: string -> System.Type
    abstract Product: measure1: System.Type * measure2: System.Type -> System.Type
    abstract Inverse: denominator: System.Type -> System.Type

let defaultUnitsOfMeasureProvider =
    { new IUnitsOfMeasureProvider with
        member x.SI(_) : Type = null
        member x.Product(_, _) = failwith "Not implemented yet"
        member x.Inverse(_) = failwith "Not implemented yet" }

let private uomTransformations =
    [ [ "²"; "^2" ], (fun (provider: IUnitsOfMeasureProvider) t -> provider.Product(t, t))
      [ "³"; "^3" ], (fun (provider: IUnitsOfMeasureProvider) t -> provider.Product(provider.Product(t, t), t))
      [ "^-1" ], (fun (provider: IUnitsOfMeasureProvider) t -> provider.Inverse(t)) ]

let parseUnitOfMeasure (provider: IUnitsOfMeasureProvider) (str: string) =
    let unit =
        uomTransformations
        |> List.collect (fun (suffixes, trans) -> suffixes |> List.map (fun suffix -> suffix, trans))
        |> List.tryPick (fun (suffix, trans) ->
            if str.EndsWith suffix then
                let baseUnitStr = str.[.. str.Length - suffix.Length - 1]
                let baseUnit = provider.SI baseUnitStr

                if baseUnit = null then
                    None
                else
                    baseUnit |> trans provider |> Some
            else
                None)

    match unit with
    | Some _ -> unit
    | None ->
        let unit = provider.SI str
        if unit = null then None else Some unit

/// The inferred types may be set explicitly via inline schemas.
/// This table specifies the mapping from (the names that users can use) to (the types used).
let nameToType =
    [ "int", (typeof<int>, TypeWrapper.None)
      "int64", (typeof<int64>, TypeWrapper.None)
      "bool", (typeof<bool>, TypeWrapper.None)
      "float", (typeof<float>, TypeWrapper.None)
      "decimal", (typeof<decimal>, TypeWrapper.None)
      "date", (typeof<DateTime>, TypeWrapper.None)
      "datetimeoffset", (typeof<DateTimeOffset>, TypeWrapper.None)
      "timespan", (typeof<TimeSpan>, TypeWrapper.None)
      "guid", (typeof<Guid>, TypeWrapper.None)
      "string", (typeof<String>, TypeWrapper.None) ]
    |> dict

let private typeAndUnitRegex =
    lazy Regex(@"^(?<type>.+)<(?<unit>.+)>$", RegexOptions.Compiled ||| RegexOptions.RightToLeft)

/// <summary>
/// Parses type specification in the schema for a single value.
/// This can be of the form: <c>type|measure|type&lt;measure&gt;</c>
/// </summary>
let parseTypeAndUnit unitsOfMeasureProvider (nameToType: IDictionary<string, (Type * TypeWrapper)>) str =
    let m = typeAndUnitRegex.Value.Match(str)

    if m.Success then
        // type<unit> case, both type and unit have to be valid
        let typ =
            m.Groups.["type"].Value.TrimEnd().ToLowerInvariant()
            |> nameToType.TryGetValue
            |> asOption

        match typ with
        | None -> None, None
        | Some typ ->
            let unitName = m.Groups.["unit"].Value.Trim()
            let unit = parseUnitOfMeasure unitsOfMeasureProvider unitName

            if unit.IsNone then
                failwithf "Invalid unit of measure %s" unitName
            else
                Some typ, unit
    else
        // it is not a full type with unit, so it can be either type or a unit
        let typ =
            str.ToLowerInvariant()
            |> nameToType.TryGetValue
            |> asOption

        match typ with
        | Some (typ, typWrapper) ->
            // Just type
            Some(typ, typWrapper), None
        | None ->
            // Just unit (or nothing)
            None, parseUnitOfMeasure unitsOfMeasureProvider str

[<AutoOpen>]
module private Helpers =

    let wordRegex = lazy Regex("\\w+", RegexOptions.Compiled)

    let numberOfNumberGroups value =
        wordRegex.Value.Matches value
        |> Seq.cast
        |> Seq.choose (fun (x: Match) -> TextConversions.AsInteger CultureInfo.InvariantCulture x.Value)
        |> Seq.length

/// Infers the type of a simple string value
/// Returns one of null|typeof<Bit0>|typeof<Bit1>|typeof<bool>|typeof<int>|typeof<int64>|typeof<decimal>|typeof<float>|typeof<Guid>|typeof<DateTime>|typeof<TimeSpan>|typeof<string>
let inferPrimitiveType (inferenceMode: InferenceMode') (cultureInfo: CultureInfo) (value: string) =

    // Helper for calling TextConversions.AsXyz functions
    let (|Parse|_|) func value = func cultureInfo value
    let (|ParseNoCulture|_|) func value = func value

    let asGuid _ value = TextConversions.AsGuid value

    let getAbbreviatedEraName era =
        cultureInfo.DateTimeFormat.GetAbbreviatedEraName(era)

    let isFakeDate (date: DateTime) value =
        // If this can be considered a decimal under the invariant culture,
        // it's a safer bet to consider it a string than a DateTime
        TextConversions.AsDecimal CultureInfo.InvariantCulture value
        |> Option.isSome
        ||
        // Prevent stuff like 12-002 being considered a date
        date.Year < 1000
        && numberOfNumberGroups value <> 3
        ||
        // Prevent stuff like ad3mar being considered a date
        cultureInfo.Calendar.Eras
        |> Array.exists (fun era ->
            value.IndexOf(cultureInfo.DateTimeFormat.GetEraName(era), StringComparison.OrdinalIgnoreCase)
            >= 0
            || value.IndexOf(getAbbreviatedEraName era, StringComparison.OrdinalIgnoreCase)
               >= 0)

    let matchValue value =
        match value with
        | "" -> Some null
        | Parse TextConversions.AsInteger 0 -> Some typeof<Bit0>
        | Parse TextConversions.AsInteger 1 -> Some typeof<Bit1>
        | ParseNoCulture TextConversions.AsBoolean _ -> Some typeof<bool>
        | Parse TextConversions.AsInteger _ -> Some typeof<int>
        | Parse TextConversions.AsInteger64 _ -> Some typeof<int64>
        | Parse TextConversions.AsTimeSpan _ -> Some typeof<TimeSpan>
        | Parse TextConversions.AsDateTimeOffset dateTimeOffset when not (isFakeDate dateTimeOffset.UtcDateTime value) ->
            Some typeof<DateTimeOffset>
        | Parse TextConversions.AsDateTime date when not (isFakeDate date value) -> Some typeof<DateTime>
        | Parse TextConversions.AsDecimal _ -> Some typeof<decimal>
        | Parse (TextConversions.AsFloat [||] false) _ -> Some typeof<float>
        | Parse asGuid _ -> Some typeof<Guid>
        | _ -> None

    let matchInlineSchema useInlineSchemasOverrides value =
        // TODO properly. The following is just to validate the concept:
        match value with
        | "" -> Some null
        | "date" -> Some typeof<DateTime>
        | "int" -> Some typeof<int>
        | "float" -> Some typeof<float>
        | _ -> None

    let fallbackType = typeof<string>

    match inferenceMode with
    | InferenceMode'.NoInference -> fallbackType
    | InferenceMode'.ValuesOnly ->
        matchValue value
        |> Option.defaultValue fallbackType
    | InferenceMode'.ValuesAndInlineSchemasHints ->
        matchInlineSchema false value
        |> Option.orElseWith (fun () -> matchValue value)
        |> Option.defaultValue fallbackType
    | InferenceMode'.ValuesAndInlineSchemasOverrides ->
        matchInlineSchema true value
        |> Option.orElseWith (fun () -> matchValue value)
        |> Option.defaultValue fallbackType

/// Infers the type of a simple string value
let getInferedTypeFromString inferenceMode cultureInfo value unit =
    match inferenceMode with
    | InferenceMode'.NoInference -> InferedType.Primitive(typeof<string>, None, false)
    | _ ->
        match inferPrimitiveType inferenceMode cultureInfo value with
        | null -> InferedType.Null
        | typ -> InferedType.Primitive(typ, unit, false)
