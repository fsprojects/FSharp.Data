#if INTERACTIVE
#I "../../packages/FSharp.Formatting.2.2.10-beta/lib/net40"
#I "../../packages/RazorEngine.3.4.0/lib/net45/"
#r "../../packages/Microsoft.AspNet.Razor.3.0.0/lib/net45/System.Web.Razor.dll"
#r "RazorEngine.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.Documentation
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

let (@@) a b = Path.Combine(a, b)

let sources = __SOURCE_DIRECTORY__ @@ "../../docs/content"

let output = Path.GetTempPath() @@ "FSharp.Data.Docs"

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

  let dir = Path.GetDirectoryName(Path.Combine(output, file))
  if not (Directory.Exists(dir)) then Directory.CreateDirectory(dir) |> ignore

  let literateDoc = Literate.ParseScriptFile( Path.Combine(sources, file), fsharpCompiler = compiler )
  literateDoc.Errors 
  |> Seq.choose (fun (SourceError(startl, endl, kind, msg)) ->
    if msg <> "Multiple references to 'mscorlib.dll' are not permitted" then
      Some <| sprintf "%A %s (%s)" (startl, endl) msg file
    else None)
  |> String.concat "\n"

// ------------------------------------------------------------------------------------
// Core API documentation

let docFiles = 
  seq { for sub in [ "library"; "tutorials"; "experimental"
                     "ja/library"; "ja/tutorials"; "ja/experimental" ] do
          for file in Directory.EnumerateFiles(Path.Combine(sources, sub), "*.fsx") do
            yield sub + "/" + Path.GetFileName(file) }

#if INTERACTIVE
for file in docFiles do 
    printfn "%s" (processFile file)
#else

[<Test>]
[<TestCaseSource "docFiles">]
let ``Documentation generated correctly `` file = 
  processFile file
  |> should equal ""

#endif