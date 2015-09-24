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
let portableRuntimeAssembly profile = sourceDirectory ++ ".." ++ ".." ++ "bin" ++ ("portable" + string profile) ++ assemblyName

let runningOnMono = Type.GetType("Mono.Runtime") <> null

let referenceAssembliesPath = 
    if runningOnMono
    then "/Library/Frameworks/Mono.framework/Versions/CurrentVersion/lib/mono/"
    else Environment.GetFolderPath Environment.SpecialFolder.ProgramFilesX86 
    ++ "Reference Assemblies" 
    ++ "Microsoft" 

let fsharp31PortableAssembliesPath profile = 
     match profile with 
     | 47 -> referenceAssembliesPath ++ "FSharp" ++ ".NETPortable" ++ "2.3.5.1" ++ "FSharp.Core.dll"
     | 7 -> referenceAssembliesPath ++ "FSharp" ++ ".NETCore" ++ "3.3.1.0" ++ "FSharp.Core.dll"
     | 259 -> referenceAssembliesPath ++ "FSharp" ++ ".NETCore" ++ "3.259.3.1" ++ "FSharp.Core.dll"
     | _ -> failwith "unimplemented portable profile"

let fsharp31AssembliesPath = referenceAssembliesPath ++ "FSharp" ++ ".NETFramework" ++ "v4.0" ++ "4.3.1.0"

let net40AssembliesPath = referenceAssembliesPath ++ "Framework" ++ ".NETFramework" ++ "v4.5" 

let portableAssembliesPath profile = 
    match profile with 
    | 47 -> referenceAssembliesPath ++ "Framework" ++ ".NETPortable" ++ "v4.0" ++ "Profile" ++ "Profile47" 
    | 7 -> referenceAssembliesPath ++ "Framework" ++ ".NETPortable" ++ "v4.5" ++ "Profile" ++ "Profile7" 
    | 259 -> referenceAssembliesPath ++ "Framework" ++ ".NETPortable" ++ "v4.5" ++ "Profile" ++ "Profile259" 
    | _ -> failwith "unimplemented portable profile"

let net40FSharp31Refs = [net40AssembliesPath ++ "mscorlib.dll"; net40AssembliesPath ++ "System.Xml.dll"; net40AssembliesPath ++ "System.Core.dll"; net40AssembliesPath ++ "System.Xml.Linq.dll"; net40AssembliesPath ++ "System.dll"; fsharp31AssembliesPath ++ "FSharp.Core.dll"]
let portable47FSharp31Refs = [portableAssembliesPath 47 ++ "mscorlib.dll"; portableAssembliesPath 47 ++ "System.Xml.Linq.dll"; fsharp31PortableAssembliesPath 47]

let portableCoreFSharp31Refs profile = 
    [ for ass in [ "System.Runtime"; "mscorlib"; "System.Collections"; "System.Core"; "System"; "System.Globalization"; "System.IO"; "System.Linq"; "System.Linq.Expressions"; 
                   "System.Linq.Queryable"; "System.Net"; "System.Net.NetworkInformation"; "System.Net.Primitives"; "System.Net.Requests"; "System.ObjectModel"; "System.Reflection"; 
                   "System.Reflection.Extensions"; "System.Reflection.Primitives"; "System.Resources.ResourceManager"; "System.Runtime.Extensions"; 
                   "System.Runtime.InteropServices.WindowsRuntime"; "System.Runtime.Serialization"; "System.Threading"; "System.Threading.Tasks"; "System.Xml"; "System.Xml.Linq"; "System.Xml.XDocument";
                   "System.Runtime.Serialization.Json"; "System.Runtime.Serialization.Primitives"; "System.Windows" ] do 
         yield portableAssembliesPath profile ++ ass + ".dll"
      yield fsharp31PortableAssembliesPath profile ]


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
    let path = testCase.ExpectedPath expectedDirectory 
    let expected = path |> File.ReadAllText |> normalize
    let outputRaw = testCase.Dump (resolutionFolder, "", runtimeAssembly, net40FSharp31Refs, signatureOnly=false, ignoreOutput=false) 
    let output = outputRaw |> normalize
    if output <> expected then
        printfn "Obtained Signature:\n%s" outputRaw
    System.IO.File.WriteAllText(path, outputRaw.Replace("\r\n", "\n"))
    output |> should equal expected

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in portable profile 47 `` (testCase:TypeProviderInstantiation) = 
    testCase.Dump(resolutionFolder, "", portableRuntimeAssembly 47, portable47FSharp31Refs, signatureOnly=false, ignoreOutput=true) |> ignore

[<Test>]
[<TestCaseSource "testCases">]
[<Platform "Net">]
let ``Generating expressions works in portable profile 7 `` (testCase:TypeProviderInstantiation) = 
    testCase.Dump(resolutionFolder, "", portableRuntimeAssembly 7, portableCoreFSharp31Refs 7, signatureOnly=false, ignoreOutput=true) |> ignore


[<Test>]
[<TestCaseSource "testCases">]
[<Platform "Net">]
let ``Generating expressions works in portable profile 259 `` (testCase:TypeProviderInstantiation) = 
    testCase.Dump(resolutionFolder, "", portableRuntimeAssembly 259, portableCoreFSharp31Refs 259, signatureOnly=false, ignoreOutput=true) |> ignore
