module FSharp.Data.Tests.XmlProvider.ProjectFile.Tests

open NUnit.Framework
open FSharp.Data
open FsUnit

type Project = XmlProvider<"projects.xml">

[<Test>]
let ``Can access the background title``() =
    let doc = Project.GetSample()
    let background = doc.Backgrounds.Background
    background.Title |> should equal "purple stars"

[<Test>]
let ``Can access the project title``() =
    let doc = Project.GetSample()
    let project = doc.Project
    project.Title |> should equal "Avery"