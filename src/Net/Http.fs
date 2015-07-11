// --------------------------------------------------------------------------------------
// Utilities for working with network, downloading resources with specified headers etc.
// --------------------------------------------------------------------------------------

#if FX_NO_DEFAULT_PARAMETER_VALUE_ATTRIBUTE

namespace System.Runtime.InteropServices

open System

[<AttributeUsageAttribute(AttributeTargets.Parameter, Inherited = false)>]
type OptionalAttribute() = 
    inherit Attribute()

#endif

namespace FSharp.Data

open System
open System.Globalization
open System.IO
open System.Net
open System.Text
open System.Text.RegularExpressions
open System.Reflection
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open FSharp.Data.Runtime

/// The method to use in an HTTP request
module HttpMethod =

    // RFC 2626 specifies 8 methods

    /// Request information about the communication options available on the request/response chain identified by the URI
    let Options = "OPTIONS"
    /// Retrieve whatever information (in the form of an entity) is identified by the URI
    let Get = "GET"
    /// Identical to GET except that the server MUST NOT return a message-body in the response
    let Head = "HEAD"
    /// Requests that the server accepts the entity enclosed in the request as a 
    /// new subordinate of the resource identified by the Request-URI in the Request-Line
    let Post = "POST"
    /// Requests that the enclosed entity be stored under the supplied Request-URI
    let Put = "PUT"
    /// Requests that the origin server deletes the resource identified by the Request-URI
    let Delete = "DELETE"
    /// Used to invoke a remote, application-layer loop- back of the request message
    let Trace = "TRACE"
    /// Reserved for use with a proxy that can dynamically switch to being a tunnel 
    let Connect = "CONNECT"

    // RFC 4918 (WebDAV) adds 7 methods

    /// Retrieves properties defined on the resource identified by the request URI
    let PropFind = "PROPFIND"
    /// Processes instructions specified in the request body to set and/or remove properties defined on the resource identified by the request URI
    let PropPatch = "PROPPATCH"
    /// Creates a new collection resource at the location specified by the Request URI
    let MkCol = "MKCOL"
    /// Creates a duplicate of the source resource, identified by the Request-URI, in the destination resource, identified by the URI in the Destination header
    let Copy = "COPY"
    /// Logical equivalent of a copy, followed by consistency maintenance processing, followed by a delete of the source where all three actions are performed atomically
    let Move = "MOVE"
    /// Used to take out a lock of any access type on the resource identified by the request URI.
    let Lock = "LOCK"
    /// Removes the lock identified by the lock token from the request URI, and all other resources included in the lock
    let Unlock = "UNLOCK"

    // RFC 5789 adds one more

    /// Requests that the origin server applies partial modifications contained in the entity enclosed in the request to the resource identified by the request URI
    let Patch = "PATCH"

