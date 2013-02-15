#r @"tools\FAKE\tools\FakeLib.dll"

open System
open System.IO
open Fake 
open Fake.AssemblyInfoFile

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let files includes = { BaseDirectories = [__SOURCE_DIRECTORY__]; Includes = includes; Excludes = [] } |> Scan

let project = "FSharp.Data"
let authors = ["Tomas Petricek"]
let summary = "Library of F# type providers and data access tools"
let description = "The F# Data library (FSharp.Data.dll) implements everything you need to access data in your F# applications and scripts. It implements F# type providers for working with structured file formats (CSV, JSON and XML) and for accessing the WorldBank data. It also includes helpers for parsing JSON files and for sending HTTP requests."
let tags = "F# fsharp data type provider WorldBank Freebase CSV XML JSON"

let releaseNotes, version = 
    let lastItem = File.ReadLines "RELEASE_NOTES.md" |> Seq.last
    let firstDash = lastItem.IndexOf('-')
    lastItem.Substring(firstDash + 1 ).Trim(), lastItem.Substring(0, firstDash).Trim([|'*'|]).Trim()

Target "AssemblyInfo" (fun _ ->

    CreateFSharpAssemblyInfo "src/AssemblyInfo.fs"
        [Attribute.Title "FSharp.Data"
         Attribute.Product project
         Attribute.Description summary
         Attribute.Version version
         Attribute.FileVersion version]

    CreateFSharpAssemblyInfo "src/AssemblyInfo.DesignTime.fs"
        [Attribute.Title "FSharp.Data.DesignTime"
         Attribute.Product project
         Attribute.Description summary
         Attribute.Version version
         Attribute.FileVersion version]
)

Target "Clean" (fun _ ->
    CleanDirs ["bin"]
)

Target "Build" (fun _ ->
    (files ["FSharp.Data.sln"; "FSharp.Data.Tests.sln"])
    |> MSBuildRelease "" "Build"
    |> ignore
)

Target "RunTests" (fun _ ->

    // will get NUnit.Runner if not present
    RestorePackages()

    let nunitVersion = GetPackageVersion "packages" "NUnit.Runners"
    let nunitPath = sprintf "packages/NUnit.Runners.%s/Tools" nunitVersion

    ActivateFinalTarget "CloseTestRunner"

    (files ["tests/*/bin/Release/FSharp.Data.Tests*.dll"])
    |> NUnit (fun p ->
        { p with
            ToolPath = nunitPath
            DisableShadowCopy = true
            OutputFile = "TestResults.xml" })
)

FinalTarget "CloseTestRunner" (fun _ ->  
    ProcessHelper.killProcess "nunit-agent.exe"
)

Target "NuGet" (fun _ ->

    let nugetPath = "tools/Nuget/nuget.exe"

    NuGet (fun p -> 
        { p with   
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = version
            //ReleaseNotes = releaseNotes
            //Tags = tags
            OutputPath = "bin"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] })
        "nuget/FSharp.Data.nuspec"
)

Target "All" DoNothing

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "RunTests"
  ==> "NuGet"
  ==> "All"

// Invoke 'build target=<Target>' to override
Run <| getBuildParamOrDefault "target" "All"
