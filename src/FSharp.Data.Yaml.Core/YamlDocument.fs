// --------------------------------------------------------------------------------------
// YAML type provider - runtime support
// --------------------------------------------------------------------------------------
namespace FSharp.Data.Runtime.BaseTypes

open System
open System.ComponentModel
open System.IO
open System.Globalization
open FSharp.Data
open YamlDotNet.RepresentationModel

#nowarn "10001"

// --------------------------------------------------------------------------------------
// Conversion from YAML nodes to JsonValue
// --------------------------------------------------------------------------------------

module internal YamlConversions =

    // For design-time type inference: quoted YAML scalars should always be typed as
    // strings, even when InferTypesFromValues=true. We use a non-numeric sentinel value
    // so that JsonInference does not re-infer "01234" as int, etc.
    let rec yamlNodeToJsonValueForInference (node: YamlNode) : JsonValue =
        match node with
        | :? YamlMappingNode as mapping ->
            let props =
                [| for kvp in mapping.Children do
                       let key =
                           match kvp.Key with
                           | :? YamlScalarNode as s -> s.Value
                           | other -> other.ToString()

                       yield (key, yamlNodeToJsonValueForInference kvp.Value) |]

            JsonValue.Record props

        | :? YamlSequenceNode as sequence ->
            let elements =
                [| for item in sequence.Children -> yamlNodeToJsonValueForInference item |]

            JsonValue.Array elements

        | :? YamlScalarNode as scalar ->
            let value = scalar.Value

            if value = null then
                JsonValue.Null
            else
                match scalar.Style with
                | YamlDotNet.Core.ScalarStyle.SingleQuoted
                | YamlDotNet.Core.ScalarStyle.DoubleQuoted
                | YamlDotNet.Core.ScalarStyle.Literal
                | YamlDotNet.Core.ScalarStyle.Folded ->
                    // Explicitly quoted scalars are always strings in YAML.
                    // Use the original value if it is clearly non-numeric; otherwise substitute
                    // a plain-letter sentinel so that value-based type inference sees a string.
                    let sentinel =
                        match
                            System.Decimal.TryParse(
                                value,
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture
                            )
                        with
                        | true, _ -> "s"
                        | false, _ ->
                            match
                                System.Double.TryParse(
                                    value,
                                    System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture
                                )
                            with
                            | true, _ -> "s"
                            | false, _ -> value

                    JsonValue.String sentinel
                | _ ->
                    // Plain scalars: use the same type-aware conversion as runtime
                    if value = "null" || value = "~" || value = "" then
                        JsonValue.Null
                    elif value = "true" || value = "True" || value = "TRUE" then
                        JsonValue.Boolean true
                    elif value = "false" || value = "False" || value = "FALSE" then
                        JsonValue.Boolean false
                    elif value = ".inf" || value = ".Inf" || value = ".INF" || value = "+.inf" then
                        JsonValue.Float System.Double.PositiveInfinity
                    elif value = "-.inf" || value = "-.Inf" || value = "-.INF" then
                        JsonValue.Float System.Double.NegativeInfinity
                    elif value = ".nan" || value = ".NaN" || value = ".NAN" then
                        JsonValue.Float System.Double.NaN
                    else
                        match
                            System.Decimal.TryParse(
                                value,
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture
                            )
                        with
                        | true, d -> JsonValue.Number d
                        | false, _ ->
                            match
                                System.Double.TryParse(
                                    value,
                                    System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture
                                )
                            with
                            | true, f -> JsonValue.Float f
                            | false, _ -> JsonValue.String value

        | _ -> JsonValue.Null

    let parseYamlForInference (text: string) : JsonValue =
        let yaml = YamlStream()
        use reader = new StringReader(text)
        yaml.Load(reader)

        if yaml.Documents.Count = 0 then
            JsonValue.Null
        else
            yamlNodeToJsonValueForInference yaml.Documents.[0].RootNode

    let rec yamlNodeToJsonValue (node: YamlNode) : JsonValue =
        match node with
        | :? YamlMappingNode as mapping ->
            let props =
                [| for kvp in mapping.Children do
                       let key =
                           match kvp.Key with
                           | :? YamlScalarNode as s -> s.Value
                           | other -> other.ToString()

                       yield (key, yamlNodeToJsonValue kvp.Value) |]

            JsonValue.Record props

        | :? YamlSequenceNode as sequence ->
            let elements = [| for item in sequence.Children -> yamlNodeToJsonValue item |]
            JsonValue.Array elements

        | :? YamlScalarNode as scalar ->
            let value = scalar.Value

            if value = null then
                JsonValue.Null
            else
                match scalar.Style with
                | YamlDotNet.Core.ScalarStyle.SingleQuoted
                | YamlDotNet.Core.ScalarStyle.DoubleQuoted
                | YamlDotNet.Core.ScalarStyle.Literal
                | YamlDotNet.Core.ScalarStyle.Folded ->
                    // Quoted/block scalars are always strings
                    JsonValue.String value
                | _ ->
                    // Plain scalars: auto-detect type (YAML core schema)
                    if value = "null" || value = "~" || value = "" then
                        JsonValue.Null
                    elif value = "true" || value = "True" || value = "TRUE" then
                        JsonValue.Boolean true
                    elif value = "false" || value = "False" || value = "FALSE" then
                        JsonValue.Boolean false
                    elif value = ".inf" || value = ".Inf" || value = ".INF" || value = "+.inf" then
                        JsonValue.Float Double.PositiveInfinity
                    elif value = "-.inf" || value = "-.Inf" || value = "-.INF" then
                        JsonValue.Float Double.NegativeInfinity
                    elif value = ".nan" || value = ".NaN" || value = ".NAN" then
                        JsonValue.Float Double.NaN
                    else
                        // Try decimal first (preserves precision for integers and most decimals)
                        match Decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture) with
                        | true, d -> JsonValue.Number d
                        | false, _ ->
                            match Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture) with
                            | true, f -> JsonValue.Float f
                            | false, _ -> JsonValue.String value

        | _ -> JsonValue.Null

    let parseYaml (text: string) : JsonValue =
        let yaml = YamlStream()
        use reader = new StringReader(text)
        yaml.Load(reader)

        if yaml.Documents.Count = 0 then
            JsonValue.Null
        else
            yamlNodeToJsonValue yaml.Documents.[0].RootNode

    let parseYamlDocuments (text: string) : JsonValue[] =
        let yaml = YamlStream()
        use reader = new StringReader(text)
        yaml.Load(reader)
        [| for doc in yaml.Documents -> yamlNodeToJsonValue doc.RootNode |]

    // Like yamlNodeToJsonValue, but for design-time type inference only.
    // In YAML, quoted scalars are always strings by spec (the quoting is an explicit type annotation).
    // When InferTypesFromValues=true, JsonInference would otherwise re-infer "01234" as int.
    // We prevent this by substituting a guaranteed non-numeric sentinel for quoted scalars,
    // so inference always returns string for them. Runtime parsing still uses yamlNodeToJsonValue.
    let rec yamlNodeToJsonValueForInference (node: YamlNode) : JsonValue =
        match node with
        | :? YamlMappingNode as mapping ->
            let props =
                [| for kvp in mapping.Children do
                       let key =
                           match kvp.Key with
                           | :? YamlScalarNode as s -> s.Value
                           | other -> other.ToString()

                       yield (key, yamlNodeToJsonValueForInference kvp.Value) |]

            JsonValue.Record props

        | :? YamlSequenceNode as sequence ->
            let elements =
                [| for item in sequence.Children -> yamlNodeToJsonValueForInference item |]

            JsonValue.Array elements

        | :? YamlScalarNode as scalar ->
            let value = scalar.Value

            if value = null then
                JsonValue.Null
            else
                match scalar.Style with
                | YamlDotNet.Core.ScalarStyle.SingleQuoted
                | YamlDotNet.Core.ScalarStyle.DoubleQuoted
                | YamlDotNet.Core.ScalarStyle.Literal
                | YamlDotNet.Core.ScalarStyle.Folded ->
                    // Quoted scalar: YAML spec says this is always a string.
                    // Use a non-numeric sentinel so InferTypesFromValues does not re-infer
                    // the value content as int/bool/date/etc.
                    JsonValue.String "quoted-string"
                | _ ->
                    // Plain scalars: same as runtime conversion
                    yamlNodeToJsonValue node

        | _ -> JsonValue.Null

    let parseYamlForInference (text: string) : JsonValue =
        let yaml = YamlStream()
        use reader = new StringReader(text)
        yaml.Load(reader)

        if yaml.Documents.Count = 0 then
            JsonValue.Null
        else
            yamlNodeToJsonValueForInference yaml.Documents.[0].RootNode


