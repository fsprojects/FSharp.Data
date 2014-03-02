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
    | AcceptDatetime of DateTime
    | AcceptEncoding of string
    | AcceptLanguage of string
    | Allow of string
    | Authorization of string
    | CacheControl of string
    | Connection of string
    | ContentEncoding of string
    | ContentLanguage of string
    | ContentLocation of string
    | ContentMD5 of string
    | ContentRange of string
    | ContentType of string
    | Date of DateTime
    | Expect of int
    | Expires of DateTime
    | From of string
    | Host of string
    | IfMatch of string
    | IfModifiedSince of DateTime
    | IfNoneMatch of string
    | IfRange of string
    | IfUnmodifiedSince of DateTime
    | KeepAlive of string
    | LastModified of DateTime
    | MaxForwards of int
    | Origin of string
    | Pragma of string
    | ProxyAuthorization of string
    | Range of Start:int64 * Finish:int64
    | Referer of string
    | TE of string
    | Trailer of string
    | Translate of string
    | Upgrade of string
    | UserAgent of string
    | Via of string
    | Warning of string
    | XHTTPMethodOverride of HttpMethod
    | Custom of Name:string * Value:string

/// Header present in an HTTP response
type HttpResponseHeader =
    | AccessControlAllowOrigin 
    | AcceptRanges 
    | Age 
    | ResponseAllow 
    | ResponseCacheControl 
    | ResponseConnection 
    | ResponseContentEncoding 
    | ResponseContentLanguage 
    | ResponseContentLength
    | ResponseContentLocation 
    | ResponseContentMD5 
    | ResponseContentDisposition 
    | ResponseContentRange 
    | ResponseContentType 
    | ResponseDate 
    | ETag 
    | ResponseExpires 
    | ResponseLastModified 
    | Link 
    | Location 
    | P3P 
    | ResponsePragma 
    | ProxyAuthenticate 
    | Refresh 
    | RetryAfter 
    | Server 
    | SetCookie
    | Status
    | StrictTransportSecurity 
    | ResponseTrailer 
    | TransferEncoding 
    | Vary 
    | ResponseVia 
    | ResponseWarning 
    | WWWAuthenticate
    | XAspNetMvcVersion
    | XAspNetVersion
    | XContentTypeOptions
    | XInstance
    | XPoweredBy
    | XRuntime
    | XUACompatible
    | XVersion
    | NonStandard of string
     
/// The body to send in an HTTP request
type HttpRequestBody =
    | TextRequest of string
    | BinaryUpload of byte[]
    | FormValues of seq<string * string>

/// The response body returned by an HTTP request
type HttpResponseBody =
    | Text of string
    | Binary of byte[]

/// The response returned by an HTTP request
type HttpResponse =
  { Body : HttpResponseBody
    StatusCode: int
    ResponseUrl : string
    Headers : Map<HttpResponseHeader,string>
    Cookies : Map<string,string> }

