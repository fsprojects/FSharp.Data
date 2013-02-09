// --------------------------------------------------------------------------------------
// Test that the documentation is generated correctly withtout F# errors 
// --------------------------------------------------------------------------------------
namespace FSharp.Data.Tests.Documentation

open FsUnit
open NUnit.Framework
open System
open System.IO
open System.Reflection
open FSharp.Literate
open FSharp.CodeFormat

module DocumentationTests = 

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
      "%ProgramFiles(x86)%\\Microsoft SDKs\\F#\\3.0\\Framework\\v4.0\\FSharp.Compiler.dll" ]
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

    Literate.ProcessScriptFile
      ( Path.Combine(sources, file), template, Path.Combine(output, file), 
        errorHandler = errorHandler,
        fsharpCompiler = compiler )
    errorCount.Value

  // ------------------------------------------------------------------------------------
  // Core API documentation

  [<Test>]
  let ``Documentation for CsvProvider.fsx generated correctly``() = 
    processFile "CsvProvider.fsx"
    |> should equal 0

  [<Test>]
  let ``Documentation for FSharpData.fsx generated correctly``() = 
    processFile "FSharpData.fsx"
    |> should equal 0

  [<Test>]
  let ``Documentation for Http.fsx generated correctly``() = 
    processFile "Http.fsx"
    |> should equal 0

  [<Test>]
  let ``Documentation for JsonProvider.fsx generated correctly``() = 
    processFile "JsonProvider.fsx"
    |> should equal 0

  [<Test>]
  let ``Documentation for WorldBank.fsx generated correctly``() = 
    processFile "WorldBank.fsx"
    |> should equal 0

  [<Test>]
  let ``Documentation for XmlProvider.fsx generated correctly``() = 
    processFile "XmlProvider.fsx"
    |> should equal 0

  [<Test>]
  let ``Documentation for JsonValue.fsx generated correctly``() = 
    processFile "JsonValue.fsx"
    |> should equal 0

  // ------------------------------------------------------------------------------------
  // Additional documentation

  [<Test>]
  let ``Documentation for JsonToXml.fsx generated correctly``() = 
    processFile "JsonToXml.fsx"
    |> should equal 0
