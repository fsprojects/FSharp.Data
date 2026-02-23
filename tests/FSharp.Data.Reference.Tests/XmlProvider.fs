module FSharp.Data.Reference.Tests.XmlProvider

open NUnit.Framework
open FsUnit

[<Test>]
let ``GetSchema works for XmlProvider with Schema and EmbeddedResource`` () =
    let schema = FSharp.Data.Tests.XmlProvider.XmlSchemaWithEmbeddedResource.GetSchema()
    schema.Count |> should equal 1
