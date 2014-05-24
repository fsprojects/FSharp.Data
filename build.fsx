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

let gitHome = "https://github.com/fsharp"
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
            |> replace ".Portable47" ""
            |> replace ".Portable7" ""
            |> replace "AssemblyInfo" "FSharp.Data"
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
    CleanDirs [internetCacheFolder @@ "DesignTimeURIs"
               internetCacheFolder @@ "FreebaseSchema"
               internetCacheFolder @@ "FreebaseRuntime"
               internetCacheFolder @@ "WorldBankSchema"
               internetCacheFolder @@ "WorldBankRuntime"]

// --------------------------------------------------------------------------------------
// Build library & test projects

Target "Build" <| fun () ->
    !! "FSharp.Data.sln"
#if MONO
#else
    ++ "FSharp.Data.Portable7.sln"
#endif
    |> MSBuildRelease "" "Rebuild"
    |> ignore

Target "BuildTests" <| fun () ->
    !! "FSharp.Data.Tests.sln"
    |> MSBuildReleaseExt "" (if buildServer = TeamCity then ["DefineConstants","TEAM_CITY"] else []) "Rebuild"
    |> ignore

Target "BuildConsoleTests" <| fun () ->
    !! "TestApps.Console.sln"
//#if MONO
//#else
//    ++ "TestApps.Console.Portable7.sln"
//#endif
    |> MSBuildReleaseExt "" (if buildServer = TeamCity then ["DefineConstants","TEAM_CITY"] else []) "Rebuild"
    |> ignore

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner
Target "RunTests" <| ignore

let runTestTask name =
    let taskName = sprintf "RunTest_%s" name
    Target taskName <| fun () ->
        !! (sprintf "tests/*/bin/Release/%s.dll" name)
        |> NUnit (fun p ->
            { p with
                DisableShadowCopy = true
                TimeOut = TimeSpan.FromMinutes 20.
                Framework = "4.0"
                Domain = MultipleDomainModel
                OutputFile = "TestResults.xml" })
    taskName ==> "RunTests" |> ignore

["FSharp.Data.Tests";"FSharp.Data.DesignTime.Tests"]
|> List.iter runTestTask

// Run the console tests
Target "RunConsoleTests" (fun _ ->
    [ for consoleTest in !! "tests/TestApps/*/bin/Release/*.exe" -> consoleTest, "" ]
    |> ProcessTestRunner.RunConsoleTests (fun p -> { p with TimeOut = TimeSpan.FromMinutes 1. } ))

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
        proj.CreateSrcSrv (sprintf "%s/%s/{0}/%%var2%%" gitRaw gitName) repo.Revision (repo.Paths files)
        Pdbstr.exec proj.OutputFilePdb proj.OutputFilePdbSrcSrv
    CopyFiles "bin" (!! "src/bin/Release/FSharp.Data.*")
    CopyFiles "bin/portable7" (!! "src/bin/portable7/Release/FSharp.Data.*")
    CopyFiles "bin/portable7" (!! "src/bin/Release/FSharp.Data.DesignTime.*")
    CopyFiles "bin/portable47" (!! "src/bin/portable47/Release/FSharp.Data.*")    
    CopyFiles "bin/portable47" (!! "src/bin/Release/FSharp.Data.DesignTime.*")

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
    printfn "  * BuildConsoleTests"
    printfn "  * RunTests"
    printfn "  * RunConsoleTests"
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
"BuildConsoleTests" ==> "All"
"RunTests" ==> "All"
"RunConsoleTests" ==> "All"

RunTargetOrDefault "Help"
