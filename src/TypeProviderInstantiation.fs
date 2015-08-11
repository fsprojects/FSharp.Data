namespace ProviderImplementation

open System
open System.IO
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime

type CsvProviderArgs = 
    { Sample : string
      Separators : string
<<<<<<< HEAD
=======
      Culture : string
>>>>>>> origin/master
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
      EmbeddedResource : string }

type XmlProviderArgs = 
    { Sample : string
      SampleIsList : bool
      Global : bool
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string 
      InferTypesFromValues : bool }

type XsdProviderArgs = 
    { SchemaFile : string
      ResolutionFolder : string }

type XsdProviderArgs = 
    { SchemaFile : string
      ResolutionFolder : string }

type JsonProviderArgs = 
    { Sample : string
      SampleIsList : bool
      RootName : string
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string 
      InferTypesFromValues : bool }

type HtmlProviderArgs = 
    { Sample : string
      PreferOptionals : bool
      IncludeLayoutTables : bool
      MissingValues : string
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string }

type WorldBankProviderArgs =
    { Sources : string
      Asynchronous : bool }

type TypeProviderInstantiation = 
    | Csv of CsvProviderArgs
    | Xml of XmlProviderArgs
    | Json of JsonProviderArgs
    | Html of HtmlProviderArgs
    | WorldBank of WorldBankProviderArgs
<<<<<<< HEAD
    | Xsd of XsdProviderArgs
=======
    | Freebase of FreebaseProviderArgs
    | Xsd of XsdProviderArgs
    | Comment of string
>>>>>>> origin/master

    member x.GenerateType resolutionFolder runtimeAssembly =
        let f, args =
            match x with
            | Comment _ -> failwith "Can't instantiate a comment"
            | Csv x -> 
                (fun cfg -> new CsvProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.Separators
<<<<<<< HEAD
=======
                   box x.Culture
>>>>>>> origin/master
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
                   box x.EmbeddedResource |] 
            | Xml x ->
                (fun cfg -> new XmlProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.SampleIsList
                   box x.Global
                   box x.Culture
<<<<<<< HEAD
                   box x.Encoding
                   box x.ResolutionFolder 
                   box x.EmbeddedResource
                   box x.InferTypesFromValues |] 
=======
                   box x.ResolutionFolder |] 
>>>>>>> origin/master
            | Xsd x ->
                let file = x.SchemaFile
                let schema =
                    if System.IO.File.Exists file then
                        System.IO.File.ReadAllText(file)
                    else
                        file
                (fun cfg -> new XsdProvider(cfg) :> TypeProviderForNamespaces),
                [| box schema
                   box resolutionFolder |] 
            | Json x -> 
                (fun cfg -> new JsonProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.SampleIsList
                   box x.RootName
                   box x.Culture
                   box x.Encoding
                   box x.ResolutionFolder 
                   box x.EmbeddedResource
                   box x.InferTypesFromValues |] 
            | Html x -> 
                (fun cfg -> new HtmlProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.PreferOptionals
                   box x.IncludeLayoutTables
                   box x.MissingValues
                   box x.Culture
                   box x.Encoding
                   box x.ResolutionFolder 
                   box x.EmbeddedResource |]
            | WorldBank x ->
                (fun cfg -> new WorldBankProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sources
                   box x.Asynchronous |] 
        Debug.generate resolutionFolder runtimeAssembly f args

    override x.ToString() =
<<<<<<< HEAD
       match x with
=======
        match x with
        | Comment c -> [c]
>>>>>>> origin/master
        | Csv x -> 
            ["Csv"
             x.Sample
             x.Separators
<<<<<<< HEAD
=======
             x.Culture
>>>>>>> origin/master
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
             x.InferTypesFromValues.ToString() ]
        | Json x -> 
            ["Json"
             x.Sample
             x.SampleIsList.ToString()
             x.RootName
             x.Culture
             x.InferTypesFromValues.ToString() ]
        | Html x -> 
            ["Html"
             x.Sample
             x.PreferOptionals.ToString()
             x.IncludeLayoutTables.ToString()
             x.Culture]
        | WorldBank x -> 
            ["WorldBank"
             x.Sources
             x.Asynchronous.ToString()]
        | Xsd x ->
           ["Xsd"
            x.SchemaFile
            x.ResolutionFolder]
