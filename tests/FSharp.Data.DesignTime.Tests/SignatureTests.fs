module FSharp.Data.DesignTime.Tests.SignatureTests

open System
open System.IO
open FsUnit
open NUnit.Framework
open ProviderImplementation

let (++) a b = Path.Combine(a, b)

let sourceDirectory = __SOURCE_DIRECTORY__

let testCases =
    sourceDirectory ++ "SignatureTestCases.config"
    |> File.ReadAllLines
     // "No data is available for encoding 932. For information on defining a custom encoding, see the documentation for the Encoding.RegisterProvider method."
    |> Array.filter (fun line -> not (line.Contains ("cp932.csv")))

let expectedDirectory = sourceDirectory ++ "expected"

let resolutionFolder = sourceDirectory ++ ".." ++ "FSharp.Data.Tests" ++ "Data"

#if DEBUG
let build = "Debug"
#else
let build = "Release"
#endif

let netstandard2RuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "src" ++ "FSharp.Data" ++ "bin" ++ build ++ "netstandard2.0" ++ "FSharp.Data.dll"

let normalize (str:string) =
    str.Replace("\r\n", "\n").Replace("\r", "\n").Replace("@\"<RESOLUTION_FOLDER>\"", "\"<RESOLUTION_FOLDER>\"")

[<Test>]
[<TestCaseSource "testCases">]
let ``Validate signature didn't change `` (testCaseSpec: string) =
    let _, testCase = TypeProviderInstantiation.Parse testCaseSpec
    let path = testCase.ExpectedPath expectedDirectory
    let expected = path |> File.ReadAllText |> normalize
    let assemblyRefs = TypeProviderInstantiation.GetRuntimeAssemblyRefs()
    let outputRaw = testCase.Dump (resolutionFolder, "", netstandard2RuntimeAssembly, assemblyRefs, signatureOnly=false, ignoreOutput=false)
    let output = outputRaw |> normalize
    if output <> expected then
         printfn "Obtained Signature:\n%s" outputRaw
         File.WriteAllText(testCase.ExpectedPath expectedDirectory + ".obtained", outputRaw)
    output |> should equal expected

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in netstandard2.0 `` (testCaseSpec: string) =
    let _, testCase = TypeProviderInstantiation.Parse testCaseSpec
    let assemblyRefs = TypeProviderInstantiation.GetRuntimeAssemblyRefs()
    testCase.Dump(resolutionFolder, "", netstandard2RuntimeAssembly, assemblyRefs, signatureOnly=false, ignoreOutput=true) |> ignore
