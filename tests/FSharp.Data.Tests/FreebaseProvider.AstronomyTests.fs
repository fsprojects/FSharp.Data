module FSharp.Data.Tests.FreebaseProvider.AstronomyTests

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

open NUnit.Framework
open FsUnit
open System
open System.Linq
open FSharp.Data

let data = FreebaseData.GetDataContext()

let astronomy = data.``Science and Technology``.Astronomy

[<Test>]
let ``Can execute > 1500 chars MQL query``() =
    let someStarDistances = 
        query { for e in astronomy.Stars do 
                where e.Distance.HasValue
                select (e.Name, e.Distance) } 
            |> Seq.toList
    someStarDistances |> Seq.head |> fst |> should equal "Arcturus"
