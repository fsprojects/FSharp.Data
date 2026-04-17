module FSharp.Data.Tests.StructuralInference

open System
open System.Globalization
open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference

// Helpers

let private culture = CultureInfo.InvariantCulture
let private uomProvider = defaultUnitsOfMeasureProvider
let private valuesOnly = InferenceMode'.ValuesOnly
let private noInference = InferenceMode'.NoInference

let private prim typ = InferedType.Primitive(typ, None, false, false)
let private primOpt typ = InferedType.Primitive(typ, None, true, false)

let private inferStr value =
    getInferedTypeFromString uomProvider valuesOnly culture value None

// ─── typeTag ────────────────────────────────────────────────────────────────

[<Test>]
let ``typeTag returns Number for numeric primitives`` () =
    typeTag (prim typeof<int>) |> should equal InferedTypeTag.Number
    typeTag (prim typeof<int64>) |> should equal InferedTypeTag.Number
    typeTag (prim typeof<decimal>) |> should equal InferedTypeTag.Number
    typeTag (prim typeof<float>) |> should equal InferedTypeTag.Number
    typeTag (prim typeof<Bit>) |> should equal InferedTypeTag.Number
    typeTag (prim typeof<Bit0>) |> should equal InferedTypeTag.Number
    typeTag (prim typeof<Bit1>) |> should equal InferedTypeTag.Number

[<Test>]
let ``typeTag returns correct tag for other primitive types`` () =
    typeTag (prim typeof<bool>) |> should equal InferedTypeTag.Boolean
    typeTag (prim typeof<string>) |> should equal InferedTypeTag.String
    typeTag (prim typeof<DateTime>) |> should equal InferedTypeTag.DateTime
    typeTag (prim typeof<DateTimeOffset>) |> should equal InferedTypeTag.DateTime
    typeTag (prim typeof<TimeSpan>) |> should equal InferedTypeTag.TimeSpan
    typeTag (prim typeof<Guid>) |> should equal InferedTypeTag.Guid

[<Test>]
let ``typeTag returns Null for Null and Top`` () =
    typeTag InferedType.Null |> should equal InferedTypeTag.Null
    typeTag InferedType.Top |> should equal InferedTypeTag.Null

[<Test>]
let ``typeTag returns correct tag for record, collection, heterogeneous, json`` () =
    typeTag (InferedType.Record(Some "Foo", [], false)) |> should equal (InferedTypeTag.Record(Some "Foo"))
    typeTag (InferedType.Record(None, [], false)) |> should equal (InferedTypeTag.Record None)
    typeTag (InferedType.Collection([], Map.empty)) |> should equal InferedTypeTag.Collection
    typeTag (InferedType.Heterogeneous(Map.empty, false)) |> should equal InferedTypeTag.Heterogeneous
    typeTag (InferedType.Json(prim typeof<string>, false)) |> should equal InferedTypeTag.Json

#if NET6_0_OR_GREATER
[<Test>]
let ``typeTag returns DateOnly and TimeOnly on NET6+`` () =
    typeTag (prim typeof<DateOnly>) |> should equal InferedTypeTag.DateOnly
    typeTag (prim typeof<TimeOnly>) |> should equal InferedTypeTag.TimeOnly
#endif

// ─── subtypeInfered: Top and Null ───────────────────────────────────────────

[<Test>]
let ``subtypeInfered: Top merged with any type returns that type`` () =
    subtypeInfered false InferedType.Top (prim typeof<int>) |> should equal (prim typeof<int>)
    subtypeInfered false (prim typeof<string>) InferedType.Top |> should equal (prim typeof<string>)
    subtypeInfered false InferedType.Top InferedType.Null |> should equal InferedType.Null
    subtypeInfered false InferedType.Top InferedType.Top |> should equal InferedType.Top

[<Test>]
let ``subtypeInfered: Null merged with string makes it optional`` () =
    // string can have empty values so with allowEmptyValues=true, string stays non-optional
    let result = subtypeInfered true InferedType.Null (prim typeof<string>)

    match result with
    | InferedType.Primitive(typ, None, false, false) when typ = typeof<string> -> () // stays non-optional
    | _ -> failwithf "Unexpected result: %A" result

