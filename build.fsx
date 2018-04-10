// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#I "packages/FAKE/tools/"
#r "FakeLib.dll"

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

let desiredSdkVersion = "2.1.100"
let mutable sdkPath = None
let getSdkPath() = (defaultArg sdkPath "dotnet")

printfn "Desired .NET SDK version = %s" desiredSdkVersion
printfn "DotNetCli.isInstalled() = %b" (DotNetCli.isInstalled())
let useMsBuildToolchain = environVar "USE_MSBUILD" <> null

if DotNetCli.isInstalled() then 
    let installedSdkVersion = DotNetCli.getVersion()
    printfn "The installed default .NET SDK version reported by FAKE's 'DotNetCli.getVersion()' is %s" installedSdkVersion
    if installedSdkVersion <> desiredSdkVersion then
        match environVar "CI" with 
        | null -> 
            if installedSdkVersion > desiredSdkVersion then 
                printfn "*** You have .NET SDK version '%s' installed, assuming it is compatible with version '%s'" installedSdkVersion desiredSdkVersion 
            else
                printfn "*** You have .NET SDK version '%s' installed, we expect at least version '%s'" installedSdkVersion desiredSdkVersion 
        | _ -> 
            printfn "*** The .NET SDK version '%s' will be installed (despite the fact that version '%s' is already installed) because we want precisely that version in CI" desiredSdkVersion installedSdkVersion
            sdkPath <- Some (DotNetCli.InstallDotNetSDK desiredSdkVersion)
else
    printfn "*** The .NET SDK version '%s' will be installed (no other version was found by FAKE helpers)" desiredSdkVersion 
    sdkPath <- Some (DotNetCli.InstallDotNetSDK desiredSdkVersion)

// Read release notes & version info from RELEASE_NOTES.md
let release = 
    File.ReadLines "RELEASE_NOTES.md" 
    |> ReleaseNotesHelper.parseReleaseNotes


let bindir = "./bin"

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
            |> replace "AssemblyInfo" "FSharp.Data"
        let versionSuffix =".0"
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
    // have to clean netcore output directories because they corrupt the full-framework outputs
    seq {
        yield bindir
        yield! !!"**/bin"
        yield! !!"**/obj"
    } |> CleanDirs
    

Target "CleanDocs" <| fun () ->
    CleanDirs ["docs/output"]

let internetCacheFolder = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)

Target "CleanInternetCaches" <| fun () ->
    CleanDirs [internetCacheFolder @@ "DesignTimeURIs"
               internetCacheFolder @@ "WorldBankSchema"
               internetCacheFolder @@ "WorldBankRuntime"]

// --------------------------------------------------------------------------------------
// Build library & test projects

let testNames = 
    [ "FSharp.Data.DesignTime.Tests" 
      "FSharp.Data.Tests.CSharp" 
      "FSharp.Data.Tests"  ]
let testProjs = 
    [ "tests/FSharp.Data.DesignTime.Tests/FSharp.Data.DesignTime.Tests.fsproj" 
      "tests/FSharp.Data.Tests.CSharp/FSharp.Data.Tests.CSharp.csproj" 
      "tests/FSharp.Data.Tests/FSharp.Data.Tests.fsproj"  ]

let buildProjs =
    [ "src/FSharp.Data.DesignTime/FSharp.Data.DesignTime.fsproj"
      "src/FSharp.Data/FSharp.Data.fsproj" ]


Target "Build" <| fun () ->
 if useMsBuildToolchain then
    buildProjs |> Seq.iter (fun proj -> 
        DotNetCli.Restore  (fun p -> { p with Project = proj; ToolPath =  getSdkPath() }))

    buildProjs |> Seq.iter (fun proj ->
        let projName = System.IO.Path.GetFileNameWithoutExtension proj
        MSBuildReleaseExt null ["SourceLinkCreate", "true"] "Build" [proj]
        |> Log (sprintf "%s-Output:\t" projName))
 else
    // Both flavours of FSharp.Data.DesignTime.dll (net45 and netstandard2.0) must be built _before_ building FSharp.Data
    buildProjs |> Seq.iter (fun proj -> 
        DotNetCli.RunCommand (fun p -> { p with ToolPath = getSdkPath() }) (sprintf "build -c Release \"%s\" /p:SourceLinkCreate=true" proj))


