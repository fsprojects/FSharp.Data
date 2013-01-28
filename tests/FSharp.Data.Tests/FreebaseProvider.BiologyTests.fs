module FSharp.Data.Tests.FreebaseProvider.BiologyTests

open NUnit.Framework
open FsUnit
open System
open System.Linq
open FSharp.Data

let data = FreebaseData.GetDataContext()

let aminoAcids = data.``Science and Technology``.Biology.``Amino Acids``

[<Test>]
let ``Can access the first 10 amino acids``() =
    let q = query {
        for acid in aminoAcids do
        take 10
        select (acid.Name, String.Join(" ", acid.Blurb.ToArray())) }
    let a = q.ToArray()
    a.Count() |> should equal 10