[<Test>]
let ``subtypeInfered: Null merged with int makes it optional`` () =
    // int cannot have empty values so it becomes optional regardless of allowEmptyValues
    let result = subtypeInfered false InferedType.Null (prim typeof<int>)

    match result with
    | InferedType.Primitive(typ, None, true, false) when typ = typeof<int> -> ()
    | _ -> failwithf "Unexpected result: %A" result

// ─── subtypeInfered: primitives ─────────────────────────────────────────────

[<Test>]
let ``subtypeInfered: identical primitives return same primitive`` () =
    subtypeInfered false (prim typeof<int>) (prim typeof<int>) |> should equal (prim typeof<int>)
    subtypeInfered false (prim typeof<string>) (prim typeof<string>) |> should equal (prim typeof<string>)
    subtypeInfered false (prim typeof<bool>) (prim typeof<bool>) |> should equal (prim typeof<bool>)

[<Test>]
let ``subtypeInfered: numeric widening Bit0 + Bit1 → Bit`` () =
    let result = subtypeInfered false (prim typeof<Bit0>) (prim typeof<Bit1>)

    match result with
    | InferedType.Primitive(typ, None, false, false) when typ = typeof<Bit> -> ()
    | _ -> failwithf "Unexpected result: %A" result

[<Test>]
let ``subtypeInfered: numeric widening int + int64 → int64`` () =
    let result = subtypeInfered false (prim typeof<int>) (prim typeof<int64>)

    match result with
    | InferedType.Primitive(typ, None, false, false) when typ = typeof<int64> -> ()
    | _ -> failwithf "Unexpected result: %A" result

[<Test>]
let ``subtypeInfered: numeric widening int + float → float`` () =
    let result = subtypeInfered false (prim typeof<int>) (prim typeof<float>)

    match result with
    | InferedType.Primitive(typ, None, false, false) when typ = typeof<float> -> ()
    | _ -> failwithf "Unexpected result: %A" result

[<Test>]
let ``subtypeInfered: numeric widening decimal + float → float`` () =
    let result = subtypeInfered false (prim typeof<decimal>) (prim typeof<float>)

    match result with
    | InferedType.Primitive(typ, None, false, false) when typ = typeof<float> -> ()
    | _ -> failwithf "Unexpected result: %A" result

[<Test>]
let ``subtypeInfered: Bit0 + bool → bool`` () =
    // bool ⊇ Bit0 (bool has Bit0 in conversion table)
    let result = subtypeInfered false (prim typeof<Bit0>) (prim typeof<bool>)

    match result with
    | InferedType.Primitive(typ, None, false, false) when typ = typeof<bool> -> ()
    | _ -> failwithf "Unexpected result: %A" result

[<Test>]
let ``subtypeInfered: incompatible types create Heterogeneous`` () =
    let result = subtypeInfered false (prim typeof<string>) (prim typeof<int>)

    match result with
    | InferedType.Heterogeneous(map, false) ->
        map |> Map.containsKey InferedTypeTag.String |> should equal true
        map |> Map.containsKey InferedTypeTag.Number |> should equal true
    | _ -> failwithf "Unexpected result: %A" result

[<Test>]
let ``subtypeInfered: Guid + string creates Heterogeneous`` () =
    let result = subtypeInfered false (prim typeof<Guid>) (prim typeof<string>)

    match result with
    | InferedType.Heterogeneous(map, false) ->
        map |> Map.containsKey InferedTypeTag.Guid |> should equal true
        map |> Map.containsKey InferedTypeTag.String |> should equal true
    | _ -> failwithf "Unexpected result: %A" result

[<Test>]
let ``subtypeInfered: optionality is preserved when merging`` () =
    // Required int merged with required int → required int
    subtypeInfered false (prim typeof<int>) (prim typeof<int>) |> should equal (prim typeof<int>)

    // Optional int merged with required int → optional int
    let result = subtypeInfered false (primOpt typeof<int>) (prim typeof<int>)

    match result with
    | InferedType.Primitive(typ, None, true, false) when typ = typeof<int> -> ()
    | _ -> failwithf "Unexpected result: %A" result

