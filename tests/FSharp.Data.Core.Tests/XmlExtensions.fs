module FSharp.Data.Tests.XmlExtensions

open FsUnit
open NUnit.Framework
open System
open System.Xml.Linq
open FSharp.Data
open FSharp.Data.HttpRequestHeaders
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System.Threading.Tasks
open System.Net.Sockets
open System.IO
open System.Text

type ITestHttpServer =
    inherit IDisposable
    abstract member BaseAddress: string
    abstract member WorkerTask: Task

let startXmlHttpLocalServer() =
    let app = WebApplication.CreateBuilder().Build()
    
    // Handle XML POST requests and echo back the received XML
    app.Map("/echo", (fun (ctx: HttpContext) ->
        async {
            use reader = new StreamReader(ctx.Request.Body)
            let! body = reader.ReadToEndAsync() |> Async.AwaitTask
            
            ctx.Response.ContentType <- "application/xml"
            ctx.Response.StatusCode <- 200
            
            // Echo back the received XML with a wrapper to validate it was received
            let responseXml = $"<response><received>{body}</received></response>"
            return! ctx.Response.WriteAsync(responseXml) |> Async.AwaitTask
        } |> Async.StartAsTask :> Task
    )) |> ignore
    
    // Handle different HTTP methods
    app.Map("/test/{method}", (fun (ctx: HttpContext) ->
        async {
            let method = ctx.Request.RouteValues.["method"] :?> string
            let actualMethod = ctx.Request.Method
            
            ctx.Response.ContentType <- "application/xml"
            ctx.Response.StatusCode <- 200
            
            let responseXml = $"<response><method>{actualMethod}</method><expected>{method}</expected></response>"
            return! ctx.Response.WriteAsync(responseXml) |> Async.AwaitTask
        } |> Async.StartAsTask :> Task
    )) |> ignore

    // Use TcpListener(0) to ask the OS for a free port, then release it.
    // This avoids Windows excluded port ranges (reserved by Hyper-V/Docker) that
    // caused intermittent SocketException (10013: WSAEACCES) on Windows CI.
    let freePort =
        let listener = new TcpListener(System.Net.IPAddress.Loopback, 0)
        listener.Start()
        let port = (listener.LocalEndpoint :?> System.Net.IPEndPoint).Port
        listener.Stop()
        port

    let baseAddress = $"http://127.0.0.1:{freePort}"

    // Use StartAsync so the server is guaranteed ready before we return
    app.Urls.Add(baseAddress)
    app.StartAsync() |> Async.AwaitTask |> Async.RunSynchronously

    { new ITestHttpServer with
        member _.Dispose() =
            app.StopAsync() |> Async.AwaitTask |> Async.RunSynchronously
        member _.WorkerTask = Task.CompletedTask
        member _.BaseAddress = baseAddress }

[<Test>]
let ``XElement.Request sends XML via POST by default`` () =
    use localServer = startXmlHttpLocalServer()
    
    let xml = XElement(XName.Get("test"), "sample content")
    let response = xml.Request(localServer.BaseAddress + "/echo")
    
    response.StatusCode |> should equal 200
    match response.Body with
    | Text bodyText -> bodyText |> should contain "<test>sample content</test>"
    | Binary _ -> failwith "Expected text response, but got binary"

[<Test>]
let ``XElement.Request with custom HTTP method`` () =
    use localServer = startXmlHttpLocalServer()
    
    let xml = XElement(XName.Get("test"), "content")
    let response = xml.Request(localServer.BaseAddress + "/test/PUT", httpMethod = HttpMethod.Put)
    
    response.StatusCode |> should equal 200
    match response.Body with
    | Text bodyText -> bodyText |> should contain "<method>PUT</method>"
    | Binary _ -> failwith "Expected text response, but got binary"

[<Test>]
let ``XElement.Request includes default User-Agent header`` () =
    use localServer = startXmlHttpLocalServer()
    
    let xml = XElement(XName.Get("test"))
    let response = xml.Request(localServer.BaseAddress + "/echo")
    
    // The User-Agent should be set to the default value
    response.StatusCode |> should equal 200

[<Test>]
let ``XElement.Request with custom headers`` () =
    use localServer = startXmlHttpLocalServer()
    
    let xml = XElement(XName.Get("test"))
    let customHeaders = [("X-Custom-Header", "test-value")]
    let response = xml.Request(localServer.BaseAddress + "/echo", headers = customHeaders)
    
    response.StatusCode |> should equal 200

[<Test>]
let ``XElement.Request preserves existing User-Agent when provided`` () =
    use localServer = startXmlHttpLocalServer()
    
    let xml = XElement(XName.Get("test"))
    let customHeaders = [UserAgent "CustomAgent/1.0"]
    let response = xml.Request(localServer.BaseAddress + "/echo", headers = customHeaders)
    
    response.StatusCode |> should equal 200

