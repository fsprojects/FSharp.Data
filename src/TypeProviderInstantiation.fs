namespace ProviderImplementation

open System
open System.IO
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProvidedTypesTesting
open FSharp.Data.Runtime

type CsvProviderArgs =
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
      InlineSample : bool }

type XmlProviderArgs =
    { Sample : string
      SampleIsList : bool
      Global : bool
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string 
      InferTypesFromValues : bool
      InlineSample : bool }

type JsonProviderArgs =
    { Sample : string
      SampleIsList : bool
      RootName : string
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string 
      InferTypesFromValues : bool 
      InlineSample : bool }

type HtmlProviderArgs =
    { Sample : string
      PreferOptionals : bool
      IncludeLayoutTables : bool
      MissingValues : string
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string 
      InlineSample : bool }

type WorldBankProviderArgs =
    { Sources : string
      Asynchronous : bool }

type Platform = Net45 | NetStandard20

type TypeProviderInstantiation =
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
                   box x.InlineSample |]
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
                   box x.InlineSample |] 
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
                   box x.InlineSample |] 
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
                   box x.InlineSample |]
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
             x.Encoding
             x.InlineSample.ToString() ]
        | Xml x -> 
            ["Xml"
             x.Sample
             x.SampleIsList.ToString()
             x.Global.ToString()
             x.Culture
             x.InferTypesFromValues.ToString()
             x.InlineSample.ToString() ]
        | Json x -> 
            ["Json"
             x.Sample
             x.SampleIsList.ToString()
             x.RootName
             x.Culture
             x.InferTypesFromValues.ToString()
             x.InlineSample.ToString() ]
        | Html x -> 
            ["Html"
             x.Sample
             x.PreferOptionals.ToString()
             x.IncludeLayoutTables.ToString()
             x.Culture
             x.InlineSample.ToString() ]
        | WorldBank x -> 
            ["WorldBank"
             x.Sources
             x.Asynchronous.ToString()]
        |> String.concat ","

    member x.ExpectedPath outputFolder =
        Path.Combine(outputFolder, (x.ToString().Replace(">", "&gt;").Replace("<", "&lt;").Replace("://", "_").Replace("/", "_") + ".expected"))

    member x.Dump (resolutionFolder, outputFolder, runtimeAssembly, runtimeAssemblyRefs, signatureOnly, ignoreOutput) =
        let replace (oldValue:string) (newValue:string) (str:string) = str.Replace(oldValue, newValue)
        let output =
            let tp = x.GenerateType resolutionFolder runtimeAssembly runtimeAssemblyRefs
            Testing.FormatProvidedType(tp, signatureOnly, ignoreOutput, 10, 100)
            |> replace "FSharp.Data.Runtime." "FDR."
            |> replace resolutionFolder "<RESOLUTION_FOLDER>"
            |> replace "@\"<RESOLUTION_FOLDER>\"" "\"<RESOLUTION_FOLDER>\""
        if outputFolder <> "" then
            File.WriteAllText(x.ExpectedPath outputFolder, output)
        output

    static member Parse (line:string) =
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
                  InlineSample = false }
        | "Xml" ->
            Xml { Sample = args.[1]
                  SampleIsList = args.[2] |> bool.Parse
                  Global = args.[3] |> bool.Parse
                  Culture = args.[4]
                  Encoding = ""
                  ResolutionFolder = ""
                  EmbeddedResource = "" 
                  InferTypesFromValues = args.[5] |> bool.Parse 
                  InlineSample = false}
        | "Json" ->
            Json { Sample = args.[1]
                   SampleIsList = args.[2] |> bool.Parse
                   RootName = args.[3]
                   Culture = args.[4]
                   Encoding = ""
                   ResolutionFolder = ""
                   EmbeddedResource = "" 
                   InferTypesFromValues = args.[5] |> bool.Parse
                   InlineSample = false }
        | "Html" ->
            Html { Sample = args.[1]
                   PreferOptionals = args.[2] |> bool.Parse
                   IncludeLayoutTables = args.[3] |> bool.Parse
                   MissingValues = ""
                   Culture = args.[4]
                   Encoding = ""
                   ResolutionFolder = ""
                   EmbeddedResource = ""
                   InlineSample = false }
        | "WorldBank" ->
            WorldBank { Sources = args.[1]
                        Asynchronous = args.[2] |> bool.Parse }
        | _ -> failwithf "Unknown: %s" args.[0]

    static member GetRuntimeAssemblyRefs platform =
        match platform with
        | Net45 -> Targets.DotNet45FSharp41Refs()
        | NetStandard20 -> Targets.DotNetStandard20FSharp41Refs()

open System.Runtime.CompilerServices

[<assembly:InternalsVisibleToAttribute("FSharp.Data.DesignTime.Tests")>]
do()
