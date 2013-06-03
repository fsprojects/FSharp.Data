// --------------------------------------------------------------------------------------
// Utilities for working with network, downloading resources with specified headers etc.
// --------------------------------------------------------------------------------------

namespace FSharp.Net

open System
open System.IO
open System.Net
open System.Text
open System.Reflection
open System.Collections.Generic

[<RequireQualifiedAccess>]
type HttpResponseBody =
    | Text of string
    | Binary of byte[]

type HttpResponse =
  { Body : HttpResponseBody
    Headers : Map<string,string> 
    ResponseUrl : string
    Cookies : Map<string,string> }

/// Utilities for working with network via HTTP. Includes methods for downloading 
/// resources with specified headers, query parameters and HTTP body
type Http private() = 

#if FX_NO_URI_WORKAROUND
#else
  /// Are we currently running on Mono?
  /// (Mono does not have the issue with encoding slashes in URLs, so we do not need workaround)
  static let runningOnMono = Type.GetType("Mono.Runtime") <> null
#endif

  /// Returns a clone of a System.Uri object that allows URL encoded slashes.
  /// (This is an ugly hack using Reflection, but it is the best way to do this
  /// in a type provider. See [this StackOverflow answer][1].
  ///
  ///  [1]: http://stackoverflow.com/questions/781205/getting-a-url-with-an-url-encoded-slash
  ///
  static let enableUriSlashes (uri:Uri) =
#if FX_NO_URI_WORKAROUND
#else
    if runningOnMono then uri else
    let uri = Uri(uri.OriginalString)
    let paq = uri.PathAndQuery
    let flagsFieldInfo = typeof<Uri>.GetField("m_Flags", BindingFlags.Instance ||| BindingFlags.NonPublic)
    let flags = flagsFieldInfo.GetValue(uri) :?> uint64
    let flags = flags &&& (~~~0x30UL)
    flagsFieldInfo.SetValue(uri, flags)
