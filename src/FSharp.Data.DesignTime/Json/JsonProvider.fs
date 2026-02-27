namespace ProviderImplementation

open System
open System.IO
open FSharp.Core.CompilerServices
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProviderHelpers
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference
open System.Net

// ----------------------------------------------------------------------------------------------

#nowarn "10001"

[<TypeProvider>]
type public JsonProvider(cfg: TypeProviderConfig) as this =
    inherit
        DisposableTypeProviderForNamespaces(cfg, assemblyReplacementMap = [ "FSharp.Data.DesignTime", "FSharp.Data" ])

    // Generate namespace and type 'FSharp.Data.JsonProvider'
    do AssemblyResolver.init ()
    let asm = System.Reflection.Assembly.GetExecutingAssembly()
    let ns = "FSharp.Data"

    let jsonProvTy =
        ProvidedTypeDefinition(asm, ns, "JsonProvider", None, hideObjectMethods = true, nonNullable = true)

    let buildTypes (typeName: string) (args: obj[]) =

        // Enable TLS 1.2 for samples requested through https.
        ServicePointManager.SecurityProtocol <- ServicePointManager.SecurityProtocol ||| SecurityProtocolType.Tls12

        // Generate the required type
        let tpType =
            ProvidedTypeDefinition(asm, ns, typeName, None, hideObjectMethods = true, nonNullable = true)

        let sample = args.[0] :?> string
        let sampleIsList = args.[1] :?> bool
        let rootName = args.[2] :?> string

        let rootName =
            if String.IsNullOrWhiteSpace rootName then
                "Root"
            else
                NameUtils.singularize rootName

        let cultureStr = args.[3] :?> string
        let encodingStr = args.[4] :?> string
        let resolutionFolder = args.[5] :?> string
        let resource = args.[6] :?> string
        let inferTypesFromValues = args.[7] :?> bool
        let preferDictionaries = args.[8] :?> bool
        let inferenceMode = args.[9] :?> InferenceMode
        let schema = args.[10] :?> string
        let preferDateOnly = args.[11] :?> bool
        let useOriginalNames = args.[12] :?> bool
        let omitNullFields = args.[13] :?> bool
        let preferOptionals = args.[14] :?> bool
        let preferDateTimeOffset = args.[15] :?> bool

        let inferenceMode =
            InferenceMode'.FromPublicApi(inferenceMode, inferTypesFromValues)

        let cultureInfo = TextRuntime.GetCulture cultureStr
        let unitsOfMeasureProvider = ProviderHelpers.unitsOfMeasureProvider

        if schema <> "" then
            if sample <> "" then
                failwith "When the Schema parameter is used, the Sample parameter cannot be used"

            if sampleIsList then
                failwith "When the Schema parameter is used, the SampleIsList parameter must be set to false"

        let getSpec _ value =

            let inferedType =
                use _holder = IO.logTime "Inference" (if schema <> "" then schema else sample)

                let rawInfered =
                    if schema <> "" then
                        // Use the JSON Schema for type inference
                        use _holder = IO.logTime "SchemaInference" schema

                        let schemaValue = JsonValue.Parse(value)
                        let jsonSchema = JsonSchema.parseSchema schemaValue
                        JsonSchema.schemaToInferedType unitsOfMeasureProvider jsonSchema
                    else
                        // Use sample-based inference
                        let samples =
                            use _holder = IO.logTime "Parsing" sample

                            if sampleIsList then
                                JsonDocument.CreateList(new StringReader(value))
                                |> Array.map (fun doc -> doc.JsonValue)
                            else
                                [| JsonValue.Parse(value) |]

                        samples
                        |> Array.map (fun sampleJson ->
                            JsonInference.inferType
                                unitsOfMeasureProvider
                                inferenceMode
                                cultureInfo
                                (not preferOptionals)
                                ""
                                sampleJson)
                        |> Array.fold (StructuralInference.subtypeInfered (not preferOptionals)) InferedType.Top

                let rawInfered =
#if NET6_0_OR_GREATER
                    if preferDateOnly && ProviderHelpers.runtimeSupportsNet6Types cfg.RuntimeAssembly then
                        rawInfered
                    else
                        StructuralInference.downgradeNet6Types rawInfered
#else
                    rawInfered
