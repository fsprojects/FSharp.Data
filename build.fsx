// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#I "packages/FAKE/tools/"
#r "FakeLib.dll"

#if MONO
#else
#load "packages/SourceLink.Fake/tools/SourceLink.fsx"
#endif

open System
open System.IO
open Fake 
open Fake.AssemblyInfoFile
open Fake.Git

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let (!!) includes = (!! includes).SetBaseDirectory __SOURCE_DIRECTORY__

// --------------------------------------------------------------------------------------
// Information about the project to be used at NuGet and in AssemblyInfo files
// --------------------------------------------------------------------------------------

let project = "FSharp.Data"
let authors = ["Tomas Petricek"; "Gustavo Guerra"]
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
let descriptionExperimental = description + """"
  This package (FSharp.Data.Experimental.dll) adds additional type providers that are work
  in progress and do not match high quality standards yet. Currently, it includes a type 
  provider for Apiary.io."""
let tagsExperimental = tags + " Apiary"

let gitHome = "https://github.com/fsharp"
let gitName = "FSharp.Data"

// Read release notes & version info from RELEASE_NOTES.md
let release = 
    File.ReadLines "RELEASE_NOTES.md" 
    |> ReleaseNotesHelper.parseReleaseNotes

// --------------------------------------------------------------------------------------
// Generate assembly info files with the right version & up-to-date information

Target "AssemblyInfo" <| fun () ->
    for file in !! "src/AssemblyInfo*.fs" do
        let replace (oldValue:string) newValue (str:string) = str.Replace(oldValue, newValue)
        let title = 
            Path.GetFileNameWithoutExtension file
            |> replace ".Portable47" ""
            |> replace ".Portable7" ""
            |> replace "AssemblyInfo" "FSharp.Data"
        let project, summary = 
            if file.Contains "Experimental" 
            then projectExperimental, summaryExperimental 
            else project, summary
        let versionSuffix =
            if file.Contains ".Portable47" then ".47"
            elif file.Contains ".Portable7" then ".7"
            else ".0"
        let version = release.AssemblyVersion + versionSuffix
        CreateFSharpAssemblyInfo file
           [ Attribute.Title title
             Attribute.Product project
             Attribute.Description summary
             Attribute.Version version
             Attribute.FileVersion version]

// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" <| fun () ->
    CleanDirs ["bin"]

Target "CleanDocs" <| fun () ->
    CleanDirs ["docs/output"]

let internetCacheFolder = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)

Target "CleanInternetCaches" <| fun () ->
    CleanDirs [internetCacheFolder @@ "ApiarySchema"
               internetCacheFolder @@ "DesignTimeURIs"
               internetCacheFolder @@ "FreebaseSchema"
               internetCacheFolder @@ "FreebaseRuntime"
               internetCacheFolder @@ "WorldBankSchema"
               internetCacheFolder @@ "WorldBankRuntime"]

// --------------------------------------------------------------------------------------
// Build library & test projects

let runningOnMono = Type.GetType("Mono.Runtime") <> null
let runningOnTeamCity = buildServer = TeamCity

Target "Build" <| fun () ->
    (if runningOnMono then (!! "FSharp.Data.sln") else (!! "FSharp.Data.sln" ++ "FSharp.Data.ExtraPlatforms.sln"))
    |> MSBuildRelease "" "Rebuild"
    |> ignore

Target "BuildTests" <| fun () ->
    !! "FSharp.Data.Tests.sln"
    |> MSBuildReleaseExt "" (if runningOnMono then ["DefineConstants","MONO"]
                             elif runningOnTeamCity then ["DefineConstants","TEAM_CITY"]
                             else []) "Rebuild"
    |> ignore

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target "RunTests" <| fun () ->
    !! "tests/*/bin/Release/FSharp.Data.Tests*.dll"
    |> NUnit (fun p ->
        { p with
            DisableShadowCopy = true
            TimeOut = TimeSpan.FromMinutes 20.
            OutputFile = "TestResults.xml" })

// --------------------------------------------------------------------------------------
// Source link the pdb files

#if MONO

Target "SourceLink" <| id

#else

open SourceLink

