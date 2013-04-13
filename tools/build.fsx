// --------------------------------------------------------------------------------------
// Builds the documentation from FSX files in the 'samples' directory
// (the documentation is stored in the 'docs' directory)
// --------------------------------------------------------------------------------------

#I "../packages/FSharp.Formatting.1.0.14/lib/net40"
#load "../packages/FSharp.Formatting.1.0.14/literate/literate.fsx"
open System.IO
open FSharp.Literate

let (++) a b = Path.Combine(a, b)
let template = __SOURCE_DIRECTORY__ ++ "template.html"
let sources  = __SOURCE_DIRECTORY__ ++ "../samples"
let output   = __SOURCE_DIRECTORY__ ++ "../docs"

// Root URL for the generated HTML
let root = "http://fsharp.github.com/FSharp.Data"

// When running locally, you can use your path
//let root = @"file://C:\Tomas\Projects\FSharp.Data\docs"


// Copy all sample documents to the "docs" directory
let sourceDocs = sources ++ "docs"
let outputDocs = output ++ "docs"

if Directory.Exists outputDocs then Directory.Delete(outputDocs, true)
Directory.CreateDirectory outputDocs
for fileInfo in DirectoryInfo(sourceDocs).EnumerateFiles() do
    fileInfo.CopyTo(outputDocs ++ fileInfo.Name) |> ignore

// Generate HTML from all FSX files in samples & subdirectories
let build () =
  for sub in [ "."; "experimental"; "library"; "tutorials" ] do
    Literate.ProcessDirectory
      ( sources ++ sub, template, output ++ sub, 
        replacements = [ "root", root ] )

build()