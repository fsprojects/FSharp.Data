module FSharp.Data.Tests.MockServer

open Nancy
open Nancy.Hosting.Self
open System
open System.Threading
open System.Text

// A Nancy Response overridden to allow different encoding on the body
type EncodedResponse(body:string, encoding:string) =
    inherit Nancy.Response()

    let writeBody (stream:IO.Stream) = 
        let bytes = Encoding.GetEncoding(encoding).GetBytes(body)
        stream.Write(bytes, 0, bytes.Length)

    do base.Contents <- Action<IO.Stream> writeBody

let recordedRequest = ref (null:Request)

type FakeServer() as self = 
    inherit NancyModule()
    do
        self.Post.["RecordRequest"] <- 
            fun _ -> 
                recordedRequest := self.Request
                200 :> obj

        self.Get.["RecordRequest"] <- 
            fun _ -> 
                recordedRequest := self.Request
                200 :> obj

        self.Get.["GoodStatusCode"] <- 
            fun _ -> 
                200 :> obj

        self.Get.["BadStatusCode"] <- 
            fun _ -> 
                401 :> obj

        self.Get.["GotBody"] <- 
            fun _ -> 
                "My body" :> obj

        self.Get.["AllTheThings"] <- 
            fun _ -> 
                let response = "Some JSON or whatever" |> Nancy.Response.op_Implicit 
                response.WithStatusCode(HttpStatusCode.ImATeapot)
                        .WithCookie("cookie1", "chocolate chip")
                        .WithCookie("cookie2", "smarties")
                        .WithHeader("Content-Encoding", "xpto")
                        .WithHeader("X-New-Fangled-Header", "some value") :> obj

        self.Get.["MoonLanguageCorrectEncoding"] <- 
            fun _ -> 
                let response = new EncodedResponse("яЏ§§їДЙ", "windows-1251")
                response.ContentType <- "text/plain; charset=windows-1251"
                response :> obj

        self.Get.["MoonLanguageNoEncoding"] <- 
            fun _ -> 
                let response = new EncodedResponse("яЏ§§їДЙ", "windows-1251")
                response.ContentType <- "text/plain"
                response :> obj

        self.Get.["MoonLanguageInvalidEncoding"] <- 
            fun _ -> 
                let response = new EncodedResponse("яЏ§§їДЙ", "windows-1251")
                response.ContentType <- "text/plain; charset=Ninky-Nonk"
                response :> obj

        self.Get.["AllHeaders"] <- 
            fun _ -> 
                let response = "Some JSON or whatever" |> Nancy.Response.op_Implicit 
                response.ContentType <- "text/html; charset=utf-8"
                response.Headers.Add("Access-Control-Allow-Origin", "*")
                response.Headers.Add("Accept-Ranges", "bytes")
                response.Headers.Add("Age", "12")
                response.Headers.Add("Allow", "GET, HEAD")
                response.Headers.Add("Cache-Control", "max-age=3600")
                response.Headers.Add("Connection", "close")
                response.Headers.Add("Content-Encoding", "blah")
                response.Headers.Add("Content-Language", "EN-gb")
                response.Headers.Add("Content-Location", "/index.htm")
                response.Headers.Add("Content-MD5", "Q2hlY2sgSW50ZWdyaXR5IQ==")
                response.Headers.Add("Content-Disposition", "attachment; filename=\"fname.ext\"")
                response.Headers.Add("Content-Range", "bytes 21010-47021/47022")
                //response.Headers.Add("Date", "") // will be current date
                response.Headers.Add("ETag", "737060cd8c284d8af7ad3082f209582d")
                response.Headers.Add("Expires", "Thu, 01 Dec 1994 16:00:00 GMT")
                response.Headers.Add("Last-Modified", "Tue, 15 Nov 1994 12:45:26 +0000")
                response.Headers.Add("Link", "</feed>; rel=\"alternate\"")
                response.Headers.Add("Location", "http://www.w3.org/pub/WWW/People.html")
                response.Headers.Add("P3P", "CP=\"your_compact_policy\"")
                response.Headers.Add("Pragma", "no-cache")
                response.Headers.Add("Proxy-Authenticate", "Basic")
                response.Headers.Add("Refresh", "5; url=http://www.w3.org/pub/WWW/People.html")
                response.Headers.Add("Retry-After", "120")
                //response.Headers.Add("Server", "") // will be 'Microsoft-HTTPAPI/2.0' or 'Mono-HTTPAPI/1.0'
                response.Headers.Add("Strict-Transport-Security", "max-age=16070400; includeSubDomains")
                response.Headers.Add("Trailer", "Max-Forwards")
                response.Headers.Add("Transfer-Encoding", "chunked")
                response.Headers.Add("Vary", "*")
                response.Headers.Add("Via", "1.0 fred, 1.1 example.com (Apache/1.1)")
                response.Headers.Add("Warning", "199 Miscellaneous warning")
                response.Headers.Add("WWW-Authenticate", "Basic")
                response.Headers.Add("X-New-Fangled-Header", "some value")
                response :> obj

        self.Get.["CookieRedirect"] <- 
            fun _ -> 
                let response = "body" |> Nancy.Response.op_Implicit
                response.WithCookie("cookie1", "baboon")
                        .WithHeader("Location", "http://localhost:1235/TestServer/NoCookies")
                        .WithStatusCode(HttpStatusCode.TemporaryRedirect) :> obj

        self.Get.["NoCookies"] <- 
            fun _ -> 
                let response = "body" |> Nancy.Response.op_Implicit
                response.StatusCode <- HttpStatusCode.OK
                response :> obj