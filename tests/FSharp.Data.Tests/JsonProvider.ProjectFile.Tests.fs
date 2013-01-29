module FSharp.Data.Tests.JsonProvider.ProjectFile.Tests

open NUnit.Framework
open FSharp.Data
open FsUnit

type Project = JsonProvider<"Data/projects.json">

[<Test>]
let ``Can access the background title``() =
    let doc = Project.GetSample()
    let background = doc.Ordercontainer.Backgrounds.Background
    let title = background.Title
    title.Text |> should equal "purple stars"

[<Test>]
let ``Can access the project title``() =
    let doc = Project.GetSample()
    let project = doc.Ordercontainer.Project
    let title = project.Title
    title.Text |> should equal "Avery"