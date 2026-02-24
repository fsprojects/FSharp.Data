namespace ProviderImplementation

open System
open System.IO
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProvidedTypesTesting
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralInference

type internal CsvProviderArgs =
    { Sample : string
      Separators : string
      InferRows : int
      Schema : string
      HasHeaders : bool
      IgnoreErrors : bool
      SkipRows : int
      AssumeMissingValues : bool
      PreferOptionals : bool
      Quote : char
      MissingValues : string
      CacheRows : bool
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string
      PreferDateOnly : bool
      StrictBooleans : bool
      UseOriginalNames : bool }

type internal XmlProviderArgs =
    { Sample : string
      SampleIsList : bool
      Global : bool
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string 
      InferTypesFromValues : bool
      Schema : string
      InferenceMode: InferenceMode
      PreferDateOnly : bool
      DtdProcessing : string
      UseOriginalNames : bool }

type internal JsonProviderArgs =
    { Sample : string
      SampleIsList : bool
      RootName : string
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string
      InferTypesFromValues : bool
      PreferDictionaries : bool
      InferenceMode: InferenceMode
      Schema: string
      PreferDateOnly : bool
      UseOriginalNames : bool }

type internal HtmlProviderArgs =
    { Sample : string
      PreferOptionals : bool
      IncludeLayoutTables : bool
      MissingValues : string
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string
      PreferDateOnly : bool }

type internal WorldBankProviderArgs =
    { Sources : string
      Asynchronous : bool }

