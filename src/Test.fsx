#if INTERACTIVE
#load "SetupTesting.fsx"
SetupTesting.generateSetupScript __SOURCE_DIRECTORY__
#load "__setup__.fsx"
#endif

open System
open System.IO
open ProviderImplementation

let (++) a b = Path.Combine(a, b)
let resolutionFolder = __SOURCE_DIRECTORY__ ++ ".." ++ "samples" ++ "docs"
#if EXPERIMENTAL
let assemblyName = "FSharp.Data.Experimental.dll"
#else
let assemblyName = "FSharp.Data.dll"
#endif
let runtimeAssembly = __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ assemblyName

//let signatureOnly = true
let signatureOnly = false

let generate (inst:TypeProviderInstantiation) = inst.generateType resolutionFolder runtimeAssembly
let prettyPrint t = Debug.prettyPrint signatureOnly t
let prettyPrintWithMaxDepth maxDepth t = Debug.prettyPrintWithMaxDepth signatureOnly maxDepth t

#if EXPERIMENTAL

Apiary { ApiName = "themoviedb" }
|> generate |> prettyPrint |> Console.WriteLine

Apiary { ApiName = "fssnip" }
|> generate |> prettyPrint |> Console.WriteLine

#else

Csv { Sample = "SmallTest.csv"
      Separator = "" 
      Culture = "" 
      InferRows = Int32.MaxValue
      ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Csv { Sample = "MSFT.csv"
      Separator = "" 
      Culture = "" 
      InferRows = Int32.MaxValue
      ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Csv { Sample = "AirQuality.csv"
      Separator = ";" 
      Culture = "" 
      InferRows = Int32.MaxValue
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
       ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Json { Sample = "TwitterStream.json"
       SampleList = true
       Culture = "" 
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

#endif