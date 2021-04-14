module FSharp.Data.Reference.Tests.Lib

open System.IO
open NUnit.Framework
open FsUnit

let value = """
[
    {
        "intLike"   : "123" ,
        "boolLike1" : 0     ,
        "boolLike2" : "1"
    },
    {
        "intLike"   : "321" ,
        "boolLike1" : 1     ,
        "boolLike2" : "0"
    }
]
"""

[<Test>]
let ``Can load JSON from embedded resource in referenced assembly`` () =
    let result = FSharp.Data.Tests.JsonProvider.EmbeddedResourceProvider.Parse value
    result
    |> Seq.map (fun v -> v.IntLike)
    |> Seq.sort
    |> Seq.toList
    |> should equal [123; 321]
