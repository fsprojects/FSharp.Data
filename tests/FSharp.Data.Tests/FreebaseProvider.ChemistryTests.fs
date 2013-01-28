module FSharp.Data.Tests.FreebaseProvider.ChemistryTests

open NUnit.Framework
open FsUnit
open FSharp.Data

let data = FreebaseData.GetDataContext()

let elements = data.``Science and Technology``.Chemistry.``Chemical Elements``

[<Test>]
let ``Can access the symbol for hydrogen``() =
    let hydrogen = elements.Individuals.Hydrogen
    hydrogen.Symbol |> should equal "H"