/// Headers that can be sent in an HTTP request
module HttpRequestHeaders =
    /// Content-Types that are acceptable for the response
    let Accept (contentType:string) = "Accept", contentType
    /// Character sets that are acceptable
    let AcceptCharset (characterSets:string) = "Accept-Charset", characterSets
    /// Acceptable version in time 
    let AcceptDatetime (dateTime:DateTime) = "Accept-Datetime", dateTime.ToString("R", CultureInfo.InvariantCulture)
    /// List of acceptable encodings. See HTTP compression.
    let AcceptEncoding (encoding:string) = "Accept-Encoding", encoding
    /// List of acceptable human languages for response 
    let AcceptLanguage (language:string) = "Accept-Language", language
    /// The Allow header, which specifies the set of HTTP methods supported.
    let Allow (methods:string) = "Allow", methods
    /// Authentication credentials for HTTP authentication
    let Authorization (credentials:string) = "Authorization", credentials
    /// Authentication header using Basic Auth encoding
    let BasicAuth (username:string) (password:string) = 
        let base64Encode (s:string) = 
            let bytes = Encoding.UTF8.GetBytes(s)
            Convert.ToBase64String(bytes)
        sprintf "%s:%s" username password |> base64Encode |> sprintf "Basic %s" |>  Authorization 
    /// Used to specify directives that MUST be obeyed by all caching mechanisms along the request/response chain 
    let CacheControl (control:string) = "Cache-Control", control
    /// What type of connection the user-agent would prefer 
    let Connection (connection:string) = "Connection", connection
    /// The type of encoding used on the data
    let ContentEncoding (encoding:string) = "Content-Encoding", encoding
    /// The language the content is in
    let ContentLanguage (language:string) = "Content-Language", language
    /// An alternate location for the returned data
    let ContentLocation (location:string) = "Content-Location", location
    /// A Base64-encoded binary MD5 sum of the content of the request body
    let ContentMD5 (md5sum:string) = "Content-MD5", md5sum
    /// Where in a full body message this partial message belongs
    let ContentRange (range:string) = "Content-Range", range
    /// The MIME type of the body of the request (used with POST and PUT requests)
    let ContentType (contentType:string) = "Content-Type", contentType
    /// The date and time that the message was sent
    let Date (date:DateTime) = "Date", date.ToString("R", CultureInfo.InvariantCulture)
    /// Indicates that particular server behaviors are required by the client
    let Expect (behaviors:string) = "Expect", behaviors
    /// Gives the date/time after which the response is considered stale
    let Expires (dateTime:DateTime) = "Expires", dateTime.ToString("R", CultureInfo.InvariantCulture)
    /// The email address of the user making the request 
    let From (email:string) = "From", email
    /// The domain name of the server (for virtual hosting), and the TCP port number on which the server is listening. 
    /// The port number may be omitted if the port is the standard port for the service requested.
    let Host (host:string) = "Host", host
    /// Only perform the action if the client supplied entity matches the same entity on the server. 
    /// This is mainly for methods like PUT to only update a resource if it has not been modified since the user last updated it. If-Match: "737060cd8c284d8af7ad3082f209582d" Permanent 
    let IfMatch (entity:string) = "If-Match", entity
    /// Allows a 304 Not Modified to be returned if content is unchanged 
    let IfModifiedSince (dateTime:DateTime) = "If-Modified-Since", dateTime.ToString("R", CultureInfo.InvariantCulture)
    /// Allows a 304 Not Modified to be returned if content is unchanged
    let IfNoneMatch (etag:string) = "If-None-Match", etag
    /// If the entity is unchanged, send me the part(s) that I am missing; otherwise, send me the entire new entity
    let IfRange (range:string) = "If-Range", range
    /// Only send the response if the entity has not been modified since a specific time
    let IfUnmodifiedSince (dateTime:DateTime) = "If-Unmodified-Since", dateTime.ToString("R", CultureInfo.InvariantCulture)
    /// Specifies a parameter used into order to maintain a persistent connection
    let KeepAlive (keepAlive:string) = "Keep-Alive", keepAlive
    /// Specifies the date and time at which the accompanying body data was last modified
    let LastModified (dateTime:DateTime) = "Last-Modified", dateTime.ToString("R", CultureInfo.InvariantCulture)
    /// Limit the number of times the message can be forwarded through proxies or gateways
    let MaxForwards (count:int) = "Max-Forwards", count.ToString()
    /// Initiates a request for cross-origin resource sharing (asks server for an 'Access-Control-Allow-Origin' response header)
    let Origin (origin:string) = "Origin", origin
    /// Implementation-specific headers that may have various effects anywhere along the request-response chain.
    let Pragma (pragma:string) = "Pragma", pragma
    /// Authorization credentials for connecting to a proxy. 
    let ProxyAuthorization (credentials:string) = "Proxy-Authorization", credentials
    /// Request only part of an entity. Bytes are numbered from 0
    let Range (start:int64, finish:int64) = "Range", sprintf "bytes=%d-%d" start finish
    /// This is the address of the previous web page from which a link to the currently requested page was followed. (The word "referrer" is misspelled in the RFC as well as in most implementations.) 
    let Referer (referer:string) = "Referer", referer
    /// The transfer encodings the user agent is willing to accept: the same values as for the response header 
    /// Transfer-Encoding can be used, plus the "trailers" value (related to the "chunked" transfer method) to 
    /// notify the server it expects to receive additional headers (the trailers) after the last, zero-sized, chunk.
    let TE (te:string) = "TE", te
    /// The Trailer general field value indicates that the given set of header fields is present in the trailer of a message encoded with chunked transfer-coding
    let Trailer (trailer:string) = "Trailer", trailer
    /// Microsoft extension to the HTTP specification used in conjunction with WebDAV functionality.
    let Translate (translate:string) = "Translate", translate 
    /// Specifies additional communications protocols that the client supports.
    let Upgrade (upgrade:string) = "Upgrade", upgrade
    /// The user agent string of the user agent
    let UserAgent (userAgent:string) = "User-Agent", userAgent
    /// Informs the server of proxies through which the request was sent
    let Via (server:string) = "Via", server
    /// A general warning about possible problems with the entity body
    let Warning (message:string) = "Warning", message
    /// Override HTTP method. 
    let XHTTPMethodOverride (httpMethod:string) = "X-HTTP-Method-Override", httpMethod