Target "SourceLink" <| fun () ->
    use repo = new GitRepo(__SOURCE_DIRECTORY__)
    for file in !! "src/*.fsproj" do
        let proj = VsProj.LoadRelease file
        logfn "source linking %s" proj.OutputFilePdb
        let files = proj.Compiles -- "**/AssemblyInfo*.fs" 
        repo.VerifyChecksums files
        proj.VerifyPdbChecksums files
        proj.CreateSrcSrv "https://raw.github.com/fsharp/FSharp.Data/{0}/%var2%" repo.Revision (repo.Paths files)
        Pdbstr.exec proj.OutputFilePdb proj.OutputFilePdbSrcSrv
    CopyFiles "bin" (!! "src/bin/Release/FSharp.Data.*")
    CopyFiles "bin/portable7" (!! "src/bin/portable7/Release/FSharp.Data.*")
    CopyFiles "bin/portable7" (!! "src/bin/Release/FSharp.*.DesignTime.*")
    CopyFiles "bin/portable47" (!! "src/bin/portable47/Release/FSharp.Data.*")    
    CopyFiles "bin/portable47" (!! "src/bin/Release/FSharp.*.DesignTime.*")

#endif

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" <| fun () ->
    // Format the description to fit on a single line (remove \r\n and double-spaces)
    let description = description
    let descriptionExperimental = descriptionExperimental.Replace("\r", "").Replace("\n", "").Replace("  ", " ")
    // Format the release notes
    let releaseNotes = release.Notes |> String.concat "\n"
    NuGet (fun p -> 
        { p with   
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = release.NugetVersion
            ReleaseNotes = releaseNotes
            Tags = tags
            OutputPath = "bin"
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
            Version = release.NugetVersion
            ReleaseNotes = releaseNotes
            Tags = tagsExperimental
            OutputPath = "bin"
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] })
        "nuget/FSharp.Data.Experimental.nuspec"

// --------------------------------------------------------------------------------------
// Generate the documentation

Target "GenerateDocs" <| fun () ->
    executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"] [] |> ignore

Target "GenerateDocsJa" <| fun () ->
    executeFSIWithArgs "docs/tools" "generate.ja.fsx" ["--define:RELEASE"] [] |> ignore

// --------------------------------------------------------------------------------------
// Release Scripts

let publishFiles what branch fromFolder toFolder =
    let tempFolder = "temp/" + branch
    CleanDir tempFolder
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") branch tempFolder
    fullclean tempFolder
    CopyRecursive fromFolder (tempFolder + "/" + toFolder) true |> tracefn "%A"
    StageAll tempFolder
    Commit tempFolder <| sprintf "Update %s for version %s" what release.NugetVersion
    Branches.push tempFolder

Target "ReleaseDocs" <| fun () ->
    publishFiles "generated documentation" "gh-pages" "docs/output" "" 

Target "ReleaseBinaries" <| fun () ->
    publishFiles "binaries" "release" "bin" "bin" 

Target "Release" DoNothing

"CleanDocs" ==> "GenerateDocsJa" ==> "GenerateDocs" ==> "ReleaseDocs"
"ReleaseDocs" ==> "Release"
"ReleaseBinaries" ==> "Release"
"NuGet" ==> "Release"

// --------------------------------------------------------------------------------------
// Help

Target "Help" <| fun () ->
    printfn ""
    printfn "  Please specify the target by calling 'build <Target>'"
    printfn ""
    printfn "  Targets for building:"
    printfn "  * Build"
    printfn "  * BuildTests"
    printfn "  * RunTests"
    printfn "  * All (calls previous 3)"
    printfn ""
    printfn "  Targets for releasing (requires write access to the 'https://github.com/fsharp/FSharp.Data.git' repository):"
    printfn "  * GenerateDocs"
    printfn "  * ReleaseDocs (calls previous)"
    printfn "  * ReleaseBinaries"
    printfn "  * NuGet (creates package only, doesn't publish)"
    printfn "  * Release (calls previous 4)"
    printfn ""
    printfn "  Other targets:"
    printfn "  * CleanInternetCaches"
#if MONO
#else
    printfn "  * SourceLink (requires autocrlf=false)"
#endif
    printfn ""

Target "All" DoNothing

"Clean" ==> "AssemblyInfo" ==> "Build"
"Build" ==> "All"
"BuildTests" ==> "All"
"RunTests" ==> "All"

RunTargetOrDefault "Help"
