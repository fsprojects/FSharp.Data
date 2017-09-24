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
open Fake.Testing.NUnit3

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let (!!) includes = (!! includes).SetBaseDirectory __SOURCE_DIRECTORY__

// --------------------------------------------------------------------------------------
// Information about the project to be used at NuGet and in AssemblyInfo files
// --------------------------------------------------------------------------------------

let project = "FSharp.Data"
let authors = ["Tomas Petricek"; "Gustavo Guerra"; "Colin Bull"]
let summary = "Library of F# type providers and data access tools"
let description = """
  The F# Data library (FSharp.Data.dll) implements everything you need to access data
  in your F# applications and scripts. It implements F# type providers for working with
  structured file formats (CSV, HTML, JSON and XML) and for accessing the WorldBank data.
  It also includes helpers for parsing CSV, HTML and JSON files and for sending HTTP requests."""
let tags = "F# fsharp data typeprovider WorldBank CSV HTML CSS JSON XML HTTP linqpad-samples"

let gitOwner = "fsharp"
let gitHome = "https://github.com/" + gitOwner
let gitName = "FSharp.Data"
let gitRaw = environVarOrDefault "gitRaw" "https://raw.github.com/fsharp"

// Read release notes & version info from RELEASE_NOTES.md
let release = 
    File.ReadLines "RELEASE_NOTES.md" 
    |> ReleaseNotesHelper.parseReleaseNotes

let isAppVeyorBuild = environVar "APPVEYOR" <> null
let nugetVersion = 
    if isAppVeyorBuild then sprintf "%s-a%s" release.NugetVersion (DateTime.UtcNow.ToString "yyMMddHHmm")
    else release.NugetVersion

Target "AppVeyorBuildVersion" (fun _ ->
    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s\"" nugetVersion) |> ignore
)

// --------------------------------------------------------------------------------------
// Generate assembly info files with the right version & up-to-date information

Target "AssemblyInfo" <| fun () ->
    for file in !! "src/AssemblyInfo*.fs" do
        let replace (oldValue:string) newValue (str:string) = str.Replace(oldValue, newValue)
        let title = 
            Path.GetFileNameWithoutExtension file
            |> replace ".Portable259" ""
            |> replace ".Portable7" ""
            |> replace "AssemblyInfo" "FSharp.Data"
        let versionSuffix =
            if file.Contains ".Portable259" then ".259"
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
    CleanDirs [internetCacheFolder @@ "DesignTimeURIs"
               internetCacheFolder @@ "WorldBankSchema"
               internetCacheFolder @@ "WorldBankRuntime"]

// --------------------------------------------------------------------------------------
// Build library & test projects

Target "Build" <| fun () ->
    !! "FSharp.Data.sln"
    |> MSBuildRelease "" "Rebuild"
    |> ignore

Target "BuildTests" <| fun () ->
    !! "FSharp.Data.Tests.sln"
    |> MSBuildReleaseExt "" (if isLocalBuild then [] else ["DefineConstants","BUILD_SERVER"]) "Rebuild"
    |> ignore

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner
Target "RunTests" <| ignore

let runTestTask name =
    let taskName = sprintf "RunTest_%s" name
    Target taskName <| fun () ->
        !! (sprintf "tests/*/bin/Release/%s.dll" name)
        |> NUnit3 (fun p ->
            { p with
                TimeOut = TimeSpan.FromMinutes 20. 
                TraceLevel = NUnit3TraceLevel.Info})
    taskName ==> "RunTests" |> ignore

["FSharp.Data.Tests";"FSharp.Data.DesignTime.Tests"]
|> List.iter runTestTask

// --------------------------------------------------------------------------------------
// Source link the pdb files

#if MONO

Target "SourceLink" <| id

#else

open SourceLink

Target "SourceLink" <| fun () ->
    for file in !! "src/*.fsproj" do
        let proj = VsProj.Load file ["Configuration","Release"; "VisualStudioVersion","12.0"]
        let files = SetBaseDir __SOURCE_DIRECTORY__ proj.Compiles -- "**/paket-files/**"
        let url = sprintf "%s/%s/{0}/%%var2%%" gitRaw gitName
        SourceLink.Index files proj.OutputFilePdb __SOURCE_DIRECTORY__ url
    CopyFiles "bin" (!! "src/bin/Release/FSharp.Data.*")
    CopyFiles "bin/portable7" (!! "src/bin/portable7/Release/FSharp.Data.*")
    CopyFiles "bin/portable7" (!! "src/bin/Release/FSharp.Data.DesignTime.*")
    CopyFiles "bin/portable259" (!! "src/bin/portable259/Release/FSharp.Data.*")    
    CopyFiles "bin/portable259" (!! "src/bin/Release/FSharp.Data.DesignTime.*")

#endif

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" <| fun () ->
    // Format the release notes
    let releaseNotes = release.Notes |> String.concat "\n"
    NuGet (fun p -> 
        { p with   
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = nugetVersion
            ReleaseNotes = releaseNotes
            Tags = tags
            OutputPath = "bin"
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] })
        "nuget/FSharp.Data.nuspec"

// --------------------------------------------------------------------------------------
// Generate the documentation

Target "GenerateDocs" <| fun () ->
    executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"] [] |> ignore

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

#load "paket-files/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

let createRelease() =

    // Set release date in release notes
    let releaseNotes = File.ReadAllText "RELEASE_NOTES.md" 
    let releaseNotes = releaseNotes.Replace("#### " + release.NugetVersion + " - Unreleased", "#### " + release.NugetVersion + " - " + DateTime.Now.ToString("MMMM d yyyy"))
    File.WriteAllText("RELEASE_NOTES.md", releaseNotes)

    // Commit assembly info and RELEASE_NOTES.md
    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.pushBranch "" "upstream" "master"

    // Create tag
    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "upstream" release.NugetVersion

    // Create github release
    let user = getBuildParamOrDefault "github-user" ""
    let pw = getBuildParamOrDefault "github-pw" ""

    let user = 
        if user = "" then 
            printf "username: "
            Console.ReadLine()
        else 
            user
    let pw = 
        if pw = "" then 
            printf "password: "
            Console.ReadLine()
        else 
            pw

    let draft =
        createClient user pw
        |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes 
   
    draft
    |> releaseDraft
    |> Async.RunSynchronously

Target "ReleaseDocs" <| fun () ->
    publishFiles "generated documentation" "gh-pages" "docs/output" "" 

Target "ReleaseBinaries" <| fun () ->
    createRelease() 
    publishFiles "binaries" "release" "bin" "bin" 

Target "Release" DoNothing

"CleanDocs" ==> "GenerateDocs" ==> "ReleaseDocs"
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
    printfn "  * All (calls previous 5)"
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
    printfn "  * SourceLink (requires autocrlf=input)"
#endif
    printfn ""

Target "All" DoNothing

"Clean" ==> "AssemblyInfo" ==> "Build"
"Build" ==> "All"
"BuildTests" ==> "All"
"RunTests" ==> "All"

Target "BuildAndRunTests" DoNothing

"BuildTests" ==> "BuildAndRunTests"
"RunTests" ==> "BuildAndRunTests"

RunTargetOrDefault "Help"