/// Headers that can be received in an HTTP response
module HttpResponseHeaders =
    /// Specifying which web sites can participate in cross-origin resource sharing
    let [<Literal>] AccessControlAllowOrigin = "Access-Control-Allow-Origin"
    /// What partial content range types this server supports
    let [<Literal>] AcceptRanges = "Accept-Ranges"
    /// The age the object has been in a proxy cache in seconds
    let [<Literal>] Age = "Age"
    /// Valid actions for a specified resource. To be used for a 405 Method not allowed
    let [<Literal>] Allow = "Allow"
    /// Tells all caching mechanisms from server to client whether they may cache this object. It is measured in seconds
    let [<Literal>] CacheControl = "Cache-Control"
    /// Options that are desired for the connection
    let [<Literal>] Connection = "Connection"
    /// The type of encoding used on the data. See HTTP compression.
    let [<Literal>] ContentEncoding = "Content-Encoding"
    /// The language the content is in
    let [<Literal>] ContentLanguage = "Content-Language"
    /// The length of the response body in octets (8-bit bytes)
    let [<Literal>] ContentLength = "Content-Length"
    /// An alternate location for the returned data
    let [<Literal>] ContentLocation = "Content-Location"
    /// A Base64-encoded binary MD5 sum of the content of the response
    let [<Literal>] ContentMD5 = "Content-MD5"
    /// An opportunity to raise a "File Download" dialogue box for a known MIME type with binary format or suggest a filename for dynamic content. Quotes are necessary with special characters.
    let [<Literal>] ContentDisposition = "Content-Disposition"
    /// Where in a full body message this partial message belongs
    let [<Literal>] ContentRange = "Content-Range"
    /// The MIME type of this content
    let [<Literal>] ContentType = "Content-Type"
    /// The date and time that the message was sent (in "HTTP-date" format as defined by RFC 2616)
    let [<Literal>] Date = "Date"
    /// An identifier for a specific version of a resource, often a message digest
    let [<Literal>] ETag = "ETag"
    /// Gives the date/time after which the response is considered stale
    let [<Literal>] Expires = "Expires"
    /// The last modified date for the requested object 
    let [<Literal>] LastModified = "Last-Modified"
    /// Used to express a typed relationship with another resource, where the relation type is defined by RFC 5988
    let [<Literal>] Link = "Link"
    /// Used in redirection, or when a new resource has been created.
    let [<Literal>] Location = "Location"
    /// This header is supposed to set P3P policy
    let [<Literal>] P3P = "P3P"
    /// Implementation-specific headers that may have various effects anywhere along the request-response chain.
    let [<Literal>] Pragma = "Pragma"
    /// Request authentication to access the proxy.
    let [<Literal>] ProxyAuthenticate = "Proxy-Authenticate"
    /// Used in redirection, or when a new resource has been created. This refresh redirects after 5 seconds.
    let [<Literal>] Refresh = "Refresh"
    /// If an entity is temporarily unavailable, this instructs the client to try again later. Value could be a specified period of time (in seconds) or a HTTP-date.[28]
    let [<Literal>] RetryAfter = "Retry-After"
    /// A name for the server
    let [<Literal>] Server = "Server"
    /// An HTTP cookie
    let [<Literal>] SetCookie = "Set-Cookie"
    /// The HTTP status of the response
    let [<Literal>] Status = "Status"
    /// A HSTS Policy informing the HTTP client how long to cache the HTTPS only policy and whether this applies to subdomains.
    let [<Literal>] StrictTransportSecurity = "Strict-Transport-Security"
    /// The Trailer general field value indicates that the given set of header fields is present in the trailer of a message encoded with chunked transfer-coding.
    let [<Literal>] Trailer = "Trailer"
    /// The form of encoding used to safely transfer the entity to the user. Currently defined methods are: chunked, compress, deflate, gzip, identity.
    let [<Literal>] TransferEncoding = "Transfer-Encoding"
    /// Tells downstream proxies how to match future request headers to decide whether the cached response can be used rather than requesting a fresh one from the origin server.
    let [<Literal>] Vary = "Vary"
    /// Informs the client of proxies through which the response was sent. 
    let [<Literal>] Via = "Via"
    /// A general warning about possible problems with the entity body.
    let [<Literal>] Warning = "Warning"
    /// Indicates the authentication scheme that should be used to access the requested entity.
    let [<Literal>] WWWAuthenticate = "WWW-Authenticate"

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
    /// If the same header is present multiple times, the values will be concatenated with comma as the separator
    Headers : Map<string,string>
    Cookies : Map<string,string> }

/// The response returned by an HTTP request with direct access to the response stream
type HttpResponseWithStream =
  { ResponseStream : Stream
    StatusCode: int
    ResponseUrl : string
    /// If the same header is present multiple times, the values will be concatenated with comma as the separator
    Headers : Map<string,string>
    Cookies : Map<string,string> }

/// Constants for common HTTP content types
module HttpContentTypes =
    /// */*
    let [<Literal>] Any = "*/*"
    /// plain/text
    let [<Literal>] Text = "text/plain"
    /// application/octet-stream
    let [<Literal>] Binary = "application/octet-stream"
    /// application/octet-stream
    let [<Literal>] Zip = "application/zip"
    /// application/octet-stream
    let [<Literal>] GZip = "application/gzip"
    /// application/x-www-form-urlencoded
    let [<Literal>] FormValues = "application/x-www-form-urlencoded"
    /// application/json
    let [<Literal>] Json = "application/json"
    /// application/javascript
    let [<Literal>] JavaScript = "application/javascript"
    /// application/xml
    let [<Literal>] Xml = "application/xml"
    /// application/rss+xml
    let [<Literal>] Rss = "application/rss+xml"
    /// application/atom+xml
    let [<Literal>] Atom = "application/atom+xml"
    /// application/rdf+xml
    let [<Literal>] Rdf = "application/rdf+xml"
    /// text/html
    let [<Literal>] Html = "text/html"
    /// application/xhtml+xml
    let [<Literal>] XHtml = "application/xhtml+xml"
    /// application/soap+xml
    let [<Literal>] Soap = "application/soap+xml"
    /// text/csv
    let [<Literal>] Csv = "text/csv"