/// The response returned by an HTTP request with direct access to the response stream
type HttpResponseWithStream =
  { ResponseStream : Stream
    StatusCode: int
    ResponseUrl : string
    Headers : Map<HttpResponseHeader,string>
    Cookies : Map<string,string> }

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

    let rec checkForRepeatedHeaders visitedHeaders remainingHeaders =
        match remainingHeaders with
        | [] -> ()
        | header::remainingHeaders ->
            for visitedHeader in visitedHeaders do
                match header, visitedHeader with
                | Custom(Name = name1), Custom(Name = name2) ->
                    if name1 = name2 then
                        failwithf "Repeated headers: %A %A" visitedHeader header
                | _ ->
                    if header.GetType() = visitedHeader.GetType() then
                        failwithf "Repeated headers: %A %A" visitedHeader header
            checkForRepeatedHeaders (header::visitedHeaders) remainingHeaders

    let setHeaders headers (req:HttpWebRequest) =
        let hasContentType = ref false
        headers |> Option.iter (checkForRepeatedHeaders [])
        headers |> Option.iter (List.iter (fun header ->
            match header with
            | Accept value -> req.Accept <- value
            | AcceptCharset value -> req.Headers.[HeaderEnum.AcceptCharset] <- value
            | AcceptDatetime value -> req.Headers.["Accept-Datetime"] <- value.ToString("R", CultureInfo.InvariantCulture)
            | AcceptEncoding value -> req.Headers.[HeaderEnum.AcceptEncoding] <- value
            | AcceptLanguage value -> req.Headers.[HeaderEnum.AcceptLanguage] <- value
            | Allow value -> req.Headers.[HeaderEnum.Allow] <- value
            | Authorization value -> req.Headers.[HeaderEnum.Authorization] <- value
            | CacheControl value -> req.Headers.[HeaderEnum.CacheControl] <- value
#if FX_NO_WEBREQUEST_CONNECTION
            | Connection value -> req.Headers.[HeaderEnum.Connection] <- value
#else
            | Connection value -> req.Connection <- value
#endif
            | ContentEncoding value -> req.Headers.[HeaderEnum.ContentEncoding] <- value
            | ContentLanguage value -> req.Headers.[HeaderEnum.ContentLanguage] <- value
            | ContentLocation value -> req.Headers.[HeaderEnum.ContentLocation] <- value
            | ContentMD5 value -> req.Headers.[HeaderEnum.ContentMd5] <- value
            | ContentRange value -> req.Headers.[HeaderEnum.ContentRange] <- value
            | ContentType value ->
                req.ContentType <- value
                hasContentType := true
#if FX_NO_WEBREQUEST_DATE
            | Date value -> req.Headers.[HeaderEnum.Date] <- value.ToString("R", CultureInfo.InvariantCulture)
#else
            | Date value -> req.Date <- value
#endif
#if FX_NO_WEBREQUEST_EXPECT
            | Expect value -> req.Headers.[HeaderEnum.Expect] <- value.ToString(CultureInfo.InvariantCulture)
#else
            | Expect value -> req.Expect <- value.ToString()
#endif
            | Expires value -> req.Headers.[HeaderEnum.Expires] <- value.ToString("R", CultureInfo.InvariantCulture)
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
            | IfUnmodifiedSince value -> req.Headers.[HeaderEnum.IfUnmodifiedSince] <- value.ToString("R", CultureInfo.InvariantCulture)
            | KeepAlive value -> req.Headers.[HeaderEnum.KeepAlive] <- value
            | LastModified value -> req.Headers.[HeaderEnum.LastModified] <- value.ToString("R", CultureInfo.InvariantCulture)
            | MaxForwards value -> req.Headers.[HeaderEnum.MaxForwards] <- value.ToString()
            | Origin value -> req.Headers.["Origin"] <- value
            | Pragma value -> req.Headers.[HeaderEnum.Pragma] <- value
#if FX_NO_WEBREQUEST_RANGE
            | Range(start, finish) -> req.Headers.[HeaderEnum.Range] <- sprintf "bytes=%d-%d" start finish
#else
            | Range(start, finish) -> req.AddRange(start, finish)
#endif
            | ProxyAuthorization value -> req.Headers.[HeaderEnum.ProxyAuthorization] <- value
#if FX_NO_WEBREQUEST_REFERER
            | Referer value -> req.Headers.[HeaderEnum.Referer] <- value
#else
            | Referer value -> req.Referer <- value
#endif            
            | TE value -> req.Headers.[HeaderEnum.Te] <- value
            | Trailer value -> req.Headers.[HeaderEnum.Trailer] <- value
            | Translate value -> req.Headers.[HeaderEnum.Translate] <- value
            | Upgrade value -> req.Headers.[HeaderEnum.Upgrade] <- value
#if FX_NO_WEBREQUEST_USERAGENT
            | UserAgent value -> req.Headers.[HeaderEnum.UserAgent] <- value
#else
            | UserAgent value -> req.UserAgent <- value
#endif
            | Via value -> req.Headers.[HeaderEnum.Via] <- value
            | XHTTPMethodOverride value -> req.Headers.["X-HTTP-Method-Override"] <- value.ToString()
            | Warning value -> req.Headers.[HeaderEnum.Warning] <- value
            | Custom(name, value) -> req.Headers.[name] <- value))
        hasContentType.Value

    let parseResponseHeader headerName =
        match headerName with
        | "Access-Control-Allow-Origin" -> AccessControlAllowOrigin
        | "Accept-Ranges" -> AcceptRanges
        | "Age" -> Age
        | "Allow" -> ResponseAllow
        | "Cache-Control" -> ResponseCacheControl
        | "Connection" -> ResponseConnection
        | "Content-Encoding" -> ResponseContentEncoding
        | "Content-Language" -> ResponseContentLanguage
        | "Content-Length" -> ResponseContentLength
        | "Content-Location" -> ResponseContentLocation
        | "Content-MD5" -> ResponseContentMD5
        | "Content-Disposition" -> ResponseContentDisposition
        | "Content-Range" -> ResponseContentRange
        | "Content-Type" -> ResponseContentType
        | "Date" -> ResponseDate
        | "ETag" -> ETag
        | "Expires" -> ResponseExpires
        | "Last-Modified" -> ResponseLastModified
        | "Link" -> Link
        | "Location" -> Location
        | "P3P" -> P3P
        | "Pragma" -> ResponsePragma
        | "Proxy-Authenticate" -> ProxyAuthenticate
        | "Refresh" -> Refresh
        | "Retry-After" -> RetryAfter
        | "Server" -> Server
        | "Set-Cookie" -> SetCookie
        | "Status" -> Status
        | "Strict-Transport-Security" -> StrictTransportSecurity
        | "Trailer" -> ResponseTrailer
        | "Transfer-Encoding" -> TransferEncoding
        | "Vary" -> Vary
        | "Via" -> ResponseVia
        | "Warning" -> ResponseWarning
        | "WWW-Authenticate" -> WWWAuthenticate
        | "X-AspNetMvc-Version" -> XAspNetMvcVersion
        | "X-AspNet-Version" -> XAspNetVersion
        | "X-Content-Type-Options" -> XContentTypeOptions
        | "X-Instance" -> XInstance
        | "X-Powered-By" -> XPoweredBy
        | "X-Runtime" -> XRuntime
        | "X-UA-Compatible" -> XUACompatible
        | "X-Version" -> XVersion
        | _ -> NonStandard headerName

    let getResponse (req:HttpWebRequest) dontThrowOnHttpError =
        if defaultArg dontThrowOnHttpError false then
            async {
                try
                    return! Async.FromBeginEnd(req.BeginGetResponse, req.EndGetResponse)
                with
                    | :? WebException as exn -> 
                        if exn.Response <> null then 
                           return exn.Response
                        else 
                            reraisePreserveStackTrace exn
                            return Unchecked.defaultof<_>
            }
        else 
            Async.FromBeginEnd(req.BeginGetResponse, req.EndGetResponse)

    let toHttpResponse forceText responseUrl statusCode contentType (_contentEncoding:string)
                       characterSet responseEncodingOverride cookies headers stream = async {

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

        use stream = stream
        let! memoryStream = asyncRead stream

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
            if forceText || isText contentType then
                use sr = 
                    match (defaultArg responseEncodingOverride ""), characterSet with
                    | "", "" -> new StreamReader(memoryStream)
                    | "", characterSet -> new StreamReader(memoryStream, Encoding.GetEncoding(characterSet))
                    | responseEncodingOverride, _ -> new StreamReader(memoryStream, Encoding.GetEncoding(responseEncodingOverride))

                sr.ReadToEnd() |> Text
            else
                memoryStream.ToArray() |> Binary

        return { Body = respBody
                 StatusCode = statusCode
                 ResponseUrl = responseUrl
                 Headers = headers
                 Cookies = cookies }
    }

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
    static member private InnerRequest(url:string, toHttpResponse, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, 
                                       ?dontThrowOnHttpError, ?responseEncodingOverride, ?customizeHttpRequest) =