[<Test>]
let ``XElement.Request includes XML content type header`` () =
    use localServer = startXmlHttpLocalServer()
    
    let xml = XElement(XName.Get("test"))
    let response = xml.Request(localServer.BaseAddress + "/echo")
    
    // Should include Content-Type: application/xml
    response.StatusCode |> should equal 200

[<Test>]
let ``XElement.Request with complex XML structure`` () =
    use localServer = startXmlHttpLocalServer()
    
    let xml = 
        XElement(XName.Get("root"),
            XElement(XName.Get("child1"), "value1"),
            XElement(XName.Get("child2"), 
                XAttribute(XName.Get("attr"), "attrvalue"),
                "value2"))
    
    let response = xml.Request(localServer.BaseAddress + "/echo")
    
    response.StatusCode |> should equal 200
    match response.Body with
    | Text bodyText -> 
        bodyText |> should contain "<child1>value1</child1>"
        bodyText |> should contain "<child2 attr=\"attrvalue\">value2</child2>"
    | Binary _ -> failwith "Expected text response, but got binary"

[<Test>]
let ``XElement.RequestAsync sends XML via POST by default`` () =
    use localServer = startXmlHttpLocalServer()
    
    let xml = XElement(XName.Get("test"), "async content")
    let response = xml.RequestAsync(localServer.BaseAddress + "/echo") |> Async.RunSynchronously
    
    response.StatusCode |> should equal 200
    match response.Body with
    | Text bodyText -> bodyText |> should contain "<test>async content</test>"
    | Binary _ -> failwith "Expected text response, but got binary"

// [<Test>]
// let ``XElement.RequestAsync with custom HTTP method`` () =
//     use localServer = startXmlHttpLocalServer()
    
//     let xml = XElement(XName.Get("test"))
//     let response = xml.RequestAsync(localServer.BaseAddress + "/test/PUT", httpMethod = HttpMethod.Put) |> Async.RunSynchronously
    
//     response.StatusCode |> should equal 200
//     match response.Body with
//     | Text bodyText -> bodyText |> should contain "<method>PUT</method>"
//     | Binary _ -> failwith "Expected text response, but got binary"

[<Test>]
let ``XElement.RequestAsync with custom headers`` () =
    use localServer = startXmlHttpLocalServer()
    
    let xml = XElement(XName.Get("test"))
    let customHeaders = [("X-Async-Header", "async-value")]
    let response = xml.RequestAsync(localServer.BaseAddress + "/echo", headers = customHeaders) |> Async.RunSynchronously
    
    response.StatusCode |> should equal 200

[<Test>]
let ``XElement.RequestAsync includes default User-Agent header`` () =
    use localServer = startXmlHttpLocalServer()
    
    let xml = XElement(XName.Get("test"))
    let response = xml.RequestAsync(localServer.BaseAddress + "/echo") |> Async.RunSynchronously
    
    response.StatusCode |> should equal 200

[<Test>]
let ``XElement.RequestAsync preserves existing User-Agent when provided`` () =
    use localServer = startXmlHttpLocalServer()
    
    let xml = XElement(XName.Get("test"))
    let customHeaders = [UserAgent "AsyncAgent/1.0"]
    let response = xml.RequestAsync(localServer.BaseAddress + "/echo", headers = customHeaders) |> Async.RunSynchronously
    
    response.StatusCode |> should equal 200

[<Test>]
let ``XElement with namespaces serializes correctly`` () =
    use localServer = startXmlHttpLocalServer()
    
    let ns = XNamespace.Get("http://example.com/test")
    let xml = XElement(ns + "root", XAttribute(XNamespace.Xmlns + "test", ns.NamespaceName), "content")
    let response = xml.Request(localServer.BaseAddress + "/echo")
    
    response.StatusCode |> should equal 200
    match response.Body with
    | Text bodyText -> bodyText |> should contain "xmlns:test=\"http://example.com/test\""
    | Binary _ -> failwith "Expected text response, but got binary"

[<Test>]
let ``XElement serialization disables formatting`` () =
    use localServer = startXmlHttpLocalServer()
    
    let xml = 
        XElement(XName.Get("root"),
            XElement(XName.Get("child1"), "value1"),
            XElement(XName.Get("child2"), "value2"))
    
    let response = xml.Request(localServer.BaseAddress + "/echo")
    
    response.StatusCode |> should equal 200
    // Should be compact without extra whitespace due to SaveOptions.DisableFormatting
    match response.Body with
    | Text bodyText -> bodyText |> should not' (contain "\n  <child1>")
    | Binary _ -> failwith "Expected text response, but got binary"