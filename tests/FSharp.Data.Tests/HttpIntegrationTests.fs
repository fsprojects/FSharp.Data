module FSharp.Data.Tests.HttpIntegrationTests

open System
open System.IO
open System.Net
open System.Net.Cache
open System.Text
open NUnit.Framework
open FsUnit
open Nancy
open Nancy.Hosting.Self
open FSharp.Data
open FSharp.Data.HttpRequestHeaders

// ? operator to get values from a Nancy DynamicDictionary
let (?) (parameters:obj) param =
    (parameters :?> Nancy.DynamicDictionary).[param]
 
let config = HostConfiguration()
config.UrlReservations.CreateAutomatically <- true
let nancyHost = new NancyHost(config, Uri("http://localhost:1235/TestServer/"))

let runningOnMono = Type.GetType("Mono.Runtime") <> null

[<TestFixtureSetUp>]
let fixtureSetup() =
    nancyHost.Start()

[<TestFixtureTearDown>]
let fixtureTearDown() =
    nancyHost.Stop()

[<SetUp>]
let setUp() =
    MockServer.recordedRequest := null

[<Test>] 
let ``should set everything correctly in the HTTP request`` ()=
    Http.Request("http://localhost:1235/TestServer/RecordRequest",
                 query = [ "search", "jeebus"; "qs2", "hi mum" ],
                 headers = [ Accept "application/xml" ],
                 cookies = ["SESSIONID", "1234"],
                 body = TextRequest "some JSON or whatever") |> ignore
    MockServer.recordedRequest.Value |> should notEqual null
    MockServer.recordedRequest.Value.Query?search.ToString() |> should equal "jeebus"
    MockServer.recordedRequest.Value.Query?qs2.ToString() |> should equal "hi mum"
    MockServer.recordedRequest.Value.Headers.Accept |> should contain ("application/xml", 1m)
    MockServer.recordedRequest.Value.Cookies.["SESSIONID"] |> should contain "1234"
    use bodyStream = new StreamReader(MockServer.recordedRequest.Value.Body,Encoding.GetEncoding(1252))
    bodyStream.ReadToEnd() |> should equal "some JSON or whatever"

[<Test>]
let ``should return the http status code for all response types`` () =
    Http.Request("http://localhost:1235/TestServer/GoodStatusCode").StatusCode |> should equal 200
    Http.Request("http://localhost:1235/TestServer/BadStatusCode", silentHttpErrors=true).StatusCode |> should equal 401

[<Test>]
let ``should return the entity body as a string`` () =
    Http.RequestString "http://localhost:1235/TestServer/GotBody" |> should equal "My body"

[<Test>]
let ``should return an empty string when there is no body`` () =
    Http.RequestString "http://localhost:1235/TestServer/GoodStatusCode" |> should equal ""

[<Test>]
let ``all details of the response should be available`` () =
    let response = Http.Request("http://localhost:1235/TestServer/AllTheThings", silentHttpErrors=true)
    response.StatusCode |> should equal 418
    response.Body |> should equal (Text "Some JSON or whatever")
    response.Cookies.["cookie1"] |> should equal "chocolate+chip" // cookies get encoded
    response.Cookies.["cookie2"] |> should equal "smarties"
    response.Headers.[HttpResponseHeaders.ContentEncoding] |> should equal "xpto"
    response.Headers.["X-New-Fangled-Header"] |> should equal "some value"

[<Test>]
let ``when called on a non-existant page returns 404`` () =
    Http.Request("http://localhost:1235/TestServer/NoPage", silentHttpErrors=true).StatusCode |> should equal 404

