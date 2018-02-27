// --------------------------------------------------------------------------------------
// Tests for Http Utilities (mainly HttpUtility.JavaScriptStringEncode now)
// --------------------------------------------------------------------------------------

#if INTERACTIVE
#r "../../bin/net45/FSharp.Data.dll"
#r "../../packages/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/FsUnit/lib/net46/FsUnit.NUnit.dll"
#else
module FSharp.Data.Tests.UriUtility
#endif

open FsUnit
open NUnit.Framework
open System
open FSharp.Data.Runtime

let runningOnMono = try System.Type.GetType("Mono.Runtime") <> null with e -> false 
let uri = new Uri("http://www.myapi.com/%2F?Foo=Bar%2F#frag") |> UriUtils.enableUriSlashes

[<Test>]
//[<Platform("Net")>]
let ``ToString contains escaped slashes`` () =
  if not runningOnMono then 
    uri.ToString() |> should equal "http://www.myapi.com/%2F?Foo=Bar%2F#frag"

[<Test>]
//[<Platform("Net")>]
let ``AbsoluteUri contains escaped slashes`` () =
  if not runningOnMono then 
    uri.AbsoluteUri |> should equal "http://www.myapi.com/%2F?Foo=Bar%2F#frag"

[<Test>]
//[<Platform("Net")>]
let ``Query contains escaped slashes`` () =
  if not runningOnMono then 
    uri.Query |> should equal "?Foo=Bar%2F"

[<Test>]
//[<Platform("Net")>]
let ``PathAndQuery contains escaped slashes`` () =
  if not runningOnMono then 
    uri.PathAndQuery |> should equal "/%2F?Foo=Bar%2F"

[<Test>]
//[<Platform("Net")>]
let ``AbsolutePath contains escaped slashes`` () =
  if not runningOnMono then 
    uri.AbsolutePath |> should equal "/%2F"

[<Test>]
//[<Platform("Net")>]
let ``Uri Fragment is properly set`` () = 
  if not runningOnMono then 
    uri.Fragment |> should equal "#frag"

[<Test>]
//[<Platform("Net")>]
let ``Uri's with fragment but no query work correctly`` () =
  if not runningOnMono then 
    let uri = new Uri("http://www.google.com/#1") |> UriUtils.enableUriSlashes
    uri.ToString() |> should equal "http://www.google.com/#1"
    uri.AbsoluteUri |> should equal "http://www.google.com/#1"
    uri.Query |> should equal ""
    uri.Fragment |> should equal "#1"
    uri.PathAndQuery |> should equal "/"
    uri.AbsolutePath |> should equal "/"