#endif
    uri

  /// Read the contents of a stream asynchronously and return it as a string
  static let asyncReadToEnd (stream:Stream) isText = async {
    // Allocate 4kb buffer for downloading dat
    let buffer = Array.zeroCreate (4 * 1024)
    use output = new MemoryStream()
    let reading = ref true

    while reading.Value do
      // Download one (at most) 4kb chunk and copy it
      let! count = stream.AsyncRead(buffer, 0, buffer.Length)
      output.Write(buffer, 0, count)
      reading := count > 0

    // Read all data into a string
    output.Seek(0L, SeekOrigin.Begin) |> ignore
    return 
        if isText then
            use sr = new StreamReader(output)
            sr.ReadToEnd() |> HttpResponseBody.Text
        else
            output.ToArray() |> HttpResponseBody.Binary }

  static member inline internal reraisePreserveStackTrace (e:Exception) =
    let remoteStackTraceString = 
      typeof<exn>.GetField("_remoteStackTraceString", BindingFlags.Instance ||| BindingFlags.NonPublic)
    if remoteStackTraceString <> null then
      remoteStackTraceString.SetValue(e, e.StackTrace + Environment.NewLine)
    raise e

  static member private InnerRequest(url:string, forceText, ?cert, ?query, ?headers, ?meth, ?body, ?bodyValues, ?cookies, ?cookieContainer) = async {

    // Format query parameters
    let url = 
      match query with
      | None -> url
      | Some query ->
          url + (if url.Contains "?" then "&" else "?") + (String.concat "&" [ for k, v in query -> k + "=" + v ])
    let uri = Uri url |> enableUriSlashes

    // do not use WebRequest.CreateHttp otherwise the silverlight proxy won't work
    let req = WebRequest.Create(uri) :?> HttpWebRequest

    req.ClientCertificates.Add cert.Value |> ignore

    // set method
    let defaultMethod =
      match body, bodyValues with
      | None, None -> "GET"
      | Some _, Some _ -> failwith "Only one of 'body' or 'bodyValues' may be specified, not both"
      | _ -> "POST"
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

    // set cookies    
    let cookieContainer = defaultArg cookieContainer (new CookieContainer())
    match cookies with
    | None -> ()
    | Some cookies ->
        for name, value in cookies do
          cookieContainer.Add(req.RequestUri, Cookie(name, value))
    req.CookieContainer <- cookieContainer

    // If we want to set some body, encode it with POST data as array of bytes
    let body = 
        match body, bodyValues with 
        | Some _, Some _ -> failwithf "Only body or bodyValues can be specified"
        | _, Some bodyValues ->
            [ for k, v in bodyValues -> Uri.EscapeDataString k + "=" + Uri.EscapeDataString v ]
            |> String.concat "&"
            |> Some
        | body, _ -> body

    match body with
    | Some (text:string) ->
        // Set default content type if it is not specified by the user
        if not !hasContentType then
          req.ContentType <- "application/x-www-form-urlencoded"

        // Write the body 
        let postBytes = Encoding.UTF8.GetBytes(text)
#if FX_NO_WEBREQUEST_CONTENTLENGTH
#else
        req.ContentLength <- int64 postBytes.Length
#endif
        use! output = Async.FromBeginEnd(req.BeginGetRequestStream, req.EndGetRequestStream)
        do! output.AsyncWrite(postBytes, 0, postBytes.Length)
        output.Flush()
    | _ -> ()

    let isText (mimeType:string) =
        mimeType.StartsWith "text/" || 
        mimeType = "application/json" || 
        mimeType = "application/xml" ||
        mimeType = "application/javascript" ||
        mimeType = "application/ecmascript" ||
        mimeType = "application/xml-dtd" ||
        mimeType.StartsWith "application/" && mimeType.EndsWith "+xml"

    // Send the request and get the response       
    try
      use! resp = Async.FromBeginEnd(req.BeginGetResponse, req.EndGetResponse)
      use stream = resp.GetResponseStream()
      let! respBody = asyncReadToEnd stream (forceText || (isText resp.ContentType))
      let cookies = Map.ofList [ for cookie in cookieContainer.GetCookies uri |> Seq.cast<Cookie> -> cookie.Name, cookie.Value ]  
      let headers = Map.ofList [ for header in resp.Headers.AllKeys -> header, resp.Headers.[header] ]
      return { Body = respBody
               Headers = headers
               ResponseUrl = resp.ResponseUri.OriginalString
               Cookies = cookies }
    with 
      // If an exception happens, augment the message with the response
      | :? WebException as exn -> 
        if exn.Response = null then Http.reraisePreserveStackTrace exn
        let responseExn =
            try
              use responseStream = exn.Response.GetResponseStream()
              use streamReader = new StreamReader(responseStream)
              let response = streamReader.ReadToEnd()
              responseStream.Position <- 0L
              if String.IsNullOrEmpty response then None
              else Some(WebException(sprintf "%s\n%s" exn.Message response, exn, exn.Status, exn.Response))
            with _ -> None
        match responseExn with
        | Some e -> raise e
        | None -> Http.reraisePreserveStackTrace exn
        return { Body = HttpResponseBody.Text ""
                 Headers = Map.empty
                 ResponseUrl = uri.OriginalString
                 Cookies = Map.empty }
  }

  /// Download an HTTP web resource from the specified URL asynchronously
  /// (allows specifying query string parameters and HTTP headers including
  /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
  /// The body for POST request can be specified either as text or as a list of parameters
  /// that will be encoded, and the method will automatically be set if not specified
  static member AsyncRequestDetailed(url, ?cert, ?query, ?headers, ?meth, ?body, ?bodyValues, ?cookies, ?cookieContainer) = 
    Http.InnerRequest(url, false, ?cert=cert, ?headers=headers, ?query=query, ?meth=meth, ?body=body, ?bodyValues=bodyValues, ?cookies=cookies, ?cookieContainer=cookieContainer)

  /// Download an HTTP web resource from the specified URL asynchronously
  /// (allows specifying query string parameters and HTTP headers including
  /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
  /// The body for POST request can be specified either as text or as a list of parameters
  /// that will be encoded, and the method will automatically be set if not specified
  static member AsyncRequest(url, ?cert, ?query, ?headers, ?meth, ?body, ?bodyValues, ?cookies, ?cookieContainer) = async {
    let! response = Http.InnerRequest(url, true, ?cert=cert, ?headers=headers, ?query=query, ?meth=meth, ?body=body, ?bodyValues=bodyValues, ?cookies=cookies, ?cookieContainer=cookieContainer)
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
  static member RequestDetailed(url, ?cert, ?query, ?headers, ?meth, ?body, ?bodyValues, ?cookies, ?cookieContainer) = 
    Http.AsyncRequestDetailed(url, ?cert=cert, ?headers=headers, ?query=query, ?meth=meth, ?body=body, ?bodyValues=bodyValues, ?cookies=cookies, ?cookieContainer=cookieContainer)
    |> Async.RunSynchronously

  /// Download an HTTP web resource from the specified URL synchronously
  /// (allows specifying query string parameters and HTTP headers including
  /// headers that have to be handled specially - such as Accept, Content-Type & Referer)
  /// The body for POST request can be specified either as text or as a list of parameters
  /// that will be encoded, and the method will automatically be set if not specified
  static member Request(url, ?cert, ?query, ?headers, ?meth, ?body, ?bodyValues, ?cookies, ?cookieContainer) = 
    Http.AsyncRequest(url, ?cert=cert, ?query=query, ?headers=headers, ?meth=meth, ?body=body, ?bodyValues=bodyValues, ?cookies=cookies, ?cookieContainer=cookieContainer)
    |> Async.RunSynchronously
