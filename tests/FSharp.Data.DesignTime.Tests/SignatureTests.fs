#if INTERACTIVE
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#r "../../bin/FSharp.Data.DesignTime.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.DesignTime.Tests.SignatureTests
#endif

open System
open System.IO
open System.Net
open FsUnit
open NUnit.Framework
open ProviderImplementation

//alow tests that access the network to work when you're behind a proxy
WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials

let (++) a b = Path.Combine(a, b)

let sourceDirectory = __SOURCE_DIRECTORY__

let testCases = 
    sourceDirectory ++ "SignatureTestCases.config" 
    |> File.ReadAllLines
    |> Array.map TypeProviderInstantiation.Parse

let testCasesForUSA =
    testCases
    |> Array.filter (function Freebase _ | WorldBank _ -> false | _ -> true)

let expectedDirectory = sourceDirectory ++ "expected" 

let resolutionFolder = sourceDirectory ++ ".." ++ "FSharp.Data.Tests" ++ "Data"
let assemblyName = "FSharp.Data.dll"
let runtimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ assemblyName
let portable47RuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ "portable47" ++ assemblyName
let portable7RuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ "portable7" ++ assemblyName

let generateAllExpected() =
    if not <| Directory.Exists expectedDirectory then 
        Directory.CreateDirectory expectedDirectory |> ignore
    for testCase in testCases do
        testCase.Dump resolutionFolder expectedDirectory runtimeAssembly (*signatureOnly*)false (*ignoreOutput*)false
        |> ignore

let normalize (str:string) =
  str.Replace("\r\n", "\n").Replace("\r", "\n").Replace("@\"<RESOLUTION_FOLDER>\"", "\"<RESOLUTION_FOLDER>\"")

[<Test>]
#if TEAM_CITY
[<TestCaseSource "testCasesForUSA">]
#else
[<TestCaseSource "testCases">]
#endif
let ``Validate signature didn't change `` (testCase:TypeProviderInstantiation) = 
    let expected = testCase.ExpectedPath expectedDirectory |> File.ReadAllText |> normalize
    let output = testCase.Dump resolutionFolder "" runtimeAssembly (*signatureOnly*)false (*ignoreOutput*)false |> normalize
    if output <> expected then
        printfn "Obtained Signature:\n%s" output
    output |> should equal expected

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in portable profile 47 `` (testCase:TypeProviderInstantiation) = 
    testCase.Dump resolutionFolder "" portable47RuntimeAssembly (*signatureOnly*)false (*ignoreOutput*)true |> ignore

[<Test>]
[<TestCaseSource "testCases">]
[<Platform "Net">]
let ``Generating expressions works in portable profile 7 `` (testCase:TypeProviderInstantiation) = 
    testCase.Dump resolutionFolder "" portable7RuntimeAssembly (*signatureOnly*)false (*ignoreOutput*)true |> ignore
