(**
# F# Data: HTTP Utilities

The .NET library provides a powerful API for creating and sending HTTP web requests.
There is a simple `WebClient` type (see [MSDN][1]) and a more flexible `HttpWebRequest`
type (see [MSDN][2]). However, these two types are quite difficult to use if you
want to quickly run a simple HTTP request and specify parameters such as method,
HTTP POST data, or additional headers.

The F# Data Library provides a simple `Http` type with four overloaded methods:
`RequestString` and `AsyncRequestString`, that can be used to create a simple request and
perform it synchronously or asynchronously, and `Request` and it's async companion `AsyncRequest` if
you want to request binary files or you want to know more about the response like the status code,
the response URL, or the returned headers and cookies.

 [1]: http://msdn.microsoft.com/en-us/library/system.net.webclient.aspx
 [2]: http://msdn.microsoft.com/en-us/library/system.net.httpwebrequest.aspx

To use the type, we first need to reference the library using `#r` (in an F# interactive) 
or add reference to a project. The type is located in `FSharp.Net` namespace:
*)

#r "../../../bin/FSharp.Data.dll"
open FSharp.Data

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
In the rest of the documentation, we focus on the `RequestString` method, because
the use of `AsyncRequestString` is exactly the same.

## Query parameters and headers

You can specify query parameters either by constructing
an URL that includes the parameters (e.g. `http://...?test=foo&more=bar`) or you
can pass them using the optional parameter `query`. The following example also explicitly
specifies the GET method, but it will be set automatically for you if you omit it:
*)

Http.RequestString("http://httpbin.org/get", query=["test", "foo"], httpMethod="GET")

(** 
Additional headers are specified similarly - using an optional parameter `headers`.
The collection can contain custom headers, but also standard headers such as the 
Accept header (which has to be set using a specific property when using `HttpWebRequest`).

The following example uses [The Movie Database](http://www.themoviedb.org) API 
to search for the word "batman". To run the sample, you'll need to register and
provide your API key:
*)

(*** do-not-eval ***)

// API key for http://www.themoviedb.org
let apiKey = "<please register to get a key>"

// Run the HTTP web request
Http.RequestString
  ( "http://api.themoviedb.org/3/search/movie", httpMethod = "GET",
    query   = [ "api_key", apiKey; "query", "batman" ],
    headers = [ "Accept", "application/json" ])

(**
The library supports a simple and unchecked string based API (used in the previous example),
but you can also use pre-defined header names to avoid spelling mistakes. The named headers
are available in `HttpRequestHeaders` (and `HttpResponseHeaders`) modules, so you can either
use the full name `HttpRequestHeaders.Accept`, or open the module and use just the short name
`Accept` as in the following example. Similarly, the `HttpContentTypes` enumeration provides
well known content types:
*)

open FSharp.Data.HttpRequestHeaders

(*** do-not-eval ***)

// Run the HTTP web request
Http.RequestString
  ( "http://api.themoviedb.org/3/search/movie",
    query   = [ "api_key", apiKey; "query", "batman" ],
    headers = [ Accept HttpContentTypes.Json ])

(**
## Getting extra information

Note that in the previous snippet, if you don't specify a valid API key, you'll get a (401) Unathorized error,
and that will throw an exception. Unlike when using `WebRequest` directly, the exception message will still include
the response content, so it's easier to debug in F# interactive when the server returns extra info.

You can also opt out of the exception by specifying the `silentHttpErrors` parameter:
*)

(*** define-output:request ***)

Http.RequestString("http://api.themoviedb.org/3/search/movie", silentHttpErrors = true)

(** This returns the following: *)
(*** include-it:request ***)

(** In this case, you might want to look at the HTTP status code so you don't confuse an error message for an actual response.
If you want to see more information about the response, including the status code, the response 
headers, the returned cookies, and the response url (which might be different to 
the url you passed when there are redirects), you can use the `Request` method instead of the `RequestString` method:

*)

let response = Http.Request("http://api.themoviedb.org/3/search/movie", silentHttpErrors = true)

// Examine information about the response
response.Headers
response.Cookies
response.ResponseUrl
response.StatusCode

(**
## Sending request data

If you want to create a POST request with HTTP POST data, you can specify the
additional data in the `body` optional parameter. This parameter is of type `HttpRequestBody`, which
is a discriminated union with three cases:

* `TextRequest` for sending a string in the request body.
* `BinaryUpload` for sending binary content in the request.
* `FormValues` for sending a set of name-value pairs correspondent to form values.

If you specify a body, you do not need to set the `httpMethod` parameter, it will be set to `Post` automatically.

The following example uses the [httpbin.org](http://httpbin.org) service which 
returns the request details:
*)

Http.RequestString("http://httpbin.org/post", body = FormValues ["test", "foo"])

(**
By default, the `Content-Type` header is set to `text/plain`, `application/x-www-form-urlencoded`,
or `application/octet-stream`, depending on which kind of `HttpRequestBody` you specify, but you can change
this behaviour by adding `content-type` to the list of headers using the optional argument `headers`:
*)

Http.RequestString
  ( "http://httpbin.org/post", 
    headers = [ ContentType HttpContentTypes.Json ],
    body = TextRequest """ {"test": 42} """)

(**
## Maintaining cookies across requests

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

If we go to another MSDN page and click on a F# code sample, and then go 
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
## Requesting binary data

The `RequestString` method will always return the response as a `string`, but if you use the 
`Request` method, it will return a `HttpResponseBody.Text` or a 
`HttpResponseBody.Binary` depending on the response `content-type` header:
*)

let logoUrl = "https://raw.github.com/fsharp/FSharp.Data/master/misc/logo.png"
match Http.Request(logoUrl).Body with
| Text text -> 
    printfn "Got text content: %s" text
| Binary bytes -> 
    printfn "Got %d bytes of binary content" bytes.Length

(**
## Customizing the HTTP request

For the cases where you need something not natively provided by the library, you can use the
`customizeHttpRequest` parameter, which expects a function that transforms an `HttpWebRequest`.

As an example, let's say you want to add a client certificate to your request. To do that,
you need to open the `X509Certificates` namespace from  `System.Security.Cryptography`,
create a `X509ClientCertificate2` value, and add it to the `ClientCertificates` list of the request.

Assuming the certificate is stored in `myCertificate.pfx`:
*)

(*** do-not-eval ***)

open System.Security.Cryptography.X509Certificates

// Load the certificate from local file
let clientCert = 
  new X509Certificate2(".\myCertificate.pfx", "password")

// Send the request with certificate
Http.Request
  ( "http://yourprotectedresouce.com/data",
    customizeHttpRequest = fun req -> req.ClientCertificates.Add(clientCert) |> ignore; req)

(**
## Related articles

 * [API Reference: HTTP class](../reference/fsharp-data-http.html)
 * [API Reference: HttpMethod module](../reference/fsharp-data-httpmethod.html)
 * [API Reference: HttpRequestHeaders module](../reference/fsharp-data-httprequestheaders.html)
 * [API Reference: HttpResponseHeaders module](../reference/fsharp-data-httpresponseheaders.html)
 * [API Reference: HttpContentTypes module](../reference/fsharp-data-httpcontenttypes.html)
 * [API Reference: HttpRequestBody discriminated union](../reference/fsharp-data-httprequestbody.html)
 * [API Reference: HttpResponse record](../reference/fsharp-data-httpresponse.html)
 * [API Reference: HttpResponseBody discriminated union](../reference/fsharp-data-httpresponsebody.html)
 * [API Reference: HttpResponseWithStream record](../reference/fsharp-data-httpresponsewithstream.html)

 *)
