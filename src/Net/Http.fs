// --------------------------------------------------------------------------------------
// Utilities for working with network, downloading resources with specified headers etc.
// --------------------------------------------------------------------------------------

namespace FSharp.Data

open System
open System.Globalization
open System.IO
open System.Net
open System.Text
open System.Reflection
open FSharp.Data.Runtime

/// The method to use in an HTTP request
type HttpMethod =
    | Options 
    | Get 
    | Head 
    | Post 
    | Put 
    | Delete 
    | Trace 
    | Connect
    override x.ToString() =
        (sprintf "%A" x).ToUpperInvariant()

/// Header to send in an HTTP request
type HttpRequestHeader =
    | Accept of string
    | AcceptCharset of string
    | AcceptLanguage of string
    | Authorization of string
    | Connection of string
    | ContentMD5 of string
    | ContentType of string
    | Date of DateTime
    | Expect of int
    | From of string
    | Host of string
    | IfMatch of string
    | IfModifiedSince of DateTime
    | IfNoneMatch of string
    | IfRange of string
    | MaxForwards of int
    | Origin of string
    | Pragma of string
    | ProxyAuthorization of string
    | Referer of string
    | Upgrade of string
    | UserAgent of string
    | Via of string
    | XHTTPMethodOverride of HttpMethod
    | Warning of string
    | Custom of Name:string * Value:string

/// Header present in an HTTP response
type HttpResponseHeader =
    | AccessControlAllowOrigin 
    | AcceptRanges 
    | Age 
    | Allow 
    | CacheControl 
    | Connection 
    | ContentEncoding 
    | ContentLanguage 
    | ContentLength
    | ContentLocation 
    | ContentMD5 
    | ContentDisposition 
    | ContentRange 
    | ContentType 
    | Date 
    | ETag 
    | Expires 
    | LastModified 
    | Link 
    | Location 
    | P3P 
    | Pragma 
    | ProxyAuthenticate 
    | Refresh 
    | RetryAfter 
    | Server 
    | SetCookie
    | Status
    | StrictTransportSecurity 
    | Trailer 
    | TransferEncoding 
    | Vary 
    | Via 
    | Warning 
    | WWWAuthenticate 
    | Custom of string
     
/// The body to send in an HTTP request
type HttpRequestBody =
    | Text of string
    | Binary of byte[]
    | FormValues of seq<string * string>

/// The response body returned by an HTTP request
type HttpResponseBody =
    | Text of string
    | Binary of byte[]

/// The response returned by an HTTP request
type HttpResponse =
  { Body : HttpResponseBody
    Headers : Map<HttpResponseHeader,string> 
    ResponseUrl : string
    Cookies : Map<string,string>
    StatusCode: int }

[<AbstractClass>]
/// Constants for common HTTP content types
type HttpContentTypes private() =

    /// plain/text
    static member Text = "plain/text"

    /// application/octet-stream
    static member Binary = "application/octet-stream"

    /// application/octet-stream
    static member Zip = "application/zip"

    /// application/octet-stream
    static member GZip = "application/gzip"

    /// application/x-www-form-urlencoded
    static member FormValues = "application/x-www-form-urlencoded"

    /// application/json
    static member Json = "application/json"

    /// application/javascript
    static member JavaScript = "application/javascript"

    /// application/xml
    static member Xml = "application/xml"

    /// application/rss+xml
    static member Rss = "application/rss+xml"

    /// application/atom+xml
    static member Atom = "application/atom+xml"

    /// application/rdf+xml
    static member Rdf = "application/rdf+xml"

    /// text/html
    static member Html = "text/html"

    /// application/xhtml+xml
    static member XHtml = "application/xhtml+xml"
    
    /// application/soap+xml
    static member Soap = "application/soap+xml"

    /// text/csv
    static member Csv = "text/csv"

type private HeaderEnum = System.Net.HttpRequestHeader

