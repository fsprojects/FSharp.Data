module FSharp.Data.Tests.HttpContentTypes

open NUnit.Framework
open FsUnit
open FSharp.Data

[<Test>]
let ``HttpContentTypes.Any should return correct MIME type`` () =
    HttpContentTypes.Any |> should equal "*/*"

[<Test>]
let ``HttpContentTypes.Text should return correct MIME type`` () =
    HttpContentTypes.Text |> should equal "text/plain"

[<Test>]
let ``HttpContentTypes.Binary should return correct MIME type`` () =
    HttpContentTypes.Binary |> should equal "application/octet-stream"

[<Test>]
let ``HttpContentTypes.Zip should return correct MIME type`` () =
    HttpContentTypes.Zip |> should equal "application/zip"

[<Test>]
let ``HttpContentTypes.GZip should return correct MIME type`` () =
    HttpContentTypes.GZip |> should equal "application/gzip"

[<Test>]
let ``HttpContentTypes.FormValues should return correct MIME type`` () =
    HttpContentTypes.FormValues |> should equal "application/x-www-form-urlencoded"

[<Test>]
let ``HttpContentTypes.Json should return correct MIME type`` () =
    HttpContentTypes.Json |> should equal "application/json"

[<Test>]
let ``HttpContentTypes.JavaScript should return correct MIME type`` () =
    HttpContentTypes.JavaScript |> should equal "application/javascript"

[<Test>]
let ``HttpContentTypes.Xml should return correct MIME type`` () =
    HttpContentTypes.Xml |> should equal "application/xml"

[<Test>]
let ``HttpContentTypes.Rss should return correct MIME type`` () =
    HttpContentTypes.Rss |> should equal "application/rss+xml"

[<Test>]
let ``HttpContentTypes.Atom should return correct MIME type`` () =
    HttpContentTypes.Atom |> should equal "application/atom+xml"

[<Test>]
let ``HttpContentTypes.Rdf should return correct MIME type`` () =
    HttpContentTypes.Rdf |> should equal "application/rdf+xml"

[<Test>]
let ``HttpContentTypes.Html should return correct MIME type`` () =
    HttpContentTypes.Html |> should equal "text/html"

[<Test>]
let ``HttpContentTypes.XHtml should return correct MIME type`` () =
    HttpContentTypes.XHtml |> should equal "application/xhtml+xml"

[<Test>]
let ``HttpContentTypes.Soap should return correct MIME type`` () =
    HttpContentTypes.Soap |> should equal "application/soap+xml"

[<Test>]
let ``HttpContentTypes.Csv should return correct MIME type`` () =
    HttpContentTypes.Csv |> should equal "text/csv"

[<Test>]
let ``HttpContentTypes.JsonRpc should return correct MIME type`` () =
    HttpContentTypes.JsonRpc |> should equal "application/json-rpc"