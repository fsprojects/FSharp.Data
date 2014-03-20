#if INTERACTIVE
#I "../../packages/FSharp.Formatting.2.4.1/lib/net40"
#I "../../packages/RazorEngine.3.3.0/lib/net40/"
#r "../../packages/Microsoft.AspNet.Razor.2.0.30506.0/lib/net40/System.Web.Razor.dll"
#r "../../packages/FSharp.Compiler.Service.0.0.32/lib/net40/FSharp.Compiler.Service.dll"
#r "RazorEngine.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.DesignTime.Tests.DocumentationTests
#endif

open FsUnit
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

  let literateDoc = Literate.ParseScriptFile(Path.Combine(sources, file))
  literateDoc.Errors 
  |> Seq.choose (fun (SourceError(startl, endl, kind, msg)) ->
    if msg <> "Multiple references to 'mscorlib.dll' are not permitted" then
      Some <| sprintf "%A %s (%s)" (startl, endl) msg file
    else None)
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
let ``Documentation generated correctly `` file = 
  let errors = processFile file
  if errors <> "" then
    Assert.Fail("Found errors when processing file '" + file + "':\n" + errors)

#endif
