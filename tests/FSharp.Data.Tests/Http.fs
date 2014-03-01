#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.Http
#endif

open FsUnit
open NUnit.Framework
open System
open FSharp.Data

[<Test>]
let ``Don't throw exceptions on http error`` () = 
    let response = Http.Request("http://api.themoviedb.org/3/search/movie", dontThrowOnHttpError = true)
    response.StatusCode |> should equal 401
    response.Body |> should equal (HttpResponseBody.Text """{"status_code":7,"status_message":"Invalid API key - You must be granted a valid key"}""")

[<Test>]
let ``Throw exceptions on http error`` () = 
    let exn =
        try
            Http.RequestString("http://api.themoviedb.org/3/search/movie") |> ignore
            ""
        with e -> 
            e.Message
    exn |> should contain """{"status_code":7,"status_message":"Invalid API key - You must be granted a valid key"}"""
