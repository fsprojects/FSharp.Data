#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.Http
#endif

open FsUnit
open NUnit.Framework
open System
open FSharp.Data
open FSharp.Data.HttpRequestHeaders

[<Test>]
let ``Don't throw exceptions on http error`` () = 
    let response = Http.Request("http://api.themoviedb.org/3/search/movie", silentHttpErrors = true)
    response.StatusCode |> should equal 401
    response.Body |> should equal (Text """{"status_code":7,"status_message":"Invalid API key: You must be granted a valid key."}""")

[<Test>]
let ``Throw exceptions on http error`` () = 
    let exn =
        try
            Http.RequestString("http://api.themoviedb.org/3/search/movie") |> ignore
            ""
        with e -> 
            e.Message
    exn |> should contain """{"status_code":7,"status_message":"Invalid API key: You must be granted a valid key."}"""

[<Test>]
let ``If the same header is added multiple times, throws an exception`` () =
    (fun () -> Http.RequestString("http://www.google.com", headers = [ UserAgent "ua1"; UserAgent "ua2" ]) |> ignore)
    |> should throw typeof<Exception>

[<Test>]
let ``If a custom header with the same name is added multiple times, an exception is thrown`` () =
    (fun () -> Http.RequestString("http://www.google.com", headers = [ "c1", "v1"; "c1", "v2" ]) |> ignore)
    |> should throw typeof<Exception>

[<Test>]
let ``Two custom header with different names don't throw an exception`` () =
    Http.RequestString("http://www.google.com", headers = [ "c1", "v1"; "c2", "v2" ]) |> ignore

[<Test>]
let ``A request with an invalid url throws an exception`` () =
    (fun() -> Http.Request "www.google.com" |> ignore) |> should throw typeof<UriFormatException>