#endif

                if preferDateTimeOffset then
                    StructuralInference.upgradeToDateTimeOffset rawInfered
                else
                    rawInfered

            use _holder = IO.logTime "TypeGeneration" (if schema <> "" then schema else sample)

            let ctx =
                JsonGenerationContext.Create(
                    cultureStr,
                    tpType,
                    unitsOfMeasureProvider,
                    inferenceMode,
                    ?preferDictionaries = Some preferDictionaries,
                    ?useOriginalNames = Some useOriginalNames,
                    ?omitNullFields = Some omitNullFields
                )

            let result = JsonTypeBuilder.generateJsonType ctx false false rootName inferedType

            { GeneratedType = tpType
              RepresentationType = result.ConvertedType
              CreateFromTextReader = fun reader -> result.Convert <@@ JsonDocument.Create(%reader) @@>
              CreateListFromTextReader = Some(fun reader -> result.Convert <@@ JsonDocument.CreateList(%reader) @@>)
              CreateFromTextReaderForSampleList = fun reader -> result.Convert <@@ JsonDocument.CreateList(%reader) @@>
              CreateFromValue =
                Some(typeof<JsonValue>, (fun value -> result.Convert <@@ JsonDocument.Create(%value, "") @@>)) }

        let source =
            if schema <> "" then Schema schema
            elif sampleIsList then SampleList sample
            else Sample sample

        generateType
            (if schema <> "" then "JSON Schema" else "JSON")
            source
            getSpec
            this
            cfg
            encodingStr
            resolutionFolder
            resource
            typeName
            None

    // Add static parameter that specifies the API we want to get (compile-time)
    let parameters =
        [ ProvidedStaticParameter("Sample", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("SampleIsList", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("RootName", typeof<string>, parameterDefaultValue = "Root")
          ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("Encoding", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("EmbeddedResource", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("InferTypesFromValues", typeof<bool>, parameterDefaultValue = true)
          ProvidedStaticParameter("PreferDictionaries", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter(
              "InferenceMode",
              typeof<InferenceMode>,
              parameterDefaultValue = InferenceMode.BackwardCompatible
          )
          ProvidedStaticParameter("Schema", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("PreferDateOnly", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("UseOriginalNames", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("OmitNullFields", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("PreferOptionals", typeof<bool>, parameterDefaultValue = true)
          ProvidedStaticParameter("PreferDateTimeOffset", typeof<bool>, parameterDefaultValue = false) ]

    let helpText =
        """<summary>Typed representation of a JSON document.</summary>
           <param name='Sample'>Location of a JSON sample file or a string containing a sample JSON document.</param>
           <param name='SampleIsList'>If true, sample should be a list of individual samples for the inference.</param>
           <param name='RootName'>The name to be used to the root type. Defaults to `Root`.</param>
           <param name='Culture'>The culture used for parsing numbers and dates. Defaults to the invariant culture.</param>
           <param name='Encoding'>The encoding used to read the sample. You can specify either the character set name or the codepage number. Defaults to UTF8 for files, and for HTTP requests when no `charset` is specified in the `Content-Type` response header.</param>
           <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution).</param>
           <param name='EmbeddedResource'>When specified, the type provider first attempts to load the sample from the specified resource
              (e.g. 'MyCompany.MyAssembly, resource_name.json'). This is useful when exposing types generated by the type provider.</param>
           <param name='InferTypesFromValues'>
              This parameter is deprecated. Please use InferenceMode instead.
              If true, turns on additional type inference from values.
              (e.g. type inference infers string values such as "123" as ints and values constrained to 0 and 1 as booleans.)</param>
           <param name='PreferDictionaries'>If true, json records are interpreted as dictionaries when the names of all the fields are inferred (by type inference rules) into the same non-string primitive type.</param>
           <param name='InferenceMode'>Possible values:
              | NoInference -> Inference is disabled. All values are inferred as the most basic type permitted for the value (i.e. string or number or bool).
              | ValuesOnly -> Types of values are inferred from the Sample. Inline schema support is disabled. This is the default.
              | ValuesAndInlineSchemasHints -> Types of values are inferred from both values and inline schemas. Inline schemas are special string values that can define a type and/or unit of measure. Supported syntax: typeof&lt;type&gt; or typeof{type} or typeof&lt;type&lt;measure&gt;&gt; or typeof{type{measure}}. Valid measures are the default SI units, and valid types are <c>int</c>, <c>int64</c>, <c>bool</c>, <c>float</c>, <c>decimal</c>, <c>date</c>, <c>datetimeoffset</c>, <c>timespan</c>, <c>guid</c> and <c>string</c>.
              | ValuesAndInlineSchemasOverrides -> Same as ValuesAndInlineSchemasHints, but value inferred types are ignored when an inline schema is present.
           </param>
           <param name='Schema'>Location of a JSON Schema file or a string containing a JSON Schema document. When specified, Sample and SampleIsList must not be used.</param>
           <param name='PreferDateOnly'>When true on .NET 6+, date-only strings (e.g. "2023-01-15") are inferred as DateOnly and time-only strings as TimeOnly. Defaults to false for backward compatibility.</param>
           <param name='UseOriginalNames'>When true, JSON property names are used as-is for generated property names instead of being normalized to PascalCase. Defaults to false.</param>
           <param name='OmitNullFields'>When true, optional fields with value None are omitted from the generated JSON rather than serialized as null. Defaults to false.</param>
           <param name='PreferOptionals'>When set to true (default), inference will use the option type for missing or null values. When false, inference will prefer to use empty string or double.NaN for missing values where possible, matching the default CsvProvider behavior.</param>
           <param name='PreferDateTimeOffset'>When true, date-time strings without an explicit timezone offset are inferred as DateTimeOffset (using the local offset) instead of DateTime. Defaults to false.</param>"""

    do jsonProvTy.AddXmlDoc helpText
    do jsonProvTy.DefineStaticParameters(parameters, buildTypes)

    // Register the main type with F# compiler
    do this.AddNamespace(ns, [ jsonProvTy ])
