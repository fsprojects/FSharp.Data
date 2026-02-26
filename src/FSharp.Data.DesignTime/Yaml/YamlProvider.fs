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
type public YamlProvider(cfg: TypeProviderConfig) as this =
    inherit
        DisposableTypeProviderForNamespaces(cfg, assemblyReplacementMap = [ "FSharp.Data.DesignTime", "FSharp.Data" ])

    // Generate namespace and type 'FSharp.Data.YamlProvider'
    do AssemblyResolver.init ()
    let asm = System.Reflection.Assembly.GetExecutingAssembly()
    let ns = "FSharp.Data"

    let yamlProvTy =
        ProvidedTypeDefinition(asm, ns, "YamlProvider", None, hideObjectMethods = true, nonNullable = true)

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
        let preferDateOnly = args.[10] :?> bool
        let useOriginalNames = args.[11] :?> bool
        let preferOptionals = args.[12] :?> bool

        let inferenceMode =
            InferenceMode'.FromPublicApi(inferenceMode, inferTypesFromValues)

        let cultureInfo = TextRuntime.GetCulture cultureStr
        let unitsOfMeasureProvider = ProviderHelpers.unitsOfMeasureProvider

        let getSpec _ value =

            let inferedType =
                use _holder = IO.logTime "Inference" sample

                // Parse YAML sample into JsonValue, then use JSON inference
                let rawInfered =
                    let samples =
                        use _holder = IO.logTime "Parsing" sample

                        if sampleIsList then
                            // If SampleIsList, parse as a YAML sequence or multiple documents
                            match YamlDocument.ParseToJsonValueForInference value with
                            | JsonValue.Array items -> items
                            | single -> [| single |]
                        else
                            [| YamlDocument.ParseToJsonValueForInference value |]

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

#if NET6_0_OR_GREATER
                if preferDateOnly && ProviderHelpers.runtimeSupportsNet6Types cfg.RuntimeAssembly then
                    rawInfered
                else
                    StructuralInference.downgradeNet6Types rawInfered
#else
                rawInfered
#endif

            use _holder = IO.logTime "TypeGeneration" sample

            let ctx =
                JsonGenerationContext.Create(
                    cultureStr,
                    tpType,
                    unitsOfMeasureProvider,
                    inferenceMode,
                    ?preferDictionaries = Some preferDictionaries,
                    ?useOriginalNames = Some useOriginalNames
                )

            let result = JsonTypeBuilder.generateJsonType ctx false false rootName inferedType

            { GeneratedType = tpType
              RepresentationType = result.ConvertedType
              CreateFromTextReader = fun reader -> result.Convert <@@ YamlDocument.Create(%reader) @@>
              CreateListFromTextReader = Some(fun reader -> result.Convert <@@ YamlDocument.CreateList(%reader) @@>)
              CreateFromTextReaderForSampleList = fun reader -> result.Convert <@@ YamlDocument.CreateList(%reader) @@>
              CreateFromValue =
                Some(typeof<JsonValue>, (fun value -> result.Convert <@@ YamlDocument.Create(%value, "") @@>)) }

        let source = if sampleIsList then SampleList sample else Sample sample

        generateType "YAML" source getSpec this cfg encodingStr resolutionFolder resource typeName None

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
          ProvidedStaticParameter("PreferDateOnly", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("UseOriginalNames", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("PreferOptionals", typeof<bool>, parameterDefaultValue = true) ]

    let helpText =
        """<summary>Typed representation of a YAML document.</summary>
           <param name='Sample'>Location of a YAML sample file or a string containing a sample YAML document.</param>
           <param name='SampleIsList'>If true, the sample should be a YAML sequence (list) and each element is used as an individual sample for type inference.</param>
           <param name='RootName'>The name to be used for the root type. Defaults to `Root`.</param>
           <param name='Culture'>The culture used for parsing numbers and dates. Defaults to the invariant culture.</param>
           <param name='Encoding'>The encoding used to read the sample. You can specify either the character set name or the codepage number. Defaults to UTF8 for files, and to ISO-8859-1 for HTTP requests, unless `charset` is specified in the `Content-Type` response header.</param>
           <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution).</param>
           <param name='EmbeddedResource'>When specified, the type provider first attempts to load the sample from the specified resource
              (e.g. 'MyCompany.MyAssembly, resource_name.yaml'). This is useful when exposing types generated by the type provider.</param>
           <param name='InferTypesFromValues'>
              This parameter is deprecated. Please use InferenceMode instead.
              If true, turns on additional type inference from values.
              (e.g. type inference infers string values such as "123" as ints and values constrained to 0 and 1 as booleans.)</param>
           <param name='PreferDictionaries'>If true, YAML mappings are interpreted as dictionaries when the names of all the fields are inferred (by type inference rules) into the same non-string primitive type.</param>
           <param name='InferenceMode'>Possible values:
              | NoInference -> Inference is disabled. All values are inferred as the most basic type permitted for the value (i.e. string or number or bool).
              | ValuesOnly -> Types of values are inferred from the Sample. This is the default.
              | ValuesAndInlineSchemasHints -> Types of values are inferred from both values and inline schemas. Inline schemas are special string values that can define a type and/or unit of measure.
              | ValuesAndInlineSchemasOverrides -> Same as ValuesAndInlineSchemasHints, but value inferred types are ignored when an inline schema is present.
           </param>
           <param name='PreferDateOnly'>When true on .NET 6+, date-only strings (e.g. "2023-01-15") are inferred as DateOnly and time-only strings as TimeOnly. Defaults to false for backward compatibility.</param>
           <param name='UseOriginalNames'>When true, YAML key names are used as-is for generated property names instead of being normalized to PascalCase. Defaults to false.</param>
           <param name='PreferOptionals'>When set to false, optional YAML fields are represented as empty string or NaN instead of option types. Defaults to true.</param>"""

    do yamlProvTy.AddXmlDoc helpText
    do yamlProvTy.DefineStaticParameters(parameters, buildTypes)

    // Register the main type with F# compiler
    do this.AddNamespace(ns, [ yamlProvTy ])
