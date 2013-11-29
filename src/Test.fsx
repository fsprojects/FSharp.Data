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

let platform = Full

let runtimeAssembly = 
    match platform with
    | Full -> __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ assemblyName
    | Portable -> __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ "portable" ++ assemblyName
    | Silverlight -> __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ "sl5" ++ assemblyName

let signatureOnly = false
let ignoreOutput = false

let generate (inst:TypeProviderInstantiation) = inst.GenerateType resolutionFolder runtimeAssembly platform
let prettyPrint (t:ProvidedTypes.ProvidedTypeDefinition) = 
    if t.Name.StartsWith "FreebaseDataProvider" 
    then Debug.prettyPrint signatureOnly ignoreOutput 5 10 t
    else Debug.prettyPrint signatureOnly ignoreOutput 10 100 t

Json { Sample = "topics.json"
       SampleIsList = true
       RootName = ""
       Culture = "" 
       ResolutionFolder = "" }
|> generate |> prettyPrint |> Console.WriteLine

Xml { Sample = "HtmlBody.xml"
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
      SafeMode = false
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
