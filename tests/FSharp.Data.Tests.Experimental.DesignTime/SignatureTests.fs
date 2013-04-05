module FSharp.Data.Tests.Experimental.DesignTime.SignatureTests

#if INTERACTIVE
#r "../../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#r "../../bin/FSharp.Data.Experimental.DesignTime.dll"
#load "../Common/FsUnit.fs"
#endif

open System
open System.IO
open FsUnit
open NUnit.Framework
open ProviderImplementation

type TestCase = 
    | TestCase of TypeProviderInstantiation
    override x.ToString() =
        let (TestCase x) = x
        match x with
        | Apiary x -> 
            ["Apiary"
             x.ApiName]
        |> String.concat ","

    static member Parse (line:string) =
        let args = line.Split [|','|]
        match args.[0] with
        | "Apiary" ->
            Apiary { ApiName = args.[1] }
        | _ -> failwithf "Unknown: %s" args.[0]
        |> TestCase

    member x.Dump resolutionFolder runtimeAssembly signatureOnly ignoreOutput =
        let (TestCase x) = x
        let output = 
            x.generateType resolutionFolder runtimeAssembly 
            |> Debug.prettyPrint signatureOnly ignoreOutput
        output.Replace("FSharp.Data.RuntimeImplementation.", "FDR.")

let (++) a b = Path.Combine(a, b)

let sourceDirectory = __SOURCE_DIRECTORY__

let testCases = 
    sourceDirectory ++ "SignatureTestCases.config" 
    |> File.ReadAllLines
    |> Array.map TestCase.Parse

let expectedDirectory = sourceDirectory ++ "expected" 

let getExpectedPath testCase = 
    expectedDirectory ++ (testCase.ToString().Replace("://", "_").Replace("/", "_") + ".expected")

let resolutionFolder = sourceDirectory ++ ".." ++ "FSharp.Data.Tests" ++ "Data"
let assemblyName = "FSharp.Data.Experimental.dll"
let runtimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ assemblyName
let portableRuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ "portable" ++ assemblyName
let silverlightRuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ "sl5" ++ assemblyName

let generateAllExpected() =
    if not <| Directory.Exists expectedDirectory then 
        Directory.CreateDirectory expectedDirectory |> ignore
    for testCase in testCases do
        let output = testCase.Dump resolutionFolder runtimeAssembly true false
        File.WriteAllText(getExpectedPath testCase, output)

let normalizeEndings (str:string) =
  str.Replace("\r\n", "\n").Replace("\r", "\n")

[<Test>]
[<TestCaseSource "testCases">]
let ``Validate signature didn't change `` (testCase:TestCase) = 
    let expected = getExpectedPath testCase |> File.ReadAllText |> normalizeEndings
    let output = testCase.Dump resolutionFolder runtimeAssembly true false |> normalizeEndings 
    output |> should equal expected

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works `` (testCase:TestCase) = 
    let expected = getExpectedPath testCase |> File.ReadAllText 
    testCase.Dump resolutionFolder runtimeAssembly false true |> ignore

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in portable `` (testCase:TestCase) = 
    let expected = getExpectedPath testCase |> File.ReadAllText 
    testCase.Dump resolutionFolder portableRuntimeAssembly false true |> ignore
