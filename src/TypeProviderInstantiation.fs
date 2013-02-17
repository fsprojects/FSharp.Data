namespace ProviderImplementation

open ProviderImplementation.ProvidedTypes

type CsvProviderArgs = 
    { Sample : string
      Separator : string
      Culture : string
      InferRows : int
      ResolutionFolder : string }

type XmlProviderArgs = 
    { Sample : string
      Global : bool
      SampleList : bool
      Culture : string
      ResolutionFolder : string }

type JsonProviderArgs = 
    { Sample : string
      SampleList : bool
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

#if EXPERIMENTAL
type ApiaryProviderArgs = 
    { ApiName : string }
#endif

type TypeProviderInstantiation = 

#if EXPERIMENTAL
    | Apiary of ApiaryProviderArgs
#else
    | Csv of CsvProviderArgs
    | Xml of XmlProviderArgs
    | Json of JsonProviderArgs
    | WorldBank of WorldBankProviderArgs
    | Freebase of FreebaseProviderArgs    
#endif

    member x.generateType resolutionFolder runtimeAssembly =
        let f, args =
            match x with
#if EXPERIMENTAL
            | Apiary x ->
                (fun cfg -> new ApiaryProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.ApiName |] 
#else
            | Csv x -> 
                (fun cfg -> new CsvProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.Separator
                   box x.Culture
                   box x.InferRows
                   box x.ResolutionFolder |] 
            | Xml x ->
                (fun cfg -> new XmlProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.Global
                   box x.SampleList
                   box x.Culture
                   box x.ResolutionFolder |] 
            | Json x -> 
                (fun cfg -> new JsonProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.SampleList
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
#endif
        Debug.generate resolutionFolder runtimeAssembly f args
