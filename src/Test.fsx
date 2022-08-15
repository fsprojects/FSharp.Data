#load "SetupTesting.fsx"
let dir = __SOURCE_DIRECTORY__ + "/FSharp.Data.DesignTime"
let proj = "FSharp.Data.DesignTime"
SetupTesting.generateSetupScript (__SOURCE_DIRECTORY__ + "/FSharp.Data.DesignTime") "FSharp.Data.DesignTime"
#load "FSharp.Data.DesignTime/__setup__FSharp.Data.DesignTime__.fsx"
#load "../paket-files/fsprojects/FSharp.TypeProviders.SDK/src/ProvidedTypesTesting.fs"
#load "../tests/FSharp.Data.DesignTime.Tests/TypeProviderInstantiation.fs"

open System
open System.Globalization
open System.IO
open ProviderImplementation
open FSharp.Data
open FSharp.Data.Runtime

let (++) a b = Path.Combine(a, b)
let resolutionFolder = __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.Tests" ++ "Data"
let outputFolder = __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.DesignTime.Tests" ++ "expected"
let assemblyName = "FSharp.Data.dll"

let dump signatureOnly ignoreOutput saveToFileSystem (inst:TypeProviderInstantiation) =
    let root = __SOURCE_DIRECTORY__ ++ ".." ++ "bin"
    let runtimeAssembly = root ++ "netstandard2.0" ++ assemblyName
    let runtimeAssemblyRefs = TypeProviderInstantiation.GetRuntimeAssemblyRefs ()
    inst.Dump(resolutionFolder, (if saveToFileSystem then outputFolder else ""), runtimeAssembly, runtimeAssemblyRefs, signatureOnly, ignoreOutput)
    |> Console.WriteLine

let dumpAll inst =
    dump false false false inst

let parameters : HtmlInference.Parameters = 
    { MissingValues = TextConversions.DefaultMissingValues
      CultureInfo = CultureInfo.InvariantCulture
      UnitsOfMeasureProvider = StructuralInference.defaultUnitsOfMeasureProvider
      PreferOptionals = false }

let includeLayout = false

let printTable tableName (url:string)  = 
    url
    |> HtmlDocument.Load
    |> HtmlRuntime.getTables (Some parameters) includeLayout
    |> List.filter (fun table -> table.Name = tableName)
    |> List.iter (printfn "+++++++++++++++++++++++++++++++++++++\n%O")

printTable "Overview" "https://en.wikipedia.org/wiki/List_of_Doctor_Who_serials"

Html { Sample = "doctor_who3.html"
       PreferOptionals = false
       IncludeLayoutTables = false
       MissingValues = "NaN,NA,N/A,#N/A,:,-,TBA,TBD"
       Culture = "" 
       Encoding = ""
       ResolutionFolder = ""
       EmbeddedResource = "" }
|> dumpAll

Json { Sample = "optionals.json"
       SampleIsList = false
       RootName = ""
       Culture = ""
       Encoding = ""
       ResolutionFolder = ""
       EmbeddedResource = ""
       InferTypesFromValues = true
       PreferDictionaries = false }
|> dumpAll

Xml { Sample = "JsonInXml.xml"      
      SampleIsList = true
      Global = false
      Culture = ""
      Encoding = ""
      ResolutionFolder = ""
      EmbeddedResource = ""
      InferTypesFromValues = true
      Schema = "" }
|> dumpAll

Csv { Sample = "AirQuality.csv"
      Separators = ";"
      InferRows = Int32.MaxValue
      Schema = ""
      HasHeaders = true
      IgnoreErrors = false
      SkipRows = 0
      AssumeMissingValues = false
      PreferOptionals = false
      Quote = '"'
      MissingValues = "NaN,NA,N/A,#N/A,:,-,TBA,TBD"
      CacheRows = true
      Culture = ""
      Encoding = ""
      ResolutionFolder = ""
      EmbeddedResource = "" }
|> dumpAll

let testCases =
    __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.DesignTime.Tests" ++ "SignatureTestCases.config"
    |> File.ReadAllLines
    |> Array.map (TypeProviderInstantiation.Parse >> snd)

for testCase in testCases do
    dump false false true testCase
