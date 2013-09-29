// --------------------------------------------------------------------------------------
// Test that the documentation is generated correctly withtout F# errors 
// --------------------------------------------------------------------------------------
module FSharp.Data.Tests.Documentation

#if INTERACTIVE
#I "../../packages/FSharp.Formatting.1.0.14/lib/net40"
#load "../../packages/FSharp.Formatting.1.0.14/literate/literate.fsx"
#r "../../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

open FsUnit
open NUnit.Framework
open System
open System.IO
open System.Net
open System.Reflection
open FSharp.Literate
open FSharp.CodeFormat

//alow tests that access the network to work when you're behind a proxy
WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials

// Initialization of the test - lookup the documentation files,
// create temp folder for the output and load the F# compiler DLL
let template = Path.Combine(__SOURCE_DIRECTORY__, "../../tools/template.html")
let sources = Path.Combine(__SOURCE_DIRECTORY__, "../../samples")
let output = Path.Combine(Path.GetTempPath(), "FSharp.Data.Docs")
if Directory.Exists(output) then Directory.Delete(output, true)
Directory.CreateDirectory(output) |> ignore

// Lookup compiler DLL
let locations = 
  [ "%ProgramFiles%\\Microsoft SDKs\\F#\\3.0\\Framework\\v4.0\\FSharp.Compiler.dll"
    "%ProgramFiles(x86)%\\Microsoft SDKs\\F#\\3.0\\Framework\\v4.0\\FSharp.Compiler.dll"
    "/Library/Frameworks/Mono.framework/Libraries/mono/Microsoft F#/v4.0/FSharp.Compiler.dll" ]

let compiler = 
  locations |> Seq.pick (fun location ->
    try 
      let location = Environment.ExpandEnvironmentVariables(location)
      if not (File.Exists(location)) then None else
        Some(Assembly.LoadFile(Environment.ExpandEnvironmentVariables(location)))
    with _ -> None)

/// Process a specified file in the documentation folder and return 
/// the total number of unexpected errors found (print them to the output too)
let processFile file =
  printfn "Processing '%s'" file
  let errorCount = ref 0

  let errorHandler(file, SourceError(startl, endl, kind, msg)) = 
    if msg <> "Multiple references to 'mscorlib.dll' are not permitted" then
      printfn "%A %s (%s)" (startl, endl) msg file
      incr errorCount

  let dir = Path.GetDirectoryName(Path.Combine(output, file))
  if not (Directory.Exists(dir)) then Directory.CreateDirectory(dir) |> ignore

  Literate.ProcessScriptFile
    ( Path.Combine(sources, file), template, Path.Combine(output, file), 
      errorHandler = errorHandler,
      fsharpCompiler = compiler )
  errorCount.Value

// ------------------------------------------------------------------------------------
// Core API documentation

let docFiles = 
  seq { for sub in [ "library"; "tutorials"; "experimental" ] do
          for file in Directory.EnumerateFiles(Path.Combine(sources, sub), "*.fsx") do
            yield sub + "/" + Path.GetFileName(file) }

[<Test>]
[<TestCaseSource "docFiles">]
let ``Documentation generated correctly `` file = 
  processFile file
  |> should equal 0