[<Test>]
[<Platform("Net")>]
let ``all of the manually-set request headers get sent to the server`` ()=
    Http.Request("http://localhost:1235/TestServer/RecordRequest",
                 headers = [ "accept", "application/xml,text/html;q=0.3"
                             AcceptCharset "utf-8, utf-16;q=0.5" 
                             AcceptDatetime (DateTime(2007,5,31,20,35,0))
                             AcceptLanguage "en-GB, en-US;q=0.1"
                             Authorization  "QWxhZGRpbjpvcGVuIHNlc2FtZQ==" 
                             Connection "conn1"
                             ContentMD5 "Q2hlY2sgSW50ZWdyaXR5IQ=="
                             ContentType "application/json"
                             Date (DateTime(1999, 12, 31, 11, 59, 59))
                             Expect "100"
                             From "user@example.com"
                             IfMatch "737060cd8c284d8af7ad3082f209582d"
                             IfModifiedSince (DateTime(2000, 12, 31, 11, 59, 59))
                             IfNoneMatch "737060cd8c284d8af7ad3082f209582d"
                             IfRange "737060cd8c284d8af7ad3082f209582d"
                             MaxForwards 5
                             Origin "http://www.mybot.com"
                             Pragma "no-cache"
                             ProxyAuthorization "QWxhZGRpbjpvcGVuIHNlc2FtZQ=="
                             Range(0L, 500L)
                             Referer "http://en.wikipedia.org/"
                             Upgrade "HTTP/2.0, SHTTP/1.3"
                             UserAgent "(X11; Linux x86_64; rv:12.0) Gecko/20100101 Firefox/21.0"
                             Via "1.0 fred, 1.1 example.com (Apache/1.1)"
                             Warning "199 Miscellaneous warning"
                             "X-Greeting", "Happy Birthday" ]) |> ignore

    MockServer.recordedRequest.Value |> should notEqual null
    MockServer.recordedRequest.Value.Headers.Accept |> should contain ("application/xml", 1m)
    MockServer.recordedRequest.Value.Headers.Accept |> should contain ("text/html", 0.3m)
    MockServer.recordedRequest.Value.Headers.AcceptCharset |> should contain ("utf-8", 1m)
    MockServer.recordedRequest.Value.Headers.AcceptCharset |> should contain ("utf-16", 0.5m)
    MockServer.recordedRequest.Value.Headers.["Accept-Datetime"] |> should equal ["Thu, 31 May 2007 20:35:00 GMT"]
    MockServer.recordedRequest.Value.Headers.AcceptLanguage |> should contain ("en-GB", 1m)
    MockServer.recordedRequest.Value.Headers.AcceptLanguage |> should contain ("en-US", 0.1m)
    MockServer.recordedRequest.Value.Headers.Authorization |> should equal "QWxhZGRpbjpvcGVuIHNlc2FtZQ=="
    MockServer.recordedRequest.Value.Headers.Connection |> should equal "conn1"
    MockServer.recordedRequest.Value.Headers.["Content-MD5"] |> should equal ["Q2hlY2sgSW50ZWdyaXR5IQ=="]
    MockServer.recordedRequest.Value.Headers.ContentType |> should equal "application/json"
    MockServer.recordedRequest.Value.Headers.Date.Value |> should equal (DateTime(1999, 12, 31, 11, 59, 59))
    MockServer.recordedRequest.Value.Headers.["Expect"] |> should equal ["100"]
    MockServer.recordedRequest.Value.Headers.["From"] |> should equal ["user@example.com"]
    MockServer.recordedRequest.Value.Headers.IfMatch |> should equal ["737060cd8c284d8af7ad3082f209582d"]
    MockServer.recordedRequest.Value.Headers.IfModifiedSince |> should equal (DateTime(2000, 12, 31, 11, 59, 59))
    MockServer.recordedRequest.Value.Headers.IfNoneMatch |> should equal ["737060cd8c284d8af7ad3082f209582d"]
    MockServer.recordedRequest.Value.Headers.IfRange |> should equal "737060cd8c284d8af7ad3082f209582d"
    MockServer.recordedRequest.Value.Headers.MaxForwards |> should equal 5
    MockServer.recordedRequest.Value.Headers.["Origin"] |> should equal ["http://www.mybot.com"]
    MockServer.recordedRequest.Value.Headers.["Pragma"] |> should equal ["no-cache"]
    MockServer.recordedRequest.Value.Headers.["Proxy-Authorization"] |> should equal ["QWxhZGRpbjpvcGVuIHNlc2FtZQ=="]
    MockServer.recordedRequest.Value.Headers.["Range"] |> should equal ["bytes=0-500"]
    MockServer.recordedRequest.Value.Headers.["Referer"] |> should equal ["http://en.wikipedia.org/"]
    MockServer.recordedRequest.Value.Headers.["Upgrade"] |> should contain "HTTP/2.0"
    MockServer.recordedRequest.Value.Headers.["Upgrade"] |> should contain "SHTTP/1.3" 
    MockServer.recordedRequest.Value.Headers.UserAgent |> should equal "(X11; Linux x86_64; rv:12.0) Gecko/20100101 Firefox/21.0"
    MockServer.recordedRequest.Value.Headers.["Via"] |> should contain ("1.0 fred")
    MockServer.recordedRequest.Value.Headers.["Via"] |> should contain ("1.1 example.com (Apache/1.1)")
    MockServer.recordedRequest.Value.Headers.["Warning"] |> should equal ["199 Miscellaneous warning"]
    MockServer.recordedRequest.Value.Headers.["X-Greeting"] |> should equal ["Happy Birthday"]

[<Test>]
let ``Encoding from content-type used`` () =
    Http.Request(
        "http://localhost:1235/TestServer/RecordRequest", 
        body = TextRequest "Hi Müm", 
        headers = [ ContentType "application/bike; charset=utf-8"]) |> ignore
    MockServer.recordedRequest.Value |> should notEqual null
    use bodyStream = new StreamReader(MockServer.recordedRequest.Value.Body,Encoding.GetEncoding("utf-8"))
    bodyStream.ReadToEnd() |> should equal "Hi Müm"
    MockServer.recordedRequest.Value.Headers.ContentLength |> should equal 7

[<Test>]
let ``Content-Length header is set automatically for Posts with a body`` () =
    Http.Request("http://localhost:1235/TestServer/RecordRequest", body = TextRequest "Hi Mum") |> ignore
    MockServer.recordedRequest.Value |> should notEqual null
    MockServer.recordedRequest.Value.Headers.ContentLength |> should equal 6