// ─── subtypeInfered: records ────────────────────────────────────────────────

[<Test>]
let ``subtypeInfered: records with same name union their fields`` () =
    let record1 =
        InferedType.Record(
            Some "R",
            [ { Name = "a"; Type = prim typeof<int> }
              { Name = "b"; Type = prim typeof<string> } ],
            false
        )

    let record2 =
        InferedType.Record(
            Some "R",
            [ { Name = "a"; Type = prim typeof<int> }
              { Name = "c"; Type = prim typeof<bool> } ],
            false
        )

    let result = subtypeInfered false record1 record2

    match result with
    | InferedType.Record(Some "R", fields, false) ->
        fields |> List.map (fun f -> f.Name) |> List.sort |> should equal [ "a"; "b"; "c" ]
        // 'b' is in record1 only → optional in result
        let bField = fields |> List.find (fun f -> f.Name = "b")

        match bField.Type with
        | InferedType.Primitive(_, _, true, _) -> () // optional
        | _ -> failwithf "'b' should be optional, got %A" bField.Type
    | _ -> failwithf "Unexpected result: %A" result

[<Test>]
let ``subtypeInfered: records with different names become Heterogeneous`` () =
    let record1 = InferedType.Record(Some "R1", [], false)
    let record2 = InferedType.Record(Some "R2", [], false)
    let result = subtypeInfered false record1 record2

    match result with
    | InferedType.Heterogeneous _ -> ()
    | _ -> failwithf "Expected Heterogeneous, got %A" result

// ─── inferCollectionType ────────────────────────────────────────────────────

[<Test>]
let ``inferCollectionType with single int type gives Collection with Number`` () =
    let result = inferCollectionType false [ prim typeof<int> ]

    match result with
    | InferedType.Collection(order, map) ->
        order |> should contain InferedTypeTag.Number
        map |> Map.containsKey InferedTypeTag.Number |> should equal true
    | _ -> failwithf "Unexpected result: %A" result

[<Test>]
let ``inferCollectionType with mixed numeric types widens to float`` () =
    let result =
        inferCollectionType false [ prim typeof<int>; prim typeof<float>; prim typeof<decimal> ]

    match result with
    | InferedType.Collection(_, map) ->
        let _, elemType = map |> Map.find InferedTypeTag.Number

        match elemType with
        | InferedType.Primitive(typ, None, false, false) when typ = typeof<float> -> ()
        | _ -> failwithf "Expected float element type, got %A" elemType
    | _ -> failwithf "Unexpected result: %A" result

[<Test>]
let ``inferCollectionType with multiple items of same type gives Multiple multiplicity`` () =
    let result =
        inferCollectionType false [ prim typeof<int>; prim typeof<int>; prim typeof<int> ]

    match result with
    | InferedType.Collection(_, map) ->
        let mult, _ = map |> Map.find InferedTypeTag.Number
        mult |> should equal InferedMultiplicity.Multiple
    | _ -> failwithf "Unexpected result: %A" result

[<Test>]
let ``inferCollectionType with single item gives Single multiplicity`` () =
    let result = inferCollectionType false [ prim typeof<int> ]

    match result with
    | InferedType.Collection(_, map) ->
        let mult, _ = map |> Map.find InferedTypeTag.Number
        mult |> should equal InferedMultiplicity.Single
    | _ -> failwithf "Unexpected result: %A" result

// ─── getInferedTypeFromString ────────────────────────────────────────────────

[<Test>]
let ``getInferedTypeFromString: empty string returns Null`` () =
    inferStr "" |> should equal InferedType.Null

[<Test>]
let ``getInferedTypeFromString: "0" and "1" infer Bit types`` () =
    inferStr "0" |> should equal (prim typeof<Bit0>)
    inferStr "1" |> should equal (prim typeof<Bit1>)

