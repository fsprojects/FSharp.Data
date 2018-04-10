#if INTERACTIVE
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../bin/typeproviders/fsharp41/net45/FSharp.Data.DesignTime.dll"
#r "../../packages/test/FsUnit/lib/net46/FsUnit.NUnit.dll"
#else
module FSharp.Data.DesignTime.Tests.SignatureTests
#endif

open System
open System.IO
open FsUnit
open NUnit.Framework
open ProviderImplementation

let (++) a b = Path.Combine(a, b)

let sourceDirectory = __SOURCE_DIRECTORY__

let testCasesTuple =
    sourceDirectory ++ "SignatureTestCases.config"
    |> File.ReadAllLines
#if NETCOREAPP2_0 // "No data is available for encoding 932. For information on defining a custom encoding, see the documentation for the Encoding.RegisterProvider method."
    |> Array.filter (fun line -> not (line.Contains ("cp932.csv")))
#endif
    |> Array.map TypeProviderInstantiation.Parse

let testCases =
    testCasesTuple
    // These WorldBank tests nearly always need updating.  COmment out this line if you want to go through the process of
    // updating them.
    |> Array.filter (snd >> function | WorldBank _ -> false | _ -> true)
    |> Array.map snd

let expectedDirectory = sourceDirectory ++ "expected"

let resolutionFolder = sourceDirectory ++ ".." ++ "FSharp.Data.Tests" ++ "Data"
let assemblyName = "FSharp.Data.dll"
let net45RuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ "lib" ++ "net45" ++ assemblyName
let netstandard2RuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ "lib" ++ "netstandard2.0" ++ assemblyName

let getRuntimeRefs platform = TypeProviderInstantiation.GetRuntimeAssemblyRefs platform

let generateAllExpected() =
    if not <| Directory.Exists expectedDirectory then
        Directory.CreateDirectory expectedDirectory |> ignore
    for (sample, testCase) in testCasesTuple do
        try
            let assemblyRefs = getRuntimeRefs Net45
            testCase.Dump (resolutionFolder, expectedDirectory, net45RuntimeAssembly, assemblyRefs, signatureOnly=false, ignoreOutput=false)
            |> ignore
        with e ->
            raise(new Exception(sprintf "Failed generating: %s" sample, e))

let normalize (str:string) =
  str.Replace("\r\n", "\n").Replace("\r", "\n").Replace("@\"<RESOLUTION_FOLDER>\"", "\"<RESOLUTION_FOLDER>\"")

[<Test>]
[<TestCaseSource "testCases">]
let ``Validate signature didn't change `` (testCase:TypeProviderInstantiation) =
    let path = testCase.ExpectedPath expectedDirectory
    let expected = path |> File.ReadAllText |> normalize
    let assemblyRefs = getRuntimeRefs Net45
    printfn "assemblyRefs = %A" assemblyRefs
    let outputRaw = testCase.Dump (resolutionFolder, "", net45RuntimeAssembly, assemblyRefs, signatureOnly=false, ignoreOutput=false)
    let output = outputRaw |> normalize
    if output <> expected then
        printfn "Obtained Signature:\n%s" outputRaw
        File.WriteAllText(testCase.ExpectedPath expectedDirectory + ".obtained", outputRaw)
    output |> should equal expected

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in netstandard2.0 `` (testCase:TypeProviderInstantiation) =
    testCase.Dump(resolutionFolder, "", netstandard2RuntimeAssembly, (getRuntimeRefs NetStandard20), signatureOnly=false, ignoreOutput=true) |> ignore