[<Test>]
let ``accept-encoding header is set automatically when decompression scheme is set`` () =
    Http.Request "http://localhost:1235/TestServer/RecordRequest" |> ignore
    MockServer.recordedRequest.Value |> should notEqual null
    MockServer.recordedRequest.Value.Headers.AcceptEncoding |> should contain "gzip"
    MockServer.recordedRequest.Value.Headers.AcceptEncoding |> should contain "deflate"

open FSharp.Data.HttpResponseHeaders

[<Test>]
let ``all of the response headers are available`` () =
    let response = Http.Request "http://localhost:1235/TestServer/AllHeaders" 
    response.Headers.[AccessControlAllowOrigin] |> should equal "*"
    response.Headers.[AcceptRanges] |> should equal "bytes"
    response.Headers.[Age] |> should equal "12"
    response.Headers.[Allow] |> should equal "GET, HEAD"
    response.Headers.[CacheControl] |> should equal "max-age=3600"
    //response.Headers.[Connection] |> should equal "close" // don't seem to get connection header from nancy
    response.Headers.[ContentEncoding] |> should equal "blah"
    response.Headers.[ContentLanguage] |> should equal "EN-gb"
    response.Headers.[ContentLocation] |> should equal "/index.htm"
    response.Headers.[ContentMD5] |> should equal "Q2hlY2sgSW50ZWdyaXR5IQ=="
    response.Headers.[ContentDisposition] |> should equal "attachment; filename=\"fname.ext\""
    response.Headers.[ContentRange] |> should equal "bytes 21010-47021/47022"
    response.Headers.[ContentType] |> should equal "text/html; charset=utf-8"
    let (parsedOK,_) = DateTime.TryParse(response.Headers.[Date])
    parsedOK |> should equal true
    response.Headers.[ETag] |> should equal "737060cd8c284d8af7ad3082f209582d"
    response.Headers.[Expires] |> should equal "Thu, 01 Dec 1994 16:00:00 GMT"
    response.Headers.[LastModified] |> should equal "Tue, 15 Nov 1994 12:45:26 +0000"
    response.Headers.[Link] |> should equal "</feed>; rel=\"alternate\""
    response.Headers.[Location] |> should equal "http://www.w3.org/pub/WWW/People.html"
    response.Headers.[P3P] |> should equal "CP=\"your_compact_policy\""
    response.Headers.[Pragma] |> should equal "no-cache"
    response.Headers.[ProxyAuthenticate] |> should equal "Basic"
    response.Headers.[Refresh] |> should equal "5; url=http://www.w3.org/pub/WWW/People.html"
    response.Headers.[RetryAfter] |> should equal "120"
    if runningOnMono then
        response.Headers.[Server] |> should equal "Mono-HTTPAPI/1.0"
    else
        response.Headers.[Server] |> should equal "Microsoft-HTTPAPI/2.0"
    response.Headers.[StrictTransportSecurity] |> should equal "max-age=16070400; includeSubDomains"
    response.Headers.[Trailer] |> should equal "Max-Forwards"
    response.Headers.[TransferEncoding] |> should equal "chunked"
    response.Headers.[Vary] |> should equal "*"
    response.Headers.[Via] |> should equal "1.0 fred, 1.1 example.com (Apache/1.1)"
    response.Headers.[Warning] |> should equal "199 Miscellaneous warning"
    response.Headers.[WWWAuthenticate] |> should equal "Basic"
    response.Headers.["X-New-Fangled-Header"] |> should equal "some value"

[<Test>]
let ``if a response character encoding is specified, that encoding is used regardless of what the response content-type specifies`` () =
    let response = Http.Request("http://localhost:1235/TestServer/MoonLanguageCorrectEncoding", responseEncodingOverride="utf-16")
    response.Body |> should equal (Text "迿ꞧ쒿") // "яЏ§§їДЙ" (as encoded with windows-1251) decoded with utf-16

[<Test>]
let ``if an invalid response character encoding is specified, an exception is thrown`` () =
    (fun() -> Http.Request("http://localhost:1235/TestServer/MoonLanguageCorrectEncoding", responseEncodingOverride="gibberish") |> ignore) 
    |> should throw typeof<ArgumentException>

[<Test>]
let ``if a response character encoding is NOT specified, the body is read using the character encoding specified in the response's content-type header`` () =
    let response = Http.Request "http://localhost:1235/TestServer/MoonLanguageCorrectEncoding" 
    response.Body |> should equal (Text "яЏ§§їДЙ")

[<Test>]
let ``if a response character encoding is NOT specified, and character encoding is NOT specified in the response's content-type header, the body is read using ISO Latin 1 character encoding`` () =
    let response = Http.Request "http://localhost:1235/TestServer/MoonLanguageNoEncoding" 
    response.Body |> should equal (Text "ÿ§§¿ÄÉ") // "яЏ§§їДЙ" (as encoded with windows-1251) decoded with ISO-8859-1 (Latin 1)

[<Test>]
let ``if a response character encoding is NOT specified, and the character encoding specified in the response's content-type header is invalid, an exception is thrown`` () =
    (fun() -> Http.Request "http://localhost:1235/TestServer/MoonLanguageInvalidEncoding"  |> ignore) 
    |> should throw typeof<ArgumentException>
