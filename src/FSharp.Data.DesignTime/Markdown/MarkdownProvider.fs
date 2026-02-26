// --------------------------------------------------------------------------------------
// Markdown type provider – typed access to YAML front matter in markdown files
// --------------------------------------------------------------------------------------
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

#nowarn "10001"

[<TypeProvider>]
type public MarkdownProvider(cfg: TypeProviderConfig) as this =
    inherit
        DisposableTypeProviderForNamespaces(cfg, assemblyReplacementMap = [ "FSharp.Data.DesignTime", "FSharp.Data" ])

    do AssemblyResolver.init ()
    let asm = System.Reflection.Assembly.GetExecutingAssembly()
    let ns = "FSharp.Data"

    let markdownProvTy =
        ProvidedTypeDefinition(asm, ns, "MarkdownProvider", None, hideObjectMethods = true, nonNullable = true)

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
        let inferTypesFromValues = args.[6] :?> bool
        let useOriginalNames = args.[7] :?> bool
        let preferOptionals = args.[8] :?> bool

        let cultureInfo = TextRuntime.GetCulture cultureStr
        let unitsOfMeasureProvider = ProviderHelpers.unitsOfMeasureProvider

        let inferenceMode =
            InferenceMode'.FromPublicApi(InferenceMode.BackwardCompatible, inferTypesFromValues)

        let getSpec _ value =

            // Parse the sample markdown, extracting the front matter as a JsonValue record
            let sampleJson = MarkdownDocument.ParseSample value

            let inferedType =
                use _holder = IO.logTime "Inference" sample

                let rawInfered =
                    JsonInference.inferType
                        unitsOfMeasureProvider
                        inferenceMode
                        cultureInfo
                        (not preferOptionals)
                        ""
                        sampleJson

#if NET6_0_OR_GREATER
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
                    ?useOriginalNames = Some useOriginalNames
                )

            let result = JsonTypeBuilder.generateJsonType ctx false false rootName inferedType

            // Add a Body property to the root generated type (the type returned by Load/Parse).
            // At runtime the underlying representation is MarkdownDocument (which implements IJsonDocument),
            // so downcasting to MarkdownDocument is always safe for root-level instances.
            match result.ConvertedType with
            | :? ProvidedTypeDefinition as rootTy ->
                let bodyProp =
                    ProvidedProperty(
                        "Body",
                        typeof<string>,
                        getterCode = fun args -> <@@ ((%%args.[0]: IJsonDocument) :?> MarkdownDocument).Body @@>
                    )

                bodyProp.AddXmlDoc "The markdown body content (all text after the front matter delimiter `---`)."
                rootTy.AddMember bodyProp
            | _ -> ()

            { GeneratedType = tpType
              RepresentationType = result.ConvertedType
              CreateFromTextReader = fun reader -> result.Convert <@@ MarkdownDocument.Load(%reader) @@>
              CreateListFromTextReader = None
              CreateFromTextReaderForSampleList = fun reader -> result.Convert <@@ MarkdownDocument.Load(%reader) @@>
              CreateFromValue = None }

        generateType "Markdown" (Sample sample) getSpec this cfg encodingStr resolutionFolder resource typeName None

    // Static parameters exposed to the user
    let parameters =
        [ ProvidedStaticParameter("Sample", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("RootName", typeof<string>, parameterDefaultValue = "Root")
          ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("Encoding", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("EmbeddedResource", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("InferTypesFromValues", typeof<bool>, parameterDefaultValue = true)
          ProvidedStaticParameter("UseOriginalNames", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("PreferOptionals", typeof<bool>, parameterDefaultValue = true) ]

    let helpText =
        """<summary>Typed representation of a Markdown document with YAML front matter.</summary>
           <param name='Sample'>Location of a Markdown sample file or a string containing a sample Markdown document with YAML front matter.</param>
           <param name='RootName'>The name to be used for the root type. Defaults to `Root`.</param>
           <param name='Culture'>The culture used for parsing numbers and dates. Defaults to the invariant culture.</param>
           <param name='Encoding'>The encoding used to read the sample. You can specify either the character set name or the codepage number. Defaults to UTF8 for files.</param>
           <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution).</param>
           <param name='EmbeddedResource'>When specified, the type provider first attempts to load the sample from the specified resource
              (e.g. 'MyCompany.MyAssembly, resource_name.md'). This is useful when exposing types generated by the type provider.</param>
           <param name='InferTypesFromValues'>If true, turns on additional type inference from values (e.g. "123" inferred as int). Defaults to true.</param>
           <param name='UseOriginalNames'>When true, YAML front matter key names are used as-is for generated property names instead of being normalized to PascalCase. Defaults to false.</param>
           <param name='PreferOptionals'>When true (default), missing or null front matter values are represented as option types. When false, missing values use empty string or NaN.</param>"""

    do markdownProvTy.AddXmlDoc helpText
    do markdownProvTy.DefineStaticParameters(parameters, buildTypes)

    // Register the main type with the F# compiler
    do this.AddNamespace(ns, [ markdownProvTy ])
