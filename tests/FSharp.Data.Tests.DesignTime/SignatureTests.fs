﻿#if INTERACTIVE
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#r "../../bin/FSharp.Data.DesignTime.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.DesignTime.SignatureTests
#endif

open System
open System.IO
open System.Net
open FsUnit
open NUnit.Framework
open ProviderImplementation

//alow tests that access the network to work when you're behind a proxy
WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials

type TypeProviderInstantiation with

    member x.Dump resolutionFolder runtimeAssembly platform signatureOnly ignoreOutput =
        let output = 
            x.GenerateType resolutionFolder runtimeAssembly platform
            |> match x with
               | Freebase _ -> Debug.prettyPrint signatureOnly ignoreOutput 5 10
               | _ -> Debug.prettyPrint signatureOnly ignoreOutput 10 100
        output.Replace("FSharp.Data.Runtime.", "FDR.")
              .Replace(__SOURCE_DIRECTORY__, "<SOURCE_DIRECTORY>")

let (++) a b = Path.Combine(a, b)

let sourceDirectory = __SOURCE_DIRECTORY__

let testCases = 
    sourceDirectory ++ "SignatureTestCases.config" 
    |> File.ReadAllLines
    |> Array.map TypeProviderInstantiation.Parse

let expectedDirectory = sourceDirectory ++ "expected" 

let getExpectedPath testCase = 
    expectedDirectory ++ (testCase.ToString().Replace(">", "&gt;").Replace("<", "&lt;").Replace("://", "_").Replace("/", "_") + ".expected")

let resolutionFolder = sourceDirectory ++ ".." ++ "FSharp.Data.Tests" ++ "Data"
let assemblyName = "FSharp.Data.dll"
let runtimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ assemblyName
let portable47RuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ "portable47" ++ assemblyName
let portable7RuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ "portable47" ++ assemblyName

let generateAllExpected() =
    if not <| Directory.Exists expectedDirectory then 
        Directory.CreateDirectory expectedDirectory |> ignore
    for testCase in testCases do
        let output = testCase.Dump resolutionFolder runtimeAssembly Platform.Full (*signatureOnly*)false (*ignoreOutput*)false
        File.WriteAllText(getExpectedPath testCase, output)

let normalize (str:string) =
  str.Replace("\r\n", "\n")
     .Replace("\r", "\n")
     .Replace(" \"<SOURCE_DIRECTORY>/../FSharp.Data.Tests/Data", " @\"<SOURCE_DIRECTORY>\..\FSharp.Data.Tests\Data")

[<Test>]
[<TestCaseSource "testCases">]
let ``Validate signature didn't change `` (testCase:TypeProviderInstantiation) = 
    let expected = getExpectedPath testCase |> File.ReadAllText |> normalize
    let output = testCase.Dump resolutionFolder runtimeAssembly Platform.Full (*signatureOnly*)false (*ignoreOutput*)false |> normalize
    output |> should equal expected

#if MONO
#else

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in portable profile 47 `` (testCase:TypeProviderInstantiation) = 
    testCase.Dump resolutionFolder portable47RuntimeAssembly Platform.Portable47 (*signatureOnly*)false (*ignoreOutput*)true |> ignore

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in portable profile 7 `` (testCase:TypeProviderInstantiation) = 
    testCase.Dump resolutionFolder portable7RuntimeAssembly Platform.Portable7 (*signatureOnly*)false (*ignoreOutput*)true |> ignore

#endif
