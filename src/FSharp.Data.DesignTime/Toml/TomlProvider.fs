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
type public TomlProvider(cfg: TypeProviderConfig) as this =
    inherit
        DisposableTypeProviderForNamespaces(cfg, assemblyReplacementMap = [ "FSharp.Data.DesignTime", "FSharp.Data" ])

    do AssemblyResolver.init ()
    let asm = System.Reflection.Assembly.GetExecutingAssembly()
    let ns = "FSharp.Data"

    let tomlProvTy =
        ProvidedTypeDefinition(asm, ns, "TomlProvider", None, hideObjectMethods = true, nonNullable = true)

    let buildTypes (typeName: string) (args: obj[]) =

        // Enable TLS 1.2 for samples requested through https.
        ServicePointManager.SecurityProtocol <- ServicePointManager.SecurityProtocol ||| SecurityProtocolType.Tls12

        let tpType =
            ProvidedTypeDefinition(asm, ns, typeName, None, hideObjectMethods = true, nonNullable = true)

        let sample = args.[0] :?> string
        let rootName = args.[1] :?> string

        let rootName =
            if String.IsNullOrWhiteSpace rootName then
                "Root"
            else
                NameUtils.singularize rootName

        let cultureStr = args.[2] :?> string
        let encodingStr = args.[3] :?> string
        let resolutionFolder = args.[4] :?> string
        let resource = args.[5] :?> string
        let inferenceMode = args.[6] :?> InferenceMode
        let preferDateOnly = args.[7] :?> bool
        let useOriginalNames = args.[8] :?> bool

        let inferenceMode' = InferenceMode'.FromPublicApi(inferenceMode, true)
        let cultureInfo = TextRuntime.GetCulture cultureStr
        let unitsOfMeasureProvider = ProviderHelpers.unitsOfMeasureProvider

        let getSpec _ value =

            let tomlValue =
                use _holder = IO.logTime "Parsing" sample
                TomlValue.Parse(value)

            let sampleJson = tomlValue.ToJsonValue()

            let inferedType =
                use _holder = IO.logTime "Inference" sample

                let rawInfered =
                    JsonInference.inferType unitsOfMeasureProvider inferenceMode' cultureInfo "" sampleJson

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
                    inferenceMode',
                    ?useOriginalNames = Some useOriginalNames
                )

            let result = JsonTypeBuilder.generateJsonType ctx false false rootName inferedType

            { GeneratedType = tpType
              RepresentationType = result.ConvertedType
              CreateFromTextReader = fun reader -> result.Convert <@@ TomlDocument.Create(%reader) @@>
              CreateListFromTextReader = None
              CreateFromTextReaderForSampleList = fun reader -> result.Convert <@@ TomlDocument.Create(%reader) @@>
              CreateFromValue =
                Some(typeof<JsonValue>, (fun value -> result.Convert <@@ JsonDocument.Create(%value, "") @@>)) }

        generateType "TOML" (Sample sample) getSpec this cfg encodingStr resolutionFolder resource typeName None

    let parameters =
        [ ProvidedStaticParameter("Sample", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("RootName", typeof<string>, parameterDefaultValue = "Root")
          ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("Encoding", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("EmbeddedResource", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter(
              "InferenceMode",
              typeof<InferenceMode>,
              parameterDefaultValue = InferenceMode.BackwardCompatible
          )
          ProvidedStaticParameter("PreferDateOnly", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("UseOriginalNames", typeof<bool>, parameterDefaultValue = false) ]

    let helpText =
        """<summary>Typed representation of a TOML document.</summary>
           <param name='Sample'>Location of a TOML sample file or a string containing a sample TOML document.</param>
           <param name='RootName'>The name to be used for the root type. Defaults to <c>Root</c>.</param>
           <param name='Culture'>The culture used for parsing numbers and dates. Defaults to the invariant culture.</param>
           <param name='Encoding'>The encoding used to read the sample. Defaults to UTF8 for files.</param>
           <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution).</param>
           <param name='EmbeddedResource'>When specified, the type provider first attempts to load the sample from the specified resource
              (e.g. 'MyCompany.MyAssembly, resource_name.toml'). This is useful when exposing types generated by the type provider.</param>
           <param name='InferenceMode'>Possible values:
              | NoInference -> Inference is disabled. All values are inferred as the most basic type.
              | ValuesOnly -> Types of values are inferred from the Sample (default).
              | ValuesAndInlineSchemasHints -> Types are inferred from both values and inline schema hints.
              | ValuesAndInlineSchemasOverrides -> Same as ValuesAndInlineSchemasHints, but value inferred types are ignored when an inline schema is present.
           </param>
           <param name='PreferDateOnly'>When true on .NET 6+, date-only strings are inferred as DateOnly and time-only strings as TimeOnly. Defaults to false.</param>
           <param name='UseOriginalNames'>When true, TOML key names are used as-is for generated property names instead of being normalized to PascalCase. Defaults to false.</param>"""

    do tomlProvTy.AddXmlDoc helpText
    do tomlProvTy.DefineStaticParameters(parameters, buildTypes)

    do this.AddNamespace(ns, [ tomlProvTy ])