Target "BuildTests" <| fun () ->
    for testProj in testProjs do 
    if useMsBuildToolchain then
        DotNetCli.Restore (fun p -> { p with Project = testProj; ToolPath = getSdkPath(); AdditionalArgs=["/v:n"] })
        MSBuildRelease null "Build" [testProj] |> Log "BuildTests.DesignTime-Output: "
    else
        DotNetCli.Build (fun p -> { p with Configuration = "Release"; Project = testProj; ToolPath = getSdkPath(); AdditionalArgs=["/v:n"] })

Target "RunTests" <| fun () ->
 if useMsBuildToolchain then
       for testName in testNames do 
           !! (sprintf "tests/*/bin/Release/net461/%s.dll" testName)
           |> NUnit3 (fun p ->
               { p with
                   TimeOut = TimeSpan.FromMinutes 20. 
                   TraceLevel = NUnit3TraceLevel.Info})
 else
    for testProj in testProjs do 
        DotNetCli.Test (fun p -> { p with Configuration = "Release"; Project = testProj; ToolPath = getSdkPath(); AdditionalArgs=["/v:n"] })


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
    Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.pushBranch "" "upstream" "master"

    // Create tag
    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "upstream" release.NugetVersion

    // Create github release    
    let token =
        match environVarOrDefault "github_token" "" with
        | s when not (System.String.IsNullOrWhiteSpace s) -> s
        | _ -> failwith "please set the github_token environment variable to a github personal access token with repro access."

    let draft =
        createClientWithToken token
        |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes 
   
    draft
    |> releaseDraft
    |> Async.RunSynchronously

Target "ReleaseDocs" <| fun () ->
    publishFiles "generated documentation" "gh-pages" "docs/output" "" 

Target "ReleaseBinaries" <| fun () ->
    createRelease() 
    publishFiles "binaries" "release" "bin" "bin" 

Target "TestSourcelink" <| fun () ->
    let testSourcelink framework proj =
        let basePath = Path.GetFileNameWithoutExtension proj
        let pdb = sprintf "bin/Release/netstandard2.0/%s.pdb" basePath
        DotNetCli.RunCommand (fun p -> { p with ToolPath = getSdkPath(); WorkingDir = Path.GetDirectoryName proj }) (sprintf "sourcelink test %s" pdb)

    ["net45"; "netstandard2.0"]
    |> Seq.collect (fun fw -> buildProjs |> Seq.map (testSourcelink fw))
    |> Seq.iter id

Target "Release" DoNothing

"CleanDocs" ==> "GenerateDocs" ==> "ReleaseDocs"
"ReleaseDocs" ==> "Release"
"ReleaseBinaries" ==> "Release"
"NuGet" ==> "Release"
"TestSourcelink" ==> "Release"

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
    printfn "  * TestSourceLink (validates the SourceLink embedded data)"
    printfn "  * Release (calls previous 5)"
    printfn ""
    printfn "  Other targets:"
    printfn "  * CleanInternetCaches"
    printfn ""
    printfn "  Set USE_MSBUILD=1 in environment to use MSBuild toolchain and .NET Framework/Mono compiler."

Target "All" DoNothing

"Clean" ==> "AssemblyInfo" ==> "Build"
"Build" ==> "All"
"BuildTests" ==> "All"
"RunTests" ==> "All"

Target "BuildAndRunTests" DoNothing

"BuildTests" ==> "BuildAndRunTests"
"RunTests" ==> "BuildAndRunTests"

RunTargetOrDefault "Help"