type internal TypeProviderInstantiation =
    | Csv of CsvProviderArgs
    | Xml of XmlProviderArgs
    | Json of JsonProviderArgs
    | Html of HtmlProviderArgs
    | WorldBank of WorldBankProviderArgs

    member x.GenerateType resolutionFolder runtimeAssembly runtimeAssemblyRefs =
        let f, args =
            match x with
            | Csv x ->
                (fun cfg -> new CsvProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.Separators
                   box x.InferRows
                   box x.Schema
                   box x.HasHeaders
                   box x.IgnoreErrors
                   box x.SkipRows
                   box x.AssumeMissingValues
                   box x.PreferOptionals
                   box x.Quote
                   box x.MissingValues
                   box x.CacheRows
                   box x.Culture
                   box x.Encoding
                   box x.ResolutionFolder
                   box x.EmbeddedResource
                   box x.PreferDateOnly
                   box x.StrictBooleans
                   box x.UseOriginalNames |]
            | Xml x ->
                (fun cfg -> new XmlProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.SampleIsList
                   box x.Global
                   box x.Culture
                   box x.Encoding
                   box x.ResolutionFolder
                   box x.EmbeddedResource
                   box x.InferTypesFromValues
                   box x.Schema
                   box x.InferenceMode
                   box x.PreferDateOnly
                   box x.DtdProcessing
                   box x.UseOriginalNames |] 
            | Json x -> 
                (fun cfg -> new JsonProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.SampleIsList
                   box x.RootName
                   box x.Culture
                   box x.Encoding
                   box x.ResolutionFolder
                   box x.EmbeddedResource
                   box x.InferTypesFromValues
                   box x.PreferDictionaries
                   box x.InferenceMode
                   box x.Schema
                   box x.PreferDateOnly
                   box x.UseOriginalNames |]
            | Html x ->
                (fun cfg -> new HtmlProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.PreferOptionals
                   box x.IncludeLayoutTables
                   box x.MissingValues
                   box x.Culture
                   box x.Encoding
                   box x.ResolutionFolder
                   box x.EmbeddedResource
                   box x.PreferDateOnly |]
            | WorldBank x ->
                (fun cfg -> new WorldBankProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sources
                   box x.Asynchronous |]

        Testing.GenerateProvidedTypeInstantiation(resolutionFolder, runtimeAssembly, runtimeAssemblyRefs, f, args)

    override x.ToString() =
        match x with
        | Csv x ->
            ["Csv"
             x.Sample
             x.Separators
             x.Schema.Replace(',', ';')
             x.HasHeaders.ToString()
             x.AssumeMissingValues.ToString()
             x.PreferOptionals.ToString()
             x.MissingValues
             x.Culture
             x.Encoding ]
        | Xml x ->
            ["Xml"
             x.Sample
             x.SampleIsList.ToString()
             x.Global.ToString()
             x.Culture
             x.InferTypesFromValues.ToString()
             x.Schema
             x.InferenceMode.ToString() ]
        | Json x ->
            ["Json"
             x.Sample
             x.SampleIsList.ToString()
             x.RootName
             x.Culture
             x.InferTypesFromValues.ToString()
             x.PreferDictionaries.ToString()
             x.InferenceMode.ToString()
             x.Schema ]
        | Html x ->
            ["Html"
             x.Sample
             x.PreferOptionals.ToString()
             x.IncludeLayoutTables.ToString()
             x.Culture ]
        | WorldBank x ->
            ["WorldBank"
             x.Sources
             x.Asynchronous.ToString() ]
        |> String.concat ","

    member x.ExpectedPath outputFolder =
        let fileName = x.ToString().Replace(">", "&gt;").Replace("<", "&lt;").Replace("://", "_").Replace("/", "_")
        // Handle both formats - with or without a dot before 'expected'
        let path1 = Path.Combine(outputFolder, (fileName + ".expected"))
        let path2 = Path.Combine(outputFolder, (fileName + "expected"))
        
        if File.Exists(path1) then path1
        else if File.Exists(path2) then path2
        else path1 // Default to the first one if neither exists

    member x.Dump (resolutionFolder, outputFolder, runtimeAssembly, runtimeAssemblyRefs, signatureOnly, ignoreOutput) =
        let replace (oldValue:string) (newValue:string) (str:string) = str.Replace(oldValue, newValue)
        let output =
            let tp, t = x.GenerateType resolutionFolder runtimeAssembly runtimeAssemblyRefs
            Testing.FormatProvidedType(tp, t, signatureOnly, ignoreOutput, 10, 100)
            |> replace "FSharp.Data.Runtime." "FDR."
            |> replace resolutionFolder "<RESOLUTION_FOLDER>"
            |> replace "@\"<RESOLUTION_FOLDER>\"" "\"<RESOLUTION_FOLDER>\""
        if outputFolder <> "" then
            File.WriteAllText(x.ExpectedPath outputFolder, output)
        output

    static member Parse (line:string) =
        printfn "Parsing %s" line
        let args = line.Split [|','|]
        args.[0],
        match args.[0] with
        | "Csv" ->
            Csv { Sample = args.[1]
                  Separators = args.[2]
                  InferRows = Int32.MaxValue
                  Schema = args.[3].Replace(';', ',')
                  HasHeaders = args.[4] |> bool.Parse
                  IgnoreErrors = false
                  SkipRows = 0
                  AssumeMissingValues = args.[5] |> bool.Parse
                  PreferOptionals = args.[6] |> bool.Parse
                  Quote = '"'
                  MissingValues = args.[7]
                  Culture = args.[8]
                  Encoding = args.[9]
                  CacheRows = false
                  ResolutionFolder = ""
                  EmbeddedResource = ""
                  PreferDateOnly = false
                  StrictBooleans = false
                  UseOriginalNames = false }
        | "Xml" ->
            Xml { Sample = args.[1]
                  SampleIsList = args.[2] |> bool.Parse
                  Global = args.[3] |> bool.Parse
                  Culture = args.[4]
                  Encoding = ""
                  ResolutionFolder = ""
                  EmbeddedResource = "" 
                  InferTypesFromValues = args.[5] |> bool.Parse
                  Schema = args.[6]
                  InferenceMode = args.[7] |> InferenceMode.Parse
                  PreferDateOnly = false
                  DtdProcessing = "Ignore"
                  UseOriginalNames = false }
        | "Json" ->
            // Handle special case for Schema.json tests where some fields might be empty
            if args.Length > 5 && not (String.IsNullOrEmpty(args.[5])) then
                Json { Sample = args.[1]
                       SampleIsList = if String.IsNullOrEmpty(args.[2]) then false else args.[2] |> bool.Parse
                       RootName = args.[3]
                       Culture = args.[4]
                       Encoding = ""
                       ResolutionFolder = ""
                       EmbeddedResource = ""
                       InferTypesFromValues = args.[5] |> bool.Parse
                       PreferDictionaries = args.[6] |> bool.Parse
                       InferenceMode = args.[7] |> InferenceMode.Parse
                       Schema = if args.Length > 8 then args.[8] else ""
                       PreferDateOnly = false
                       UseOriginalNames = false }
            else
                // This is for schema-based tests in the format "Json,,,,,true,false,BackwardCompatible,SimpleSchema.json"
                Json { Sample = args.[1]
                       SampleIsList = false
                       RootName = args.[3]
                       Culture = args.[4]
                       Encoding = ""
                       ResolutionFolder = ""
                       EmbeddedResource = ""
                       InferTypesFromValues = true
                       PreferDictionaries = false
                       InferenceMode = InferenceMode.Parse "BackwardCompatible"
                       Schema = if args.Length > 8 then args.[8] else ""
                       PreferDateOnly = false
                       UseOriginalNames = false }
        | "Html" ->
            Html { Sample = args.[1]
                   PreferOptionals = args.[2] |> bool.Parse
                   IncludeLayoutTables = args.[3] |> bool.Parse
                   MissingValues = ""
                   Culture = args.[4]
                   Encoding = ""
                   ResolutionFolder = ""
                   EmbeddedResource = ""
                   PreferDateOnly = false }
        | "WorldBank" ->
            WorldBank { Sources = args.[1]
                        Asynchronous = args.[2] |> bool.Parse }
        | _ -> failwithf "Unknown: %s" args.[0]

    static member GetRuntimeAssemblyRefs () =
        let (++) a b = Path.Combine(a, b)
        #if DEBUG
        let build = "Debug"
        #else
        let build = "Release"
        #endif

        let extraDlls = 
            [ "FSharp.Data.Http"
              "FSharp.Data.Runtime.Utilities"
              "FSharp.Data.Csv.Core"
              "FSharp.Data.Html.Core"
              "FSharp.Data.Xml.Core"
              "FSharp.Data.Json.Core" 
              "FSharp.Data.WorldBank.Core" ]
        let extraRefs = 
            [ for j in  extraDlls do
                __SOURCE_DIRECTORY__ ++ ".." ++ ".." ++ "src" ++ "FSharp.Data" ++ "bin" ++ build ++ "netstandard2.0" ++ (j + ".dll") ]
        extraRefs @ Targets.DotNetStandard20FSharpRefs()

