<Query Kind="FSharpProgram">
  <GACReference>FSharp.Core, Version=4.3.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</GACReference>
  <Reference>&lt;ProgramFilesX86&gt;\Reference Assemblies\Microsoft\FSharp\.NETFramework\v4.0\4.3.0.0\FSharp.Core.dll</Reference>
  <NuGetReference>FSharp.Data</NuGetReference>
</Query>

open FSharp.Data

[<Literal>] 
let apiUrl = "https://jira.atlassian.com/rest/api/2/search?filter=-4"  // all issues
type Jira = JsonProvider<apiUrl>
let jira = Jira.Load(apiUrl)

let tickets = jira.Issues |> Array.map (fun ticket -> (ticket.Id, ticket.Fields.Summary))
tickets |> Dump