// --------------------------------------------------------------------------------------
// Tests for Http Utilities (mainly HttpUtility.JavaScriptStringEncode now)
// --------------------------------------------------------------------------------------

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.UriUtility
#endif

open FsUnit
open NUnit.Framework
open System
open FSharp.Data

let uri = new Uri("http://www.myapi.com/%2F?Foo=Bar%2F#frag") |> UriUtils.enableUriSlashes

[<Test>]
let ``ToString contains escaped slashes`` () =
    uri.ToString() |> should equal "http://www.myapi.com/%2F?Foo=Bar%2F#frag"

[<Test>]
let ``AbsoluteUri contains escaped slashes`` () =
    uri.AbsoluteUri |> should equal "http://www.myapi.com/%2F?Foo=Bar%2F#frag"

[<Test>]
let ``Query contains escaped slashes`` () =
    uri.Query |> should equal "?Foo=Bar%2F"

[<Test>]
let ``PathAndQuery contains escaped slashes`` () =
    uri.PathAndQuery |> should equal "/%2F?Foo=Bar%2F"

[<Test>]
let ``AbsolutePath contains escaped slashes`` () =
    uri.AbsolutePath |> should equal "/%2F"

[<Test>]
let ``Uri Fragment is properly set`` () = 
    uri.Fragment |> should equal "#frag"
