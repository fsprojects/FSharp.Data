<Query Kind="FSharpProgram">
  <GACReference>FSharp.Core, Version=4.3.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</GACReference>
  <Reference>&lt;ProgramFilesX86&gt;\Reference Assemblies\Microsoft\FSharp\.NETFramework\v4.0\4.3.0.0\FSharp.Core.dll</Reference>
  <NuGetReference>FSharp.Data</NuGetReference>
</Query>

open FSharp.Data

// Beware of rate limiting while running this sample: https://developer.github.com/v3/#rate-limiting
type GitHub = JsonProvider<"https://api.github.com/repos/fsharp/FSharp.Data/issues">

let topRecentlyUpdatedIssues = 
    GitHub.GetSamples()
    |> Seq.filter (fun issue -> issue.State = "open")
    |> Seq.sortBy (fun issue -> System.DateTime.Now - issue.UpdatedAt)
    |> Seq.truncate 5

for issue in topRecentlyUpdatedIssues do
    printfn "#%d %s" issue.Number issue.Title