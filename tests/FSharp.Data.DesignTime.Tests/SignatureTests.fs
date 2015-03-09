#if INTERACTIVE
#r "../../packages/NUnit/lib/nunit.framework.dll"
#r "../../bin/FSharp.Data.DesignTime.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.DesignTime.Tests.SignatureTests
#endif

open System.IO
open FsUnit
open NUnit.Framework
open ProviderImplementation

let (++) a b = Path.Combine(a, b)

let sourceDirectory = __SOURCE_DIRECTORY__

let testCasesTuple = 
    sourceDirectory ++ "SignatureTestCases.config" 
    |> File.ReadAllLines
    |> Array.map TypeProviderInstantiation.Parse

let testCases = 
    testCasesTuple
#if BUILD_SERVER
    |> Array.filter (snd >> function | WorldBank _ -> false | _ -> true)
#endif
    |> Array.map snd

let expectedDirectory = sourceDirectory ++ "expected" 

let resolutionFolder = sourceDirectory ++ ".." ++ "FSharp.Data.Tests" ++ "Data"
let assemblyName = "FSharp.Data.dll"
let runtimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ assemblyName
let portable47RuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ "portable47" ++ assemblyName
let portable7RuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ "portable7" ++ assemblyName

let generateAllExpected() =
    if not <| Directory.Exists expectedDirectory then 
        Directory.CreateDirectory expectedDirectory |> ignore
    for (sample, testCase) in testCasesTuple do
        try
            testCase.Dump resolutionFolder expectedDirectory runtimeAssembly (*signatureOnly*)false (*ignoreOutput*)false
            |> ignore
        with e -> 
            raise(new System.Exception(sprintf "Failed generating: %s" sample, e))

let normalize (str:string) =
  str.Replace("\r\n", "\n").Replace("\r", "\n").Replace("@\"<RESOLUTION_FOLDER>\"", "\"<RESOLUTION_FOLDER>\"")

[<TestCaseSource "testCases">]
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