[<AutoOpen>]
module private Helpers =

    /// consumes a stream asynchronously until the end
    /// and returns a memory stream with the full content
    let asyncRead (stream:Stream) = async {
        // Allocate 4kb buffer for downloading data
        let buffer = Array.zeroCreate (4 * 1024)
        let output = new MemoryStream()
        let reading = ref true
      
        while reading.Value do
            // Download one (at most) 4kb chunk and copy it
            let! count = stream.AsyncRead(buffer, 0, buffer.Length)
            output.Write(buffer, 0, count)
            reading := count > 0
      
        output.Seek(0L, SeekOrigin.Begin) |> ignore 
        return output 
    }

    let writeBody (req:HttpWebRequest) (postBytes:byte[]) = async { 
#if FX_NO_WEBREQUEST_CONTENTLENGTH
#else
        req.ContentLength <- int64 postBytes.Length
#endif
        use! output = Async.FromBeginEnd(req.BeginGetRequestStream, req.EndGetRequestStream)
        do! output.AsyncWrite(postBytes, 0, postBytes.Length)
        output.Flush()
    }

    let reraisePreserveStackTrace (e:Exception) =
        try
#if FX_NET_CORE_REFLECTION
            let remoteStackTraceString = typeof<exn>.GetRuntimeField("_remoteStackTraceString");
#else
            let remoteStackTraceString = typeof<exn>.GetField("_remoteStackTraceString", BindingFlags.Instance ||| BindingFlags.NonPublic);
