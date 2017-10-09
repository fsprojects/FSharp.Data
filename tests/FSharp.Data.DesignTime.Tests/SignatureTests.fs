#if INTERACTIVE
#r "../../packages/NUnit/lib/net45/nunit.framework.dll"
#r "../../bin/FSharp.Data.DesignTime.dll"
#r "../../packages/FsUnit/lib/net45/FsUnit.NUnit.dll"
#else
module FSharp.Data.DesignTime.Tests.SignatureTests
#endif

open System
open System.IO
open System.Reflection
open FSharp.Core.CompilerServices
open FsUnit
open NUnit.Framework
open ProviderImplementation
open ProviderImplementation.ProvidedTypes

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

let getRuntimeRefs = TypeProviderInstantiation.GetRuntimeAssemblyRefs

[<AutoOpen>]
module Utils = 
    let isNull x = match x with null -> true | _ -> false


/// Simulate a real host of TypeProviderConfig
type internal DllInfo(path: string) =
    member x.FileName = path

/// Simulate a real host of TypeProviderConfig
type internal TcImports(bas: TcImports option, dllInfos: DllInfo list) =
    member x.Base = bas
    member x.DllInfos = dllInfos


type internal Testing() =

    /// Simulates a real instance of TypeProviderConfig
    static member MakeSimulatedTypeProviderConfig (resolutionFolder: string, runtimeAssembly: string, runtimeAssemblyRefs: string list) =

        let cfg = TypeProviderConfig(fun _ -> false)
        let (?<-) cfg prop value =
            let ty = cfg.GetType()
            match ty.GetProperty(prop,BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic) with
            | null -> ty.GetField(prop,BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic).SetValue(cfg, value)|> ignore
            | p -> p.GetSetMethod(nonPublic = true).Invoke(cfg, [| box value |]) |> ignore
        cfg?ResolutionFolder <- resolutionFolder
        cfg?RuntimeAssembly <- runtimeAssembly
        cfg?ReferencedAssemblies <- Array.zeroCreate<string> 0

        // Fake an implementation of SystemRuntimeContainsType the shape expected by AssemblyResolver.fs.
        let dllInfos = [yield DllInfo(runtimeAssembly); for r in runtimeAssemblyRefs do yield DllInfo(r)]
        let tcImports = TcImports(Some(TcImports(None,[])),dllInfos)
        let systemRuntimeContainsType = (fun (_s:string) -> if tcImports.DllInfos.Length = 1 then true else true)
        cfg?systemRuntimeContainsType <- systemRuntimeContainsType

        //Diagnostics.Debugger.Launch() |> ignore
        Diagnostics.Debug.Assert(cfg.GetType().GetField("systemRuntimeContainsType",BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance) |> isNull |> not)
        Diagnostics.Debug.Assert(systemRuntimeContainsType.GetType().GetField("tcImports",BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance) |> isNull |> not)
        Diagnostics.Debug.Assert(typeof<TcImports>.GetField("dllInfos",BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance) |> isNull |> not)
        Diagnostics.Debug.Assert(typeof<TcImports>.GetProperty("Base",BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance) |> isNull |> not)
        Diagnostics.Debug.Assert(typeof<DllInfo>.GetProperty("FileName",BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance) |> isNull |> not)

        cfg

[<Test>]
let ``test basic binding context net40``() =
   let refs = getRuntimeRefs Net45
   let config =  ProviderImplementation.ProvidedTypesTesting.Testing.MakeSimulatedTypeProviderConfig (resolutionFolder=__SOURCE_DIRECTORY__, runtimeAssembly="whatever.dll", runtimeAssemblyRefs=refs)
   let ctxt1 = ProvidedTypesContext.Create (config, isForGenerated=false)

   match ctxt1.TryBindAssembly(AssemblyName("mscorlib")) with
   | Choice1Of2 asm -> asm.GetType("System.Object").FullName |> should equal "System.Object"
   | Choice2Of2 err -> raise err

[<Test>]
let ``test basic binding context portable7``() =
   let refs = getRuntimeRefs Portable7
   let config =  ProviderImplementation.ProvidedTypesTesting.Testing.MakeSimulatedTypeProviderConfig (resolutionFolder=__SOURCE_DIRECTORY__, runtimeAssembly="whatever.dll", runtimeAssemblyRefs=refs)
   let ctxt1 = ProvidedTypesContext.Create (config, isForGenerated=false)

   match ctxt1.TryBindAssembly(AssemblyName("System.Runtime")) with
   | Choice1Of2 asm -> asm.GetType("System.Object").FullName |> should equal "System.Object"
   | Choice2Of2 err -> raise err
   match ctxt1.TryBindAssembly(AssemblyName("mscorlib")) with
   | Choice1Of2 asm -> asm.GetType("System.Object").FullName |> should equal "System.Object"
   | Choice2Of2 err -> raise err

[<Test>]
let ``test basic binding context portable259``() =
   let refs = getRuntimeRefs Portable259
   let config =  ProviderImplementation.ProvidedTypesTesting.Testing.MakeSimulatedTypeProviderConfig (resolutionFolder=__SOURCE_DIRECTORY__, runtimeAssembly="whatever.dll", runtimeAssemblyRefs=refs)
   let ctxt1 = ProvidedTypesContext.Create (config, isForGenerated=false)

   match ctxt1.TryBindAssembly(AssemblyName("System.Runtime")) with
   | Choice1Of2 asm -> asm.GetType("System.Object").FullName |> should equal "System.Object"
   | Choice2Of2 err -> raise err
   match ctxt1.TryBindAssembly(AssemblyName("mscorlib")) with
   | Choice1Of2 asm -> asm.GetType("System.Object").FullName |> should equal "System.Object"
   | Choice2Of2 err -> raise err
   match ctxt1.TryBindAssembly(AssemblyName("mscorlib")) with
   | Choice1Of2 asm -> asm.GetType("System.Object").Assembly.GetName().Name |> should equal "System.Runtime"
   | Choice2Of2 err -> raise err


let generateAllExpected() =
    if not <| Directory.Exists expectedDirectory then
        Directory.CreateDirectory expectedDirectory |> ignore
    for (sample, testCase) in testCasesTuple do
        try
            testCase.Dump (resolutionFolder, expectedDirectory, runtimeAssembly, (getRuntimeRefs Net45), signatureOnly=false, ignoreOutput=false)
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
    let outputRaw = testCase.Dump (resolutionFolder, "", runtimeAssembly, (getRuntimeRefs Net45), signatureOnly=false, ignoreOutput=false)
    let output = outputRaw |> normalize
    if output <> expected then
        printfn "Obtained Signature:\n%s" outputRaw
    //System.IO.File.WriteAllText(path, outputRaw.Replace("\r\n", "\n"))
    output |> should equal expected

[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in portable profile 7 `` (testCase:TypeProviderInstantiation) =
    testCase.Dump(resolutionFolder, "", portableRuntimeAssembly 7, (getRuntimeRefs Portable7), signatureOnly=false, ignoreOutput=true) |> ignore


[<Test>]
[<TestCaseSource "testCases">]
let ``Generating expressions works in portable profile 259 `` (testCase:TypeProviderInstantiation) =
    testCase.Dump(resolutionFolder, "", portableRuntimeAssembly 259, (getRuntimeRefs Portable259), signatureOnly=false, ignoreOutput=true) |> ignore
