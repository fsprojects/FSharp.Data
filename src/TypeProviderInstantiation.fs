namespace ProviderImplementation

open System
open ProviderImplementation.ProvidedTypes
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.Freebase.FreebaseRequests

type CsvProviderArgs = 
    { Sample : string
      Separator : string
      Culture : string
      InferRows : int
      Schema : string
      HasHeaders : bool
      IgnoreErrors : bool
      SafeMode : bool
      PreferOptionals : bool
      Quote : char
      MissingValues : string
      CacheRows : bool
      ResolutionFolder : string }

#if SILVERLIGHT
#else
type XmlProviderArgs = 
    { Sample : string
      SampleIsList : bool
      Global : bool
      Culture : string
      ResolutionFolder : string }
#endif

type JsonProviderArgs = 
    { Sample : string
      SampleIsList : bool
      RootName : string
      Culture : string
      ResolutionFolder : string }

type WorldBankProviderArgs =
    { Sources : string
      Asynchronous : bool }

type FreebaseProviderArgs =
    { Key : string
      ServiceUrl : string
      NumIndividuals : int
      UseUnitsOfMeasure : bool 
      Pluralize : bool 
      SnapshotDate : string
      LocalCache : bool
      AllowLocalQueryEvaluation : bool }

type TypeProviderInstantiation = 
    | Csv of CsvProviderArgs
#if SILVERLIGHT
#else
    | Xml of XmlProviderArgs
#endif
    | Json of JsonProviderArgs
    | WorldBank of WorldBankProviderArgs
    | Freebase of FreebaseProviderArgs    

    member x.GenerateType resolutionFolder runtimeAssembly =
        let f, args =
            match x with
            | Csv x -> 
                (fun cfg -> new CsvProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.Separator
                   box x.Culture
                   box x.InferRows
                   box x.Schema
                   box x.HasHeaders
                   box x.IgnoreErrors
                   box x.SafeMode
                   box x.PreferOptionals
                   box x.Quote
                   box x.MissingValues
                   box x.CacheRows
                   box x.ResolutionFolder |] 
#if SILVERLIGHT
#else
            | Xml x ->
                (fun cfg -> new XmlProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.SampleIsList
                   box x.Global
                   box x.Culture
                   box x.ResolutionFolder |] 
#endif
            | Json x -> 
                (fun cfg -> new JsonProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.SampleIsList
                   box x.RootName
                   box x.Culture
                   box x.ResolutionFolder|] 
            | WorldBank x ->
                (fun cfg -> new WorldBankProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sources
                   box x.Asynchronous |] 
            | Freebase x ->
                (fun cfg -> new FreebaseTypeProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Key
                   box x.ServiceUrl
                   box x.NumIndividuals
                   box x.UseUnitsOfMeasure 
                   box x.Pluralize
                   box x.SnapshotDate
                   box x.LocalCache
                   box x.AllowLocalQueryEvaluation |]
        Debug.generate resolutionFolder runtimeAssembly f args

    override x.ToString() =
        match x with
        | Csv x -> 
            ["Csv"
             x.Sample
             x.Separator
             x.Culture
             x.Schema.Replace(',', ';')
             x.HasHeaders.ToString()
             x.SafeMode.ToString()
             x.PreferOptionals.ToString()]
#if SILVERLIGHT
#else
        | Xml x -> 
            ["Xml"
             x.Sample
             x.SampleIsList.ToString()
             x.Global.ToString()
             x.Culture]
#endif
        | Json x -> 
            ["Json"
             x.Sample
             x.SampleIsList.ToString()
             x.RootName
             x.Culture]
        | WorldBank x -> 
            ["WorldBank"
             x.Sources
             x.Asynchronous.ToString()]
        | Freebase x -> 
            ["Freebase"
             x.NumIndividuals.ToString()
             x.UseUnitsOfMeasure.ToString()
             x.Pluralize.ToString()]
        |> String.concat ","

    static member Parse (line:string) =
        let args = line.Split [|','|]
        match args.[0] with
        | "Csv" ->
            Csv { Sample = args.[1]
                  Separator = args.[2]
                  Culture = args.[3]
                  InferRows = Int32.MaxValue
                  Schema = args.[4].Replace(';', ',')
                  HasHeaders = args.[5] |> bool.Parse
                  IgnoreErrors = false
                  SafeMode = args.[6] |> bool.Parse
                  PreferOptionals = args.[7] |> bool.Parse
                  Quote = '"'
                  MissingValues = String.Join(",", Operations.DefaultMissingValues)
                  CacheRows = false
                  ResolutionFolder = "" }
#if SILVERLIGHT
#else
        | "Xml" ->
            Xml { Sample = args.[1]
                  SampleIsList = args.[2] |> bool.Parse
                  Global = args.[3] |> bool.Parse
                  Culture = args.[4]
                  ResolutionFolder = "" }
#endif
        | "Json" ->
            Json { Sample = args.[1]
                   SampleIsList = args.[2] |> bool.Parse
                   RootName = args.[3]
                   Culture = args.[4] 
                   ResolutionFolder = ""}
        | "WorldBank" ->
            WorldBank { Sources = args.[1]
                        Asynchronous = args.[2] |> bool.Parse }
        | "Freebase" ->
            Freebase { Key = args.[1]
                       NumIndividuals = args.[2] |> Int32.Parse
                       UseUnitsOfMeasure = args.[3] |> bool.Parse
                       Pluralize = args.[4] |> bool.Parse
                       SnapshotDate = "2013-10-19T20:32:39"
                       ServiceUrl = FreebaseQueries.DefaultServiceUrl
                       LocalCache = true
                       AllowLocalQueryEvaluation = true }
        | _ -> failwithf "Unknown: %s" args.[0]