#if INTERACTIVE
#load "SetupTesting.fsx"
SetupTesting.generateSetupScript __SOURCE_DIRECTORY__ "FSharp.Data.DesignTime"
#load "__setup__FSharp.Data.DesignTime__.fsx"
#else
module Test
#endif

open System
open System.IO
open ProviderImplementation

let (++) a b = Path.Combine(a, b)
let resolutionFolder = __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.Tests" ++ "Data"
let assemblyName = "FSharp.Data.dll"
let runtimeAssembly = __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ assemblyName

let signatureOnly = false
let ignoreOutput = false

let generate (inst:TypeProviderInstantiation) = inst.generateType resolutionFolder runtimeAssembly
let prettyPrint t = Debug.prettyPrint signatureOnly ignoreOutput t
let prettyPrintWithMaxDepth maxDepth t = Debug.prettyPrintWithMaxDepth signatureOnly ignoreOutput maxDepth t

Csv { Sample = "SmallTest.csv"
      Separator = "" 
      Culture = "" 
      InferRows = Int32.MaxValue
      Schema = ""
      HasHeaders = true
      IgnoreErrors = false
      SafeMode = false
      PreferOptionals = false
      Quote = '"'
      MissingValues = "NaN,NA,#N/A,:"
      CacheRows = true
      ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Csv { Sample = "MSFT.csv"
      Separator = "" 
      Culture = "" 
      InferRows = Int32.MaxValue
      Schema = ""
      HasHeaders = true
      IgnoreErrors = false
      SafeMode = false
      PreferOptionals = false
      Quote = '"'
      MissingValues = "NaN,NA,#N/A,:"
      CacheRows = true
      ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Csv { Sample = "AirQuality.csv"
      Separator = ";" 
      Culture = "" 
      InferRows = Int32.MaxValue
      Schema = ""
      HasHeaders = true
      IgnoreErrors = false
      SafeMode = false
      PreferOptionals = false
      Quote = '"'
      MissingValues = "NaN,NA,#N/A,:"
      CacheRows = true
      ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Csv { Sample = "Titanic.csv"
      Separator = "" 
      Culture = "" 
      InferRows = Int32.MaxValue
      Schema = "PassengerId=int"
      HasHeaders = true
      IgnoreErrors = false
      SafeMode = true
      PreferOptionals = true
      Quote = '"'
      MissingValues = "NaN,NA,#N/A,:"
      CacheRows = true
      ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Xml { Sample = "Writers.xml"
      Global = false
      SampleList = false
      Culture = "" 
      ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Xml { Sample = "HtmlBody.xml"
      Global = true
      SampleList = false
      Culture = "" 
      ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Xml { Sample = "http://tomasp.net/blog/rss.aspx"
      Global = false
      SampleList = false
      Culture = "" 
      ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Json { Sample = "WorldBank.json"
       SampleList = false
       Culture = "" 
       RootName = "WorldBank"
       ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Json { Sample = "TwitterStream.json"
       SampleList = true
       Culture = "" 
       RootName = ""
       ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Json { Sample = "list_my.json"
       SampleList = false
       Culture = "" 
       RootName = "Topic"
       ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

WorldBank { Sources = ""
            Asynchronous = false }
|> generate |> prettyPrint |> Console.WriteLine

WorldBank { Sources = "World Development Indicators;Global Development Finance"
            Asynchronous = true }
|> generate |> prettyPrint |> Console.WriteLine

Freebase { Key = "none" 
           ServiceUrl = "https://www.googleapis.com/freebase/v1" 
           NumIndividuals = 10 
           UseUnitsOfMeasure = true 
           Pluralize = true 
           SnapshotDate = "now" 
           LocalCache = true 
           AllowLocalQueryEvaluation = true }
|> generate |> prettyPrintWithMaxDepth 3 |> Console.WriteLine
