module FSharp.Data.Tests.Http

open FsUnit
open NUnit.Framework
open System
open System.IO
open System.Net
open FSharp.Data
open FSharp.Data.HttpRequestHeaders
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System.Net.NetworkInformation

type ITestHttpServer =
    inherit IDisposable
    abstract member BaseAddress: string
    abstract member WorkerTask: Task

let startHttpLocalServer() =
    let app = WebApplication.CreateBuilder().Build()
    app.Map("/{status}", (fun (ctx: HttpContext) ->
        async {
            match ctx.Request.RouteValues.TryGetValue("status") with
            | true, (:? string as status) ->
                let status = status |> int

                match ctx.Request.Query.TryGetValue("sleep") with
                | true, values when values.Count = 1 ->
                    let value = values[0] |> int
                    do! Async.Sleep value
                | _ -> ()

                if ctx.Request.Body <> null then
                    let buffer = Array.create 8192 (byte 0)
                    let mutable read = -1
                    while read <> 0 do
                        let! x = ctx.Request.Body.ReadAsync(buffer, 0, 8192) |> Async.AwaitTask
                        read <- x

                Results.StatusCode(status).ExecuteAsync(ctx)
                |> Async.AwaitTask
                |> ignore
            | _ -> failwith "Unexpected request."
                
        } |> Async.StartAsTask :> Task
        )) |> ignore

    let freePort =
        let random = new System.Random()
        let mutable port = random.Next(10000, 65000) // Use a random high port instead of a fixed port
        while
            IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
            |> Array.map (fun x -> x.Port)
            |> Array.contains port do
                port <- random.Next(10000, 65000)
        port

    let baseAddress = $"http://localhost:{freePort}"

    let workerTask = app.RunAsync(baseAddress)
    printfn $"Started local http server with address {baseAddress}"

    { new ITestHttpServer with
        member this.Dispose() =
            app.StopAsync() |> Async.AwaitTask |> ignore
            printfn $"Stopped local http server with address {baseAddress}"
        member this.WorkerTask = workerTask
        member this.BaseAddress = baseAddress }

[<Test>]
let ``Don't throw exceptions on http error`` () =
    use localServer = startHttpLocalServer()
    let response = Http.Request(localServer.BaseAddress + "/401", silentHttpErrors = true)
    response.StatusCode |> should equal 401

[<Test>]
let ``Throw exceptions on http error`` () =
    use localServer = startHttpLocalServer()
    let exceptionThrown =
        try
            Http.RequestString(localServer.BaseAddress + "/401") |> ignore
            false
        with e ->
            true

    exceptionThrown |> should equal true

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

[<Test>]
let ``Cookies with commas are parsed correctly`` () =
    let uri = Uri "http://www.nasdaq.com/symbol/ibm/dividend-history"
    let cookieHeader = "selectedsymboltype=IBM,COMMON STOCK,NYSE; domain=.nasdaq.com; expires=Sun, 21-May-2017 15:29:03 GMT; path=/,selectedsymbolindustry=IBM,technology; domain=.nasdaq.com; expires=Sun, 21-May-2017 15:29:03 GMT; path=/,NSC_W.TJUFEFGFOEFS.OBTEBR.80=ffffffffc3a08e3045525d5f4f58455e445a4a423660;expires=Sat, 21-May-2016 15:39:03 GMT;path=/;httponly"
    let cookies =
        CookieHandling.getAllCookiesFromHeader cookieHeader uri
        |> Array.map (snd >> (fun c -> c.Name, c.Value))
    cookies |> should equal
        [| "selectedsymboltype", "IBM,COMMON STOCK,NYSE"
           "selectedsymbolindustry", "IBM,technology"
           "NSC_W.TJUFEFGFOEFS.OBTEBR.80", "ffffffffc3a08e3045525d5f4f58455e445a4a423660" |]

