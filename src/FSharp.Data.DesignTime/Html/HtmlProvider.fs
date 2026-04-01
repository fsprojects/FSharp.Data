// --------------------------------------------------------------------------------------
// HTML type provider
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open FSharp.Core.CompilerServices
open ProviderImplementation.ProviderHelpers
open ProviderImplementation.ProvidedTypes
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralInference
open System.Net

#nowarn "10001"

[<TypeProvider>]
type public HtmlProvider(cfg: TypeProviderConfig) as this =
    inherit
        DisposableTypeProviderForNamespaces(cfg, assemblyReplacementMap = [ "FSharp.Data.DesignTime", "FSharp.Data" ])

    // Generate namespace and type 'FSharp.Data.HtmlProvider'
    do AssemblyResolver.init ()
    let asm = System.Reflection.Assembly.GetExecutingAssembly()
    let ns = "FSharp.Data"

    let htmlProvTy =
        ProvidedTypeDefinition(asm, ns, "HtmlProvider", None, hideObjectMethods = true, nonNullable = true)

    let buildTypes (typeName: string) (args: obj[]) =

        // Enable TLS 1.2 for samples requested through https.
        ServicePointManager.SecurityProtocol <- ServicePointManager.SecurityProtocol ||| SecurityProtocolType.Tls12

        let sample = args.[0] :?> string
        let preferOptionals = args.[1] :?> bool
        let includeLayoutTables = args.[2] :?> bool
        let missingValuesStr = args.[3] :?> string
        let cultureStr = args.[4] :?> string
        let encodingStr = args.[5] :?> string
        let resolutionFolder = args.[6] :?> string
        let resource = args.[7] :?> string
        let preferDateOnly = args.[8] :?> bool

        // Allowing inline schemas does not seem very valuable for this provider.
        // Let's stick to the default values for now.
        let inferenceMode = InferenceMode'.ValuesOnly

        let getSpec _ value =

            let doc =
                use _holder = IO.logTime "Parsing" sample
                HtmlDocument.Parse value

            let htmlType =
                use _holder = IO.logTime "Inference" sample

                let inferenceParameters: HtmlInference.Parameters =
                    { MissingValues = TextRuntime.GetMissingValues missingValuesStr
                      CultureInfo = TextRuntime.GetCulture cultureStr
                      UnitsOfMeasureProvider = ProviderHelpers.unitsOfMeasureProvider
                      PreferOptionals = preferOptionals
                      InferenceMode = inferenceMode }

#if NET6_0_OR_GREATER
                let supportsNet6Types =
                    preferDateOnly && ProviderHelpers.runtimeSupportsNet6Types cfg.RuntimeAssembly
#else
                let supportsNet6Types = false
#endif

                doc
                |> HtmlRuntime.getHtmlObjects (Some inferenceParameters) includeLayoutTables
                |> HtmlGenerator.generateTypes
                    asm
                    ns
                    typeName
                    (inferenceParameters, missingValuesStr, cultureStr)
                    supportsNet6Types

            use _holder = IO.logTime "TypeGeneration" sample

            { GeneratedType = htmlType
              RepresentationType = htmlType
              CreateFromTextReader = fun reader -> <@@ HtmlDocument.Create(includeLayoutTables, %reader) @@>
              CreateListFromTextReader = None
              CreateFromTextReaderForSampleList = fun _ -> failwith "Not Applicable"
              CreateFromValue = None }

        generateType "HTML" (Sample sample) getSpec this cfg encodingStr resolutionFolder resource typeName None

    // Add static parameter that specifies the API we want to get (compile-time)
    let parameters =
        [ ProvidedStaticParameter("Sample", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("PreferOptionals", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("IncludeLayoutTables", typeof<bool>, parameterDefaultValue = false)
          ProvidedStaticParameter("MissingValues", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("Encoding", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("EmbeddedResource", typeof<string>, parameterDefaultValue = "")
          ProvidedStaticParameter("PreferDateOnly", typeof<bool>, parameterDefaultValue = false) ]

    let helpText =
        """<summary>Typed representation of an HTML file.</summary>
           <param name='Sample'>Location of an HTML sample file or a string containing a sample HTML document.</param>
           <param name='PreferOptionals'>When set to true, inference will prefer to use the option type instead of nullable types, <c>double.NaN</c> or <c>""</c> for missing values. Defaults to false.</param>
           <param name='IncludeLayoutTables'>Includes tables that are potentially layout tables (with cellpadding=0 and cellspacing=0 attributes)</param>
           <param name='MissingValues'>The set of strings recognized as missing values. Defaults to <c>"""
        + String.Join(",", TextConversions.DefaultMissingValues)
        + """</c>.</param>
           <param name='Culture'>The culture used for parsing numbers and dates. Defaults to the invariant culture.</param>
           <param name='Encoding'>The encoding used to read the sample. You can specify either the character set name or the codepage number. Defaults to UTF8 for files, and for HTTP requests when no <c>charset</c> is specified in the <c>Content-Type</c> response header.</param>
           <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution).</param>
           <param name='EmbeddedResource'>When specified, the type provider first attempts to load the sample from the specified resource 
              (e.g. 'MyCompany.MyAssembly, resource_name.html'). This is useful when exposing types generated by the type provider.</param>
           <param name='PreferDateOnly'>When true on .NET 6+, date-only strings are inferred as DateOnly and time-only strings as TimeOnly. Defaults to false for backward compatibility.</param>
        """

    do htmlProvTy.AddXmlDoc helpText
    do htmlProvTy.DefineStaticParameters(parameters, buildTypes)

    // Register the main type with F# compiler
    do this.AddNamespace(ns, [ htmlProvTy ])
