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

let releaseNotes = release.Notes |> String.concat "\n"

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
             Attribute.Version release.AssemblyVersion
             Attribute.FileVersion release.AssemblyVersion] )
)
// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    CleanDirs ["bin"]
)

Target "CleanDocs" (fun _ ->
    CleanDirs ["docs/output"]
)

let internetCacheFolder = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)

Target "CleanInternetCaches" (fun _ ->
    CleanDirs [internetCacheFolder @@ "ApiarySchema"
               internetCacheFolder @@ "DesignTimeURIs"
               internetCacheFolder @@ "FreebaseSchema"
               internetCacheFolder @@ "FreebaseRuntime"
               internetCacheFolder @@ "WorldBankSchema"
               internetCacheFolder @@ "WorldBankRuntime"]
)

// --------------------------------------------------------------------------------------
// Build Visual Studio solutions

let runningOnMono = Type.GetType("Mono.Runtime") <> null

let noPCL = runningOnMono

Target "Build" (fun _ ->
    (if noPCL then (!! "FSharp.Data.sln") else (!! "FSharp.Data.sln" ++ "FSharp.Data.ExtraPlatforms.sln"))
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

Target "BuildTests" (fun _ ->
    !! "FSharp.Data.Tests.sln"
    |> MSBuildReleaseExt "" (if noPCL then ["DefineConstants","NO_PCL"] else []) "Rebuild"
    |> ignore
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner & kill test runner when complete

Target "RunTests" (fun _ ->
    let nunitVersion = GetPackageVersion "packages" "NUnit.Runners"
    let nunitPath = sprintf "packages/NUnit.Runners.%s/Tools" nunitVersion
    ActivateFinalTarget "CloseTestRunner"

    !! "tests/*/bin/Release/FSharp.Data.Tests*.dll"
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
// Source link the pdb files

#if MONO

Target "SourceLink" (fun _ -> ())

#else

open SourceLink

Target "SourceLink" (fun _ ->
    use repo = new GitRepo(__SOURCE_DIRECTORY__)
    !! "src/*.fsproj" 
    |> Seq.iter (fun f ->
        let proj = VsProj.LoadRelease f
        logfn "source linking %s" proj.OutputFilePdb
        let files = proj.Compiles -- "**/AssemblyInfo*.fs" 
        repo.VerifyChecksums files
        proj.VerifyPdbChecksums files
        proj.CreateSrcSrv "https://raw.github.com/fsharp/FSharp.Data/{0}/%var2%" repo.Revision (repo.Paths files)
        Pdbstr.exec proj.OutputFilePdb proj.OutputFilePdbSrcSrv
    )
    CopyFiles "bin" (!! "src/bin/Release/FSharp.Data.*")
    CopyFiles "bin/portable7" (!! "src/bin/portable7/Release/FSharp.Data.*")
    CopyFiles "bin/portable7" (!! "src/bin/Release/FSharp.*.DesignTime.*")
    CopyFiles "bin/portable47" (!! "src/bin/portable47/Release/FSharp.Data.*")    
    CopyFiles "bin/portable47" (!! "src/bin/Release/FSharp.*.DesignTime.*")
)
#endif

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
            Version = release.NugetVersion
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
            Version = release.NugetVersion
            ReleaseNotes = releaseNotes
            Tags = tagsExperimental
            OutputPath = "bin"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] })
        "nuget/FSharp.Data.Experimental.nuspec"

)

// --------------------------------------------------------------------------------------
// Generate the documentation

Target "GenerateDocs" (fun _ ->
    executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"] [] |> ignore
)

Target "GenerateDocsJa" (fun _ ->
    executeFSIWithArgs "docs/tools" "generate.ja.fsx" ["--define:RELEASE"] [] |> ignore
)

// --------------------------------------------------------------------------------------
// Release Scripts

Target "ReleaseDocs" (fun _ ->
    CleanDirs ["temp/gh-pages"]
    Repository.clone "" (gitHome + "/" + gitName + ".git") "temp/gh-pages"
    Branches.checkoutBranch "temp/gh-pages" "gh-pages"
    CopyRecursive "docs/output" "temp/gh-pages" true |> printfn "%A"
    CommandHelper.runSimpleGitCommand "temp/gh-pages" "add ." |> printfn "%s"
    let cmd = sprintf """commit -a -m "Update generated documentation for version %s""" release.NugetVersion
    CommandHelper.runSimpleGitCommand "temp/gh-pages" cmd |> printfn "%s"
    Branches.push "temp/gh-pages"
)

Target "ReleaseBinaries" (fun _ ->
    CleanDirs ["temp/release"]
    Repository.clone "" (gitHome + "/" + gitName + ".git") "temp/release"
    Branches.checkoutBranch "temp/release" "release"
    CopyRecursive "bin" "temp/release/bin" true |> printfn "%A"
    let cmd = sprintf """commit -a -m "Update binaries for version %s""" release.NugetVersion
    CommandHelper.runSimpleGitCommand "temp/release" cmd |> printfn "%s"
    Branches.push "temp/release"
)

Target "Release" DoNothing

"CleanDocs" ==> "GenerateDocsJa" ==> "GenerateDocs" ==> "ReleaseDocs"
"ReleaseDocs" ==> "Release"
"ReleaseBinaries" ==> "Release"
"NuGet" ==> "Release"

// --------------------------------------------------------------------------------------
// Help

Target "Help" (fun _ ->
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
    printfn "")

Target "All" DoNothing

"Clean" ==> "AssemblyInfo" ==> "Build"
"Build" ==> "All"
"BuildTests" ==> "All"
"RunTests" ==> "All"

RunTargetOrDefault "Help"
