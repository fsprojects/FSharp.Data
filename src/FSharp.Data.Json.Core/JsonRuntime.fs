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

#if NET6_0_OR_GREATER
    static member ConvertDateOnly(cultureStr, json) =
        json
        |> Option.bind (JsonConversions.AsDateOnly(TextRuntime.GetCulture cultureStr))

    static member ConvertTimeOnly(cultureStr, json) =
        json
        |> Option.bind (JsonConversions.AsTimeOnly(TextRuntime.GetCulture cultureStr))
#endif

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
        | None, Some((JsonValue.Array _ | JsonValue.Record _) as x) ->
            failwithf "Expecting %s at '%s', got %s" (getTypeName ()) path
            <| x.ToString(JsonSaveOptions.DisableFormatting)
        | None, _ when typeof<'T> = typeof<string> -> "" |> unbox
        | None, _ when typeof<'T> = typeof<float> -> Double.NaN |> unbox
        | None, None -> failwithf "'%s' is missing" path
        | None, Some x ->
            failwithf "Expecting %s at '%s', got %s" (getTypeName ()) path
            <| x.ToString(JsonSaveOptions.DisableFormatting)

    /// Operation that extracts the value from an option and always throws if the value is not present.
    /// Used when ExceptionIfMissing=true to raise an exception for missing fields instead of returning defaults.
    static member GetNonOptionalValueStrict<'T>(path: string, opt: option<'T>, originalValue) : 'T =
        let getTypeName () =
            let name = typeof<'T>.Name

            if name.StartsWith("i", StringComparison.OrdinalIgnoreCase) then
                "an " + name
            else
                "a " + name

        match opt, originalValue with
        | Some value, _ -> value
        | None, Some((JsonValue.Array _ | JsonValue.Record _) as x) ->
            failwithf "Expecting %s at '%s', got %s" (getTypeName ()) path
            <| x.ToString(JsonSaveOptions.DisableFormatting)
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
            |> Array.mapi (fun i value -> doc.CreateNew(value, "[" + (string i) + "]") |> mapping.Invoke)
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
        (doc: IJsonDocument, mappingKey: Func<IJsonDocument, 'Key>, mappingValue: Func<IJsonDocument, 'Value>)
        =
        JsonRuntime.GetRecordProperties(doc)
        |> Seq.map (fun (k, v) ->
            let key = doc.CreateNew(JsonValue.String k, k) |> mappingKey.Invoke

            let value = doc.CreateNew(v, k) |> mappingValue.Invoke
            key, value)


    /// Get a value by the key from infered dictionary
    static member InferedDictionaryContainsKey<'Key when 'Key: equality>
        (doc: IJsonDocument, mappingKey: Func<IJsonDocument, 'Key>, key: 'Key)
        =
        let finder (k, _) =
            (doc.CreateNew(JsonValue.String k, k) |> mappingKey.Invoke) = key

        (JsonRuntime.GetRecordProperties(doc) |> Array.tryFind finder).IsSome

    /// Try get a value by the key from infered dictionary
    static member TryGetValueByKeyFromInferedDictionary<'Key, 'Value when 'Key: equality>
        (doc: IJsonDocument, mappingKey: Func<IJsonDocument, 'Key>, mappingValue: Func<IJsonDocument, 'Value>, key: 'Key) =
        let picker (k, v) =
            if (doc.CreateNew(JsonValue.String k, k) |> mappingKey.Invoke) = key then
                doc.CreateNew(v, k) |> mappingValue.Invoke |> Some
            else
                None

        JsonRuntime.GetRecordProperties(doc) |> Array.tryPick picker

    /// Get a value by the key from infered dictionary
    static member GetValueByKeyFromInferedDictionary<'Key, 'Value when 'Key: equality>
        (doc: IJsonDocument, mappingKey: Func<IJsonDocument, 'Key>, mappingValue: Func<IJsonDocument, 'Value>, key: 'Key) =
        match JsonRuntime.TryGetValueByKeyFromInferedDictionary(doc, mappingKey, mappingValue, key) with
        | Some value -> value
        | _ ->
            key
            |> sprintf "The given key '%A' was not present in the dictionary."
            |> System.Collections.Generic.KeyNotFoundException
            |> raise

    /// Get keys from infered dictionary
    static member GetKeysFromInferedDictionary<'Key when 'Key: equality>
        (doc: IJsonDocument, mappingKey: Func<IJsonDocument, 'Key>)
        =
        JsonRuntime.GetRecordProperties(doc)
        |> Array.map (fun (k, _) -> doc.CreateNew(JsonValue.String k, k) |> mappingKey.Invoke)

    /// Get values from infered dictionary
    static member GetValuesFromInferedDictionary<'Value>
        (doc: IJsonDocument, mappingValue: Func<IJsonDocument, 'Value>)
        =
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
        JsonRuntime.TryGetPropertyPacked(doc, name) |> Option.map mapping.Invoke

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
        | InferedTypeTag.TimeSpan -> JsonConversions.AsTimeSpan(TextRuntime.GetCulture cultureStr) >> Option.isSome
        | InferedTypeTag.Guid -> JsonConversions.AsGuid >> Option.isSome
#if NET6_0_OR_GREATER
        | InferedTypeTag.DateOnly -> JsonConversions.AsDateOnly(TextRuntime.GetCulture cultureStr) >> Option.isSome
        | InferedTypeTag.TimeOnly -> JsonConversions.AsTimeOnly(TextRuntime.GetCulture cultureStr) >> Option.isSome
#endif
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
        (doc: IJsonDocument, cultureStr, tagCode, mapping: Func<IJsonDocument, 'T>)
        =
        match doc.JsonValue with
        | JsonValue.Array elements ->
            elements
            |> Array.filter (JsonRuntime.Matches cultureStr (InferedTypeTag.ParseCode tagCode))
            |> Array.mapi (fun i value -> doc.CreateNew(value, "[" + (string i) + "]") |> mapping.Invoke)
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

    static member private ToJsonValue (cultureInfo: CultureInfo) (value: obj) =
        let inline optionToJson f =
            function
            | None -> JsonValue.Null
            | Some v -> f v

        match value with
        | null -> JsonValue.Null
        | :? Array as v -> JsonValue.Array [| for elem in v -> JsonRuntime.ToJsonValue cultureInfo elem |]

        | :? string as v -> JsonValue.String v
        | :? DateTime as v -> v.ToString("O", cultureInfo) |> JsonValue.String
        | :? DateTimeOffset as v -> v.ToString("O", cultureInfo) |> JsonValue.String
        | :? TimeSpan as v -> v.ToString("g", cultureInfo) |> JsonValue.String
        | :? int as v -> JsonValue.Number(decimal v)
        | :? int64 as v -> JsonValue.Number(decimal v)
        | :? float as v -> JsonValue.Number(decimal v)
        | :? decimal as v -> JsonValue.Number v
        | :? bool as v -> JsonValue.Boolean v
        | :? Guid as v -> v.ToString() |> JsonValue.String
        | :? IJsonDocument as v -> v.JsonValue
        | :? JsonValue as v -> v

        | :? option<string> as v -> optionToJson JsonValue.String v
        | :? option<DateTime> as v ->
            optionToJson (fun (dt: DateTime) -> dt.ToString(cultureInfo) |> JsonValue.String) v
        | :? option<DateTimeOffset> as v ->
            optionToJson (fun (dt: DateTimeOffset) -> dt.ToString(cultureInfo) |> JsonValue.String) v
        | :? option<TimeSpan> as v ->
            optionToJson (fun (ts: TimeSpan) -> ts.ToString("g", cultureInfo) |> JsonValue.String) v
        | :? option<int> as v -> optionToJson (decimal >> JsonValue.Number) v
        | :? option<int64> as v -> optionToJson (decimal >> JsonValue.Number) v
        | :? option<float> as v -> optionToJson (decimal >> JsonValue.Number) v
        | :? option<decimal> as v -> optionToJson JsonValue.Number v
        | :? option<bool> as v -> optionToJson JsonValue.Boolean v
        | :? option<Guid> as v -> optionToJson (fun (g: Guid) -> g.ToString() |> JsonValue.String) v
        | :? option<IJsonDocument> as v -> optionToJson (fun (v: IJsonDocument) -> v.JsonValue) v
        | :? option<JsonValue> as v -> optionToJson id v

        | _ -> failwithf "Can't create JsonValue from %A" value

    /// Creates a scalar JsonValue and wraps it in a json document
    static member CreateValue(value: obj, cultureStr) =
        let cultureInfo = TextRuntime.GetCulture cultureStr
        let json = JsonRuntime.ToJsonValue cultureInfo value
        JsonDocument.Create(json, "")

    // Creates a JsonValue.Record and wraps it in a json document
    static member CreateRecord(properties, cultureStr) =
        let cultureInfo = TextRuntime.GetCulture cultureStr

        let json =
            properties
            |> Array.map (fun (k, v: obj) -> k, JsonRuntime.ToJsonValue cultureInfo v)
            |> JsonValue.Record

        JsonDocument.Create(json, "")

    // Returns a new JSON record document with one property replaced (or added if absent).
    // Used by generated With* methods.
    static member WithRecordProperty(doc: IJsonDocument, name: string, value: obj, cultureStr: string) =
        let cultureInfo = TextRuntime.GetCulture cultureStr
        let newPropValue = JsonRuntime.ToJsonValue cultureInfo value
        let existingProps = JsonRuntime.GetRecordProperties(doc)
        let mutable found = false

        let updatedProps =
            existingProps
            |> Array.map (fun (k, v) ->
                if k = name then
                    found <- true
                    k, newPropValue
                else
                    k, v)

        let finalProps =
            if found then
                updatedProps
            else
                Array.append updatedProps [| name, newPropValue |]

        JsonDocument.Create(JsonValue.Record finalProps, "")

    // Creates a JsonValue.Record, omitting null fields, and wraps it in a json document
    static member CreateRecordOmitNulls(properties, cultureStr) =
        let cultureInfo = TextRuntime.GetCulture cultureStr

        let json =
            properties
            |> Array.choose (fun (k, v: obj) ->
                let jv = JsonRuntime.ToJsonValue cultureInfo v

                match jv with
                | JsonValue.Null -> None
                | _ -> Some(k, jv))
            |> JsonValue.Record

        JsonDocument.Create(json, "")

    // Creates a JsonValue.Record from key*value seq and wraps it in a json document
    static member CreateRecordFromDictionary<'Key, 'Value when 'Key: equality>
        (keyValuePairs: ('Key * 'Value) seq, cultureStr, mappingKeyBack: Func<'Key, string>)
        =
        let cultureInfo = TextRuntime.GetCulture cultureStr

        let json =
            keyValuePairs
            |> Seq.map (fun (k, v) -> (k |> mappingKeyBack.Invoke), JsonRuntime.ToJsonValue cultureInfo (v :> obj))
            |> Seq.toArray
            |> JsonValue.Record

        JsonDocument.Create(json, "")

    /// Creates a scalar JsonValue.Array and wraps it in a json document
    static member CreateArray(elements: obj[], cultureStr) =
        let cultureInfo = TextRuntime.GetCulture cultureStr

        let json =
            elements
            |> Array.collect (
                JsonRuntime.ToJsonValue cultureInfo
                >> function
                    | JsonValue.Array elements -> elements
                    | JsonValue.Null -> [||]
                    | element -> [| element |]
            )
            |> JsonValue.Array

        JsonDocument.Create(json, "")
