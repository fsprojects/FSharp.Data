#if INTERACTIVE
#load "SetupTesting.fsx"
SetupTesting.generateSetupScript __SOURCE_DIRECTORY__ "FSharp.Data.Experimental.DesignTime"
#load "__setup__FSharp.Data.Experimental.DesignTime__.fsx"
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
let assemblyName = "FSharp.Data.Experimental.dll"

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
let prettyPrint t = Debug.prettyPrint signatureOnly ignoreOutput 10 100 t

Apiary { ApiName = "bionames"
         SpecialNames = "Rhogeessa=TaxonName,Apomys=TaxonName,Philautus acutirostris=TaxonName" }
|> generate |> prettyPrint |> Console.WriteLine

let testCases = 
    __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.Tests.Experimental.DesignTime" ++ "SignatureTestCases.config"
    |> File.ReadAllLines
    |> Array.map TypeProviderInstantiation.Parse

for testCase in testCases do
    testCase 
    |> generate 
    |> prettyPrint
    |> Console.WriteLine