[<Test>]
let ``Cookies with '=' are parsed correctly`` () =
    let uri = Uri "http://nevermind.com"
    let cookieHeader = "IdSession=Qze9H7HpvcVoh/ANulOl6Z1P8Omd1cLb9FOLL5o2aOlDMn/dFJi+RWDAPHZ4nDmWeno2puyOTFM/P4yzZMQsPoJ1gvEaJqG54kerM6WW4bv6ql72/Tn3NnCZTaokm8uaboLICgckUM2J7KOx5iL8uGyTN/g04/jZKlP1HgyatQL6kCG4qCQUMrdqZjqkbgW3eCpeyeI9rXF1bNC8hsKaqJ37Du/oBvbIMUbgenogfjzmlCgtAzv4la2Eo8+3cvDHkKPnksCP8kt8JbyXECyXBOjPrpFjtYv9UUGfyhwqRTRNTmH5+5UAsDDFrYe+vonYiDwXel8TfK3AZhQGXcF598AVPVfB1RO5S/mt7faDS7cEfz14nUsYtaNAZcwH7gwm06VJUX5eWiZzlBGx4SVBkNzP0QhLM9AqNP889y9BmZ2JaGb3fJtCWL3MfzM23mbwSemcERkoV3v1rIH8mb6ZgGm0hyEbbtu/RegkLAgNO+YB6c0Os6Pv6OnK0So4xlNakchaWhl1eMfOf4Gx0miJv4o2XbmAmbSNYkybi3n8vz4="
    let cookies =
        CookieHandling.getAllCookiesFromHeader cookieHeader uri
        |> Array.map (snd >> (fun c -> c.Name, c.Value))
    cookies |> should equal
        [| "IdSession", "Qze9H7HpvcVoh/ANulOl6Z1P8Omd1cLb9FOLL5o2aOlDMn/dFJi+RWDAPHZ4nDmWeno2puyOTFM/P4yzZMQsPoJ1gvEaJqG54kerM6WW4bv6ql72/Tn3NnCZTaokm8uaboLICgckUM2J7KOx5iL8uGyTN/g04/jZKlP1HgyatQL6kCG4qCQUMrdqZjqkbgW3eCpeyeI9rXF1bNC8hsKaqJ37Du/oBvbIMUbgenogfjzmlCgtAzv4la2Eo8+3cvDHkKPnksCP8kt8JbyXECyXBOjPrpFjtYv9UUGfyhwqRTRNTmH5+5UAsDDFrYe+vonYiDwXel8TfK3AZhQGXcF598AVPVfB1RO5S/mt7faDS7cEfz14nUsYtaNAZcwH7gwm06VJUX5eWiZzlBGx4SVBkNzP0QhLM9AqNP889y9BmZ2JaGb3fJtCWL3MfzM23mbwSemcERkoV3v1rIH8mb6ZgGm0hyEbbtu/RegkLAgNO+YB6c0Os6Pv6OnK0So4xlNakchaWhl1eMfOf4Gx0miJv4o2XbmAmbSNYkybi3n8vz4="|]

[<Test>]
let ``An empty cookie header is parsed correctly`` () =
    let uri = Uri "https://news.google.com/news?hl=en&ned=us&ie=UTF8&nolr=1&output=rss&q=FSharp&num=1000"
    let cookieHeader = ""
    let cookies = CookieHandling.getAllCookiesFromHeader cookieHeader uri
    cookies |> should equal [| |]

[<Test>]
let ``Cookies in CookieContainer are returned`` () =
    let cookieContainer = CookieContainer()
    let someUri = Uri "http://nevermind.com"
    cookieContainer.Add(someUri, Cookie("key", "value"))
    let header = Map.empty
    let cookies = CookieHandling.getCookiesAndManageCookieContainer someUri someUri header cookieContainer true false
    cookies |> should haveCount cookieContainer.Count

[<Test>]
let ``Cookies in header are added to CookieContainer and returned`` () =
    let cookieContainer = CookieContainer()
    let someUri = Uri "http://nevermind.com"
    cookieContainer.Add(someUri, Cookie("key1", "value1"))
    let header = Map.ofList [(HttpResponseHeaders.SetCookie, ("key2=value2"))]
    let cookies = CookieHandling.getCookiesAndManageCookieContainer someUri someUri header cookieContainer true false
    cookieContainer.Count |> should equal 2
    cookies |> should haveCount cookieContainer.Count

[<Test>]
let ``Cookies in header already existing in CookieContainer are added twice, and only new value is returned`` () =
    let cookieContainer = CookieContainer()
    let someUri = Uri "http://nevermind.com"
    cookieContainer.Add(someUri, Cookie("key", "value1"))
    let header = Map.ofList [(HttpResponseHeaders.SetCookie, ("key=value2"))]
    let cookies = CookieHandling.getCookiesAndManageCookieContainer someUri someUri header cookieContainer true false
    cookieContainer.Count |> should equal 2
    cookies |> should haveCount 1
    cookies.["key"] |> should equal "value2"