<<<<<<< HEAD
       |> String.concat ","
=======
        | Freebase x -> 
            ["Freebase"
             x.NumIndividuals.ToString()
             x.UseUnitsOfMeasure.ToString()
             x.Pluralize.ToString()]
        
        |> String.concat ","
>>>>>>> origin/master

    member x.ExpectedPath outputFolder = 
        Path.Combine(outputFolder, (x.ToString().Replace(">", "&gt;").Replace("<", "&lt;").Replace("://", "_").Replace("/", "_") + ".expected"))

    member x.Dump resolutionFolder outputFolder runtimeAssembly signatureOnly ignoreOutput =
        let replace (oldValue:string) (newValue:string) (str:string) = str.Replace(oldValue, newValue)        
        let output = 
<<<<<<< HEAD
            x.GenerateType resolutionFolder runtimeAssembly
            |> Debug.prettyPrint signatureOnly ignoreOutput 10 100
            |> replace "FSharp.Data.Runtime." "FDR."
            |> replace resolutionFolder "<RESOLUTION_FOLDER>"
=======
            match x with
            | Comment _ -> ""
            | _ ->
               x.GenerateType resolutionFolder runtimeAssembly
               |> match x with
                  | Freebase _ -> Debug.prettyPrint signatureOnly ignoreOutput 5 10
                  | _ -> Debug.prettyPrint signatureOnly ignoreOutput 10 100
               |> replace "FSharp.Data.Runtime." "FDR."
               |> replace resolutionFolder "<RESOLUTION_FOLDER>"
>>>>>>> origin/master
        if outputFolder <> "" then
            File.WriteAllText(x.ExpectedPath outputFolder, output)
        output

    static member Parse (line:string) =
        let args = line.Split [|','|]
        args.[0],
        match args.[0] with
        | _ as token when token.StartsWith("//") ->  args |> String.Concat |> Comment
        | "Csv" ->
            Csv { Sample = args.[1]
                  Separators = args.[2]
<<<<<<< HEAD
=======
                  Culture = args.[3]
>>>>>>> origin/master
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
                  EmbeddedResource = "" }
        | "Xml" ->
            Xml { Sample = args.[1]
                  SampleIsList = args.[2] |> bool.Parse
                  Global = args.[3] |> bool.Parse
                  Culture = args.[4]
                  Encoding = ""
                  ResolutionFolder = ""
                  EmbeddedResource = "" 
                  InferTypesFromValues = args.[5] |> bool.Parse }
        | "Json" ->
            Json { Sample = args.[1]
                   SampleIsList = args.[2] |> bool.Parse
                   RootName = args.[3]
                   Culture = args.[4] 
                   Encoding = ""
                   ResolutionFolder = ""
                   EmbeddedResource = "" 
                   InferTypesFromValues = args.[5] |> bool.Parse }
        | "Html" ->
            Html { Sample = args.[1]
                   PreferOptionals = args.[2] |> bool.Parse
                   IncludeLayoutTables = args.[3] |> bool.Parse
                   MissingValues = ""
                   Culture = args.[4] 
                   Encoding = ""
                   ResolutionFolder = ""
                   EmbeddedResource = "" }
        | "WorldBank" ->
            WorldBank { Sources = args.[1]
                        Asynchronous = args.[2] |> bool.Parse }
<<<<<<< HEAD
=======
        | "Freebase" ->
            Freebase { Key = args.[1]
                       NumIndividuals = args.[2] |> Int32.Parse
                       UseUnitsOfMeasure = args.[3] |> bool.Parse
                       Pluralize = args.[4] |> bool.Parse
                       SnapshotDate = ""
                       ServiceUrl = FreebaseQueries.DefaultServiceUrl
                       LocalCache = true
                       AllowLocalQueryEvaluation = true 
                       UseRefinedTypes = true }
>>>>>>> origin/master
        | "Xsd" ->
            Xsd {
               SchemaFile = args.[1]
               ResolutionFolder = ""
            }
        | _ -> failwithf "Unknown: %s" args.[0]

open System.Runtime.CompilerServices

[<assembly:InternalsVisibleToAttribute("FSharp.Data.DesignTime.Tests")>]
do()
