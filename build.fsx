// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r "paket: groupref fake //"

#if !FAKE
#load ".fake/build.fsx/intellisense.fsx"
#r "netstandard"
#endif

open System
open System.IO
open Fake.Core
open Fake.DotNet
open Fake.DotNet.NuGet
open Fake.DotNet.Testing
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Tools.Git

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let (!!) includes = (!! includes).SetBaseDirectory __SOURCE_DIRECTORY__

// --------------------------------------------------------------------------------------
// Information about the project to be used at NuGet and in AssemblyInfo files
// --------------------------------------------------------------------------------------

let project = "FSharp.Data"
let authors = "Tomas Petricek;Gustavo Guerra;Colin Bull;fsprojects contributors"
let summary = "Library of F# type providers and data access tools"
let description = """
  The FSharp.Data library (FSharp.Data.dll) contains type providers and utilities to access
  common data formats in your F# applications and scripts. It contains F# type providers for working with
  structured file formats (CSV, HTML, JSON and XML) and helpers for parsing CSV, HTML and JSON files and for sending HTTP requests."""
let tags = "F# fsharp data typeprovider WorldBank CSV HTML CSS JSON XML HTTP linqpad-samples"

let gitOwner = "fsprojects"
let gitHome = "https://github.com/" + gitOwner
let gitName = "FSharp.Data"

let packageProjectUrl = "https://fsprojects.github.io/FSharp.Data/"
let repositoryType = "git"
let repositoryUrl = "https://github.com/fsprojects/FSharp.Data"
let license = "Apache-2.0"

// Read release notes & version info from RELEASE_NOTES.md
let release = ReleaseNotes.load "RELEASE_NOTES.md"

// --------------------------------------------------------------------------------------
// Generate assembly info files with the right version & up-to-date information

Target.create "AssemblyInfo" <| fun _ ->
    for file in !! "src/AssemblyInfo*.fs" do
        let replace (oldValue:string) newValue (str:string) = str.Replace(oldValue, newValue)
        let title =
            Path.GetFileNameWithoutExtension file
            |> replace "AssemblyInfo" "FSharp.Data"
        let versionSuffix =".0"
        let version = release.AssemblyVersion + versionSuffix
        AssemblyInfoFile.createFSharp file
           [ AssemblyInfo.Title title
             AssemblyInfo.Product project
             AssemblyInfo.Description summary
             AssemblyInfo.Version version
             AssemblyInfo.FileVersion version]

// --------------------------------------------------------------------------------------
// Clean build results

Target.create "Clean" <| fun _ ->
    seq {
        yield! !!"**/bin"
        yield! !!"**/obj"
    } |> Shell.cleanDirs

Target.create "CleanDocs" <| fun _ ->
    Shell.cleanDirs ["docs/output"]

let internetCacheFolder = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)

Target.create "CleanInternetCaches" <| fun _ ->
    Shell.cleanDirs [ internetCacheFolder @@ "DesignTimeURIs"
                      internetCacheFolder @@ "WorldBankSchema"
                      internetCacheFolder @@ "WorldBankRuntime"]

// --------------------------------------------------------------------------------------
// Build library & test projects

Target.create "Build" <| fun _ ->
    "FSharp.Data.sln"
    |>  DotNet.build (fun o ->
            { o with Configuration = DotNet.BuildConfiguration.Release })

Target.create "RunTests" <| fun _ ->
    "FSharp.Data.sln"
    |>  DotNet.test (fun o ->
            { o with Configuration = DotNet.BuildConfiguration.Release })

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "NuGet" <| fun _ ->
    // Format the release notes
    let releaseNotes = release.Notes |> String.concat "\n"

    let properties = [
        ("Version", release.NugetVersion)
        ("Authors", authors)
        ("PackageProjectUrl", packageProjectUrl)
        ("PackageTags", tags)
        ("RepositoryType", repositoryType)
        ("RepositoryUrl", repositoryUrl)
        ("PackageLicenseExpression", license)
        ("PackageReleaseNotes", releaseNotes)
        ("Summary", summary)
        ("PackageDescription", description)
        ("EnableSourceLink", "true")
        ("PublishRepositoryUrl", "true")
        ("EmbedUntrackedSources", "true")
        ("IncludeSymbols", "true")
        ("SymbolPackageFormat", "snupkg")
    ]

    DotNet.pack (fun p ->
        { p with
            Configuration = DotNet.BuildConfiguration.Release
            OutputPath = Some "bin"
            MSBuildParams = { p.MSBuildParams with Properties = properties}
        }
    ) "src/FSharp.Data/FSharp.Data.fsproj"

// --------------------------------------------------------------------------------------
// Generate the documentation
Target.create "GenerateDocs" (fun _ ->
    Shell.cleanDir ".fsdocs"
    DotNet.exec id "fsdocs" ("build --properties Configuration=Release --eval --clean --parameters fsdocs-package-version " + release.NugetVersion) |> ignore
)

// --------------------------------------------------------------------------------------
// Release Scripts
let publishFiles what branch fromFolder toFolder =
    let tempFolder = "temp/" + branch
    Shell.cleanDir tempFolder
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") branch tempFolder
    Repository.fullclean tempFolder
    Shell.copyRecursive fromFolder (tempFolder + "/" + toFolder) true |> Trace.tracefn "%A"
    Staging.stageAll tempFolder
    Commit.exec tempFolder <| sprintf "Update %s for version %s" what release.NugetVersion
    Branches.push tempFolder

Target.create "Release" ignore


// --------------------------------------------------------------------------------------
// Help

Target.create "Help" <| fun _ ->
    printfn ""
    printfn "  Please specify the target by calling 'build -t <Target>'"
    printfn ""
    printfn "  Targets for building:"
    printfn "  * Build"
    printfn "  * RunTests"
    printfn "  * GenerateDocs"
    printfn "  * NuGet (creates package only, doesn't publish)"
    printfn "  * All (calls previous 5)"
    printfn ""
    printfn "  Targets for releasing (requires write access to the 'https://github.com/fsharp/FSharp.Data.git' repository):"
    printfn "  * Release (calls All)"
    printfn "  * ReleaseDocsManually (note: doc release now done by github action)"
    printfn ""
    printfn "  Other targets:"
    printfn "  * CleanInternetCaches"
    printfn ""

Target.create "All" ignore

"Clean" ==> "AssemblyInfo" ==> "Build"
"Build" ==> "CleanDocs" ==> "GenerateDocs" ==> "All"
"Build" ==> "NuGet" ==> "All"
"Build" ==> "All"
"Build" ==> "RunTests" ==> "All"
"All" ==> "Release"

Target.runOrDefaultWithArguments "Help"
