// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#I @"tools/FAKE/tools"
#r @"tools/FAKE/tools/FakeLib.dll"

open System
open System.IO
open Fake 
open Fake.AssemblyInfoFile
open Fake.Git

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let files includes = 
  { BaseDirectories = [__SOURCE_DIRECTORY__]
    Includes = includes
    Excludes = [] } |> Scan

// Information about the project to be used at NuGet and in AssemblyInfo files
let project = "FSharp.Data"
let authors = ["Tomas Petricek, Gustavo Guerra"]
let summary = "Library of F# type providers and data access tools"
let description = """
  The F# Data library (FSharp.Data.dll) implements everything you need to access data
  in your F# applications and scripts. It implements F# type providers for working with
  structured file formats (CSV, JSON and XML) and for accessing the WorldBank and Freebase
  data. It also includes helpers for parsing JSON and CSV files and for sending HTTP requests."""
let tags = "F# fsharp data typeprovider WorldBank Freebase CSV XML JSON HTTP"

// Information for the project containing experimental providers
let projectExperimental = "FSharp.Data.Experimental"
let summaryExperimental = summary + " (experimental extensions)"
let tagsExperimental = tags + " Apiary"
let descriptionExperimental = description + """"
  This package (FSharp.Data.Experimental.dll) adds additional type providers that are work
  in progress and do not match high quality standards yet. Currently, it includes a type 
  provider for Apiary.io."""

// Read additional information from the release notes document
let releaseNotes, version = 
    let lastItem = File.ReadLines "RELEASE_NOTES.md" |> Seq.last
    let firstDash = lastItem.IndexOf('-')
    ( lastItem.Substring(firstDash + 1 ).Trim(), 
      lastItem.Substring(0, firstDash).Trim([|'*'|]).Trim() )

// --------------------------------------------------------------------------------------
// Generate assembly info files with the right version & up-to-date information

Target "AssemblyInfo" (fun _ ->
    [ ("src/AssemblyInfo.fs", "FSharp.Data", project, summary)
      ("src/AssemblyInfo.DesignTime.fs", "FSharp.Data.DesignTime", project, summary)
      ( "src/AssemblyInfo.Experimental.fs", "FSharp.Data.Experimental", 
        projectExperimental, summaryExperimental )
      ( "src/AssemblyInfo.Experimental.DesignTime.fs", 
        "FSharp.Data.Experimental.DesignTime", projectExperimental,
        summaryExperimental) ]
    |> Seq.iter (fun (fileName, title, project, summary) ->
        CreateFSharpAssemblyInfo fileName
           [ Attribute.Title title
             Attribute.Product project
             Attribute.Description summary
             Attribute.Version version
             Attribute.FileVersion version] )
)

// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    CleanDirs ["bin"]
)

// --------------------------------------------------------------------------------------
// Build library (builds Visual Studio solution, which builds multiple versions
// of the runtime library & desktop + Silverlight version of design time library)

Target "Build" (fun _ ->
    (files ["FSharp.Data.sln"; "FSharp.Data.Tests.sln"])
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner & kill test runner when complete

Target "RunTests" (fun _ ->

    let nunitVersion = GetPackageVersion "packages" "NUnit.Runners"
    let nunitPath = sprintf "packages/NUnit.Runners.%s/Tools" nunitVersion

    ActivateFinalTarget "CloseTestRunner"

    (files ["tests/*/bin/Release/FSharp.Data.Tests*.dll"])
    |> NUnit (fun p ->
        { p with
            ToolPath = nunitPath
            DisableShadowCopy = true
            TimeOut = TimeSpan.FromMinutes 20.
            OutputFile = "TestResults.xml" })
)

FinalTarget "CloseTestRunner" (fun _ ->  
    ProcessHelper.killProcess "nunit-agent.exe"
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->

    // Format the description to fit on a single line (remove \r\n and double-spaces)
    let description = description.Replace("\r", "").Replace("\n", "").Replace("  ", " ")
    let descriptionExperimental = descriptionExperimental.Replace("\r", "").Replace("\n", "").Replace("  ", " ")
    let nugetPath = ".nuget/nuget.exe"

    NuGet (fun p -> 
        { p with   
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = version
            ReleaseNotes = releaseNotes
            Tags = tags
            OutputPath = "bin"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] })
        "nuget/FSharp.Data.nuspec"

    NuGet (fun p -> 
        { p with   
            Authors = authors
            Project = projectExperimental
            Summary = summaryExperimental
            Description = descriptionExperimental
            Version = version
            ReleaseNotes = releaseNotes
            Tags = tags
            OutputPath = "bin"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] })
        "nuget/FSharp.Data.Experimental.nuspec"

)

// --------------------------------------------------------------------------------------
// Release Scripts

Target "UpdateDocs" (fun _ ->

    executeFSI "tools" "build.fsx" [] |> ignore

    DeleteDir "gh-pages"
    Repository.clone "" "https://github.com/fsharp/FSharp.Data.git" "gh-pages"
    Branches.checkoutBranch "gh-pages" "gh-pages"
    CopyRecursive "docs" "gh-pages" true |> printfn "%A"
    CommandHelper.runSimpleGitCommand "gh-pages" (sprintf """commit -a -m "Update generated documentation for version %s""" version) |> printfn "%s"
    Branches.push "gh-pages"
)

Target "UpdateBinaries" (fun _ ->

    DeleteDir "release"
    Repository.clone "" "https://github.com/fsharp/FSharp.Data.git" "release"
    Branches.checkoutBranch "release" "release"
    CopyRecursive "bin" "release/bin" true |> printfn "%A"
    CommandHelper.runSimpleGitCommand "release" (sprintf """commit -a -m "Update binaries for version %s""" version) |> printfn "%s"
    Branches.push "release"
)

Target "Release" DoNothing

"UpdateDocs" ==> "Release"
"UpdateBinaries" ==> "Release"

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "RunTests"
  ==> "NuGet"
  ==> "All"

RunTargetOrDefault "All"
