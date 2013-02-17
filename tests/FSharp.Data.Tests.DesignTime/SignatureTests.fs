﻿#if EXPERIMENTAL
module FSharp.Data.Tests.Experimental.DesignTime.SignatureTests
#else
module FSharp.Data.Tests.DesignTime.SignatureTests
#endif
#if INTERACTIVE
#r "../../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#r "../../bin/FSharp.Data.DesignTime.dll"
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
#if EXPERIMENTAL
        | Apiary x -> 
            ["Apiary"
             x.ApiName]
#else
        | Csv x -> 
            ["Csv"
             x.Sample
             x.Separator
             x.Culture]
        | Xml x -> 
            ["Xml"
             x.Sample
             x.Global.ToString()
             x.SampleList.ToString()
             x.Culture]
        | Json x -> 
            ["Json"
             x.Sample
             x.SampleList.ToString()
             x.Culture]
        | WorldBank x -> 
            ["WorldBank"
             x.Sources
             x.Asynchronous.ToString()]
        | Freebase x -> 
            ["Freebase"
             x.NumIndividuals.ToString()
             x.UseUnitsOfMeasure.ToString()
             x.Pluralize.ToString()
             x.LocalCache.ToString()
             x.AllowLocalQueryEvaluation.ToString()]
#endif
        |> String.concat ","

    static member Parse (line:string) =
        let args = line.Split [|','|]
        match args.[0] with
#if EXPERIMENTAL
        | "Apiary" ->
            Apiary { ApiName = args.[1] }
#else
        | "Csv" ->
            Csv { Sample = args.[1]
                  Separator = args.[2]
                  Culture = args.[3]
                  InferRows = Int32.MaxValue
                  ResolutionFolder = "" }
        | "Xml" ->
            Xml { Sample = args.[1]
                  Global = args.[2] |> bool.Parse
                  SampleList = args.[3] |> bool.Parse
                  Culture = args.[4]
                  ResolutionFolder = "" }
        | "Json" ->
            Json { Sample = args.[1]
                   SampleList = args.[2] |> bool.Parse
                   Culture = args.[3] 
                   ResolutionFolder = ""}
        | "WorldBank" ->
            WorldBank { Sources = args.[1]
                        Asynchronous = args.[2] |> bool.Parse }
        | "Freebase" ->
            Freebase { Key = args.[1]
                       NumIndividuals = args.[2] |> Int32.Parse
                       UseUnitsOfMeasure = args.[3] |> bool.Parse
                       Pluralize = args.[4] |> bool.Parse
                       SnapshotDate = "now"
                       ServiceUrl = "https://www.googleapis.com/freebase/v1"
                       LocalCache = args.[5] |> bool.Parse
                       AllowLocalQueryEvaluation = args.[6] |> bool.Parse }
#endif
        | _ -> failwithf "Unknown: %s" args.[0]
        |> TestCase

    member x.Dump resolutionFolder runtimeAssembly signatureOnly =        
        let (TestCase x) = x
        let outputFunc = 
            match x with
#if EXPERIMENTAL
#else
            | Freebase _ -> Debug.prettyPrintWithMaxDepth signatureOnly 3
#endif
            | _ -> Debug.prettyPrint signatureOnly
        let output = 
            x.generateType resolutionFolder runtimeAssembly 
            |> outputFunc
        output.Replace("FSharp.Data.RuntimeImplementation.", "FDR.")

let (++) a b = Path.Combine(a, b)

#if EXPERIMENTAL
let sourceDirectory = __SOURCE_DIRECTORY__ ++ ".." ++ "FSharp.Data.Tests.Experimental.DesignTime"
#else
let sourceDirectory = __SOURCE_DIRECTORY__
#endif

let testCases = 
    sourceDirectory ++ "SignatureTestCases.config" 
    |> File.ReadAllLines
    |> Array.map TestCase.Parse

let expectedDirectory = sourceDirectory ++ "expected" 

let getExpectedPath testCase = 
    expectedDirectory ++ (testCase.ToString().Replace("://", "_").Replace("/", "_") + ".expected")

let resolutionFolder = sourceDirectory ++ ".." ++ "FSharp.Data.Tests" ++ "Data"
#if EXPERIMENTAL
let assemblyName = "FSharp.Data.Experimental.dll"
#else
let assemblyName = "FSharp.Data.dll"
#endif
let runtimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ assemblyName
let portableRuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ "portable" ++ assemblyName
let silverlightRuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ "sl5" ++ assemblyName

let generateAllExpected() =
    if not <| Directory.Exists expectedDirectory then 
        Directory.CreateDirectory expectedDirectory |> ignore
    for testCase in testCases do
        let output = testCase.Dump resolutionFolder runtimeAssembly true
        File.WriteAllText(getExpectedPath testCase, output)

let normalizeEndings (str:string) =
  str.Replace("\r\n", "\n").Replace("\r", "\n")

[<Test>]
[<TestCaseSource "testCases">]
let ``Validate signature didn't change `` (testCase:TestCase) = 
    let expected = getExpectedPath testCase |> File.ReadAllText |> normalizeEndings
    let output = testCase.Dump resolutionFolder runtimeAssembly true |> normalizeEndings 
    output |> should equal expected

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works `` (testCase:TestCase) = 
    let expected = getExpectedPath testCase |> File.ReadAllText 
    testCase.Dump resolutionFolder runtimeAssembly false |> ignore

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in portable `` (testCase:TestCase) = 
    let expected = getExpectedPath testCase |> File.ReadAllText 
    testCase.Dump resolutionFolder portableRuntimeAssembly false |> ignore

#if EXPERIMENTAL
#else
[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in silverlight `` (testCase:TestCase) = 
    let expected = getExpectedPath testCase |> File.ReadAllText 
    testCase.Dump resolutionFolder silverlightRuntimeAssembly false |> ignore
#endif