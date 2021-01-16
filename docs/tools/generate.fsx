// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// --------------------------------------------------------------------------------------

// Binaries that have XML documentation (in a corresponding generated XML file)
let referenceBinaries = [ "FSharp.Data.dll" ]
// Web site location for the generated documentation
let repo = "https://github.com/fsprojects/FSharp.Data/tree/master/"
let website = "/FSharp.Data"

// Specify more information about your project
let info =
  [ "project-name", "F# Data"
    "project-author", "Tomas Petricek; Gustavo Guerra; Colin Bull"
    "project-summary", "The F# Data library implements type providers for working with structured file formats (CSV, HTML, JSON and XML) and for accessing the WorldBank data. It also includes helpers for parsing CSV, HTML and JSON files, and for sending HTTP requests."
    "project-github", "https://github.com/fsprojects/FSharp.Data"
    "project-nuget", "https://nuget.org/packages/FSharp.Data" ]

// --------------------------------------------------------------------------------------
// For typical project, no changes are needed below
// --------------------------------------------------------------------------------------

#I "../../packages/test/FSharp.Charting/lib/net45"
#r "Fsharp.Charting.dll"
#r "System.Windows.Forms.DataVisualization"
#r "../../packages/FAKE/tools/FakeLib.dll"
#load "../../packages/test/FSharp.Formatting/FSharp.Formatting.fsx"

open System.IO
open Fake
open Fake.IO
open Fake.IO.FileSystemOperators
open FSharp.Charting
open System.Drawing.Imaging
open System.Windows.Forms
open FSharp.Literate
open FSharp.Markdown
open FSharp.Formatting.Razor

// When called from 'build.fsx', use the public project URL as <root>
// otherwise, use the current 'output' directory.
#if RELEASE
let root = website
#else
let root = "file://" + (__SOURCE_DIRECTORY__ @@ "../output")
#endif

// Paths with template/source/output locations
let bin         = __SOURCE_DIRECTORY__ @@ "../../bin/lib/net45"
let content     = __SOURCE_DIRECTORY__ @@ "../content"
let output      = __SOURCE_DIRECTORY__ @@ "../output"
let files       = __SOURCE_DIRECTORY__ @@ "../files"
let data        = __SOURCE_DIRECTORY__ @@ "../content/data"
let templatesEn = __SOURCE_DIRECTORY__ @@ "templates"
let templatesJa = __SOURCE_DIRECTORY__ @@ "templates/ja"
let formatting  = __SOURCE_DIRECTORY__ @@ "../../packages/test/FSharp.Formatting/"
let docTemplate = formatting @@ "templates/docpage.cshtml"

// Where to look for *.cshtml templates (in this order)
let layoutRootsEn =
  [ templatesEn
    formatting @@ "templates" 
    formatting @@ "templates/reference" ]

let layoutRootsJa =
  [ templatesJa
    formatting @@ "templates" 
    formatting @@ "templates/reference" ]

// Copy static files and CSS + JS from F# Formatting
let copyFiles () =
  Directory.ensure (output @@ "data")
  Shell.copyRecursive data (output @@ "data") true |> Log "Copying data files: "
  Shell.copyRecursive files output true |> Log "Copying files: "
  Directory.ensure (output @@ "content")
  Shell.copyRecursive (formatting @@ "styles") (output @@ "content") true 
    |> Log "Copying styles and scripts: "

// Build API reference from XML comments
let buildReference () =
  Shell.cleanDir (output @@ "reference")
  RazorMetadataFormat.Generate
    ( referenceBinaries |> List.map ((@@) bin),
      output @@ "reference",
      layoutRoots = layoutRootsEn, 
      parameters = ("root", root)::info,
      sourceRepo = repo,
      sourceFolder = __SOURCE_DIRECTORY__ @@ ".." @@ "../")

let createFsiEvaluator root output =

  // Counter for saving files
  let imageCounter = 
    let count = ref 0
    (fun () -> incr count; !count)

  let transformation (value:obj, _:System.Type) =
    match value with 
    | :? ChartTypes.GenericChart as ch ->
        // Pretty print F# Chart - save the chart to the "images" directory 
        // and return a DirectImage reference to the appropriate location
        let id = imageCounter().ToString()
        let file = "chart" + id + ".png"
        Directory.ensure (output @@ "images")
        // We need to reate host control, but it does not have to be visible
        ( use ctl = new ChartTypes.ChartControl(ch, Dock = DockStyle.Fill, Width=800, Height=300)
          ch.CopyAsBitmap().Save(output @@ "images" @@ file, ImageFormat.Png) )
        Some [ Paragraph([DirectImage ("Chart", (root + "/images/" + file), None, None)], None) ]

    | _ -> None 
    
  // Create FSI evaluator, register transformations & return
  let fsiEvaluator = FsiEvaluator(fsiObj = FsiEvaluatorConfig.CreateNoOpFsiObject())
  fsiEvaluator.RegisterTransformation(transformation)
  fsiEvaluator

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  let subdirs = Directory.EnumerateDirectories(content, "*", SearchOption.AllDirectories)
  
  // FSI evaluator will put images into 'output/images' and 
  // reference them as 'root/images/image1.png' in the HTML
  let fsiEvaluator = createFsiEvaluator root output

  for dir in Seq.append [content] subdirs do
    let sub = if dir.Length > content.Length then dir.Substring(content.Length + 1) else "."
    let layoutRoots = if dir.Contains "ja" then layoutRootsJa else layoutRootsEn
    RazorLiterate.ProcessDirectory
      ( dir, docTemplate, output @@ sub, replacements = ("root", root)::info,
        layoutRoots = layoutRoots, fsiEvaluator = fsiEvaluator, processRecursive = false )

// Generate
copyFiles()

let refWatch = System.Diagnostics.Stopwatch()
refWatch.Start()
buildReference()
refWatch.Stop()

let libWatch = System.Diagnostics.Stopwatch()
libWatch.Start()
buildDocumentation()
libWatch.Stop()

printfn "Reference Documentation took %O to generate" refWatch.Elapsed
printfn "Library Documentation took %O to generate" libWatch.Elapsed