type private HeaderEnum = System.Net.HttpRequestHeader

/// Constants for common HTTP encodings
module HttpEncodings = 

    /// ISO-8859-1
    let PostDefaultEncoding = Encoding.GetEncoding("ISO-8859-1") // http://stackoverflow.com/questions/708915/detecting-the-character-encoding-of-an-http-post-request/708942#708942

    /// ISO-8859-1
    let ResponseDefaultEncoding = Encoding.GetEncoding("ISO-8859-1") // http://www.ietf.org/rfc/rfc2616.txt

    let internal getEncoding (encodingStr:string) = 
#if FX_NO_GETENCODING_BY_CODEPAGE
        Encoding.GetEncoding encodingStr
#else
        match Int32.TryParse(encodingStr, NumberStyles.Integer, CultureInfo.InvariantCulture) with
        | true, codepage -> Encoding.GetEncoding codepage                
        | _ -> Encoding.GetEncoding encodingStr
#endif

[<AutoOpen>]
module private HttpHelpers =

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

    let getProperty (typ:Type) obj prop =        
#if FX_NET_CORE_REFLECTION
        let prop = try typ.GetRuntimeProperty prop with _ -> null
        if prop <> null && prop.CanRead then
            try
                prop.GetValue(obj) |> unbox |> Some
            with _ -> 
                None
        else
            None
#else
        let prop = try typ.GetProperty prop with _ -> null
        if prop <> null && prop.CanRead then
            try
                prop.GetValue(obj, [| |]) |> unbox |> Some
            with _ -> 
                None
        else
            None
#endif

    let (?) obj prop =
        getProperty (obj.GetType()) obj prop

    let (?<-) obj prop value =
#if FX_NET_CORE_REFLECTION
        let prop = obj.GetType().GetRuntimeProperty(prop)
        if prop <> null && prop.CanWrite then
            try 
                prop.SetValue(obj, box value) |> ignore
                true
            with _ -> 
                false
        else
            false
#else
        let prop = obj.GetType().GetProperty(prop)
        if prop <> null && prop.CanWrite then
            try 
                prop.SetValue(obj, value, [| |]) |> ignore
                true
            with _ -> 
                false
        else
            false