// --------------------------------------------------------------------------------------
// YamlDocument - implements IJsonDocument so it can be used with JSON-based generated code
// --------------------------------------------------------------------------------------

/// <summary>Underlying representation of types generated by YamlProvider</summary>
[<StructuredFormatDisplay("{JsonValue}")>]
type YamlDocument =

    private
        {
            /// <exclude />
            Json: JsonValue
            /// <exclude />
            Path: string
        }

    interface IJsonDocument with
        member x.JsonValue = x.Json
        member x.Path() = x.Path

        member x.CreateNew(value, pathIncrement) =
            YamlDocument.Create(value, x.Path + pathIncrement)

    /// The underlying JsonValue representation of the YAML document
    member x.JsonValue = x.Json

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    override x.ToString() = x.JsonValue.ToString()

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    static member ParseToJsonValue(text: string) : JsonValue = YamlConversions.parseYaml text

    /// <summary>
    /// Like ParseToJsonValue but for design-time type inference: explicitly quoted YAML scalars
    /// are represented as string sentinels so that InferTypesFromValues does not re-infer
    /// "01234" as int, etc.
    /// </summary>
    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    static member ParseToJsonValueForInference(text: string) : JsonValue =
        YamlConversions.parseYamlForInference text

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    static member ParseToJsonValueArray(text: string) : JsonValue[] = YamlConversions.parseYamlDocuments text

    /// <exclude />
    /// Design-time only: like ParseToJsonValue but quoted scalars are represented with a
    /// non-numeric sentinel string so that InferTypesFromValues does not re-infer them.
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    static member ParseToJsonValueForInference(text: string) : JsonValue =
        YamlConversions.parseYamlForInference text

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    static member Create(value, path) =
        { Json = value; Path = path } :> IJsonDocument

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    static member Create(reader: TextReader) =
        use reader = reader
        let text = reader.ReadToEnd()
        let value = YamlConversions.parseYaml text
        YamlDocument.Create(value, "")

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    static member CreateList(reader: TextReader) =
        use reader = reader
        let text = reader.ReadToEnd()

        // If the top-level YAML document is a sequence, treat each element as an item
        match YamlConversions.parseYaml text with
        | JsonValue.Array items ->
            items
            |> Array.mapi (fun i value -> YamlDocument.Create(value, "[" + string i + "]"))
        | single ->
            // Otherwise, treat the whole document as one item
            [| YamlDocument.Create(single, "") |]
