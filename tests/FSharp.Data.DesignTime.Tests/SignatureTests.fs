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
open ProviderImplementation.AssemblyReader

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

[<Test>]
let ``test basic binding context net40``() = 
   let ctxt1 = ProviderImplementation.TypeProviderBindingContext.TypeProviderBindingContext (TypeProviderInstantiation.GetRuntimeAssemblyRefs Net40)

   ctxt1.SystemRuntimeScopeRef |> ignore
   match ctxt1.TryBindAssembly("mscorlib") with 
   | Choice1Of2 asm -> asm.BindType(USome "System", "Object").FullName |> should equal "System.Object"
   | Choice2Of2 err -> raise err

[<Test>]
let ``test basic binding context portable7``() = 
   let ctxt1 = ProviderImplementation.TypeProviderBindingContext.TypeProviderBindingContext (TypeProviderInstantiation.GetRuntimeAssemblyRefs Portable7)

   ctxt1.SystemRuntimeScopeRef |> ignore
   match ctxt1.TryBindAssembly("System.Runtime") with 
   | Choice1Of2 asm -> asm.BindType(USome "System", "Object").FullName |> should equal "System.Object"
   | Choice2Of2 err -> raise err
   match ctxt1.TryBindAssembly("mscorlib") with 
   | Choice1Of2 asm -> asm.BindType(USome "System", "Object").FullName |> should equal "System.Object"
   | Choice2Of2 err -> raise err

[<Test>]
let ``test basic binding context portable47``() = 
   let ctxt1 = ProviderImplementation.TypeProviderBindingContext.TypeProviderBindingContext (TypeProviderInstantiation.GetRuntimeAssemblyRefs Portable47)

   ctxt1.SystemRuntimeScopeRef |> ignore
   match ctxt1.TryBindAssembly("mscorlib") with 
   | Choice1Of2 asm -> asm.BindType(USome "System", "Object").FullName |> should equal "System.Object"
   | Choice2Of2 err -> raise err

[<Test>]
let ``test basic binding context portable259``() = 
   let ctxt1 = ProviderImplementation.TypeProviderBindingContext.TypeProviderBindingContext (TypeProviderInstantiation.GetRuntimeAssemblyRefs Portable259)

   ctxt1.SystemRuntimeScopeRef |> ignore
   match ctxt1.TryBindAssembly("System.Runtime") with 
   | Choice1Of2 asm -> asm.BindType(USome "System", "Object").FullName |> should equal "System.Object"
   | Choice2Of2 err -> raise err
   match ctxt1.TryBindAssembly("mscorlib") with 
   | Choice1Of2 asm -> asm.BindType(USome "System", "Object").FullName |> should equal "System.Object"
   | Choice2Of2 err -> raise err
   match ctxt1.TryBindAssembly("mscorlib") with 
   | Choice1Of2 asm -> asm.BindType(USome "System", "Object").Assembly.GetName().Name |> should equal "System.Runtime"
   | Choice2Of2 err -> raise err


let generateAllExpected() =
    if not <| Directory.Exists expectedDirectory then 
        Directory.CreateDirectory expectedDirectory |> ignore
    for (sample, testCase) in testCasesTuple do
        try
            testCase.Dump (resolutionFolder, expectedDirectory, runtimeAssembly, (TypeProviderInstantiation.GetRuntimeAssemblyRefs Net40), signatureOnly=false, ignoreOutput=false)
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
    let outputRaw = testCase.Dump (resolutionFolder, "", runtimeAssembly, (TypeProviderInstantiation.GetRuntimeAssemblyRefs Net40), signatureOnly=false, ignoreOutput=false) 
    let output = outputRaw |> normalize
    if output <> expected then
        printfn "Obtained Signature:\n%s" outputRaw
    //System.IO.File.WriteAllText(path, outputRaw.Replace("\r\n", "\n"))
    output |> should equal expected

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in portable profile 47 `` (testCase:TypeProviderInstantiation) = 
    testCase.Dump(resolutionFolder, "", portableRuntimeAssembly 47, (TypeProviderInstantiation.GetRuntimeAssemblyRefs Portable47), signatureOnly=false, ignoreOutput=true) |> ignore

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in portable profile 7 `` (testCase:TypeProviderInstantiation) = 
    testCase.Dump(resolutionFolder, "", portableRuntimeAssembly 7, (TypeProviderInstantiation.GetRuntimeAssemblyRefs Portable7), signatureOnly=false, ignoreOutput=true) |> ignore


[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in portable profile 259 `` (testCase:TypeProviderInstantiation) = 
    testCase.Dump(resolutionFolder, "", portableRuntimeAssembly 259, (TypeProviderInstantiation.GetRuntimeAssemblyRefs Portable259), signatureOnly=false, ignoreOutput=true) |> ignore
