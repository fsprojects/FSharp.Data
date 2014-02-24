// --------------------------------------------------------------------------------------
// Utilities for working with network, downloading resources with specified headers etc.
// --------------------------------------------------------------------------------------

namespace FSharp.Data

open System
open System.IO
open System.Security
open System.Net
open System.Text
open System.Reflection

[<RequireQualifiedAccess>]
/// The body to send in an HTTP request
type RequestBody =
    | Text of string
    | Binary of byte[]
    | FormValues of seq<string * string>

[<RequireQualifiedAccess>]
/// The response body returned by an HTTP request
type ResponseBody =
    | Text of string
    | Binary of byte[]

/// The response returned by an HTTP request
type HttpResponse =
  { Body : ResponseBody
    Headers : Map<string,string> 
    ResponseUrl : string
    Cookies : Map<string,string>
    StatusCode: int }

/// Utilities for working with network via HTTP. Includes methods for downloading 
/// resources with specified headers, query parameters and HTTP body
type Http private() = 

  /// consumes a stream asynchronously until the end
  /// and returns a memory stream with the full content
  static let asyncRead (stream:Stream) = async {
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

  static let writeBody (req:HttpWebRequest) (postBytes:byte[]) = async { 
#if FX_NO_WEBREQUEST_CONTENTLENGTH
#else
      req.ContentLength <- int64 postBytes.Length
#endif
      use! output = Async.FromBeginEnd(req.BeginGetRequestStream, req.EndGetRequestStream)
      do! output.AsyncWrite(postBytes, 0, postBytes.Length)
      output.Flush()
  }

  static let reraisePreserveStackTrace (e:Exception) =
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

  static let augmentWebExceptionsWithDetails f = async {
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

  static let toHttpResponse forceText responseUrl (contentType:string) (_contentEncoding:string) statusCode cookies headers (memoryStream:MemoryStream) =

    let isText (mimeType:string) =
      let isText (mimeType:string) =
        let mimeType = mimeType.Trim()
        mimeType.StartsWith "text/" || 
        mimeType = "application/json" || 
        mimeType = "application/xml" ||
        mimeType = "application/javascript" ||
        mimeType = "application/ecmascript" ||
        mimeType = "application/xml-dtd" ||
        mimeType.StartsWith "application/" && mimeType.EndsWith "+xml"
      mimeType.Split([| ';' |], StringSplitOptions.RemoveEmptyEntries)
      |> Array.exists isText

    let respBody = 
#if FX_NO_WEBREQUEST_AUTOMATICDECOMPRESSION
      let memoryStream = 
        if _contentEncoding = "gzip" then
          new MemoryStream(Ionic.Zlib.GZipStream.UncompressBuffer(memoryStream.ToArray()))
        elif _contentEncoding = "deflate" then
          new MemoryStream(Ionic.Zlib.DeflateStream.UncompressBuffer(memoryStream.ToArray()))
        else
          memoryStream
#endif
      if forceText || (isText contentType) then
          use sr = new StreamReader(memoryStream)
          sr.ReadToEnd() |> ResponseBody.Text
      else
          memoryStream.ToArray() |> ResponseBody.Binary

    { Body = respBody
      Headers = headers
      ResponseUrl = responseUrl
      Cookies = cookies
      StatusCode = statusCode }

  /// Appends the query parameters to the url, taking care of proper escaping
  static member internal AppendQueryToUrl(url:string, query, ?valuesArePlaceholders) =
    match query with
    | [] -> url
    | query ->
        let valuesArePlaceholders = defaultArg valuesArePlaceholders false
        url
        + if url.Contains "?" then "&" else "?"
        + String.concat "&" [ for k, v in query -> Uri.EscapeUriString k + "=" + if valuesArePlaceholders then v else Uri.EscapeUriString v ]

#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
  static member internal InnerRequest(url:string, toHttpResponse, ?query, ?headers, ?meth, ?body, ?cookies, ?cookieContainer) = async {
#else
  static member internal InnerRequest(url:string, toHttpResponse, ?query, ?headers, ?meth, ?body, ?cookies, ?cookieContainer, ?certificate) = async {
#endif
    let uri = 
        Uri(Http.AppendQueryToUrl(url, defaultArg query []))
        |> UriUtils.enableUriSlashes
   
    let toSecureString str =
        let securedStr = new SecureString()
        String.iter securedStr.AppendChar str
        securedStr
  
    let removeAuthorizationPart(uri:Uri) =
        new Uri(sprintf "%s%s%s%s" uri.Scheme Uri.SchemeDelimiter uri.Authority uri.PathAndQuery)

    let removeQueryPart(uri:Uri) =
        new Uri(sprintf "%s%s%s%s" uri.Scheme Uri.SchemeDelimiter uri.Authority uri.AbsolutePath)

    let createCredentialCache(authUri, userName:string, password:SecureString) =             
        // The given credentials will be added to the cache with both HTTP Basic Authentication and Digest methods
        // and the the software stacks will negotiate the appropriate authorization method.
        let cc = new CredentialCache()
        cc.Add(authUri, "BASIC", new NetworkCredential(UserName = userName, SecurePassword = password))
        cc.Add(authUri, "DIGEST", new NetworkCredential(UserName = userName, SecurePassword = password))
        cc
            
    let createRequestWithCredentials(uri:Uri, credentials:CredentialCache) = 
        // PreAuthenticate is set in order to reduce the number of authorization headers sent when making several requests to the same URI.
        let client = WebRequest.Create(uri) :?> HttpWebRequest
        client.Credentials <- credentials
        client.PreAuthenticate <- true
        client

    // do not use WebRequest.CreateHttp otherwise silverlight proxies don't work
    let createRequest(uri:Uri) = 
        match uri.UserInfo.Split([|':'|]) with
        | [|userName; password|] -> createRequestWithCredentials(uri |> removeQueryPart, createCredentialCache(uri |> removeAuthorizationPart |> removeQueryPart, userName, password |> toSecureString))
        | _ ->  WebRequest.Create(uri) :?> HttpWebRequest
    
    let req = createRequest uri
    
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
#else
    certificate |> Option.map req.ClientCertificates.Add |> ignore
#endif

    // set method
    let defaultMethod = if body.IsSome then "POST" else "GET"
    req.Method <- (defaultArg meth defaultMethod).ToUpperInvariant()   

    let (|StringEquals|_|) (s1:string) s2 = 
      if s1.Equals(s2, StringComparison.OrdinalIgnoreCase) 
      then Some () else None

    // set headers, but look for special headers like Accept
    let hasContentType = ref false
    headers |> Option.iter (fun headers ->
      for header, value in headers do
        match header with
        | StringEquals "accept" -> req.Accept <- value
        | StringEquals "content-type" -> hasContentType := true; req.ContentType <- value
#if FX_NO_WEBREQUEST_USERAGENT
        | StringEquals "user-agent" -> req.Headers.[HttpRequestHeader.UserAgent] <- value
#else
        | StringEquals "user-agent" -> req.UserAgent <- value
#endif
#if FX_NO_WEBREQUEST_REFERER
        | StringEquals "referer" -> req.Headers.[HttpRequestHeader.Referer] <- value
#else
        | StringEquals "referer" -> req.Referer <- value
#endif
#if FX_NO_WEBREQUEST_HOST
        | StringEquals "host" -> req.Headers.[HttpRequestHeader.Host] <- value
#else
        | StringEquals "host" -> req.Host <- value
#endif       
        | _ -> req.Headers.[header] <- value)

#if FX_NO_WEBREQUEST_AUTOMATICDECOMPRESSION
    req.Headers.[HttpRequestHeader.AcceptEncoding] <- "gzip,deflate"
#else
    req.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.Deflate
#endif

    // set cookies    
    let cookieContainer = defaultArg cookieContainer (new CookieContainer())
    match cookies with
    | None -> ()
    | Some cookies ->
        for name, value in cookies do
          cookieContainer.Add(req.RequestUri, Cookie(name, value))
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
          | RequestBody.Text text -> "text/plain", Encoding.UTF8.GetBytes(text)
          | RequestBody.Binary bytes -> "application/octet-stream", bytes
          | RequestBody.FormValues values -> 
              let bytes = 
                  [ for k, v in values -> Uri.EscapeDataString k + "=" + Uri.EscapeDataString v ]
                  |> String.concat "&"
                  |> Encoding.UTF8.GetBytes
              "application/x-www-form-urlencoded", bytes
        // Set default content type if it is not specified by the user
        if not !hasContentType then req.ContentType <- defaultContentType
        do! writeBody req bytes
    | None -> ()

    // Send the request and get the response
    return! augmentWebExceptionsWithDetails <| fun () -> async {
      use! resp = Async.FromBeginEnd(req.BeginGetResponse, req.EndGetResponse)
      let statusCode = 
          match resp with
          | :? HttpWebResponse as resp -> int resp.StatusCode
          | _ -> 0
      let cookies = Map.ofList [ for cookie in cookieContainer.GetCookies uri |> Seq.cast<Cookie> -> cookie.Name, cookie.Value ]  
      let headers = Map.ofList [ for header in resp.Headers.AllKeys -> header, resp.Headers.[header] ]
      use networkStream = resp.GetResponseStream()
      let! memoryStream = asyncRead networkStream
      let contentEncoding = defaultArg (Map.tryFind "Content-Encoding" headers) ""
      return toHttpResponse resp.ResponseUri.OriginalString resp.ContentType contentEncoding statusCode cookies headers memoryStream }
  }

  /// Download an HTTP web resource from the specified URL asynchronously
  /// (allows specifying query string parameters and HTTP headers including
  /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
  /// The body for POST request can be specified either as text or as a list of parameters
  /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
  static member AsyncRequest(url, ?query, ?headers, ?meth, ?body, ?cookies, ?cookieContainer) = 
    Http.InnerRequest(url, toHttpResponse false, ?query=query, ?headers=headers, ?meth=meth, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer)
#else
  static member AsyncRequest(url, ?query, ?headers, ?meth, ?body, ?cookies, ?cookieContainer, ?certificate) = 
    Http.InnerRequest(url, toHttpResponse false, ?query=query, ?headers=headers, ?meth=meth, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, ?certificate=certificate)
#endif

  /// Download an HTTP web resource from the specified URL asynchronously
  /// (allows specifying query string parameters and HTTP headers including
  /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
  /// The body for POST request can be specified either as text or as a list of parameters
  /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
  static member AsyncRequestString(url, ?query, ?headers, ?meth, ?body, ?cookies, ?cookieContainer) = async {
    let! response = Http.InnerRequest(url, toHttpResponse true, ?query=query, ?headers=headers, ?meth=meth, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer)
#else
  static member AsyncRequestString(url, ?query, ?headers, ?meth, ?body, ?cookies, ?cookieContainer, ?certificate)  = async {
    let! response = Http.InnerRequest(url, toHttpResponse true, ?query=query, ?headers=headers, ?meth=meth, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, ?certificate=certificate)
#endif
    return
        match response.Body with
        | ResponseBody.Text text -> text
        | ResponseBody.Binary binary -> failwithf "Expecting text, but got a binary response (%d bytes)" binary.Length
  }

  /// Download an HTTP web resource from the specified URL synchronously
  /// (allows specifying query string parameters and HTTP headers including
  /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
  /// The body for POST request can be specified either as text or as a list of parameters
  /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
  static member Request(url, ?query, ?headers, ?meth, ?body, ?cookies, ?cookieContainer) = 
    Http.AsyncRequest(url, ?query=query, ?headers=headers, ?meth=meth, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer)
#else
  static member Request(url, ?query, ?headers, ?meth, ?body, ?cookies, ?cookieContainer, ?certificate) = 
    Http.AsyncRequest(url, ?query=query, ?headers=headers, ?meth=meth, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, ?certificate=certificate)
#endif
    |> Async.RunSynchronously

  /// Download an HTTP web resource from the specified URL synchronously
  /// (allows specifying query string parameters and HTTP headers including
  /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
  /// The body for POST request can be specified either as text or as a list of parameters
  /// that will be encoded, and the method will automatically be set if not specified
#if FX_NO_WEBREQUEST_CLIENTCERTIFICATES
  static member RequestString(url, ?query, ?headers, ?meth, ?body, ?cookies, ?cookieContainer) = 
    Http.AsyncRequestString(url, ?query=query, ?headers=headers, ?meth=meth, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer)
#else                              
  static member RequestString(url, ?query, ?headers, ?meth, ?body, ?cookies, ?cookieContainer, ?certificate) = 
    Http.AsyncRequestString(url, ?query=query, ?headers=headers, ?meth=meth, ?body=body, ?cookies=cookies, ?cookieContainer=cookieContainer, ?certificate=certificate)
#endif
    |> Async.RunSynchronously
