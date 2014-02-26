#if INTERACTIVE
#load "SetupTesting.fsx"
SetupTesting.generateSetupScript __SOURCE_DIRECTORY__ "FSharp.Data.DesignTime"
#load "__setup__FSharp.Data.DesignTime__.fsx"
#else
module Test
#endif

open System
open System.IO
open System.Net
open ProviderImplementation

//alow test cases that access the network to work when you're behind a proxy
WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials

let (++) a b = Path.Combine(a, b)
let resolutionFolder = __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.Tests" ++ "Data"
let assemblyName = "FSharp.Data.dll"

type Platform = Net40 | Portable7 | Portable47

let platform = Portable47

let runtimeAssembly = 
    match platform with
    | Net40 -> __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ assemblyName
    | Portable7 -> __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ "portable7" ++ assemblyName
    | Portable47 -> __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ "portable47" ++ assemblyName    

let signatureOnly = false
let ignoreOutput = false

let generate (inst:TypeProviderInstantiation) = inst.GenerateType resolutionFolder runtimeAssembly
let prettyPrint (t:ProvidedTypes.ProvidedTypeDefinition) = 
    if t.Name.StartsWith "FreebaseDataProvider" 
    then Debug.prettyPrint signatureOnly ignoreOutput 5 10 t
    else Debug.prettyPrint signatureOnly ignoreOutput 10 100 t

Html { Sample = "list_of_counties_wikipedia.html"
       PreferOptionals = false
       Culture = "" 
       ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Json { Sample = "optionals.json"
       SampleIsList = false
       RootName = ""
       Culture = "" 
       ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Xml { Sample = "http://tomasp.net/blog/rss.aspx"
      SampleIsList = false
      Global = true
      Culture = "" 
      ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Csv { Sample = "AirQuality.csv"
      Separator = ";" 
      Culture = "" 
      InferRows = Int32.MaxValue
      Schema = ""
      HasHeaders = true
      IgnoreErrors = false
      AssumeMissingValues = false
      PreferOptionals = false
      Quote = '"'
      MissingValues = "NaN,NA,#N/A,:"
      CacheRows = true
      ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

let testCases = 
    __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.Tests.DesignTime" ++ "SignatureTestCases.config"
    |> File.ReadAllLines
    |> Array.map TypeProviderInstantiation.Parse

for testCase in testCases do
    testCase 
    |> generate 
    |> prettyPrint
    |> Console.WriteLine