#else
    static member private InnerRequest(url:string, toHttpResponse, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, 
                                       ?dontThrowOnHttpError, ?responseEncodingOverride, ?customizeHttpRequest, ?certificate:Security.Cryptography.X509Certificates.X509Certificate) =
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

        let body = body |> Option.map (fun body ->

            let defaultContentType, bytes =
                match body with
                | TextRequest text -> HttpContentTypes.Text, Encoding.UTF8.GetBytes(text)
                | BinaryUpload bytes -> HttpContentTypes.Binary, bytes
                | FormValues values -> 
                    let bytes = 
                        [ for k, v in values -> Uri.EscapeDataString k + "=" + Uri.EscapeDataString v ]
                        |> String.concat "&"
                        |> Encoding.UTF8.GetBytes
                    HttpContentTypes.FormValues, bytes

            // Set default content type if it is not specified by the user
            if not hasContentType then req.ContentType <- defaultContentType

            bytes)

        // Send the request and get the response
        augmentWebExceptionsWithDetails <| fun () -> async {
   
            match body with
            | Some body -> do! writeBody req body
            | None -> ()

            let req = 
                match customizeHttpRequest with
                | Some customizeHttpRequest -> customizeHttpRequest req
                | None -> req

            let! resp = getResponse req dontThrowOnHttpError

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

            let contentEncoding = defaultArg (Map.tryFind ResponseContentEncoding headers) ""

            let stream = resp.GetResponseStream()

            return! toHttpResponse resp.ResponseUri.OriginalString statusCode resp.ContentType contentEncoding characterSet responseEncodingOverride cookies headers stream
        }

    /// Download an HTTP web resource from the specified URL asynchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
    static member AsyncRequest(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?responseEncodingOverride, ?customizeHttpRequest) = 
        Http.InnerRequest(url, toHttpResponse (*forceText*)false, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?responseEncodingOverride=responseEncodingOverride, ?customizeHttpRequest=customizeHttpRequest)
