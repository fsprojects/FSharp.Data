(**
# F# Data: HTTP Utilities

The .NET library provides powerful API for creating and sending HTTP web requests.
There is a simple `WebClient` type (see [MSDN][1]) and a more flexible `HttpWebRequest`
type (see [MSDN][2]). However, these two types are quite difficult to use if you
want to quickly run a simple HTTP request and specify parameters such as method,
HTTP POST data or additional headers.

The F# Data Library provides a simple `Http` type with two overloaded methods:
`Download` and `AsyncDownload` that can be used to create a simple request and
perform it synchronously or asynchronously.

 [1]: http://msdn.microsoft.com/en-us/library/system.net.webclient.aspx
 [2]: http://msdn.microsoft.com/en-us/library/system.net.httpwebrequest.aspx

To use the type, we first need to reference the library using `#r` (in an F# interactive) 
or add reference to a project. The type is located in `FSharp.Net` namespace:
*)

#r "../bin/FSharp.Data.dll"
open FSharp.Net

(**
## Sending simple requests

To send a simple HTTP (GET) request that downloads a specified web page, you 
can use `Http.Download` and `Http.AsyncDownload` with just a single parameter:
*)

// Download the content of a web site
Http.Download("http://tomasp.net")

// Download web site asynchronously
async { let! html = Http.AsyncDownload("http://tomasp.net")
        printfn "%d" html.Length }
|> Async.Start

(** 
In the rest of the documentation, we focus at the `Download` method, because
the use of `AsyncDownload` is exactly the same.

## Query parameters and headers

If you use the GET method, you can specify query parameters either by constructing
a URL that includes the parameters (e.g. `http://...?test=foo&more=bar`) or you
can pass them using the optional parameter `query`. The following example also explicitly
specifies the GET method (which is the default option):
*)

Http.Download
  ( "http://httpbin.org/get", 
    query = ["test", "foo"], meth="GET")

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
Http.Download
  ( "http://api.themoviedb.org/3/search/movie",
    query   = [ "api_key", apiKey; "query", "batman" ],
    headers = [ "accept", "application/json" ])

(**
## Sending request data

If you want to create a POST request with HTTP POST data, you can specify the
additional data using the `body` parameter. The following example uses the 
[httpbin.org](http://httpbin.org) service which returns the request details:
*)

Http.Download
  ( "http://httpbin.org/post", 
    meth="POST", body="test=foo")

(**
By default, the Content-Type header is set to `application/x-www-form-urlencoded`,
but you can change this behaviour by adding `content-type` to the list of headers
using the optional argument `headers`:
*)

Http.Download
  ( "http://httpbin.org/post", 
    headers = ["content-type", "application/json"],
    meth="POST", body=""" {"test": 42} """)

