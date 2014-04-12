#if INTERACTIVE
#load "SetupTesting.fsx"
SetupTesting.generateSetupScript __SOURCE_DIRECTORY__ "FSharp.Data.DesignTime"
#load "__setup__FSharp.Data.DesignTime__.fsx"
#else
module internal Test
#endif

open System
open System.IO
open System.Net
open ProviderImplementation

//alow test cases that access the network to work when you're behind a proxy
WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials

let (++) a b = Path.Combine(a, b)
let resolutionFolder = __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.Tests" ++ "Data"
let outputFolder = __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.DesignTime.Tests" ++ "expected"
let assemblyName = "FSharp.Data.dll"

type Platform = Net40 | Portable7 | Portable47

let dump signatureOnly ignoreOutput platform saveToFileSystem (inst:TypeProviderInstantiation) =
    let runtimeAssembly = 
        match platform with
        | Net40 -> __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ assemblyName
        | Portable7 -> __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ "portable7" ++ assemblyName
        | Portable47 -> __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ "portable47" ++ assemblyName    
    inst.Dump resolutionFolder (if saveToFileSystem then outputFolder else "") runtimeAssembly signatureOnly ignoreOutput
    |> Console.WriteLine

let dumpNet40 = dump false false Net40
let dumpPortable47 = dump false false Portable47

Json { Sample = "optionals.json"
       SampleIsList = false
       RootName = ""
       Culture = "" 
       ResolutionFolder = "" }
|> dumpPortable47 false

Xml { Sample = "JsonInXml.xml"
      SampleIsList = true
      Global = false
      Culture = "" 
      ResolutionFolder = "" }
|> dumpPortable47 false

Csv { Sample = "AirQuality.csv"
      Separators = ";" 
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
|> dumpPortable47 false

let testCases = 
    __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.DesignTime.Tests" ++ "SignatureTestCases.config"
    |> File.ReadAllLines
    |> Array.map TypeProviderInstantiation.Parse

for testCase in testCases do
    dumpNet40 true testCase
