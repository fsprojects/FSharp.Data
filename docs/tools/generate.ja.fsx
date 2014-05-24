﻿// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// --------------------------------------------------------------------------------------

// Web site location for the generated documentation
let website = "/FSharp.Data/ja"

// Specify more information about your project
let info =
  [ "project-name", "F# Data"
    "project-author", "Tomas Petricek; Gustavo Guerra"
    "project-summary", "The F# Data library implements type providers for working with structured file formats (CSV, JSON and XML) and for accessing the WorldBank and Freebase services. It also includes helpers for other data-related tasks."
    "project-github", "http://github.com/fsharp/FSharp.Data"
    "project-nuget", "https://nuget.org/packages/FSharp.Data" ]

// --------------------------------------------------------------------------------------
// For typical project, no changes are needed below
// --------------------------------------------------------------------------------------

#I "../../packages/FSharp.Charting.0.90.6/lib/net40"
#I "../../packages/FSharp.Compiler.Service.0.0.44/lib/net40"
#I "../../packages/FSharp.Formatting.2.4.8/lib/net40"
#I "../../packages/RazorEngine.3.3.0/lib/net40/"
#r "../../packages/Microsoft.AspNet.Razor.2.0.30506.0/lib/net40/System.Web.Razor.dll"
#r "../../packages/FAKE/tools/FakeLib.dll"
#r "Fsharp.Charting.dll"
#r "System.Windows.Forms.DataVisualization.dll"
#r "RazorEngine.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.Markdown.dll"
#r "FSharp.MetadataFormat.dll"
open System.IO
open Fake
open Fake.FileHelper
open FSharp.Charting
open System.Drawing
open System.Drawing.Imaging
open System.Windows.Forms
open FSharp.Literate
open FSharp.Markdown
open FSharp.MetadataFormat

// When called from 'build.fsx', use the public project URL as <root>
// otherwise, use the current 'output' directory.
#if RELEASE
let root = website
#else
let root = "file://" + (__SOURCE_DIRECTORY__ @@ "../output/ja")
#endif

// Paths with template/source/output locations
let bin        = __SOURCE_DIRECTORY__ @@ "../../bin"
let content    = __SOURCE_DIRECTORY__ @@ "../content/ja"
let output     = __SOURCE_DIRECTORY__ @@ "../output"
let outputJa   = __SOURCE_DIRECTORY__ @@ "../output/ja"
let files      = __SOURCE_DIRECTORY__ @@ "../files"
let data       = __SOURCE_DIRECTORY__ @@ "../content/data"
let templates  = __SOURCE_DIRECTORY__ @@ "templates/ja"
let formatting = __SOURCE_DIRECTORY__ @@ "../../packages/FSharp.Formatting.2.4.8/"
let docTemplate = formatting @@ "templates/docpage.cshtml"

// Where to look for *.cshtml templates (in this order)
let layoutRoots =
  [ templates
    formatting @@ "templates" 
    formatting @@ "templates/reference" ]

// Copy static files and CSS + JS from F# Formatting
let copyFiles () =
  ensureDirectory (output @@ "data")
  CopyRecursive data (output @@ "data") true |> Log "Copying data files: "
  CopyRecursive files output true |> Log "Copying file: "
  ensureDirectory (output @@ "content")
  CopyRecursive (formatting @@ "styles") (output @@ "content") true 
    |> Log "Copying styles and scripts: "

// FSI evaluator will put images into 'output/images' and 
// reference them as '/images/image1.png' in the HTML
let createFsiEvaluator root output =

  // Counter for saving files
  let imageCounter = 
    let count = ref 0
    (fun () -> incr count; !count)

  let transformation (value:obj, typ:System.Type) =
    match value with 
    | :? ChartTypes.GenericChart as ch ->
        // Pretty print F# Chart - save the chart to the "images" directory 
        // and return a DirectImage reference to the appropriate location
        let id = imageCounter().ToString()
        let file = "chart" + id + ".png"
        ensureDirectory (outputJa @@ "images")

        // We need to reate host control, but it does not have to be visible
        ( use ctl = new ChartTypes.ChartControl(ch, Dock = DockStyle.Fill, Width=800, Height=300)
          ch.CopyAsBitmap().Save(outputJa @@ "images" @@ file, ImageFormat.Png) )
        Some [ Paragraph [DirectImage ("Chart", (root + "/images/" + file, None))]  ]

    | _ -> None 
    
  // Create FSI evaluator, register transformations & return
  let fsiEvaluator = FsiEvaluator()
  fsiEvaluator.RegisterTransformation(transformation)
  fsiEvaluator

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  let subdirs = Directory.EnumerateDirectories(content, "*", SearchOption.AllDirectories)
                |> Seq.filter (fun x -> x.Contains "ja")
  let fsiEvaluator = createFsiEvaluator root output
  for dir in Seq.append [content] subdirs do
    let sub = if dir.Length > content.Length then dir.Substring(content.Length + 1) else "."
    Literate.ProcessDirectory
      ( dir, docTemplate, outputJa @@ sub, replacements = ("root", root)::info,
        layoutRoots = layoutRoots, fsiEvaluator = fsiEvaluator )

// Generate
copyFiles()
buildDocumentation()