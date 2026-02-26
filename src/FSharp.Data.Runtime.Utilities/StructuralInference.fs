/// Implements type inference for unstructured documents like XML or JSON
module FSharp.Data.Runtime.StructuralInference

#nowarn "44"

open System
open System.Diagnostics
open System.Collections.Generic
open System.Globalization
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open System.Text.RegularExpressions

/// This is the internal DU representing all the valid cases we support, mapped from the public InferenceMode.
[<Struct;
  Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
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

[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
let asOption inp =
    match inp with
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
#if NET6_0_OR_GREATER
    @ [ typeof<DateOnly>; typeof<TimeOnly> ]
#endif

/// Checks whether a type supports unit of measure
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
let supportsUnitsOfMeasure typ = List.exists ((=) typ) numericTypes

/// Returns a tag of a type - a tag represents a 'kind' of type
/// (essentially it describes the different bottom types we have)
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
let typeTag inferredType =
    match inferredType with
    | InferedType.Record(name = n) -> InferedTypeTag.Record n
    | InferedType.Collection _ -> InferedTypeTag.Collection
    | InferedType.Null
    | InferedType.Top -> InferedTypeTag.Null
    | InferedType.Heterogeneous _ -> InferedTypeTag.Heterogeneous
    | InferedType.Primitive(typ = typ) ->
        if typ = typeof<Bit> || List.exists ((=) typ) numericTypes then
            InferedTypeTag.Number
        elif typ = typeof<bool> then
            InferedTypeTag.Boolean
        elif typ = typeof<string> then
            InferedTypeTag.String
        elif typ = typeof<DateTime> || typ = typeof<DateTimeOffset> then
            InferedTypeTag.DateTime
        elif typ = typeof<TimeSpan> then
            InferedTypeTag.TimeSpan
        elif typ = typeof<Guid> then
            InferedTypeTag.Guid
#if NET6_0_OR_GREATER
        elif typ = typeof<DateOnly> then
            InferedTypeTag.DateOnly
        elif typ = typeof<TimeOnly> then
            InferedTypeTag.TimeOnly
#endif
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
      typeof<DateTime>,
      [ typeof<DateTimeOffset>
#if NET6_0_OR_GREATER
        typeof<DateOnly>
#endif
        ]
#if NET6_0_OR_GREATER
      typeof<TimeSpan>, [ typeof<TimeOnly> ]
#endif
      ]

let private subtypePrimitives typ1 typ2 =
    Debug.Assert(List.exists ((=) typ1) primitiveTypes)
    Debug.Assert(List.exists ((=) typ2) primitiveTypes)

    let convertibleTo typ source =
        typ = source
        || conversionTable |> List.find (fst >> (=) typ) |> snd |> List.exists ((=) source)

    // If both types are the same, then that's good
    if typ1 = typ2 then
        Some typ1
    else
        // try to find the smaller type that both types are convertible to
        conversionTable
        |> List.map fst
        |> List.tryPick (fun superType ->
            if convertibleTo superType typ1 && convertibleTo superType typ2 then
                Some superType
            else
                None)

/// Active pattern that calls `subtypePrimitives` on two primitive types
let private (|SubtypePrimitives|_|) allowEmptyValues =
    function
    // When a type should override the other, make sure we preserve optionality
    // (so that null and inline schemas are always considered at the same level of importance)
    | InferedType.Primitive(t, u, o1, true), InferedType.Primitive(_, _, o2, false)
    | InferedType.Primitive(_, _, o2, false), InferedType.Primitive(t, u, o1, true) -> Some(t, u, o1 || o2, true)
    | InferedType.Primitive(t1, u1, o1, x1), InferedType.Primitive(t2, u2, o2, x2) ->

        // Re-annotate with the unit, if it is the same one
        match subtypePrimitives t1 t2 with
        | Some t ->
            let unit = if u1 = u2 then u1 else None

            let optional =
                (o1 || o2) && not (allowEmptyValues && InferedType.CanHaveEmptyValues t)

            assert (x1 = x2) // The other cases should be handled above.
            Some(t, unit, optional, x1)
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
let rec internal subtypeInfered allowEmptyValues ot1 ot2 =
    match ot1, ot2 with
    // Subtype of matching types or one of equal types
    | SubtypePrimitives allowEmptyValues t -> InferedType.Primitive t
    | InferedType.Record(n1, t1, o1), InferedType.Record(n2, t2, o2) when n1 = n2 ->
        InferedType.Record(n1, unionRecordTypes allowEmptyValues t1 t2, o1 || o2)
    | InferedType.Json(t1, o1), InferedType.Json(t2, o2) ->
        InferedType.Json(subtypeInfered allowEmptyValues t1 t2, o1 || o2)
    | InferedType.Heterogeneous(t1, o1), InferedType.Heterogeneous(t2, o2) ->
        InferedType.Heterogeneous(
            let map, containsOptional = unionHeterogeneousTypes allowEmptyValues t1 t2
            map |> Map.ofList, containsOptional || o1 || o2
        )
    | InferedType.Collection(o1, t1), InferedType.Collection(o2, t2) ->
        InferedType.Collection(unionCollectionOrder o1 o2, unionCollectionTypes allowEmptyValues t1 t2 |> Map.ofList)

    // Top type can be merged with anything else
    | t, InferedType.Top
    | InferedType.Top, t -> t
    // Merging with Null type will make a type optional if it's not already
    | t, InferedType.Null
    | InferedType.Null, t -> t.EnsuresHandlesMissingValues allowEmptyValues

    // Heterogeneous can be merged with any type
    | InferedType.Heterogeneous(h, o), other
    | other, InferedType.Heterogeneous(h, o) ->
        // Add the other type as another option. We should never add
        // heterogeneous type as an option of other heterogeneous type.
        assert (typeTag other <> InferedTypeTag.Heterogeneous)

        let tagMerged, containsOptional =
            unionHeterogeneousTypes allowEmptyValues h (Map.ofSeq [ typeTag other, other ])

        let containsOptional = containsOptional || o

        // When other is a primitive infered from an inline schema in overriding mode,
        // try to replace the heterogeneous type with the overriding primitive:
        match other with
        | InferedType.Primitive(_, _, _, true) ->
            let primitiveOverrides, nonPrimitives =
                let primitiveOverrides, nonPrimitives = ResizeArray(), ResizeArray()

                tagMerged
                |> List.iter (fun (tag, typ) ->
                    match typ with
                    | InferedType.Primitive(_, _, _, true) -> primitiveOverrides.Add(tag, typ)
                    | InferedType.Primitive(_, _, _, false) -> () // We don't need to track normal primitives
                    | _ -> nonPrimitives.Add(tag, typ))

                primitiveOverrides |> List.ofSeq, nonPrimitives |> List.ofSeq

            // For all the following cases, if there is at least one overriding primitive,
            // normal primitives are discarded.
            match primitiveOverrides, nonPrimitives with
            // No overriding primitives. Just return the heterogeneous type.
            | [], _ -> InferedType.Heterogeneous(tagMerged |> Map.ofList, containsOptional)
            // If there is a single overriding primitive and no non-primitive,
            // return only this overriding primitive (and take care to reestablish optionality if needed).
            | [ (_, singlePrimitive) ], [] ->
                match singlePrimitive with
                | InferedType.Primitive(t, u, o, x) -> InferedType.Primitive(t, u, o || containsOptional, x)
                | _ -> failwith "There should be only primitive types here."
            // If there are non primitives, keep the heterogeneous type.
            | [ singlePrimitive ], nonPrimitives ->
                InferedType.Heterogeneous(singlePrimitive :: nonPrimitives |> Map.ofList, containsOptional)
            // If there are more than one overriding primitive, also keep the heterogeneous type
            | primitives, nonPrimitives ->
                InferedType.Heterogeneous(primitives @ nonPrimitives |> Map.ofList, containsOptional)

        | _otherType -> InferedType.Heterogeneous(tagMerged |> Map.ofList, containsOptional)

    // Otherwise the types are incompatible so we build a new heterogeneous type
    | t1, t2 ->
        let h1, h2 = Map.ofSeq [ typeTag t1, t1 ], Map.ofSeq [ typeTag t2, t2 ]

        InferedType.Heterogeneous(
            let map, containsOptional = unionHeterogeneousTypes allowEmptyValues h1 h2
            map |> Map.ofList, containsOptional
        )

// debug: change the function to return `result`,
// and paste the following in a debug tracepoint before returning the result:
// {ot1f}\nAND\n{ot2f}\nGIVES\n{resultf}\n
//let ot1f, ot2f, resultf = sprintf "%A" ot1, sprintf "%A"  ot2, sprintf "%A" result
//ot1f |> ignore
//ot2f |> ignore
//resultf |> ignore

/// Given two heterogeneous types, get a single type that can represent all the
/// types that the two heterogeneous types can.
and private unionHeterogeneousTypes allowEmptyValues cases1 cases2 =
    let mutable containsOptional = false

    List.pairBy (fun (KeyValue(k, _)) -> k) cases1 cases2
    |> List.map (fun (tag, fst, snd) ->
        match tag, fst, snd with
        | tag, Some(KeyValue(_, t)), None
        | tag, None, Some(KeyValue(_, t)) ->
            let typ, wasOptional = t.GetDropOptionality()
            containsOptional <- containsOptional || wasOptional
            tag, typ
        | tag, Some(KeyValue(_, t1)), Some(KeyValue(_, t2)) ->
            let typ, wasOptional = (subtypeInfered allowEmptyValues t1 t2).GetDropOptionality()
            containsOptional <- containsOptional || wasOptional
            tag, typ
        | _ -> failwith "unionHeterogeneousTypes: pairBy returned None, None"),
    containsOptional

/// A collection can contain multiple types - in that case, we do keep
/// the multiplicity for each different type tag to generate better types
/// (this is essentially the same as `unionHeterogeneousTypes`, but
/// it also handles the multiplicity)
and private unionCollectionTypes allowEmptyValues cases1 cases2 =
    List.pairBy (fun (KeyValue(k, _)) -> k) cases1 cases2
    |> List.map (fun (tag, fst, snd) ->
        match tag, fst, snd with
        | tag, Some(KeyValue(_, (m, t))), None
        | tag, None, Some(KeyValue(_, (m, t))) ->
            // If one collection contains something exactly once
            // but the other does not contain it, then it is optional
            let m = if m = Single then OptionalSingle else m
            let t = if m <> Single then t.DropOptionality() else t
            tag, (m, t)
        | tag, Some(KeyValue(_, (m1, t1))), Some(KeyValue(_, (m2, t2))) ->
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
        | _ -> failwith "unionCollectionTypes: pairBy returned None, None")

and internal unionCollectionOrder order1 order2 =
    order1 @ (order2 |> List.filter (fun x -> not (List.exists ((=) x) order1)))

/// Get the union of record types (merge their properties)
/// This matches the corresponding members and marks them as `Optional`
/// if one may be missing. It also returns subtype of their types.
and internal unionRecordTypes allowEmptyValues t1 t2 =
    List.pairBy (fun (p: InferedProperty) -> p.Name) t1 t2
    |> List.map (fun (name, fst, snd) ->
        match fst, snd with
        // If one is missing, return the other, but optional
        | Some p, None
        | None, Some p ->
            { p with
                Type = subtypeInfered allowEmptyValues p.Type InferedType.Null }
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
let internal inferCollectionType allowEmptyValues types =
    let groupedTypes =
        types
        |> Seq.groupBy typeTag
        |> Seq.map (fun (tag, types) ->
            let multiple = if Seq.length types > 1 then Multiple else Single
            tag, (multiple, Seq.fold (subtypeInfered allowEmptyValues) InferedType.Top types))
        |> Seq.toList

    InferedType.Collection(List.map fst groupedTypes, Map.ofList groupedTypes)

[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
type IUnitsOfMeasureProvider =
    abstract SI: str: string -> System.Type
    abstract Product: measure1: System.Type * measure2: System.Type -> System.Type
    abstract Inverse: denominator: System.Type -> System.Type

[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
let defaultUnitsOfMeasureProvider =
    { new IUnitsOfMeasureProvider with
        member x.SI(_) : Type = null
        member x.Product(_, _) = failwith "Not implemented yet"
        member x.Inverse(_) = failwith "Not implemented yet" }

let private uomTransformations =
    [ [ "²"; "^2" ], (fun (provider: IUnitsOfMeasureProvider) t -> provider.Product(t, t))
      [ "³"; "^3" ], (fun (provider: IUnitsOfMeasureProvider) t -> provider.Product(provider.Product(t, t), t))
      [ "^-1" ], (fun (provider: IUnitsOfMeasureProvider) t -> provider.Inverse(t)) ]

[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
let parseUnitOfMeasure (provider: IUnitsOfMeasureProvider) (str: string) =
    let unit =
        uomTransformations
        |> List.collect (fun (suffixes, trans) -> suffixes |> List.map (fun suffix -> suffix, trans))
        |> List.tryPick (fun (suffix, trans) ->
            if str.EndsWith suffix then
                let baseUnitStr = str.[.. str.Length - suffix.Length - 1]
                let baseUnit = provider.SI baseUnitStr

                if isNull baseUnit then
                    None
                else
                    baseUnit |> trans provider |> Some
            else
                None)

    match unit with
    | Some _ -> unit
    | None ->
        let unit = provider.SI str
        if isNull unit then None else Some unit

/// The inferred types may be set explicitly via inline schemas.
/// This table specifies the mapping from (the names that users can use) to (the types used).
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
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
      "string", (typeof<String>, TypeWrapper.None)
#if NET6_0_OR_GREATER
      "dateonly", (typeof<DateOnly>, TypeWrapper.None)
      "timeonly", (typeof<TimeOnly>, TypeWrapper.None)
#endif
      ]
    |> dict

// type<unit} or type{unit> is valid while it shouldn't, but well...
let private typeAndUnitRegex =
    lazy Regex(@"^(?<type>.+)(<|{)(?<unit>.+)(>|})$", RegexOptions.Compiled ||| RegexOptions.RightToLeft)

/// Matches a value of the form "typeof<value>" where the nested value is of the form "type<unit>" or just "type".
/// ({} instead of <> is allowed so it can be used in xml)
let private validInlineSchema =
    lazy
        Regex(
            @"^typeof(<|{)"
            + @"(?<typeDefinition>(?<typeOrUnit>[^<>{}\s]+)|(?<typeAndUnit>[^<>{}\s]+(<|{)[^<>{}\s]+(>|})))"
            + @"(>|})$",
            RegexOptions.Compiled
        )

/// <summary>
/// Parses type specification in the schema for a single value.
/// This can be of the form: <c>type|measure|type&lt;measure&gt;</c>
/// type{measure} is also supported to ease definition in xml values.
/// </summary>
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
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
        let typ = str.ToLowerInvariant() |> nameToType.TryGetValue |> asOption

        match typ with
        | Some(typ, typWrapper) ->
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

/// Infers the type of a string value
/// Returns one of null|typeof<Bit0>|typeof<Bit1>|typeof<bool>|typeof<int>|typeof<int64>|typeof<decimal>|typeof<float>|typeof<Guid>|typeof<DateTime>|typeof<TimeSpan>|typeof<string>
/// with the desiredUnit applied,
/// or a value parsed from an inline schema.
/// (For inline schemas, the unit parsed from the schema takes precedence over desiredUnit when present)
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
let inferPrimitiveType
    (unitsOfMeasureProvider: IUnitsOfMeasureProvider)
    (inferenceMode: InferenceMode')
    (cultureInfo: CultureInfo)
    (preferFloats: bool)
    (value: string)
    (desiredUnit: Type option)
    =

    // Helper for calling TextConversions.AsXyz functions
    let (|Parse|_|) func value = func cultureInfo value
    let (|ParseNoCulture|_|) func value = func value

    let asGuid _ value = TextConversions.AsGuid value

    let getAbbreviatedEraName era =
        cultureInfo.DateTimeFormat.GetAbbreviatedEraName(era)

    let isFakeDate (date: DateTime) value =
        // If this can be considered a decimal under the invariant culture,
        // it's a safer bet to consider it a string than a DateTime
        TextConversions.AsDecimal CultureInfo.InvariantCulture value |> Option.isSome
        ||
        // Prevent stuff like 12-002 being considered a date
        date.Year < 1000 && numberOfNumberGroups value <> 3
        ||
        // Prevent stuff like ad3mar being considered a date
        cultureInfo.Calendar.Eras
        |> Array.exists (fun era ->
            value.IndexOf(cultureInfo.DateTimeFormat.GetEraName(era), StringComparison.OrdinalIgnoreCase)
            >= 0
            || value.IndexOf(getAbbreviatedEraName era, StringComparison.OrdinalIgnoreCase)
               >= 0)

    let matchValue value =
        let makePrimitive typ =
            Some(InferedType.Primitive(typ, desiredUnit, false, false))

        match value with
        | "" -> Some InferedType.Null
        | Parse TextConversions.AsInteger 0 -> makePrimitive typeof<Bit0>
        | Parse TextConversions.AsInteger 1 -> makePrimitive typeof<Bit1>
        | ParseNoCulture TextConversions.AsBoolean _ -> makePrimitive typeof<bool>
        | Parse TextConversions.AsInteger _ -> makePrimitive typeof<int>
        | Parse TextConversions.AsInteger64 _ -> makePrimitive typeof<int64>
        | Parse TextConversions.AsTimeSpan _ -> makePrimitive typeof<TimeSpan>
        | Parse TextConversions.AsDateTimeOffset dateTimeOffset when not (isFakeDate dateTimeOffset.UtcDateTime value) ->
            makePrimitive typeof<DateTimeOffset>
#if NET6_0_OR_GREATER
        | Parse TextConversions.AsDateOnly dateOnly when not (isFakeDate (dateOnly.ToDateTime(TimeOnly.MinValue)) value) ->
            makePrimitive typeof<DateOnly>
#endif
        | Parse TextConversions.AsDateTime date when not (isFakeDate date value) -> makePrimitive typeof<DateTime>
        | Parse TextConversions.AsDecimal _ when not preferFloats -> makePrimitive typeof<decimal>
        | Parse (TextConversions.AsFloat [||] false) _ -> makePrimitive typeof<float>
        | Parse asGuid _ -> makePrimitive typeof<Guid>
        | _ -> None

    /// Parses values looking like "typeof<int> or typeof<int<metre>>" and returns the appropriate type.
    let matchInlineSchema useInlineSchemasOverrides value =
        match value with
        | "" -> Some InferedType.Null
        | nonEmptyValue ->
            // Validates that it looks like an inline schema before trying to extract the type and unit:
            let m = validInlineSchema.Value.Match(nonEmptyValue)

            match m.Success with
            | false -> None
            | true ->
                let typ, unit =
                    parseTypeAndUnit unitsOfMeasureProvider nameToType m.Groups.["typeDefinition"].Value

                let unit = if unit.IsNone then desiredUnit else unit

                match typ, unit with
                | None, _ -> None
                | Some(typ, typeWrapper), unit ->
                    match typeWrapper with
                    | TypeWrapper.None -> Some(InferedType.Primitive(typ, unit, false, useInlineSchemasOverrides))
                    // To keep it simple and prevent weird situations (and preserve backward compat),
                    // only structural inference can create optional types.
                    // Optional types in inline schemas are not allowed.
                    | TypeWrapper.Option -> failwith "Option types are not allowed in inline schemas."
                    | TypeWrapper.Nullable -> failwith "Nullable types are not allowed in inline schemas."

    let fallbackType = InferedType.Primitive(typeof<string>, None, false, false)

    match inferenceMode with
    | InferenceMode'.NoInference -> fallbackType
    | InferenceMode'.ValuesOnly -> matchValue value |> Option.defaultValue fallbackType
    | InferenceMode'.ValuesAndInlineSchemasHints ->
        matchInlineSchema false value
        |> Option.orElseWith (fun () -> matchValue value)
        |> Option.defaultValue fallbackType
    | InferenceMode'.ValuesAndInlineSchemasOverrides ->
        matchInlineSchema true value
        |> Option.orElseWith (fun () -> matchValue value)
        |> Option.defaultValue fallbackType

/// Infers the type of a simple string value
[<Obsolete("This API will be made internal in a future release. Please file an issue at https://github.com/fsprojects/FSharp.Data/issues/1458 if you need this public.")>]
let getInferedTypeFromString unitsOfMeasureProvider inferenceMode cultureInfo value unit =
    inferPrimitiveType unitsOfMeasureProvider inferenceMode cultureInfo false value unit

/// Infers the type of a simple string value, preferring float over decimal
let internal getInferedTypeFromStringPreferFloats unitsOfMeasureProvider inferenceMode cultureInfo value unit =
    inferPrimitiveType unitsOfMeasureProvider inferenceMode cultureInfo true value unit

#if NET6_0_OR_GREATER
/// Replaces DateOnly → DateTime and TimeOnly → TimeSpan throughout an InferedType tree.
/// Used in design-time code when the target framework does not support these .NET 6+ types.
let internal downgradeNet6Types (inferedType: InferedType) : InferedType =
    let downgradeTag tag =
        match tag with
        | InferedTypeTag.DateOnly -> InferedTypeTag.DateTime
        | InferedTypeTag.TimeOnly -> InferedTypeTag.TimeSpan
        | _ -> tag

    let downgradeType (typ: Type) =
        if typ = typeof<DateOnly> then typeof<DateTime>
        elif typ = typeof<TimeOnly> then typeof<TimeSpan>
        else typ

    // Use reference-equality-based visited set to handle cyclic InferedType graphs
    // (e.g. recursive XML schemas). When a cycle is detected we return the original node.
    let visited =
        System.Collections.Generic.HashSet<InferedType>(
            { new System.Collections.Generic.IEqualityComparer<InferedType> with
                member _.Equals(x, y) = obj.ReferenceEquals(x, y)

                member _.GetHashCode(x) =
                    System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(x) }
        )

    let rec convert infType =
        if not (visited.Add(infType)) then
            infType // cycle detected – return original to avoid infinite recursion
        else
            let result =
                match infType with
                | InferedType.Primitive(typ, unit, optional, overrideOnMerge) ->
                    InferedType.Primitive(downgradeType typ, unit, optional, overrideOnMerge)
                | InferedType.Record(name, props, optional) ->
                    InferedType.Record(name, props |> List.map (fun p -> { p with Type = convert p.Type }), optional)
                | InferedType.Collection(order, types) ->
                    InferedType.Collection(
                        order |> List.map downgradeTag,
                        types
                        |> Map.toSeq
                        |> Seq.map (fun (k, (m, t)) -> downgradeTag k, (m, convert t))
                        |> Map.ofSeq
                    )
                | InferedType.Heterogeneous(types, containsOptional) ->
                    InferedType.Heterogeneous(
                        types
                        |> Map.toSeq
                        |> Seq.map (fun (k, t) -> downgradeTag k, convert t)
                        |> Map.ofSeq,
                        containsOptional
                    )
                | InferedType.Json(innerType, optional) -> InferedType.Json(convert innerType, optional)
                | _ -> infType

            result

    convert inferedType

/// Replaces DateOnly → DateTime and TimeOnly → TimeSpan in a PrimitiveInferedProperty.
/// Used in design-time code when the target framework does not support these .NET 6+ types.
let internal downgradeNet6PrimitiveProperty (field: StructuralTypes.PrimitiveInferedProperty) =
    let v = field.Value

    if v.InferedType = typeof<DateOnly> then
        { field with
            Value =
                { v with
                    InferedType = typeof<DateTime>
                    RuntimeType = typeof<DateTime> } }
    elif v.InferedType = typeof<TimeOnly> then
        { field with
            Value =
                { v with
                    InferedType = typeof<TimeSpan>
                    RuntimeType = typeof<TimeSpan> } }
    else
        field
#endif

/// Replaces DateTime → DateTimeOffset throughout an InferedType tree.
/// Used when PreferDateTimeOffset=true to infer all date-time values as DateTimeOffset.
let internal upgradeToDateTimeOffset (inferedType: InferedType) : InferedType =
    let upgradeTag tag =
        match tag with
        | InferedTypeTag.DateTime -> InferedTypeTag.DateTimeOffset
        | _ -> tag

    let upgradeType (typ: Type) =
        if typ = typeof<DateTime> then
            typeof<DateTimeOffset>
        else
            typ

    let visited =
        System.Collections.Generic.HashSet<InferedType>(
            { new System.Collections.Generic.IEqualityComparer<InferedType> with
                member _.Equals(x, y) = obj.ReferenceEquals(x, y)

                member _.GetHashCode(x) =
                    System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(x) }
        )

    let rec convert infType =
        if not (visited.Add(infType)) then
            infType
        else
            match infType with
            | InferedType.Primitive(typ, unit, optional, overrideOnMerge) ->
                InferedType.Primitive(upgradeType typ, unit, optional, overrideOnMerge)
            | InferedType.Record(name, props, optional) ->
                InferedType.Record(name, props |> List.map (fun p -> { p with Type = convert p.Type }), optional)
            | InferedType.Collection(order, types) ->
                InferedType.Collection(
                    order |> List.map upgradeTag,
                    types
                    |> Map.toSeq
                    |> Seq.map (fun (k, (m, t)) -> upgradeTag k, (m, convert t))
                    |> Map.ofSeq
                )
            | InferedType.Heterogeneous(types, containsOptional) ->
                InferedType.Heterogeneous(
                    types
                    |> Map.toSeq
                    |> Seq.map (fun (k, t) -> upgradeTag k, convert t)
                    |> Map.ofSeq,
                    containsOptional
                )
            | InferedType.Json(innerType, optional) -> InferedType.Json(convert innerType, optional)
            | _ -> infType

    convert inferedType

/// Replaces DateTime → DateTimeOffset in a PrimitiveInferedProperty.
/// Used when PreferDateTimeOffset=true.
let internal upgradeToDateTimeOffsetPrimitiveProperty (field: StructuralTypes.PrimitiveInferedProperty) =
    let v = field.Value

    if v.InferedType = typeof<DateTime> then
        { field with
            Value =
                { v with
                    InferedType = typeof<DateTimeOffset>
                    RuntimeType = typeof<DateTimeOffset> } }
    else
        field
