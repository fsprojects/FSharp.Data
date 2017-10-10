﻿#if INTERACTIVE
#I "../../packages/FSharp.Formatting/lib/net40"
#I "../../packages/FSharpVSPowerTools.Core/lib/net45"
#r "System.Web.Razor.dll"
#r "RazorEngine.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"
#r "../../packages/FSharp.Compiler.Service/lib/net40/FSharp.Compiler.Service.dll"
#r "../../packages/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/FsUnit/lib/net45/FsUnit.NUnit.dll"
#else
module FSharp.Data.DesignTime.Tests.DocumentationTests
#endif

open NUnit.Framework
open System.IO
open FSharp.Literate
open FSharp.CodeFormat

// Initialization of the test - lookup the documentation files,
// create temp folder for the output and load the F# compiler DLL

let (@@) a b = Path.Combine(a, b)

let sources = __SOURCE_DIRECTORY__ @@ "../../docs/content"

let output = Path.GetTempPath() @@ "FSharp.Data.Docs"

if Directory.Exists(output) then Directory.Delete(output, true)
Directory.CreateDirectory(output) |> ignore

/// Process a specified file in the documentation folder and return 
/// the total number of unexpected errors found (print them to the output too)
let processFile file =
  printfn "Processing '%s'" file

  let dir = Path.GetDirectoryName(Path.Combine(output, file))
  if not (Directory.Exists(dir)) then Directory.CreateDirectory(dir) |> ignore

  let evaluationErrors = ResizeArray()
#if INTERACTIVE
  let fsiEvaluator = FsiEvaluator()
  fsiEvaluator.EvaluationFailed |> Event.add evaluationErrors.Add
  let literateDoc = Literate.ParseScriptFile(Path.Combine(sources, file), fsiEvaluator = fsiEvaluator)
#else
  let literateDoc = Literate.ParseScriptFile(Path.Combine(sources, file))
#endif
  Seq.append
    (literateDoc.Errors 
     |> Seq.choose (fun (SourceError(startl, endl, kind, msg)) ->
       if msg <> "Multiple references to 'mscorlib.dll' are not permitted" &&
          not (msg.Contains("Possible incorrect indentation: this token is offside of context started at position")) then
         Some <| sprintf "%A %s (%s)" (startl, endl) msg file
       else None))
    (evaluationErrors |> Seq.map (fun x -> x.ToString()))
  |> String.concat "\n"

// ------------------------------------------------------------------------------------
// Core API documentation

let docFiles = 
  seq { for sub in [ "library"; "tutorials"; "ja/library"; "ja/tutorials"; ] do
          for file in Directory.EnumerateFiles(Path.Combine(sources, sub), "*.fsx") do
            yield sub + "/" + Path.GetFileName(file) }

#if INTERACTIVE
for file in docFiles do 
    printfn "%s" (processFile file)
#else

[<Test>]
[<TestCaseSource "docFiles">]
[<Ignore "Temporarily disabled">]
let ``Documentation generated correctly `` file = 
  let errors = processFile file
  if errors <> "" then
    Assert.Fail("Found errors when processing file '" + file + "':\n" + errors)

#endif
