(**
# F# Data: HTTP Utilities

The .NET library provides powerful API for creating and sending HTTP web requests.
There is a simple `WebClient` type (see [MSDN][1]) and a more flexible `HttpWebRequest`
type (see [MSDN][2]). However, these two types are quite difficult to use if you
want to quickly run a simple HTTP request and specify parameters such as method,
HTTP POST data or additional headers.

The F# Data Library provides a simple `Http` type with four overloaded methods:
`RequestString` and `AsyncRequestString` that can be used to create a simple request and
perform it synchronously or asynchronously, and `Request` and it's async companion `RequestAsync` if
you want to know more about the response like `statuscode`, `responseurl` and `cookies`.

 [1]: http://msdn.microsoft.com/en-us/library/system.net.webclient.aspx
 [2]: http://msdn.microsoft.com/en-us/library/system.net.httpwebrequest.aspx

To use the type, we first need to reference the library using `#r` (in an F# interactive) 
or add reference to a project. The type is located in `FSharp.Net` namespace:
*)

#r "../../bin/FSharp.Data.dll"
open FSharp.Net

(**
## Sending simple requests

To send a simple HTTP (GET) request that downloads a specified web page, you 
can use `Http.RequestString` and `Http.AsyncRequestString` with just a single parameter:
*)

// Download the content of a web site
Http.RequestString("http://tomasp.net")

// Download web site asynchronously
async { let! html = Http.AsyncRequestString("http://tomasp.net")
        printfn "%d" html.Length }
|> Async.Start

(** 
In the rest of the documentation, we focus at the `Request(String)` method, because
the use of `AsyncRequest(String)` is exactly the same.

## Query parameters and headers

You can specify query parameters either by constructing
a URL that includes the parameters (e.g. `http://...?test=foo&more=bar`) or you
can pass them using the optional parameter `query`. The following example also explicitly
specifies the GET method, but it will be set automatically for you if you omit it:
*)

Http.RequestString("http://httpbin.org/get", query=["test", "foo"], meth="GET")

(** 
Additional headers are specified similarly - using an optional parameter `headers`.
The collection can contain custom headers, but also standard headers such as the 
Accept header (which has to be set explicitly when using `HttpWebRequest`).

The following example uses [The Movie Database](http://www.themoviedb.org) API 
to search for the word "batman". To run the sample, you'll need to register and
provide your API key:
*)
// API key for http://www.themoviedb.org
let apiKey = "<please register to get a key>"

// Run the HTTP web request
Http.RequestString
  ( "http://api.themoviedb.org/3/search/movie",
    query   = [ "api_key", apiKey; "query", "batman" ],
    headers = [ "accept", "application/json" ])

(**
## Sending request data

If you want to create a POST request with HTTP POST data, you can specify the
additional data in a string using the `body` parameter, or you can specify a set
of name-value pairs using the `bodyValues` parameter. If you specify body data,
you do not need to set the `meth` parameter - it will be set to `GET` automatically.
The following example uses the [httpbin.org](http://httpbin.org) service which 
returns the request details:
*)

Http.RequestString("http://httpbin.org/post", bodyValues=["test", "foo"])

(**
By default, the Content-Type header is set to `application/x-www-form-urlencoded`,
but you can change this behaviour by adding `content-type` to the list of headers
using the optional argument `headers`:
*)

Http.RequestString
  ( "http://httpbin.org/post", 
    headers = ["content-type", "application/json"],
    body = ReqBody.Text """ {"test": 42} """)

(**
You can also send binary data in the HTTP POST request's body. Just change the header appropriately 
and use the `ReqBody.Binary` constructor in your request.
*)

let myBinaryData = System.Text.Encoding.UTF8.GetBytes "Hello!"

Http.RequestString
  ( "http://httpbin.org/post", 
    headers = ["content-type", "application/octet-stream"],
    body = ReqBody.Binary myBinaryData )

(**
## Sending a client certificate

If you want to add a client certificate to your requests, then you can use the 
optional parameter `certificate` and pass the `X509ClientCertificate` value as
an argument. To do that, you need to open the `X509Certificates` namespace from 
`System.Security.Cryptography`. Assuming the certificate is stored in `myCertificate.pfx`,
you can write:
*)


open System.Security.Cryptography.X509Certificates

let clientCert = new X509Certificate2 (".\myCertificate.pfx", "password")

Http.RequestString("http://yourprotectedresouce.com/data",
             certificate = clientCert)


(**
## Maintaing cookies across requests

If you want to maintain cookies between requests, you can specify the `cookieContainer` 
parameter. The following example will request the MSDN documentation for the 
`HttpRequest` class. It will return the code snippets in C# and not F#:
*)

// Build URL with documentation for a given class
let msdnUrl className = 
  let root = "http://msdn.microsoft.com"
  sprintf "%s/en-gb/library/%s.aspx" root className

// Get the page and search for F# code
let docInCSharp = Http.RequestString(msdnUrl "system.web.httprequest")
docInCSharp.Contains "<a>F#</a>"

(**

If we now go to another MSDN page and click on a F# code sample, and then go 
back to the `HttpRequest` class documentation, while maintaining the same `cookieContainer`, 
we will be presented with the F# code snippets:
*)

open System.Net
let cc = CookieContainer()

// Send a request to switch the language
Http.RequestString
  ( msdnUrl "system.datetime", 
    query = ["cs-save-lang", "1"; "cs-lang","fsharp"], 
    cookieContainer = cc) |> ignore

// Request the documentation again & search for F#
let docInFSharp = 
  Http.RequestString
    ( msdnUrl "system.web.httprequest", 
      cookieContainer = cc )
docInFSharp.Contains "<a>F#</a>"

(**
If you want to see more information about the response, including the response 
headers, the returned cookies, and the response url (which might be different to 
the url you passed when there are redirects), you can use the `Request` method:
*)

let response = Http.Request(msdnUrl "system.web.httprequest")

// Examine information about the response
response.Cookies
response.ResponseUrl
response.StatusCode

(**
## Requesting binary data

The `RequestString` method will always return the response as a `string`, but if you use the 
`Request` method, it will return a `ResBody.Text` or a 
`ResBody.Binary` depending on the response `content-type` header:
*)

let logoUrl = "https://raw.github.com/fsharp/FSharp.Data/master/misc/logo.png"
match Http.Request(logoUrl).Body with
| ResBody.Text text -> 
    printfn "Got text content: %s" text
| ResBody.Binary bytes -> 
    printfn "Got %d bytes of binary content" bytes.Length
