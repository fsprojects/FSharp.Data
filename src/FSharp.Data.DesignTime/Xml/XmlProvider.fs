namespace ProviderImplementation

open System.IO
open System.Xml.Linq
open System.Xml.Schema
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
type public XmlProvider(cfg: TypeProviderConfig) as this =
    inherit
        DisposableTypeProviderForNamespaces(cfg, assemblyReplacementMap = [ "FSharp.Data.DesignTime", "FSharp.Data" ])

    // Generate namespace and type 'FSharp.Data.XmlProvider'
    do AssemblyResolver.init ()
    let asm = System.Reflection.Assembly.GetExecutingAssembly()
    let ns = "FSharp.Data"

    let xmlProvTy =
        ProvidedTypeDefinition(asm, ns, "XmlProvider", None, hideObjectMethods = true, nonNullable = true)

    let buildTypes (typeName: string) (args: obj[]) =

        // Enable TLS 1.2 for samples requested through https.
        ServicePointManager.SecurityProtocol <- ServicePointManager.SecurityProtocol ||| SecurityProtocolType.Tls12

        // Generate the required type
        let tpType =
            ProvidedTypeDefinition(asm, ns, typeName, None, hideObjectMethods = true, nonNullable = true)

        let sample = args.[0] :?> string
        let sampleIsList = args.[1] :?> bool
        let globalInference = args.[2] :?> bool
        let cultureStr = args.[3] :?> string
        let encodingStr = args.[4] :?> string
        let resolutionFolder = args.[5] :?> string
        let resource = args.[6] :?> string
        let inferTypesFromValues = args.[7] :?> bool
        let schema = args.[8] :?> string
        let inferenceMode = args.[9] :?> InferenceMode
        let preferDateOnly = args.[10] :?> bool
        let useOriginalNames = args.[11] :?> bool

        let inferenceMode =
            InferenceMode'.FromPublicApi(inferenceMode, inferTypesFromValues)

        if schema <> "" then
            if sample <> "" then
                failwith "When the Schema parameter is used, the Sample parameter cannot be used"

            if sampleIsList then
                failwith "When the Schema parameter is used, the SampleIsList parameter must be set to false"

        let unitsOfMeasureProvider = ProviderHelpers.unitsOfMeasureProvider

        let getSpec _ value =

            if schema <> "" then

                let schemaSet =
                    use _holder = IO.logTime "Parsing" sample
                    XmlSchema.parseSchema resolutionFolder value

                let inferedType =
                    use _holder = IO.logTime "Inference" sample

                    let t =
                        schemaSet |> XsdParsing.getElements |> List.ofSeq |> XsdInference.inferElements
#if NET6_0_OR_GREATER
                    if preferDateOnly && ProviderHelpers.runtimeSupportsNet6Types cfg.RuntimeAssembly then
                        t
                    else
                        StructuralInference.downgradeNet6Types t
#else
                    t
#endif

                use _holder = IO.logTime "TypeGeneration" sample

                let ctx =
                    XmlGenerationContext.Create(
                        unitsOfMeasureProvider,
                        inferenceMode,
                        cultureStr,
                        tpType,
                        globalInference || schema <> "",
                        useOriginalNames
                    )

                let result = XmlTypeBuilder.generateXmlType ctx inferedType

                { GeneratedType = tpType
                  RepresentationType = result.ConvertedType
                  CreateFromTextReader = fun reader -> result.Converter <@@ XmlElement.Create(%reader) @@>
                  CreateListFromTextReader = None
                  CreateFromTextReaderForSampleList =
                    fun reader -> // hack: this will actually parse the schema
                        <@@ XmlSchema.parseSchemaFromTextReader resolutionFolder %reader @@>
                  CreateFromValue = None }


            else

                let samples =
                    use _holder = IO.logTime "Parsing" sample

                    if sampleIsList then
                        XmlElement.CreateList(new StringReader(value))
                        |> Array.map (fun doc -> doc.XElement)
                    else
                        [| XDocument.Parse(value).Root |]

                let inferedType =
                    use _holder = IO.logTime "Inference" sample

                    let t =
                        samples
                        |> XmlInference.inferType
                            unitsOfMeasureProvider
                            inferenceMode
                            (TextRuntime.GetCulture cultureStr)
                            false
                            globalInference
                        |> Array.fold (StructuralInference.subtypeInfered false) InferedType.Top
#if NET6_0_OR_GREATER
                    if preferDateOnly && ProviderHelpers.runtimeSupportsNet6Types cfg.RuntimeAssembly then
                        t
                    else
                        StructuralInference.downgradeNet6Types t
