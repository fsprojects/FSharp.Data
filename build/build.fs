module Build


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

let buildscript () =
    Environment.CurrentDirectory <- __SOURCE_DIRECTORY__ </> ".."

    let (!!) includes =
        (!!includes).SetBaseDirectory(__SOURCE_DIRECTORY__ </> "..")

    // --------------------------------------------------------------------------------------
    // Information about the project to be used at NuGet and in AssemblyInfo files
    // --------------------------------------------------------------------------------------

    let project = "FSharp.Data"
    let authors = "Tomas Petricek;Gustavo Guerra;Colin Bull;fsprojects contributors"
    let summary = "Library of F# type providers and data access tools"

    let description =
        """
    The FSharp.Data packages contain type providers and utilities to access
    common data formats (CSV, HTML, JSON and XML in your F# applications and scripts.

    * FSharp.Data -- includes everything
        * FSharp.Data.Http -- http types/helpers
        * FSharp.Data.Csv.Core -- csv types/helpers
        * FSharp.Data.Json.Core -- json types/helpers
        * FSharp.Data.Html.Core -- html types/helpers
        * FSharp.Data.Xml.Core -- xml types/helpers"""

    let tags =
        "F# fsharp data typeprovider WorldBank CSV HTML CSS JSON XML HTTP linqpad-samples"

    let gitOwner = "fsprojects"
    let gitHome = "https://github.com/" + gitOwner
    let gitName = "FSharp.Data"

    let packageProjectUrl = "https://fsprojects.github.io/FSharp.Data/"
    let repositoryType = "git"
    let repositoryUrl = "https://github.com/fsprojects/FSharp.Data"
    let license = "Apache-2.0"

    // Read release notes & version info from RELEASE_NOTES.md
    let release = ReleaseNotes.load "RELEASE_NOTES.md"

    let isCI = Environment.GetEnvironmentVariable("CI") <> null

    // --------------------------------------------------------------------------------------
    // Generate assembly info files with the right version & up-to-date information

    Target.create "AssemblyInfo" (fun _ ->
        for file in !!"src/AssemblyInfo*.fs" do
            let replace (oldValue: string) newValue (str: string) = str.Replace(oldValue, newValue)

            let title =
                Path.GetFileNameWithoutExtension file |> replace "AssemblyInfo" "FSharp.Data"

            let versionSuffix = ".0"
            let version = release.AssemblyVersion + versionSuffix

            AssemblyInfoFile.createFSharp
                file
                [ AssemblyInfo.Title title
                  AssemblyInfo.Product project
                  AssemblyInfo.Description summary
                  AssemblyInfo.Version version
                  AssemblyInfo.FileVersion version ])

    // --------------------------------------------------------------------------------------
    // Clean build results

    Target.create "Clean" (fun _ ->
        seq {
            yield! !!"src/**/bin"
            yield! !!"tests/**/bin"
            yield! !!"src/**/obj"
            yield! !!"tests/**/obj"
        }
        |> Shell.cleanDirs)

    Target.create "CleanDocs" (fun _ -> Shell.cleanDirs [ "docs/output" ])

    let internetCacheFolder =
        Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)

    Target.create "CleanInternetCaches" (fun _ ->
        Shell.cleanDirs
            [ internetCacheFolder @@ "DesignTimeURIs"
              internetCacheFolder @@ "WorldBankSchema"
              internetCacheFolder @@ "WorldBankRuntime" ])

    // --------------------------------------------------------------------------------------
    // Build library & test projects

    Target.create "Build" (fun _ ->
        "FSharp.Data.sln"
        |> DotNet.build (fun o ->
            { o with
                Configuration = DotNet.BuildConfiguration.Release
                MSBuildParams =
                    { o.MSBuildParams with
                        DisableInternalBinLog = true } }))

    Target.create "RunTests" (fun _ ->
        let setParams (o: DotNet.TestOptions) =
            { o with
                Configuration = DotNet.BuildConfiguration.Release
                MSBuildParams =
                    { o.MSBuildParams with
                        DisableInternalBinLog = true }
                Logger = if isCI then Some "GitHubActions" else None }

        "FSharp.Data.sln" |> DotNet.test setParams)

    // --------------------------------------------------------------------------------------
    // Build packages

    Target.create "Pack" (fun _ ->
        // Format the release notes
        let releaseNotes = release.Notes |> String.concat "\n"

        let properties =
            [ ("Version", release.NugetVersion)
              ("Authors", authors)
              ("PackageProjectUrl", packageProjectUrl)
              ("PackageTags", tags)
              ("RepositoryType", repositoryType)
              ("RepositoryUrl", repositoryUrl)
              ("PackageLicenseExpression", license)
              ("PackageReleaseNotes", releaseNotes)
              ("Summary", summary)
              ("PackageDescription", description) ]

        DotNet.pack
            (fun p ->
                { p with
                    Configuration = DotNet.BuildConfiguration.Release
                    OutputPath = Some "bin"
                    MSBuildParams =
                        { p.MSBuildParams with
                            Properties = properties
                            DisableInternalBinLog = true } })
            "FSharp.Data.sln")

    // --------------------------------------------------------------------------------------
    // Generate the documentation
    Target.create "GenerateDocs" (fun _ ->
        Shell.cleanDir ".fsdocs"

        // List of projects to include in documentation (excluding benchmark project)
        let docProjects =
            [ "src/FSharp.Data/FSharp.Data.fsproj"
              "src/FSharp.Data.DesignTime/FSharp.Data.DesignTime.fsproj"
              "src/FSharp.Data.Json.Core/FSharp.Data.Json.Core.fsproj"
              "src/FSharp.Data.Csv.Core/FSharp.Data.Csv.Core.fsproj"
              "src/FSharp.Data.Html.Core/FSharp.Data.Html.Core.fsproj"
              "src/FSharp.Data.Http/FSharp.Data.Http.fsproj"
              "src/FSharp.Data.Runtime.Utilities/FSharp.Data.Runtime.Utilities.fsproj"
              "src/FSharp.Data.Xml.Core/FSharp.Data.Xml.Core.fsproj"
              "src/FSharp.Data.WorldBank.Core/FSharp.Data.WorldBank.Core.fsproj" ]

        let projectArgs = docProjects |> String.concat " "

        let result =
            DotNet.exec
                id
                "fsdocs"
                ("build --projects "
                 + projectArgs
                 + " --properties Configuration=Release --strict --eval --clean --parameters fsdocs-package-version "
                 + release.NugetVersion)

        if not result.OK then
            printfn "Errors while generating docs: %A" result.Messages
            failwith "Failed to generate docs")

    // --------------------------------------------------------------------------------------
    // Help

    Target.create "Help" (fun _ ->
        printfn ""
        printfn "  Please specify the target by calling 'build -t <Target>'"
        printfn ""
        printfn "  Targets for building:"
        printfn "  * Build"
        printfn "  * RunTests"
        printfn "  * GenerateDocs"
        printfn "  * Pack (creates package only, doesn't publish)"
        printfn "  * All (calls previous 5)"
        printfn ""
        printfn "  Other targets:"
        printfn "  * CleanInternetCaches"
        printfn "  * Format"
        printfn "  * CheckFormat"
        printfn "")

    let sourceFiles =
        !!"src/**/*.fs" ++ "src/**/*.fsi" ++ "build/build.fs"
        -- "src/**/obj/**/*.fs"
        -- "src/AssemblyInfo*.fs"

    Target.create "Format" (fun _ ->
        let result =
            sourceFiles
            |> Seq.map (sprintf "\"%s\"")
            |> String.concat " "
            |> DotNet.exec id "fantomas"

        if not result.OK then
            printfn "Errors while formatting all files: %A" result.Messages)

    Target.create "CheckFormat" (fun _ ->
        let result =
            sourceFiles
            |> Seq.map (sprintf "\"%s\"")
            |> String.concat " "
            |> sprintf "%s --check"
            |> DotNet.exec id "fantomas"

        if result.ExitCode = 0 then
            Trace.log "No files need formatting"
        elif result.ExitCode = 99 then
            failwith
                "Some files need formatting, run `dotnet run --project build/build.fsproj -- -t Format` to format them"
        else
            Trace.logf "Errors while formatting: %A" result.Errors
            failwith "Unknown errors while formatting")

    Target.create "RunBenchmarks" (fun _ ->
        let benchmarkProject =
            "tests" </> "FSharp.Data.Benchmarks" </> "FSharp.Data.Benchmarks.fsproj"

        DotNet.build
            (fun opts ->
                { opts with
                    Configuration = DotNet.BuildConfiguration.Release })
            benchmarkProject

        let benchmarkDir = "tests" </> "FSharp.Data.Benchmarks"

        Shell.Exec("dotnet", "run -c Release -- --job dry --filter \"*ParseSimpleJson*\"", benchmarkDir)
        |> fun exitCode ->
            if exitCode <> 0 then
                failwith "Benchmark execution failed")

    Target.create "All" ignore

    "Clean" ==> "AssemblyInfo" ==> "CheckFormat" ==> "Build"

    "Build" ==> "CleanDocs" ==> "GenerateDocs" ==> "All"

    "Build" ==> "Pack" ==> "All"
    "Build" ==> "All"
    "Build" ==> "RunTests" ==> "All"
    "Build" ==> "RunBenchmarks"

    Target.runOrDefaultWithArguments "Help"

[<EntryPoint>]
let main argv =
    argv
    |> Array.toList
    |> Context.FakeExecutionContext.Create false "build.fsx"
    |> Context.RuntimeContext.Fake
    |> Context.setExecutionContext

    buildscript ()
    Target.runOrDefaultWithArguments "build"
    0
