// --------------------------------------------------------------------------------------
// JSON type provider - methods that are called from the generated erased code
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Runtime

open System
open System.Globalization
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralTypes

/// <exclude />
type JsonValueOptionAndPath =
    { JsonOpt: JsonValue option
      Path: string }

/// Static helper methods called from the generated code for working with JSON
type JsonRuntime =

    // --------------------------------------------------------------------------------------
    // json option -> type

    static member ConvertString(cultureStr, json) =
        json
        |> Option.bind (JsonConversions.AsString true (TextRuntime.GetCulture cultureStr))

    static member ConvertInteger(cultureStr, json) =
        json
        |> Option.bind (JsonConversions.AsInteger(TextRuntime.GetCulture cultureStr))

    static member ConvertInteger64(cultureStr, json) =
        json
        |> Option.bind (JsonConversions.AsInteger64(TextRuntime.GetCulture cultureStr))

    static member ConvertDecimal(cultureStr, json) =
        json
        |> Option.bind (JsonConversions.AsDecimal(TextRuntime.GetCulture cultureStr))

    static member ConvertFloat(cultureStr, missingValuesStr, json) =
        json
        |> Option.bind (
            JsonConversions.AsFloat
                (TextRuntime.GetMissingValues missingValuesStr)
                true
                (TextRuntime.GetCulture cultureStr)
        )

    static member ConvertBoolean(json) =
        json |> Option.bind JsonConversions.AsBoolean

    static member ConvertDateTimeOffset(cultureStr, json) =
        json
        |> Option.bind (JsonConversions.AsDateTimeOffset(TextRuntime.GetCulture cultureStr))

    static member ConvertDateTime(cultureStr, json) =
        json
        |> Option.bind (JsonConversions.AsDateTime(TextRuntime.GetCulture cultureStr))

    static member ConvertTimeSpan(cultureStr, json) =
        json
        |> Option.bind (JsonConversions.AsTimeSpan(TextRuntime.GetCulture cultureStr))

    static member ConvertGuid(json) =
        json |> Option.bind JsonConversions.AsGuid

    /// Operation that extracts the value from an option and reports a meaningful error message when the value is not there
    /// If the originalValue is a scalar, for missing strings we return "", and for missing doubles we return NaN
    /// For other types an error is thrown
    static member GetNonOptionalValue<'T>(path: string, opt: option<'T>, originalValue) : 'T =
        let getTypeName () =
            let name = typeof<'T>.Name

            if name.StartsWith("i", StringComparison.OrdinalIgnoreCase) then
                "an " + name
            else
                "a " + name

        match opt, originalValue with
        | Some value, _ -> value
        | None,
          Some ((JsonValue.Array _
          | JsonValue.Record _) as x) ->
            failwithf "Expecting %s at '%s', got %s" (getTypeName ()) path
            <| x.ToString(JsonSaveOptions.DisableFormatting)
        | None, _ when typeof<'T> = typeof<string> -> "" |> unbox
        | None, _ when typeof<'T> = typeof<float> -> Double.NaN |> unbox
        | None, None -> failwithf "'%s' is missing" path
        | None, Some x ->
            failwithf "Expecting %s at '%s', got %s" (getTypeName ()) path
            <| x.ToString(JsonSaveOptions.DisableFormatting)

    /// Converts JSON array to array of target types
    static member ConvertArray<'T>(doc: IJsonDocument, mapping: Func<IJsonDocument, 'T>) =
        match doc.JsonValue with
        | JsonValue.Array elements ->
            elements
            |> Array.filter (function
                | JsonValue.Null -> false
                | JsonValue.String s when s |> TextConversions.AsString |> Option.isNone -> false
                | _ -> true)
            |> Array.mapi (fun i value ->
                doc.CreateNew(value, "[" + (string i) + "]")
                |> mapping.Invoke)
        | JsonValue.Null -> [||]
        | x ->
            failwithf "Expecting an array at '%s', got %s" (doc.Path())
            <| x.ToString(JsonSaveOptions.DisableFormatting)

    /// Get properties of the record
    static member GetRecordProperties(doc: IJsonDocument) =
        match doc.JsonValue with
        | JsonValue.Record items -> items
        | JsonValue.Null -> [||]
        | x ->
            failwithf "Expecting a record at '%s', got %s" (doc.Path())
            <| x.ToString(JsonSaveOptions.DisableFormatting)

    /// Converts JSON record to dictionary
    static member ConvertRecordToDictionary<'Key, 'Value when 'Key: equality>
        (
            doc: IJsonDocument,
            mappingKey: Func<IJsonDocument, 'Key>,
            mappingValue: Func<IJsonDocument, 'Value>
        ) =
        JsonRuntime.GetRecordProperties(doc)
        |> Seq.map (fun (k, v) ->
            let key =
                doc.CreateNew(JsonValue.String k, k)
                |> mappingKey.Invoke

            let value = doc.CreateNew(v, k) |> mappingValue.Invoke
            key, value)


    /// Get a value by the key from infered dictionary
    static member InferedDictionaryContainsKey<'Key when 'Key: equality>
        (
            doc: IJsonDocument,
            mappingKey: Func<IJsonDocument, 'Key>,
            key: 'Key
        ) =
        let finder (k, _) =
            (doc.CreateNew(JsonValue.String k, k)
             |> mappingKey.Invoke) = key

        (JsonRuntime.GetRecordProperties(doc)
         |> Array.tryFind finder)
            .IsSome

    /// Try get a value by the key from infered dictionary
    static member TryGetValueByKeyFromInferedDictionary<'Key, 'Value when 'Key: equality>
        (
            doc: IJsonDocument,
            mappingKey: Func<IJsonDocument, 'Key>,
            mappingValue: Func<IJsonDocument, 'Value>,
            key: 'Key
        ) =
        let picker (k, v) =
            if (doc.CreateNew(JsonValue.String k, k)
                |> mappingKey.Invoke) = key then
                doc.CreateNew(v, k) |> mappingValue.Invoke |> Some
            else
                None

        JsonRuntime.GetRecordProperties(doc)
        |> Array.tryPick picker

    /// Get a value by the key from infered dictionary
    static member GetValueByKeyFromInferedDictionary<'Key, 'Value when 'Key: equality>
        (
            doc: IJsonDocument,
            mappingKey: Func<IJsonDocument, 'Key>,
            mappingValue: Func<IJsonDocument, 'Value>,
            key: 'Key
        ) =
        match JsonRuntime.TryGetValueByKeyFromInferedDictionary(doc, mappingKey, mappingValue, key) with
        | Some value -> value
        | _ ->
            key
            |> sprintf "The given key '%A' was not present in the dictionary."
            |> System.Collections.Generic.KeyNotFoundException
            |> raise

    /// Get keys from infered dictionary
    static member GetKeysFromInferedDictionary<'Key when 'Key: equality>
        (
            doc: IJsonDocument,
            mappingKey: Func<IJsonDocument, 'Key>
        ) =
        JsonRuntime.GetRecordProperties(doc)
        |> Array.map (fun (k, _) ->
            doc.CreateNew(JsonValue.String k, k)
            |> mappingKey.Invoke)

    /// Get values from infered dictionary
    static member GetValuesFromInferedDictionary<'Value>
        (
            doc: IJsonDocument,
            mappingValue: Func<IJsonDocument, 'Value>
        ) =
        JsonRuntime.GetRecordProperties(doc)
        |> Array.map (fun (k, v) -> doc.CreateNew(v, k) |> mappingValue.Invoke)

    /// Get optional json property
    static member TryGetPropertyUnpacked(doc: IJsonDocument, name) =
        doc.JsonValue.TryGetProperty(name)
        |> Option.bind (function
            | JsonValue.Null
            | JsonValue.String "" -> None
            | x -> Some x)

    /// Get optional json property and wrap it together with path
    static member TryGetPropertyUnpackedWithPath(doc: IJsonDocument, name) =
        { JsonOpt = JsonRuntime.TryGetPropertyUnpacked(doc, name)
          Path = doc.Path() + "/" + name }

    /// Get optional json property wrapped in json document
    static member TryGetPropertyPacked(doc: IJsonDocument, name) =
        JsonRuntime.TryGetPropertyUnpacked(doc, name)
        |> Option.map (fun value -> doc.CreateNew(value, "/" + name))

    /// Get json property and wrap in json document
    static member GetPropertyPacked(doc: IJsonDocument, name) =
        match JsonRuntime.TryGetPropertyPacked(doc, name) with
        | Some doc -> doc
        | None ->
            failwithf "Property '%s' not found at '%s': %s" name (doc.Path())
            <| doc.JsonValue.ToString(JsonSaveOptions.DisableFormatting)

    /// Get json property and wrap in json document, and return null if not found
    static member GetPropertyPackedOrNull(doc: IJsonDocument, name) =
        match JsonRuntime.TryGetPropertyPacked(doc, name) with
        | Some doc -> doc
        | None -> doc.CreateNew(JsonValue.Null, "/" + name)

    /// Get optional json property and convert to a specified type
    static member ConvertOptionalProperty<'T>(doc: IJsonDocument, name, mapping: Func<IJsonDocument, 'T>) =
        JsonRuntime.TryGetPropertyPacked(doc, name)
        |> Option.map mapping.Invoke

    static member private Matches cultureStr tag =
        match tag with
        | InferedTypeTag.Number ->
            let cultureInfo = TextRuntime.GetCulture cultureStr

            fun json ->
                (JsonConversions.AsDecimal cultureInfo json).IsSome
                || (JsonConversions.AsFloat [||] true cultureInfo json).IsSome
        | InferedTypeTag.Boolean -> JsonConversions.AsBoolean >> Option.isSome
        | InferedTypeTag.String ->
            JsonConversions.AsString true (TextRuntime.GetCulture cultureStr)
            >> Option.isSome
        | InferedTypeTag.DateTime ->
            let cultureInfo = TextRuntime.GetCulture cultureStr

            fun json ->
                (JsonConversions.AsDateTimeOffset cultureInfo json).IsSome
                || (JsonConversions.AsDateTime cultureInfo json).IsSome
        | InferedTypeTag.DateTimeOffset ->
            let cultureInfo = TextRuntime.GetCulture cultureStr
            fun json -> (JsonConversions.AsDateTimeOffset cultureInfo json).IsSome
        | InferedTypeTag.TimeSpan ->
            JsonConversions.AsTimeSpan(TextRuntime.GetCulture cultureStr)
            >> Option.isSome
        | InferedTypeTag.Guid -> JsonConversions.AsGuid >> Option.isSome
        | InferedTypeTag.Collection ->
            function
            | JsonValue.Array _ -> true
            | _ -> false
        | InferedTypeTag.Record _ ->
            function
            | JsonValue.Record _ -> true
            | _ -> false
        | InferedTypeTag.Json -> failwith "Json type not supported"
        | InferedTypeTag.Null -> failwith "Null type not supported"
        | InferedTypeTag.Heterogeneous -> failwith "Heterogeneous type not supported"

    /// Returns all array values that match the specified tag
    static member GetArrayChildrenByTypeTag<'T>
        (
            doc: IJsonDocument,
            cultureStr,
            tagCode,
            mapping: Func<IJsonDocument, 'T>
        ) =
        match doc.JsonValue with
        | JsonValue.Array elements ->
            elements
            |> Array.filter (JsonRuntime.Matches cultureStr (InferedTypeTag.ParseCode tagCode))
            |> Array.mapi (fun i value ->
                doc.CreateNew(value, "[" + (string i) + "]")
                |> mapping.Invoke)
        | JsonValue.Null -> [||]
        | x ->
            failwithf "Expecting an array at '%s', got %s" (doc.Path())
            <| x.ToString(JsonSaveOptions.DisableFormatting)

    /// Returns single or no value from an array matching the specified tag
    static member TryGetArrayChildByTypeTag<'T>(doc, cultureStr, tagCode, mapping: Func<IJsonDocument, 'T>) =
        match JsonRuntime.GetArrayChildrenByTypeTag(doc, cultureStr, tagCode, mapping) with
        | [| child |] -> Some child
        | [||] -> None
        | _ ->
            failwithf "Expecting an array with single or no elements at '%s', got %s" (doc.Path())
            <| doc.JsonValue.ToString(JsonSaveOptions.DisableFormatting)

    /// Returns a single array children that matches the specified tag
    static member GetArrayChildByTypeTag(doc, cultureStr, tagCode) =
        match JsonRuntime.GetArrayChildrenByTypeTag(doc, cultureStr, tagCode, Func<_, _>(id)) with
        | [| child |] -> child
        | _ ->
            failwithf "Expecting an array with single element at '%s', got %s" (doc.Path())
            <| doc.JsonValue.ToString(JsonSaveOptions.DisableFormatting)

    /// Returns a single or no value by tag type
    static member TryGetValueByTypeTag<'T>(doc: IJsonDocument, cultureStr, tagCode, mapping: Func<IJsonDocument, 'T>) =
        if JsonRuntime.Matches cultureStr (InferedTypeTag.ParseCode tagCode) doc.JsonValue then
            Some(mapping.Invoke doc)
        else
            None

    static member private ToJsonValue (cultureInfo: CultureInfo) (originalType: PrimitiveType option) (value: obj) =
        let inline optionToJson f =
            function
            | None -> JsonValue.Null
            | Some v -> f v

        let inline boolToDecimal x = if x then 1m else 0m
        let inline boolToString x = if x then "true" else "false"
        let inline intToString (x: int) = x.ToString(cultureInfo)
        let inline int64ToString (x: int64) = x.ToString(cultureInfo)
        let inline floatToString (x: float) = x.ToString(cultureInfo)
        let inline decimalToString (x: decimal) = x.ToString(cultureInfo)

        match value, originalType with
        | null, _ -> JsonValue.Null
        | :? Array as v, _ ->
            JsonValue.Array [| for elem in v -> JsonRuntime.ToJsonValue cultureInfo originalType elem |]

        | :? string as v, _ -> JsonValue.String v
        | :? DateTime as v, _ -> v.ToString("O", cultureInfo) |> JsonValue.String
        | :? DateTimeOffset as v, _ -> v.ToString("O", cultureInfo) |> JsonValue.String
        | :? TimeSpan as v, _ -> v.ToString("g", cultureInfo) |> JsonValue.String
        | :? int as v, Some PrimitiveType.String -> JsonValue.String(intToString v)
        | :? int as v, _ -> JsonValue.Number(decimal v)
        | :? int64 as v, Some PrimitiveType.String -> JsonValue.String(int64ToString v)
        | :? int64 as v, _ -> JsonValue.Number(decimal v)
        | :? float as v, Some PrimitiveType.String -> JsonValue.String(floatToString v)
        | :? float as v, _ -> JsonValue.Number(decimal v)
        | :? decimal as v, Some PrimitiveType.String -> JsonValue.String(decimalToString v)
        | :? decimal as v, _ -> JsonValue.Number v
        | :? bool as v, Some PrimitiveType.Number -> JsonValue.Number(boolToDecimal v)
        | :? bool as v, Some PrimitiveType.String -> JsonValue.String(boolToString v)
        | :? bool as v, _ -> JsonValue.Boolean v
        | :? Guid as v, _ -> v.ToString() |> JsonValue.String
        | :? IJsonDocument as v, _ -> v.JsonValue
        | :? JsonValue as v, _ -> v

        | :? option<string> as v, _ -> optionToJson JsonValue.String v
        | :? option<DateTime> as v, _ ->
            optionToJson (fun (dt: DateTime) -> dt.ToString(cultureInfo) |> JsonValue.String) v
        | :? option<DateTimeOffset> as v, _ ->
            optionToJson (fun (dt: DateTimeOffset) -> dt.ToString(cultureInfo) |> JsonValue.String) v
        | :? option<TimeSpan> as v, _ ->
            optionToJson (fun (ts: TimeSpan) -> ts.ToString("g", cultureInfo) |> JsonValue.String) v
        | :? option<int> as v, Some PrimitiveType.String -> optionToJson (intToString >> JsonValue.String) v
        | :? option<int> as v, _ -> optionToJson (decimal >> JsonValue.Number) v
        | :? option<int64> as v, Some PrimitiveType.String -> optionToJson (int64ToString >> JsonValue.String) v
        | :? option<int64> as v, _ -> optionToJson (decimal >> JsonValue.Number) v
        | :? option<float> as v, Some PrimitiveType.String -> optionToJson (floatToString >> JsonValue.String) v
        | :? option<float> as v, _ -> optionToJson (decimal >> JsonValue.Number) v
        | :? option<decimal> as v, Some PrimitiveType.String -> optionToJson (decimalToString >> JsonValue.String) v
        | :? option<decimal> as v, _ -> optionToJson JsonValue.Number v
        | :? option<bool> as v, Some PrimitiveType.Number -> optionToJson (boolToDecimal >> JsonValue.Number) v
        | :? option<bool> as v, Some PrimitiveType.String -> optionToJson (boolToString >> JsonValue.String) v
        | :? option<bool> as v, _ -> optionToJson JsonValue.Boolean v
        | :? option<Guid> as v, _ -> optionToJson (fun (g: Guid) -> g.ToString() |> JsonValue.String) v
        | :? option<IJsonDocument> as v, _ -> optionToJson (fun (v: IJsonDocument) -> v.JsonValue) v
        | :? option<JsonValue> as v, _ -> optionToJson id v

        | _ -> failwithf "Can't create JsonValue from %A" value

    /// Creates a scalar JsonValue and wraps it in a json document
    static member CreateValue(value: obj, cultureStr) =
        let cultureInfo = TextRuntime.GetCulture cultureStr
        // Actual original primitive type is not needed here,
        // because this code path is only used to generate special multiple-choice types (from heterogeneous inferred types),
        // and in that case the generated ctor will have different parameter overrides
        // for all the possible primitive types, giving the user full control.
        let originalType = None
        let json = JsonRuntime.ToJsonValue cultureInfo originalType value
        JsonDocument.Create(json, "")

    // Creates a JsonValue.Record and wraps it in a json document
    static member CreateRecord(properties, cultureStr) =
        let cultureInfo = TextRuntime.GetCulture cultureStr

        let json =
            properties
            |> Array.map (fun (k, v: obj, originalType) ->
                k, JsonRuntime.ToJsonValue cultureInfo (originalType |> PrimitiveType.FromInt) v)
            |> JsonValue.Record

        JsonDocument.Create(json, "")

    // Creates a JsonValue.Record from key*value seq and wraps it in a json document
    static member CreateRecordFromDictionary<'Key, 'Value when 'Key: equality>
        (
            keyValuePairs: ('Key * 'Value) seq,
            cultureStr,
            mappingKeyBack: Func<'Key, string>,
            originalValueType
        ) =
        let cultureInfo = TextRuntime.GetCulture cultureStr

        let json =
            keyValuePairs
            |> Seq.map (fun (k, v) ->
                (k |> mappingKeyBack.Invoke),
                JsonRuntime.ToJsonValue cultureInfo (originalValueType |> PrimitiveType.FromInt) (v :> obj))
            |> Seq.toArray
            |> JsonValue.Record

        JsonDocument.Create(json, "")

    /// Creates a scalar JsonValue.Array and wraps it in a json document
    /// elements is actually an obj[][]: an array of all the user-provided arrays from a ctor
    /// (e.g [ [1;2;3] ; ["a";"b";"c"]  ] in the case of an array inferred to contain IntsOrStrings)
    static member CreateArray(elements, cultureStr) =
        let cultureInfo = TextRuntime.GetCulture cultureStr

        let json =
            elements
            |> Array.map (fun (array: obj, originalType) ->
                JsonRuntime.ToJsonValue cultureInfo (originalType |> PrimitiveType.FromInt) array)
            |> Array.collect (function
                | JsonValue.Array elements -> elements
                | JsonValue.Null -> [||]
                | element -> [| element |])
            |> JsonValue.Array

        JsonDocument.Create(json, "")
