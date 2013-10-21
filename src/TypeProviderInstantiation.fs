namespace ProviderImplementation

open ProviderImplementation.ProvidedTypes

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

type XmlProviderArgs = 
    { Sample : string
      SampleIsList : bool
      Global : bool
      Culture : string
      ResolutionFolder : string }

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
    | Xml of XmlProviderArgs
    | Json of JsonProviderArgs
    | WorldBank of WorldBankProviderArgs
    | Freebase of FreebaseProviderArgs    

    member x.generateType resolutionFolder runtimeAssembly =
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
            | Xml x ->
                (fun cfg -> new XmlProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.SampleIsList
                   box x.Global
                   box x.Culture
                   box x.ResolutionFolder |] 
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
