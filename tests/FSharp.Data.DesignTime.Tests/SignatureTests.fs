module FSharp.Data.DesignTime.Tests.SignatureTests

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
     // "No data is available for encoding 932. For information on defining a custom encoding, see the documentation for the Encoding.RegisterProvider method."
    |> Array.filter (fun line -> not (line.Contains ("cp932.csv")))
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
let netstandard2RuntimeAssembly = sourceDirectory ++ ".." ++ ".." ++ "src" ++ "FSharp.Data" ++ "bin" ++
                                #if DEBUG
                                    "Debug"
                                #else
                                    "Release"
                                #endif
                                ++ "netstandard2.0" ++ assemblyName

let getRuntimeRefs platform = TypeProviderInstantiation.GetRuntimeAssemblyRefs platform

let normalize (str:string) =
    str.Replace("\r\n", "\n").Replace("\r", "\n").Replace("@\"<RESOLUTION_FOLDER>\"", "\"<RESOLUTION_FOLDER>\"")

[<Test>]
[<TestCaseSource "testCases">]
let ``Validate signature didn't change `` (testCase:TypeProviderInstantiation) =
   let path = testCase.ExpectedPath expectedDirectory
   let expected = path |> File.ReadAllText |> normalize
   let assemblyRefs = getRuntimeRefs NetStandard20
   printfn "assemblyRefs = %A" assemblyRefs
   let outputRaw = testCase.Dump (resolutionFolder, "", netstandard2RuntimeAssembly, assemblyRefs, signatureOnly=false, ignoreOutput=false)
   let output = outputRaw |> normalize
   if output <> expected then
       printfn "Obtained Signature:\n%s" outputRaw
       File.WriteAllText(testCase.ExpectedPath expectedDirectory + ".obtained", outputRaw)
   output |> should equal expected

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in netstandard2.0 `` (testCase:TypeProviderInstantiation) =
    testCase.Dump(resolutionFolder, "", netstandard2RuntimeAssembly, (getRuntimeRefs NetStandard20), signatureOnly=false, ignoreOutput=true) |> ignore