#else
    static member AsyncRequest(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?responseEncodingOverride, ?customizeHttpRequest, ?certificate) = 
        Http.InnerRequest(url, toHttpResponse (*forceText*)false, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?responseEncodingOverride=responseEncodingOverride, ?customizeHttpRequest=customizeHttpRequest, ?certificate=certificate)
#endif

    /// Download an HTTP web resource from the specified URL asynchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
    static member AsyncRequestString(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?responseEncodingOverride, ?customizeHttpRequest) = async {
        let! response = Http.InnerRequest(url, toHttpResponse (*forceText*)true, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer,
                                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?responseEncodingOverride=responseEncodingOverride, ?customizeHttpRequest=customizeHttpRequest)
#else
    static member AsyncRequestString(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?responseEncodingOverride, ?customizeHttpRequest, ?certificate)  = async {
        let! response = Http.InnerRequest(url, toHttpResponse (*forceText*)true, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?responseEncodingOverride=responseEncodingOverride, ?customizeHttpRequest=customizeHttpRequest, ?certificate=certificate)
#endif
        return
            match response.Body with
            | Text text -> text
            | Binary binary -> failwithf "Expecting text, but got a binary response (%d bytes)" binary.Length
    }

    /// Download an HTTP web resource from the specified URL synchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
    static member AsyncRequestStream(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?customizeHttpRequest) =
        let toHttpResponse responseUrl statusCode _contentType _contentEncoding _characterSet _responseEncodingOverride cookies headers stream = async {
            return { ResponseStream = stream
                     StatusCode = statusCode
                     ResponseUrl = responseUrl
                     Headers = headers
                     Cookies = cookies }
        }
        Http.InnerRequest(url, toHttpResponse, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?customizeHttpRequest=customizeHttpRequest)
#else
    static member AsyncRequestStream(url,?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?customizeHttpRequest, ?certificate:Security.Cryptography.X509Certificates.X509Certificate) =
        // responseEncodingOverride is never set for this overload
        let toHttpResponse responseUrl statusCode _contentType _contentEncoding _characterSet _responseEncodingOverride cookies headers stream = async {
            return { ResponseStream = stream
                     StatusCode = statusCode
                     ResponseUrl = responseUrl
                     Headers = headers
                     Cookies = cookies }
        }
        Http.InnerRequest(url, toHttpResponse, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?customizeHttpRequest=customizeHttpRequest, ?certificate=certificate)
#endif

    /// Download an HTTP web resource from the specified URL synchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
    static member Request(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?responseEncodingOverride, ?customizeHttpRequest) = 
        Http.AsyncRequest(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?responseEncodingOverride=responseEncodingOverride, ?customizeHttpRequest=customizeHttpRequest)
#else
    static member Request(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?responseEncodingOverride, ?customizeHttpRequest, ?certificate) = 
        Http.AsyncRequest(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                          ?dontThrowOnHttpError=dontThrowOnHttpError, ?responseEncodingOverride=responseEncodingOverride, ?customizeHttpRequest=customizeHttpRequest, ?certificate=certificate)
#endif
        |> Async.RunSynchronously

    /// Download an HTTP web resource from the specified URL synchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
    static member RequestString(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?responseEncodingOverride, ?customizeHttpRequest) = 
        Http.AsyncRequestString(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                                ?dontThrowOnHttpError=dontThrowOnHttpError, ?responseEncodingOverride=responseEncodingOverride, ?customizeHttpRequest=customizeHttpRequest)
#else                              
    static member RequestString(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?responseEncodingOverride, ?customizeHttpRequest, ?certificate) = 
        Http.AsyncRequestString(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                                ?dontThrowOnHttpError=dontThrowOnHttpError, ?responseEncodingOverride=responseEncodingOverride, ?customizeHttpRequest=customizeHttpRequest, ?certificate=certificate)
#endif
        |> Async.RunSynchronously

    /// Download an HTTP web resource from the specified URL synchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
    static member RequestStream(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?customizeHttpRequest) = 
        Http.AsyncRequestStream(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                                ?dontThrowOnHttpError=dontThrowOnHttpError, ?customizeHttpRequest=customizeHttpRequest)
#else                              
    static member RequestStream(url, ?query, ?headers, ?httpMethod, ?body, ?cookies, ?cookieContainer, ?dontThrowOnHttpError, ?customizeHttpRequest, ?certificate) = 
        Http.AsyncRequestStream(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                                ?dontThrowOnHttpError=dontThrowOnHttpError, ?customizeHttpRequest=customizeHttpRequest, ?certificate=certificate)
#endif
        |> Async.RunSynchronously