#endif

    let writeBody (req:HttpWebRequest) (postBytes:byte[]) =
        // On Mono, a bug in HttpWebRequest causes a deadlock when using it with Async.FromBeginEnd
        // To work around, we use a different FromBeginEnd
        // See https://github.com/fsharp/FSharp.Data/issues/762
        // and https://bugzilla.xamarin.com/show_bug.cgi?id=25519
        let alternateFromBeginEnd beginAction endAction obj =
            Threading.Tasks.TaskFactory().FromAsync(
                Func<_, _, _>(fun c s -> beginAction(c, s)),
                Func<_, _>(endAction),
                obj)
            |> Async.AwaitTask

        async {
#if FX_NO_WEBREQUEST_CONTENTLENGTH
            ignore (req?ContentLength <- int64 postBytes.Length)
#else
            req.ContentLength <- int64 postBytes.Length
#endif
            use! output =
                if Type.GetType("Mono.Runtime") <> null
                then alternateFromBeginEnd req.BeginGetRequestStream req.EndGetRequestStream req
                else Async.FromBeginEnd(req.BeginGetRequestStream, req.EndGetRequestStream)

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
                let name1, name2 = fst header, fst visitedHeader 
                if name1 = name2 then failwithf "Repeated headers: %A %A" visitedHeader header
            checkForRepeatedHeaders (header::visitedHeaders) remainingHeaders

    let setHeaders headers (req:HttpWebRequest) =
        let hasContentType = ref false
        checkForRepeatedHeaders [] headers
        headers |> List.iter (fun (header:string, value) ->
            match header.ToLowerInvariant() with
            | "accept" -> req.Accept <- value
            | "accept-charset" -> req.Headers.[HeaderEnum.AcceptCharset] <- value
            | "accept-datetime" -> req.Headers.["Accept-Datetime"] <- value
            | "accept-encoding" -> req.Headers.[HeaderEnum.AcceptEncoding] <- value
            | "accept-language" -> req.Headers.[HeaderEnum.AcceptLanguage] <- value
            | "allow" -> req.Headers.[HeaderEnum.Allow] <- value
            | "authorization" -> req.Headers.[HeaderEnum.Authorization] <- value
            | "cache-control" -> req.Headers.[HeaderEnum.CacheControl] <- value
#if FX_NO_WEBREQUEST_CONNECTION
            | "connection" -> if not (req?Connection <- value) then req.Headers.[HeaderEnum.Connection] <- value
#else
            | "connection" -> req.Connection <- value
#endif
            | "content-encoding" -> req.Headers.[HeaderEnum.ContentEncoding] <- value
            | "content-Language" -> req.Headers.[HeaderEnum.ContentLanguage] <- value
            | "content-Location" -> req.Headers.[HeaderEnum.ContentLocation] <- value
            | "content-md5" -> req.Headers.[HeaderEnum.ContentMd5] <- value
            | "content-range" -> req.Headers.[HeaderEnum.ContentRange] <- value
            | "content-type" ->
                req.ContentType <- value
                hasContentType := true
#if FX_NO_WEBREQUEST_DATE
            | "date" -> if not (req?Date <- DateTime.ParseExact(value, "R", CultureInfo.InvariantCulture)) then req.Headers.[HeaderEnum.Date] <- value
#else
            | "date" -> req.Date <- DateTime.ParseExact(value, "R", CultureInfo.InvariantCulture)
#endif
#if FX_NO_WEBREQUEST_EXPECT
            | "expect" -> if not (req?Expect <- value) then req.Headers.[HeaderEnum.Expect] <- value
#else
            | "expect" -> req.Expect <- value
#endif
            | "expires" -> req.Headers.[HeaderEnum.Expires] <- value
            | "from" -> req.Headers.[HeaderEnum.From] <- value
#if FX_NO_WEBREQUEST_HOST
            | "host" -> if not (req?Host <- value) then req.Headers.[HeaderEnum.Host] <- value
#else
            | "host" -> req.Host <- value
#endif       
            | "if-match" -> req.Headers.[HeaderEnum.IfMatch] <- value
#if FX_NO_WEBREQUEST_IFMODIFIEDSINCE
            | "if-modified-since" -> if not (req?IfModifiedSince <- DateTime.ParseExact(value, "R", CultureInfo.InvariantCulture)) then req.Headers.[HeaderEnum.IfModifiedSince] <- value
#else
            | "if-modified-since" -> req.IfModifiedSince <- DateTime.ParseExact(value, "R", CultureInfo.InvariantCulture)
#endif
            | "if-none-match" -> req.Headers.[HeaderEnum.IfNoneMatch] <- value
            | "if-range" -> req.Headers.[HeaderEnum.IfRange] <- value
            | "if-unmodified-since" -> req.Headers.[HeaderEnum.IfUnmodifiedSince] <- value
            | "keep-alive" -> req.Headers.[HeaderEnum.KeepAlive] <- value
            | "last-modified" -> req.Headers.[HeaderEnum.LastModified] <- value
            | "max-forwards" -> req.Headers.[HeaderEnum.MaxForwards] <- value
            | "origin" -> req.Headers.["Origin"] <- value
            | "pragma" -> req.Headers.[HeaderEnum.Pragma] <- value
#if FX_NO_WEBREQUEST_RANGE
            | "range" -> req.Headers.[HeaderEnum.Range] <- value
#else
            | "range" -> 
                if not (value.StartsWith("bytes=")) then failwith "Invalid value for the Range header"
                let bytes = value.Substring("bytes=".Length).Split('-')
                if bytes.Length <> 2 then failwith "Invalid value for the Range header"
                req.AddRange(int64 bytes.[0], int64 bytes.[1])
#endif
            | "proxy-authorization" -> req.Headers.[HeaderEnum.ProxyAuthorization] <- value
#if FX_NO_WEBREQUEST_REFERER
            | "referer" -> if not (req?Referer <- value) then try req.Headers.[HeaderEnum.Referer] <- value with _ -> ()
#else
            | "referer" -> req.Referer <- value
#endif            
            | "te" -> req.Headers.[HeaderEnum.Te] <- value
            | "trailer" -> req.Headers.[HeaderEnum.Trailer] <- value
            | "translate" -> req.Headers.[HeaderEnum.Translate] <- value
            | "upgrade" -> req.Headers.[HeaderEnum.Upgrade] <- value
#if FX_NO_WEBREQUEST_USERAGENT
            | "user-agent" -> if not (req?UserAgent <- value) then try req.Headers.[HeaderEnum.UserAgent] <- value with _ -> ()
#else
            | "user-agent" -> req.UserAgent <- value
#endif
            | "via" -> req.Headers.[HeaderEnum.Via] <- value
            | "warning" -> req.Headers.[HeaderEnum.Warning] <- value
            | _ -> req.Headers.[header] <- value
        )
        hasContentType.Value

    let getResponse (req:HttpWebRequest) silentHttpErrors =
        if defaultArg silentHttpErrors false then
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

    // No inlining to don't cause a depency on ZLib.Portable when a PCL version of FSharp.Data is used in full .NET
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let decompressGZip (memoryStream:MemoryStream) =
#if FX_NO_WEBREQUEST_AUTOMATICDECOMPRESSION
        new MemoryStream(Ionic.Zlib.GZipStream.UncompressBuffer(memoryStream.ToArray()))
#else
        failwith "Automatic gzip decompression failed"
        memoryStream
#endif

    // No inlining to don't cause a depency on ZLib.Portable when a PCL version of FSharp.Data is used in full .NET
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let decompressDeflate (memoryStream:MemoryStream) =
#if FX_NO_WEBREQUEST_AUTOMATICDECOMPRESSION
        new MemoryStream(Ionic.Zlib.DeflateStream.UncompressBuffer(memoryStream.ToArray()))
#else
        failwith "Automatic deflate decompression failed"
        memoryStream
#endif

    let toHttpResponse forceText responseUrl statusCode contentType contentEncoding
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
                mimeType.StartsWith "application/" && mimeType.EndsWith "+xml" ||
                mimeType.StartsWith "application/" && mimeType.EndsWith "+json"
            mimeType.Split([| ';' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.exists isText

        use stream = stream
        let! memoryStream = asyncRead stream

        let memoryStream = 
            // this only applies when automatic decompression is off
            if contentEncoding = "gzip" then decompressGZip memoryStream
            elif contentEncoding = "deflate" then decompressDeflate memoryStream
            else memoryStream

        let respBody = 
            if forceText || isText contentType then
                let encoding =
                    match (defaultArg responseEncodingOverride ""), characterSet with
                    | "", "" -> HttpEncodings.ResponseDefaultEncoding
                    // some web servers respond with broken things like Content-Type: text/xml; charset="UTF-8"
                    // this goes against rfc2616, but it breaks Encoding.GetEncoding, so let us strip this char out
                    | "", characterSet -> Encoding.GetEncoding (characterSet.Replace("\"",""))
                    | responseEncodingOverride, _ -> HttpEncodings.getEncoding responseEncodingOverride
                use sr = new StreamReader(memoryStream, encoding)
                sr.ReadToEnd() |> Text
            else
                memoryStream.ToArray() |> Binary

        return { Body = respBody
                 StatusCode = statusCode
                 ResponseUrl = responseUrl
                 Headers = headers
                 Cookies = cookies }
    }

#if FX_NO_WEBREQUEST_AUTOMATICDECOMPRESSION
    let isWindowsPhone =
        let runningOnMono = Type.GetType("Mono.Runtime") <> null
        if runningOnMono then
            false
        else
            match getProperty typeof<Environment> null "OSVersion" with
            | Some osVersion ->
                match osVersion?Version with
                | Some (version:Version) ->
                    // Latest Windows is 6.x, so OS >= 8 will be Windows Phone
                    version.Major >= 8
                | _ -> false
            | _ -> false
#endif

    // .NET has trouble parsing some cookies. See http://stackoverflow.com/a/22098131/165633
    let getAllCookiesFromHeader (header:string) (responseUri:Uri) (cookieContainer:CookieContainer) =
        let cookiesWithWrongSplit = header.Replace("\r", "").Replace("\n", "").Split(',')
        let cookies = ResizeArray()
        let mutable i = 0
        while i < cookiesWithWrongSplit.Length do
            // the next command is not a new cookie but part of the current one
            if cookiesWithWrongSplit.[i].IndexOf("expires=", StringComparison.OrdinalIgnoreCase) > 0 then
                cookies.Add(cookiesWithWrongSplit.[i] + "," + cookiesWithWrongSplit.[i + 1])
                i <- i + 1
            else
                cookies.Add(cookiesWithWrongSplit.[i])
            i <- i + 1
        for cookieStr in cookies do
            let cookie = new Cookie()
            cookieStr.Split ';'
            |> Array.iteri (fun i cookiePart ->
                let cookiePart = cookiePart.Trim()
                if i = 0 then
                    let firstEqual = cookiePart.IndexOf "="
                    cookie.Name <- cookiePart.Substring(0, firstEqual)
                    cookie.Value <- cookiePart.Substring(firstEqual + 1)
                elif cookiePart.IndexOf("path", StringComparison.OrdinalIgnoreCase) = 0 then
                    let kvp = cookiePart.Split '='
                    if kvp.[1] <> "" && kvp.[1] <> "/" then
                        cookie.Path <- kvp.[1]
                elif cookiePart.IndexOf("domain", StringComparison.OrdinalIgnoreCase) = 0 then
                    let kvp = cookiePart.Split '='
                    if kvp.[1] <> "" then
                        cookie.Domain <- kvp.[1]
                elif cookiePart.Equals("secure", StringComparison.OrdinalIgnoreCase) then
                    cookie.Secure <- true
                elif cookiePart.Equals("httponly", StringComparison.OrdinalIgnoreCase) then
                    cookie.HttpOnly <- true
            )

            if cookie.Domain = "" then
                cookie.Domain <- responseUri.Host

            let uri = Uri((if cookie.Secure then "https://" else "http://") + cookie.Domain.TrimStart('.') + cookie.Path)
            cookieContainer.Add(uri, cookie)

/// Utilities for working with network via HTTP. Includes methods for downloading 
/// resources with specified headers, query parameters and HTTP body
[<AbstractClass>]
type Http private() =

    static let regexOptions = 
#if FX_NO_REGEX_COMPILATION
        RegexOptions.None
#else
        RegexOptions.Compiled
#endif

    static let charsetRegex = Regex("charset=([^;\s]*)", regexOptions)

    /// Appends the query parameters to the url, taking care of proper escaping
    static member internal AppendQueryToUrl(url:string, query) =
        match query with
        | [] -> url
        | query ->
            url
            + if url.Contains "?" then "&" else "?"
            + String.concat "&" [ for k, v in query -> Uri.EscapeDataString k + "=" + Uri.EscapeDataString v ]

    static member private InnerRequest(url:string, toHttpResponse, [<Optional>] ?query, [<Optional>] ?headers:seq<_>, [<Optional>] ?httpMethod, [<Optional>] ?body, [<Optional>] ?cookies:seq<_>, [<Optional>] ?cookieContainer, 
                                       ?silentHttpErrors, [<Optional>] ?responseEncodingOverride, [<Optional>] ?customizeHttpRequest) =
        let uri = 
            Uri(Http.AppendQueryToUrl(url, defaultArg query []))
            |> UriUtils.enableUriSlashes

        // do not use WebRequest.CreateHttp otherwise silverlight proxies don't work
        let req = WebRequest.Create(uri) :?> HttpWebRequest

        // set method
        let defaultMethod = if body.IsSome then HttpMethod.Post else HttpMethod.Get
        req.Method <- (defaultArg httpMethod defaultMethod).ToString()

        // set headers
        let headers = defaultArg (Option.map List.ofSeq headers) []
        let hasContentType = setHeaders headers req

        let nativeAutomaticDecompression = ref true

    #if FX_NO_WEBREQUEST_AUTOMATICDECOMPRESSION
        if isWindowsPhone || not (req?AutomaticDecompression <- 3) then 
            nativeAutomaticDecompression := false
            req.Headers.[HeaderEnum.AcceptEncoding] <- "gzip,deflate"
    #else
        req.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.Deflate
    #endif

        // set cookies
        let cookieContainer = defaultArg cookieContainer (new CookieContainer())

        match cookies with
        | None -> ()
        | Some cookies -> cookies |> List.ofSeq |> List.iter (fun (name, value) -> cookieContainer.Add(req.RequestUri, Cookie(name, value)))
        try
            req.CookieContainer <- cookieContainer
        with :? NotImplementedException ->
            // silverlight doesn't support setting cookies
            if cookies.IsSome then
                failwith "Cookies not supported by this platform"

        let getEncoding contentType =
            let charset = charsetRegex.Match(contentType)
            if charset.Success then
                Encoding.GetEncoding charset.Groups.[1].Value
            else
                HttpEncodings.PostDefaultEncoding

        let body = body |> Option.map (fun body ->

            let defaultContentType, (bytes: Encoding -> byte[]) =
                match body with
                | TextRequest text -> HttpContentTypes.Text, (fun e -> e.GetBytes(text))
                | BinaryUpload bytes -> HttpContentTypes.Binary, (fun _ -> bytes)
                | FormValues values ->
                    let bytes (e:Encoding) =
                        [ for k, v in values -> Uri.EscapeDataString k + "=" + Uri.EscapeDataString v ]
                        |> String.concat "&"
                        |> e.GetBytes
                    HttpContentTypes.FormValues, bytes

            // Set default content type if it is not specified by the user
            let encoding =
                if not hasContentType then
                    req.ContentType <- defaultContentType

                getEncoding req.ContentType

            bytes encoding)

        // Send the request and get the response
        augmentWebExceptionsWithDetails <| fun () -> async {

            let req = 
                match customizeHttpRequest with
                | Some customizeHttpRequest -> customizeHttpRequest req
                | None -> req
   
            match body with
            | Some body -> do! writeBody req body
            | None -> ()

            let! resp = getResponse req silentHttpErrors

            let headers = 
                [ for header in resp.Headers.AllKeys do 
                    yield header, resp.Headers.[header] ]
                |> Map.ofList
                
            match headers.TryFind HttpResponseHeaders.SetCookie with
            | Some cookieHeader -> getAllCookiesFromHeader cookieHeader resp.ResponseUri cookieContainer
            | None -> ()

            let cookies = Map.ofList [ for cookie in cookieContainer.GetCookies uri |> Seq.cast<Cookie> -> cookie.Name, cookie.Value ]  

            let contentType = if resp.ContentType = null then "application/octet-stream" else resp.ContentType

            let statusCode, characterSet = 
                match resp with
                | :? HttpWebResponse as resp -> 
#if FX_NO_WEBRESPONSE_CHARACTERSET
                    int resp.StatusCode, (defaultArg resp?CharacterSet "")
#else
                    int resp.StatusCode, resp.CharacterSet
#endif
                | _ -> 0, ""

            let characterSet = if characterSet = null then "" else characterSet

            let contentEncoding = 
                // .NET removes the gzip/deflate from the content encoding header when it handles the decompression itself, but Mono doesn't, so we clear it explicitely
                if !nativeAutomaticDecompression then ""
                else defaultArg (Map.tryFind HttpResponseHeaders.ContentEncoding headers) ""

            let stream = resp.GetResponseStream()

            return! toHttpResponse resp.ResponseUri.OriginalString statusCode contentType contentEncoding characterSet responseEncodingOverride cookies headers stream
        }

    /// Download an HTTP web resource from the specified URL asynchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
    static member AsyncRequest(url, [<Optional>] ?query, [<Optional>] ?headers, [<Optional>] ?httpMethod, [<Optional>] ?body, [<Optional>] ?cookies, [<Optional>] ?cookieContainer, [<Optional>] ?silentHttpErrors, [<Optional>] ?responseEncodingOverride, [<Optional>] ?customizeHttpRequest) = 
        Http.InnerRequest(url, toHttpResponse (*forceText*)false, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                          ?silentHttpErrors=silentHttpErrors, ?responseEncodingOverride=responseEncodingOverride, ?customizeHttpRequest=customizeHttpRequest)

    /// Download an HTTP web resource from the specified URL asynchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
    static member AsyncRequestString(url, [<Optional>] ?query, [<Optional>] ?headers, [<Optional>] ?httpMethod, [<Optional>] ?body, [<Optional>] ?cookies, [<Optional>] ?cookieContainer, [<Optional>] ?silentHttpErrors, [<Optional>] ?responseEncodingOverride, [<Optional>] ?customizeHttpRequest) = async {
        let! response = Http.InnerRequest(url, toHttpResponse (*forceText*)true, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer,
                                          ?silentHttpErrors=silentHttpErrors, ?responseEncodingOverride=responseEncodingOverride, ?customizeHttpRequest=customizeHttpRequest)
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
    static member AsyncRequestStream(url, [<Optional>] ?query, [<Optional>] ?headers, [<Optional>] ?httpMethod, [<Optional>] ?body, [<Optional>] ?cookies, [<Optional>] ?cookieContainer, [<Optional>] ?silentHttpErrors, [<Optional>] ?customizeHttpRequest) =
        let toHttpResponse responseUrl statusCode _contentType contentEncoding _characterSet _responseEncodingOverride cookies headers stream = async {
            let! stream = async {
                // this only applies when automatic decompression is off
                if contentEncoding = "gzip" then
                    use stream = stream
                    let! memoryStream = asyncRead stream
                    return decompressGZip memoryStream :> Stream
                elif contentEncoding = "deflate" then
                    use stream = stream
                    let! memoryStream = asyncRead stream
                    return decompressDeflate memoryStream :> Stream
                else
                    return stream }
            return { ResponseStream = stream
                     StatusCode = statusCode
                     ResponseUrl = responseUrl
                     Headers = headers
                     Cookies = cookies }
        }
        Http.InnerRequest(url, toHttpResponse, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer,
                          ?silentHttpErrors=silentHttpErrors, ?customizeHttpRequest=customizeHttpRequest)

    /// Download an HTTP web resource from the specified URL synchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
    static member Request(url, [<Optional>] ?query, [<Optional>] ?headers, [<Optional>] ?httpMethod, [<Optional>] ?body, [<Optional>] ?cookies, [<Optional>] ?cookieContainer, [<Optional>] ?silentHttpErrors, [<Optional>] ?responseEncodingOverride, [<Optional>] ?customizeHttpRequest) = 
        Http.AsyncRequest(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                          ?silentHttpErrors=silentHttpErrors, ?responseEncodingOverride=responseEncodingOverride, ?customizeHttpRequest=customizeHttpRequest)
        |> Async.RunSynchronously

    /// Download an HTTP web resource from the specified URL synchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
    static member RequestString(url, [<Optional>] ?query, [<Optional>] ?headers, [<Optional>] ?httpMethod, [<Optional>] ?body, [<Optional>] ?cookies, [<Optional>] ?cookieContainer, [<Optional>] ?silentHttpErrors, [<Optional>] ?responseEncodingOverride, [<Optional>] ?customizeHttpRequest) = 
        Http.AsyncRequestString(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                                ?silentHttpErrors=silentHttpErrors, ?responseEncodingOverride=responseEncodingOverride, ?customizeHttpRequest=customizeHttpRequest)
        |> Async.RunSynchronously

    /// Download an HTTP web resource from the specified URL synchronously
    /// (allows specifying query string parameters and HTTP headers including
    /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
    /// The body for POST request can be specified either as text or as a list of parameters
    /// that will be encoded, and the method will automatically be set if not specified
    static member RequestStream(url, [<Optional>] ?query, [<Optional>] ?headers, [<Optional>] ?httpMethod, [<Optional>] ?body, [<Optional>] ?cookies, [<Optional>] ?cookieContainer, [<Optional>] ?silentHttpErrors, [<Optional>] ?customizeHttpRequest) = 
        Http.AsyncRequestStream(url, ?query=query, ?headers=headers, ?httpMethod=httpMethod, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, 
                                ?silentHttpErrors=silentHttpErrors, ?customizeHttpRequest=customizeHttpRequest)
        |> Async.RunSynchronously