[<Test>]
let ``getInferedTypeFromString: boolean strings infer bool`` () =
    inferStr "true" |> should equal (prim typeof<bool>)
    inferStr "false" |> should equal (prim typeof<bool>)
    inferStr "True" |> should equal (prim typeof<bool>)
    inferStr "yes" |> should equal (prim typeof<bool>)
    inferStr "no" |> should equal (prim typeof<bool>)

[<Test>]
let ``getInferedTypeFromString: integer strings infer int`` () =
    inferStr "42" |> should equal (prim typeof<int>)
    inferStr "-123" |> should equal (prim typeof<int>)
    inferStr "2147483647" |> should equal (prim typeof<int>) // Int32.MaxValue

[<Test>]
let ``getInferedTypeFromString: large integer strings infer int64`` () =
    inferStr "2147483648" |> should equal (prim typeof<int64>) // Int32.MaxValue + 1
    inferStr "9999999999" |> should equal (prim typeof<int64>)

[<Test>]
let ``getInferedTypeFromString: decimal strings infer decimal`` () =
    inferStr "3.14" |> should equal (prim typeof<decimal>)
    inferStr "-0.5" |> should equal (prim typeof<decimal>)

[<Test>]
let ``getInferedTypeFromString: unrecognised strings fall back to string`` () =
    inferStr "hello" |> should equal (prim typeof<string>)
    inferStr "not-a-number" |> should equal (prim typeof<string>)
    inferStr "abc123" |> should equal (prim typeof<string>)

[<Test>]
let ``getInferedTypeFromString: GUID strings infer Guid`` () =
    inferStr "6F9619FF-8B86-D011-B42D-00C04FC964FF" |> should equal (prim typeof<Guid>)

[<Test>]
let ``getInferedTypeFromString: NoInference mode always returns string`` () =
    let inferNoInference value =
        getInferedTypeFromString uomProvider noInference culture value None

    inferNoInference "42" |> should equal (prim typeof<string>)
    inferNoInference "true" |> should equal (prim typeof<string>)
    inferNoInference "3.14" |> should equal (prim typeof<string>)

// ─── InferenceMode' ─────────────────────────────────────────────────────────

[<Test>]
let ``InferenceMode FromPublicApi BackwardCompatible with legacyInferTypesFromValues=true → ValuesOnly`` () =
    InferenceMode'.FromPublicApi(InferenceMode.BackwardCompatible, true)
    |> should equal InferenceMode'.ValuesOnly

[<Test>]
let ``InferenceMode FromPublicApi BackwardCompatible with legacyInferTypesFromValues=false → NoInference`` () =
    InferenceMode'.FromPublicApi(InferenceMode.BackwardCompatible, false)
    |> should equal InferenceMode'.NoInference

[<Test>]
let ``InferenceMode FromPublicApi explicit modes map correctly`` () =
    InferenceMode'.FromPublicApi(InferenceMode.NoInference) |> should equal InferenceMode'.NoInference
    InferenceMode'.FromPublicApi(InferenceMode.ValuesOnly) |> should equal InferenceMode'.ValuesOnly

    InferenceMode'.FromPublicApi(InferenceMode.ValuesAndInlineSchemasHints)
    |> should equal InferenceMode'.ValuesAndInlineSchemasHints

    InferenceMode'.FromPublicApi(InferenceMode.ValuesAndInlineSchemasOverrides)
    |> should equal InferenceMode'.ValuesAndInlineSchemasOverrides

// ─── supportsUnitsOfMeasure ──────────────────────────────────────────────────

[<Test>]
let ``supportsUnitsOfMeasure returns true for numeric types`` () =
    supportsUnitsOfMeasure typeof<int> |> should equal true
    supportsUnitsOfMeasure typeof<int64> |> should equal true
    supportsUnitsOfMeasure typeof<decimal> |> should equal true
    supportsUnitsOfMeasure typeof<float> |> should equal true

[<Test>]
let ``supportsUnitsOfMeasure returns false for non-numeric types`` () =
    supportsUnitsOfMeasure typeof<string> |> should equal false
    supportsUnitsOfMeasure typeof<bool> |> should equal false
    supportsUnitsOfMeasure typeof<DateTime> |> should equal false
    supportsUnitsOfMeasure typeof<Guid> |> should equal false