[<Test>]
let ``Cookies with unescaped JSON raise a CookieException (need to avoid cookieContainer parameter or ignoreCookieErrors`` () =
    let uri = Uri "http://nevermind.com"
    let header = Map.ofList [(HttpResponseHeaders.SetCookie, "hab={\"echanges\":1,\"notifications\":1,\"messages\":1}")]
    let cookieContainer = CookieContainer()
    (fun () -> CookieHandling.getCookiesAndManageCookieContainer uri uri header cookieContainer true false |> ignore) |> should throw typeof<CookieException>

[<Test>]
let ``Cookies with unescaped JSON is not added in cookieContainer but is still returned when ignoreCookieErrors parameter is true`` () =
    let uri = Uri "http://nevermind.com"
    let header = Map.ofList [(HttpResponseHeaders.SetCookie, "hab={\"echanges\":1,\"notifications\":1,\"messages\":1}")]
    let cookieContainer = CookieContainer()
    let cookies = CookieHandling.getCookiesAndManageCookieContainer uri uri header cookieContainer true true
    cookieContainer.Count |> should equal 0
    cookies |> should haveCount 1

[<Test>]
let ``Cookies is not added in cookieContainer but is still returned when addCookieInCookieContainer parameter is false (deducted from option cookieContainer passed in InnerRequest method)`` () =
    let uri = Uri "http://nevermind.com"
    let header = Map.ofList [(HttpResponseHeaders.SetCookie, "hab={\"echanges\":1,\"notifications\":1,\"messages\":1}")]
    let cookieContainer = CookieContainer()
    let cookies = CookieHandling.getCookiesAndManageCookieContainer uri uri header cookieContainer false false
    cookieContainer.Count |> should equal 0
    cookies |> should haveCount 1

[<Test>]
let ``Web request's timeout is used`` () =
    use localServer = startHttpLocalServer()
    let exc = Assert.Throws<WebException> (fun () ->
        Http.Request(localServer.BaseAddress + "/200?sleep=1000", customizeHttpRequest = (fun req -> req.Timeout <- 1; req)) |> ignore)

    exc.Status |> should equal WebExceptionStatus.Timeout

[<Test>]
let ``Timeout argument is used`` () =
    use localServer = startHttpLocalServer()
    let exc = Assert.Throws<WebException> (fun () ->
        Http.Request(localServer.BaseAddress + "/200?sleep=1000", timeout = 1) |> ignore)

    exc.Status |> should equal WebExceptionStatus.Timeout

[<Test>]
let ``Setting timeout in customizeHttpRequest overrides timeout argument`` () =
    use localServer = startHttpLocalServer()
    let response =
        Http.Request(localServer.BaseAddress + "/401?sleep=1000", silentHttpErrors = true,
            customizeHttpRequest = (fun req -> req.Timeout <- Threading.Timeout.Infinite; req), timeout = 1)

    response.StatusCode |> should equal 401

let testFormDataSizesInBytes = [
    4000    // previous test size
    20000   // previous test size
    40000   // > 80k, reported by user @danyx23 on full-framework
    100000  // > 200k, reported by user danyx23 on .net core
    200000  // > 400k, just future-proofing
]

[<Test; TestCaseSource("testFormDataSizesInBytes")>]
let testFormDataBodySize (size: int) =
    use localServer = startHttpLocalServer()
    let bodyString = seq {for _i in 0..size -> "x\n"} |> String.concat ""
    let body = FormValues([("input", bodyString)])
    Assert.DoesNotThrowAsync(fun () -> Http.AsyncRequest (url= localServer.BaseAddress + "/200", httpMethod="POST", body=body, timeout = 10000) |> Async.Ignore |> Async.StartAsTask :> _)

[<Test; TestCaseSource("testFormDataSizesInBytes")>]
let testMultipartFormDataBodySize (size: int) =
    use localServer = startHttpLocalServer()
    let bodyString = seq {for _i in 0..size -> "x\n"} |> String.concat ""
    let multipartItem = [ MultipartItem("input", "input.txt", new MemoryStream(Encoding.UTF8.GetBytes(bodyString)) :> Stream) ]
    let body = Multipart(Guid.NewGuid().ToString(), multipartItem)

    Assert.DoesNotThrowAsync(fun () -> Http.AsyncRequest (url= localServer.BaseAddress + "/200", httpMethod="POST", body=body, timeout = 10000) |> Async.Ignore |> Async.StartAsTask :> _)

[<Test>]
let ``escaping of url parameters`` () =
    let url = "https://graph.microsoft.com/beta/me/insights/shared"
    let queryParams = [
        "$select", "Property1,Property2/SubProperty,*"
        "$filter", "Subject eq 'A? & #B = C/D'"
        "$top", "10"
        "$expand", "ExtendedProperties($filter=PropertyId eq 'String {36FF76DC-215F-4246-9544-DAB709259CE8} Name Some/Property2.0')"
        "$orderby", "Property2 desc"
    ]

    Http.AppendQueryToUrl(url, queryParams)
    |> should equal "https://graph.microsoft.com/beta/me/insights/shared?%24select=Property1%2CProperty2%2FSubProperty%2C%2A&%24filter=Subject%20eq%20%27A%3F%20%26%20%23B%20%3D%20C%2FD%27&%24top=10&%24expand=ExtendedProperties%28%24filter%3DPropertyId%20eq%20%27String%20%7B36FF76DC-215F-4246-9544-DAB709259CE8%7D%20Name%20Some%2FProperty2.0%27%29&%24orderby=Property2%20desc"

[<Test>]
let ``escaping of reserve characters in query`` () =
    let url = "http://nevermind.com"
    let queryParams = [
        "key!", "v@lue1"
        "key#", "value2&(value:/?#[]@*+,;=)"
    ]

    Http.AppendQueryToUrl(url, queryParams)
    |> should equal "http://nevermind.com?key%21=v%40lue1&key%23=value2%26%28value%3A%2F%3F%23%5B%5D%40%2A%2B%2C%3B%3D%29"
    
[<Test>]
let ``correct multipart content format`` () =
    let numFiles = 2
    let boundary = "**"
    let content = "Text file content"
    let multiPartItem (content: string) name = MultipartItem(name, name, (new MemoryStream(Encoding.UTF8.GetBytes(content))) :> Stream)
    let multiparts = seq {for i in [0..numFiles] -> multiPartItem content (i.ToString()) } 
    let combinedStream = HttpHelpers.writeMultipart boundary multiparts Encoding.UTF8
    use ms = new MemoryStream()
    combinedStream.CopyTo(ms)
    let str = Encoding.UTF8.GetString(ms.ToArray())
    Console.WriteLine(str)
    let singleMultipartFormat file = sprintf "--%s\r\nContent-Disposition: form-data; name=\"%i\"; filename=\"%i\"\r\nContent-Type: application/octet-stream\r\n\r\n%s\r\n" boundary file file content
     // No need extra newline /r/n before closing delimiter
    let finalFormat = [sprintf "--%s--" boundary] |> Seq.append (seq {for i in [0..numFiles] -> singleMultipartFormat i }) |>  String.concat ""
    str |> should equal finalFormat

[<Test>]
let ``CombinedStream has length with Some length`` () =
    use combinedStream = new HttpHelpers.CombinedStream(Some 10L, [])
    combinedStream.Length |> should equal 10L
    
[<Test>]
let ``CombinedStream can seek with Some length`` () =
    use combinedStream = new HttpHelpers.CombinedStream(Some 10L, [])
    combinedStream.CanSeek |> should equal true
    
[<Test>]
let ``CombinedStream length throws with None length`` () =
    use combinedStream = new HttpHelpers.CombinedStream(None, [])
    (fun () -> combinedStream.Length |> ignore) |> should throw typeof<Exception>
    
[<Test>]
let ``CombinedStream cannot seek with None length`` () =
    use combinedStream = new HttpHelpers.CombinedStream(None, [])
    combinedStream.CanSeek |> should equal false
    
type nonSeekableStream (b: byte[]) =
    inherit MemoryStream(b)
    override _.Length with get():Int64 = failwith "Im not seekable"
    override _.CanSeek with get() = false
    
[<Test>]
let ``Non-seekable streams create non-seekable CombinedStream`` () =
    use nonSeekms = new nonSeekableStream(Array.zeroCreate 10)
    let multiparts = [MultipartItem("","", nonSeekms)]
    let combinedStream = HttpHelpers.writeMultipart "-" multiparts Encoding.UTF8
    (fun () -> combinedStream.Length |> ignore) |> should throw typeof<Exception>
    combinedStream.CanSeek |> should equal false
    
[<Test>]
let ``Seekable streams create Seekable CombinedStream`` () =
    let byteLen = 10L
    let result = byteLen + 108L // As no extra /r/n, 2 bytes removed, 108 is headers
    use ms = new MemoryStream(Array.zeroCreate (int byteLen))
    let multiparts = [MultipartItem("","", ms)]
    let combinedStream = HttpHelpers.writeMultipart "-" multiparts Encoding.UTF8
    combinedStream.Length |> should equal result
    combinedStream.CanSeek |> should equal true
    
#nowarn "44" // Use of deprecated HttpWebRequest

[<Test>]
let ``HttpWebRequest length is set with seekable streams`` () =
    use ms = new MemoryStream(Array.zeroCreate 10)
    let wr = HttpWebRequest.Create("http://x") :?> HttpWebRequest
    wr.Method <- "POST"
    HttpHelpers.writeBody wr ms |> Async.RunSynchronously
    wr.ContentLength |> should equal 10
    
[<Test>]
let ``HttpWebRequest length is not set with non-seekable streams`` () =
    use nonSeekms = new nonSeekableStream(Array.zeroCreate 10)
    let wr = HttpWebRequest.Create("http://x") :?> HttpWebRequest
    wr.Method <- "POST"
    HttpHelpers.writeBody wr nonSeekms |> Async.RunSynchronously
    wr.ContentLength |> should equal 0
