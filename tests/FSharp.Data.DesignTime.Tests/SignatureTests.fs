#if INTERACTIVE
#r "../../packages/NUnit/lib/nunit.framework.dll"
#r "../../bin/FSharp.Data.DesignTime.dll"
#load "../Common/FsUnit.fs"
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

let runningOnMono = Type.GetType("Mono.Runtime") <> null

let referenceAssembliesPath = 
    if runningOnMono
    then "/Library/Frameworks/Mono.framework/Versions/CurrentVersion/lib/mono/"
    else Environment.GetFolderPath Environment.SpecialFolder.ProgramFilesX86 
    ++ "Reference Assemblies" 
    ++ "Microsoft" 

let fsharp30Portable47AssembliesPath1 = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ "3.0" 
    ++ "Runtime" 
    ++ ".NETPortable"

let fsharp30Portable47AssembliesPath2 = 
     referenceAssembliesPath
     ++ "FSharp" 
     ++ ".NETPortable" 
     ++ "2.3.5.0"

let fsharp31Portable47AssembliesPath = 
     referenceAssembliesPath
     ++ "FSharp" 
     ++ ".NETPortable" 
     ++ "2.3.5.1"

let fsharp31Portable7AssembliesPath = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ ".NETCore" 
    ++ "3.3.1.0" 

let fsharp30AssembliesPath1 = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ "3.0" 
    ++ "Runtime" 
    ++ "v4.0"

let fsharp30AssembliesPath2 = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ ".NETFramework" 
    ++ "v4.0"
    ++ "4.3.0.0"

let fsharp31AssembliesPath = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ ".NETFramework" 
    ++ "v4.0"
    ++ "4.3.1.0"

let net40AssembliesPath = 
    referenceAssembliesPath
    ++ "Framework" 
    ++ ".NETFramework" 
    ++ "v4.5" 

let portable47AssembliesPath = 
    referenceAssembliesPath
    ++ "Framework" 
    ++ ".NETPortable" 
    ++ "v4.0" 
    ++ "Profile" 
    ++ "Profile47" 

let portable7AssembliesPath = 
    referenceAssembliesPath
    ++ "Framework" 
    ++ ".NETPortable" 
    ++ "v4.5" 
    ++ "Profile" 
    ++ "Profile7" 

let net40FSharp31Refs = [net40AssembliesPath ++ "mscorlib.dll"; net40AssembliesPath ++ "System.Xml.dll"; net40AssembliesPath ++ "System.Core.dll"; net40AssembliesPath ++ "System.Xml.Linq.dll"; net40AssembliesPath ++ "System.dll"; fsharp31AssembliesPath ++ "FSharp.Core.dll"]
let portable47FSharp31Refs = [portable47AssembliesPath ++ "mscorlib.dll"; fsharp31Portable47AssembliesPath ++ "FSharp.Core.dll"]
let portable7FSharp31Refs = [portable7AssembliesPath ++ "mscorlib.dll"; fsharp31Portable7AssembliesPath ++ "FSharp.Core.dll"]

let generateAllExpected() =
    if not <| Directory.Exists expectedDirectory then 
        Directory.CreateDirectory expectedDirectory |> ignore
    for (sample, testCase) in testCasesTuple do
        try
            testCase.Dump (resolutionFolder, expectedDirectory, runtimeAssembly, net40FSharp31Refs, signatureOnly=false, ignoreOutput=false)
            |> ignore
        with e -> 
            raise(new Exception(sprintf "Failed generating: %s" sample, e))

let normalize (str:string) =
  str.Replace("\r\n", "\n").Replace("\r", "\n").Replace("@\"<RESOLUTION_FOLDER>\"", "\"<RESOLUTION_FOLDER>\"")

[<Test>]
[<TestCaseSource "testCases">]
let ``Validate signature didn't change `` (testCase:TypeProviderInstantiation) = 
    let expected = testCase.ExpectedPath expectedDirectory |> File.ReadAllText |> normalize
    let output = testCase.Dump (resolutionFolder, "", runtimeAssembly, net40FSharp31Refs, signatureOnly=false, ignoreOutput=false) |> normalize
    if output <> expected then
        printfn "Obtained Signature:\n%s" output
    output |> should equal expected

for t in testCases do ``Validate signature didn't change `` t

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in portable profile 47 `` (testCase:TypeProviderInstantiation) = 
    testCase.Dump(resolutionFolder, "", portable47RuntimeAssembly, portable47FSharp31Refs, signatureOnly=false, ignoreOutput=true) |> ignore

[<Test>]
[<TestCaseSource "testCases">]
[<Platform "Net">]
let ``Generating expressions works in portable profile 7 `` (testCase:TypeProviderInstantiation) = 
    testCase.Dump(resolutionFolder, "", portable7RuntimeAssembly, portable7FSharp31Refs, signatureOnly=false, ignoreOutput=true) |> ignore