#else
                    t
#endif

                use _holder = IO.logTime "TypeGeneration" sample

                let ctx =
                    XmlGenerationContext.Create(
                        unitsOfMeasureProvider,
                        inferenceMode,
                        cultureStr,
                        tpType,
                        globalInference || schema <> "",
                        useOriginalNames
                    )

                let result = XmlTypeBuilder.generateXmlType ctx inferedType

                { GeneratedType = tpType
                  RepresentationType = result.ConvertedType
                  CreateFromTextReader = fun reader -> result.Converter <@@ XmlElement.Create(%reader) @@>
                  CreateListFromTextReader = None
                  CreateFromTextReaderForSampleList =
                    fun reader -> result.Converter <@@ XmlElement.CreateList(%reader) @@>
                  CreateFromValue = None }

        let source =
            if schema <> "" then Schema schema
            elif sampleIsList then SampleList sample
            else Sample sample

        generateType
            (if schema <> "" then "XSD" else "XML")
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
          ProvidedStaticParameter("Global", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("Encoding", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("EmbeddedResource", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("InferTypesFromValues", typeof<bool>, parameterDefaultValue = true)
          ProvidedStaticParameter("Schema", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter(
              "InferenceMode",
              typeof<InferenceMode>,
              parameterDefaultValue = InferenceMode.BackwardCompatible
          )
          ProvidedStaticParameter("PreferDateOnly", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("UseOriginalNames", typeof<bool>, parameterDefaultValue = false) ]

    let helpText =
        """<summary>Typed representation of a XML file.</summary>
           <param name='Sample'>Location of a XML sample file or a string containing a sample XML document.</param>
           <param name='SampleIsList'>If true, the children of the root in the sample document represent individual samples for the inference.</param>
           <param name='Global'>If true, the inference unifies all XML elements with the same name.</param>                     
           <param name='Culture'>The culture used for parsing numbers and dates. Defaults to the invariant culture.</param>
           <param name='Encoding'>The encoding used to read the sample. You can specify either the character set name or the codepage number. Defaults to UTF8 for files, and to ISO-8859-1 the for HTTP requests, unless <c>charset</c> is specified in the <c>Content-Type</c> response header.</param>
           <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution).</param>
           <param name='EmbeddedResource'>When specified, the type provider first attempts to load the sample from the specified resource 
              (e.g. 'MyCompany.MyAssembly, resource_name.xml'). This is useful when exposing types generated by the type provider.</param>
           <param name='InferTypesFromValues'>
              This parameter is deprecated. Please use InferenceMode instead.
              If true, turns on additional type inference from values. 
              (e.g. type inference infers string values such as "123" as ints and values constrained to 0 and 1 as booleans. The XmlProvider also infers string values as JSON.)</param>
           <param name='Schema'>Location of a schema file or a string containing xsd.</param>
           <param name='InferenceMode'>Possible values:
              | NoInference -> Inference is disabled. All values are inferred as the most basic type permitted for the value (usually string).
              | ValuesOnly -> Types of values are inferred from the Sample. Inline schema support is disabled. This is the default.
              | ValuesAndInlineSchemasHints -> Types of values are inferred from both values and inline schemas. Inline schemas are special string values that can define a type and/or unit of measure. Supported syntax: typeof&lt;type&gt; or typeof{type} or typeof&lt;type&lt;measure&gt;&gt; or typeof{type{measure}}. Valid measures are the default SI units, and valid types are <c>int</c>, <c>int64</c>, <c>bool</c>, <c>float</c>, <c>decimal</c>, <c>date</c>, <c>datetimeoffset</c>, <c>timespan</c>, <c>guid</c> and <c>string</c>.
              | ValuesAndInlineSchemasOverrides -> Same as ValuesAndInlineSchemasHints, but value inferred types are ignored when an inline schema is present.
              Note inline schemas are not used from Xsd documents.
           </param>
           <param name='PreferDateOnly'>When true on .NET 6+, date-only strings are inferred as DateOnly and time-only strings as TimeOnly. Defaults to false for backward compatibility.</param>
           <param name='UseOriginalNames'>When true, XML element and attribute names are used as-is for generated property names instead of being normalized to PascalCase. Defaults to false.</param>"""


    do xmlProvTy.AddXmlDoc helpText
    do xmlProvTy.DefineStaticParameters(parameters, buildTypes)

    // Register the main type with F# compiler
    do this.AddNamespace(ns, [ xmlProvTy ])
