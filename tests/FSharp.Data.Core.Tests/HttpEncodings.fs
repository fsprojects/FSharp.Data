module FSharp.Data.Tests.HttpEncodings

open FsUnit
open NUnit.Framework
open System.Text
open FSharp.Data

[<Test>]
let ``HttpEncodings.PostDefaultEncoding returns ISO-8859-1`` () =
    HttpEncodings.PostDefaultEncoding.WebName |> should equal "iso-8859-1"

[<Test>]
let ``HttpEncodings.ResponseDefaultEncoding returns UTF-8`` () =
    HttpEncodings.ResponseDefaultEncoding.WebName |> should equal "utf-8"

[<Test>]
let ``HttpEncodings.getEncoding with valid encoding name works`` () =
    let utf8Encoding = HttpEncodings.getEncoding "utf-8"
    utf8Encoding.WebName |> should equal "utf-8"

[<Test>]
let ``HttpEncodings.getEncoding with codepage number works`` () =
    let utf8Encoding = HttpEncodings.getEncoding "65001"
    utf8Encoding.WebName |> should equal "utf-8"

[<Test>]
let ``HttpEncodings.getEncoding with ASCII encoding name works`` () =
    let asciiEncoding = HttpEncodings.getEncoding "ascii"
    asciiEncoding.WebName |> should equal "us-ascii"

[<Test>]
let ``HttpEncodings.getEncoding with ISO-8859-1 encoding name works`` () =
    let iso88591 = HttpEncodings.getEncoding "iso-8859-1"
    iso88591.WebName |> should equal "iso-8859-1"

[<Test>]
let ``HttpEncodings.getEncoding with UTF-16 codepage works`` () =
    let utf16 = HttpEncodings.getEncoding "1200"
    utf16.WebName |> should equal "utf-16"

[<Test>]
let ``HttpEncodings.getEncoding with invalid encoding name throws`` () =
    (fun () -> HttpEncodings.getEncoding "invalid-encoding-name" |> ignore)
    |> should throw typeof<System.ArgumentException>

[<Test>]
let ``HttpEncodings.getEncoding with invalid codepage number throws`` () =
    (fun () -> HttpEncodings.getEncoding "99999" |> ignore)
    |> should throw typeof<System.ArgumentOutOfRangeException>