#endif
            if remoteStackTraceString <> null then
                remoteStackTraceString.SetValue(e, e.StackTrace + Environment.NewLine)
        with _ -> ()
        raise e

    let augmentWebExceptionsWithDetails f = async {
        try
            return! f()
        with 
            // If an exception happens, augment the message with the response
            | :? WebException as exn -> 
              if exn.Response = null then reraisePreserveStackTrace exn
              let responseExn =
                  try
                    use responseStream = exn.Response.GetResponseStream()
                    use streamReader = new StreamReader(responseStream)
                    let response = streamReader.ReadToEnd()
                    try 
                      // on some platforms this fails
                      responseStream.Position <- 0L
                    with _ -> ()
                    if String.IsNullOrEmpty response then None
                    else Some(WebException(sprintf "%s\nResponse from %s:\n%s" exn.Message exn.Response.ResponseUri.OriginalString response, exn, exn.Status, exn.Response))
                  with _ -> None
              match responseExn with
              | Some e -> raise e
              | None -> reraisePreserveStackTrace exn 
              // just to keep the type-checker happy:
              return Unchecked.defaultof<_>
    }

    let setHeaders headers (req:HttpWebRequest) =
        let hasContentType = ref false
        headers |> Option.iter (List.iter (fun header ->
            match header with
            | Accept value -> req.Accept <- value
            | AcceptCharset value -> req.Headers.[HeaderEnum.AcceptCharset] <- value
            | AcceptLanguage value -> req.Headers.[HeaderEnum.AcceptLanguage] <- value
            | Authorization value -> req.Headers.[HeaderEnum.Authorization] <- value
#if FX_NO_WEBREQUEST_CONNECTION
            | HttpRequestHeader.Connection value -> req.Headers.[HeaderEnum.Connection] <- value
#else
            | HttpRequestHeader.Connection value -> req.Connection <- value
#endif
            | HttpRequestHeader.ContentMD5 value -> req.Headers.[HeaderEnum.ContentMd5] <- value
            | HttpRequestHeader.ContentType value ->
                req.ContentType <- value
                hasContentType := true
#if FX_NO_WEBREQUEST_DATE
            | HttpRequestHeader.Date value -> req.Headers.[HeaderEnum.Date] <- value.ToString("R", CultureInfo.InvariantCulture)
#else
            | HttpRequestHeader.Date value -> req.Date <- value
#endif
#if FX_NO_WEBREQUEST_EXPECT
            | Expect value -> req.Headers.[HeaderEnum.Expect] <- value.ToString(CultureInfo.InvariantCulture)
#else
            | Expect value -> req.Expect <- value.ToString()
#endif
            | From value -> req.Headers.[HeaderEnum.From] <- value
#if FX_NO_WEBREQUEST_HOST
            | Host value -> req.Headers.[HeaderEnum.Host] <- value
#else
            | Host value -> req.Host <- value
#endif       
            | IfMatch value -> req.Headers.[HeaderEnum.IfMatch] <- value
#if FX_NO_WEBREQUEST_IFMODIFIEDSINCE
            | IfModifiedSince value -> req.Headers.[HeaderEnum.IfModifiedSince] <- value.ToString("R", CultureInfo.InvariantCulture)
#else
            | IfModifiedSince value -> req.IfModifiedSince <- value
#endif
            | IfNoneMatch value -> req.Headers.[HeaderEnum.IfNoneMatch] <- value
            | IfRange value -> req.Headers.[HeaderEnum.IfRange] <- value
            | MaxForwards value -> req.Headers.[HeaderEnum.MaxForwards] <- value.ToString()
            | Origin value -> req.Headers.["Origin"] <- value
            | HttpRequestHeader.Pragma value -> req.Headers.[HeaderEnum.Pragma] <- value
            | ProxyAuthorization value -> req.Headers.[HeaderEnum.ProxyAuthorization] <- value
#if FX_NO_WEBREQUEST_REFERER
            | Referer value -> req.Headers.[HeaderEnum.Referer] <- value
#else
            | Referer value -> req.Referer <- value
#endif            
            | Upgrade value -> req.Headers.[HeaderEnum.Upgrade] <- value
#if FX_NO_WEBREQUEST_USERAGENT
            | UserAgent value -> req.Headers.[HeaderEnum.UserAgent] <- value
#else
            | UserAgent value -> req.UserAgent <- value
#endif
            | HttpRequestHeader.Via value -> req.Headers.["Via"] <- value
            | XHTTPMethodOverride value -> req.Headers.["X-HTTP-Method-Override"] <- value.ToString()
            | HttpRequestHeader.Warning value -> req.Headers.["Warning"] <- value
            | HttpRequestHeader.Custom(name, value) -> req.Headers.[name] <- value))
        hasContentType.Value

    let parseResponseHeader headerName =
        match headerName with
        | "Access-Control-Allow-Origin" -> AccessControlAllowOrigin
        | "Accept-Ranges" -> AcceptRanges
        | "Age" -> Age
        | "Allow" -> Allow
        | "Cache-Control" -> CacheControl
        | "Connection" -> HttpResponseHeader.Connection
        | "Content-Encoding" -> ContentEncoding
        | "Content-Language" -> ContentLanguage
        | "Content-Length" -> ContentLength
        | "Content-Location" -> ContentLocation
        | "Content-MD5" -> HttpResponseHeader.ContentMD5
        | "Content-Disposition" -> ContentDisposition
        | "Content-Range" -> ContentRange
        | "Content-Type" -> HttpResponseHeader.ContentType
        | "Date" -> HttpResponseHeader.Date
        | "ETag" -> ETag
        | "Expires" -> Expires
        | "Last-Modified" -> LastModified
        | "Link" -> Link
        | "Location" -> Location
        | "P3P" -> P3P
        | "Pragma" -> HttpResponseHeader.Pragma
        | "Proxy-Authenticate" -> ProxyAuthenticate
        | "Refresh" -> Refresh
        | "Retry-After" -> RetryAfter
        | "Server" -> Server
        | "Set-Cookie" -> SetCookie
        | "Status" -> Status
        | "Strict-Transport-Security" -> StrictTransportSecurity
        | "Trailer" -> Trailer
        | "Transfer-Encoding" -> TransferEncoding
        | "Vary" -> Vary
        | "Via" -> HttpResponseHeader.Via
        | "Warning" -> HttpResponseHeader.Warning
        | "WWW-Authenticate" -> WWWAuthenticate
        | _ -> Custom headerName

    let getResponse (req:HttpWebRequest) dontThrowOnHttpError = async {
        if defaultArg dontThrowOnHttpError false then
            try
                return! Async.FromBeginEnd(req.BeginGetResponse, req.EndGetResponse)
            with
                | :? WebException as exn -> 
                    if exn.Response <> null then 
                       return exn.Response
                    else 
                        reraisePreserveStackTrace exn
                        return Unchecked.defaultof<_>
        else
            return! Async.FromBeginEnd(req.BeginGetResponse, req.EndGetResponse)
        }

    let toHttpResponse forceText responseUrl statusCode (contentType:string) (_contentEncoding:string) characterSet cookies headers (memoryStream:MemoryStream) =

        let isText (mimeType:string) =
            let isText (mimeType:string) =
                let mimeType = mimeType.Trim()
                mimeType.StartsWith "text/" || 
                mimeType = HttpContentTypes.Json || 
                mimeType = HttpContentTypes.Xml ||
                mimeType = HttpContentTypes.JavaScript ||
                mimeType = "application/ecmascript" ||
                mimeType = "application/xml-dtd" ||
                mimeType.StartsWith "application/" && mimeType.EndsWith "+xml"
            mimeType.Split([| ';' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.exists isText

#if FX_NO_WEBREQUEST_AUTOMATICDECOMPRESSION
        let memoryStream = 
            if _contentEncoding = "gzip" then
                new MemoryStream(Ionic.Zlib.GZipStream.UncompressBuffer(memoryStream.ToArray()))
            elif _contentEncoding = "deflate" then
                new MemoryStream(Ionic.Zlib.DeflateStream.UncompressBuffer(memoryStream.ToArray()))
            else
                memoryStream
#endif

        let respBody = 
            if forceText || (isText contentType) then
                use sr = 
                    match characterSet with
                    | "" -> new StreamReader(memoryStream)
                    | characterSet -> new StreamReader(memoryStream, Encoding.GetEncoding(characterSet))
                sr.ReadToEnd() |> HttpResponseBody.Text
            else
                memoryStream.ToArray() |> HttpResponseBody.Binary

        { Body = respBody
          Headers = headers
          ResponseUrl = responseUrl
          Cookies = cookies
          StatusCode = statusCode }

/// Utilities for working with network via HTTP. Includes methods for downloading 
/// resources with specified headers, query parameters and HTTP body
[<AbstractClass>]
type Http private() = 

    /// Appends the query parameters to the url, taking care of proper escaping
    static member internal AppendQueryToUrl(url:string, query) =
        match query with
        | [] -> url
        | query ->
            url
            + if url.Contains "?" then "&" else "?"
            + String.concat "&" [ for k, v in query -> Uri.EscapeUriString k + "=" + Uri.EscapeUriString v ]

#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
    static member internal InnerRequest(url:string, toHttpResponse, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, 
                                        ?dontThrowOnHttpError, ?customizeHttpRequest) = async {
#else
    static member internal InnerRequest(url:string, toHttpResponse, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, 
                                        ?dontThrowOnHttpError, ?customizeHttpRequest, ?certificate:Security.Cryptography.X509Certificates.X509Certificate) = async {
#endif

        let uri = 
            Uri(Http.AppendQueryToUrl(url, defaultArg query []))
            |> UriUtils.enableUriSlashes

        // do not use WebRequest.CreateHttp otherwise silverlight proxies don't work
        let req = WebRequest.Create(uri) :?> HttpWebRequest

#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
#else
        certificate |> Option.map req.ClientCertificates.Add |> ignore
#endif

        // set method
        let defaultMethod = if body.IsSome then HttpMethod.Post else HttpMethod.Get
        req.Method <- (defaultArg httpMethod defaultMethod).ToString()

        // set headers
        let hasContentType = setHeaders headers req

    #if FX_NO_WEBREQUEST_AUTOMATICDECOMPRESSION
        req.Headers.[HeaderEnum.AcceptEncoding] <- "gzip,deflate"
    #else
        req.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.Deflate
    #endif

        // set cookies    
        let cookieContainer = defaultArg cookieContainer (new CookieContainer())

        match cookies with
        | None -> ()
        | Some cookies -> cookies |> List.iter (fun (name, value) -> cookieContainer.Add(req.RequestUri, Cookie(name, value)))
        try
            req.CookieContainer <- cookieContainer
        with :? NotImplementedException ->
            // silverlight doesn't support setting cookies
            if cookies.IsSome then
                failwith "Cookies not supported by this platform"

        match body with
        | Some body ->
            let defaultContentType, bytes =
              match body with
              | HttpRequestBody.Text text -> "text/plain", Encoding.UTF8.GetBytes(text)
              | HttpRequestBody.Binary bytes -> "application/octet-stream", bytes
              | HttpRequestBody.FormValues values -> 
                  let bytes = 
                      [ for k, v in values -> Uri.EscapeDataString k + "=" + Uri.EscapeDataString v ]
                      |> String.concat "&"
                      |> Encoding.UTF8.GetBytes
                  HttpContentTypes.FormValues, bytes
            // Set default content type if it is not specified by the user
            if not hasContentType then req.ContentType <- defaultContentType
            do! writeBody req bytes
        | None -> ()

        let req = 
            match customizeHttpRequest with
            | Some customizeHttpRequest -> customizeHttpRequest req
            | None -> req

        // Send the request and get the response
        return! augmentWebExceptionsWithDetails <| fun () -> async {
            use! resp = getResponse req dontThrowOnHttpError
            let cookies = Map.ofList [ for cookie in cookieContainer.GetCookies uri |> Seq.cast<Cookie> -> cookie.Name, cookie.Value ]  
            let headers = 
                [ for header in resp.Headers.AllKeys do 
                    yield parseResponseHeader header, resp.Headers.[header] ]
                |> Map.ofList
            let statusCode, characterSet = 
                match resp with
                | :? HttpWebResponse as resp -> 
#if FX_NO_WEBRESPONSE_CHARACTERSET
                    int resp.StatusCode, ""
#else
                    int resp.StatusCode, resp.CharacterSet
#endif
                | _ -> 0, ""
            use networkStream = resp.GetResponseStream()
            let! memoryStream = asyncRead networkStream
            let contentEncoding = defaultArg (Map.tryFind ContentEncoding headers) ""
            return toHttpResponse resp.ResponseUri.OriginalString statusCode resp.ContentType contentEncoding characterSet cookies headers memoryStream }
    }

    /// Download an HTTP web resource from the specified URL asynchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
    static member AsyncRequest(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?customizeHttpRequest) = 
        Http.InnerRequest(url, toHttpResponse (*forceText*)false, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?customizeHttpRequest=customizeHttpRequest)
#else
    static member AsyncRequest(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?customizeHttpRequest, ?certificate) = 
        Http.InnerRequest(url, toHttpResponse (*forceText*)false, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?customizeHttpRequest=customizeHttpRequest, ?certificate=certificate)
#endif

    /// Download an HTTP web resource from the specified URL asynchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
    static member AsyncRequestString(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?customizeHttpRequest) = async {
        let! response = Http.InnerRequest(url, toHttpResponse (*forceText*)true, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer,
                                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?customizeHttpRequest=customizeHttpRequest)
#else
    static member AsyncRequestString(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?customizeHttpRequest, ?certificate)  = async {
        let! response = Http.InnerRequest(url, toHttpResponse (*forceText*)true, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?customizeHttpRequest=customizeHttpRequest, ?certificate=certificate)
#endif
        return
            match response.Body with
            | HttpResponseBody.Text text -> text
            | HttpResponseBody.Binary binary -> failwithf "Expecting text, but got a binary response (%d bytes)" binary.Length
    }

    /// Download an HTTP web resource from the specified URL synchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
    static member Request(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?customizeHttpRequest) = 
        Http.AsyncRequest(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?customizeHttpRequest=customizeHttpRequest)
#else
    static member Request(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?customizeHttpRequest, ?certificate) = 
        Http.AsyncRequest(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?customizeHttpRequest=customizeHttpRequest, ?certificate=certificate)
#endif
        |> Async.RunSynchronously

    /// Download an HTTP web resource from the specified URL synchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
    static member RequestString(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?customizeHttpRequest) = 
        Http.AsyncRequestString(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                                ?dontThrowOnHttpError=dontThrowOnHttpError, ?customizeHttpRequest=customizeHttpRequest)
#else                              
    static member RequestString(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?customizeHttpRequest, ?certificate) = 
        Http.AsyncRequestString(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                                ?dontThrowOnHttpError=dontThrowOnHttpError, ?customizeHttpRequest=customizeHttpRequest, ?certificate=certificate)
#endif
        |> Async.RunSynchronously
