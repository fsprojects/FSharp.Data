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

/// Utilities for working with network via HTTP. Includes methods for downloading 
/// resources with specified headers, query parameters and HTTP body
type Http private() = 

  /// Returns a clone of a System.Uri object that allows URL encoded slashes.
  /// (This is an ugly hack using Reflection, but it is the best way to do this
  /// in a type provider. See [this StackOverflow answer][1].
  ///
  ///  [1]: http://stackoverflow.com/questions/781205/getting-a-url-with-an-url-encoded-slash
  ///
  static let enableUriSlashes (uri:Uri) =
#if FX_NO_URI_WORKAROUND
#else
    let uri = Uri(uri.OriginalString)
    let paq = uri.PathAndQuery
    let flagsFieldInfo = typeof<Uri>.GetField("m_Flags", BindingFlags.Instance ||| BindingFlags.NonPublic)
    let flags = flagsFieldInfo.GetValue(uri) :?> uint64
    let flags = flags &&& (~~~0x30UL)
    flagsFieldInfo.SetValue(uri, flags)
#endif
    uri

  /// Read the contents of a stream asynchronously and return it as a string
  static let asyncReadToEnd (stream:Stream) = async {
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
    use sr = new StreamReader(output)
    return sr.ReadToEnd() }

  /// Download an HTTP web resource from the specified URL asynchronously
  static member AsyncRequest(url:string) = async {
    let req = WebRequest.Create(enableUriSlashes(Uri(url)))
    let! resp = req.AsyncGetResponse() 
    use stream = resp.GetResponseStream()
    return! asyncReadToEnd(stream) }

  /// Download an HTTP web resource from the specified URL
  static member Request(url:string) = 
    Http.AsyncRequest(url) |> Async.RunSynchronously

  /// Download an HTTP web resource from the specified URL asynchronously
  /// (allows specifying query string parameters and HTTP headers including
  /// headers that have to be handled specially - such as Accept)
  static member AsyncRequest(url:string, ?query, ?headers, ?meth, ?body) = async {
    let query = defaultArg query []
    let headers = defaultArg headers []
    let meth = (defaultArg meth "GET").ToUpperInvariant()

    // Format query parameters & send HTTP request
    let query = 
      [ for (k, v) in query -> k + "=" + v ]
      |> String.concat "&"
    let url = if query = "" then url else url + "?" + query
    let req = HttpWebRequest.Create(enableUriSlashes(Uri url)) 
    let req = req :?> HttpWebRequest
    req.Method <- meth
    
    // Set headers, but look for special headers like Accept
    for header, value in headers do
      if String.Compare(header, "accept", StringComparison.OrdinalIgnoreCase) = 0 then
        req.Accept <- value
      elif String.Compare(header, "content-type", StringComparison.OrdinalIgnoreCase) = 0 then
        req.ContentType <- value
      else
#if FX_NO_WEBHEADERS_ADD
        req.Headers.[header] <- value
#else
        req.Headers.Add(header, value) 
#endif

    // If we want to set some body, encode it with POST data as array of bytes
    match body with 
    | Some (text:string) ->
        let postBytes = Encoding.UTF8.GetBytes(text)
        if headers |> Seq.forall (fun (header, _) -> String.Compare(header, "content-type", StringComparison.OrdinalIgnoreCase) <> 0) then
          req.ContentType <- "application/x-www-form-urlencoded"
#if FX_NO_WEBREQUEST_CONTENTLENGTH
#else
        req.ContentLength <- int64 postBytes.Length
#endif
        use! output = Async.FromBeginEnd(req.BeginGetRequestStream, req.EndGetRequestStream)
        do! output.AsyncWrite(postBytes, 0, postBytes.Length)
        output.Flush()
    | _ -> ()
    
    // Send the request and get the response       
    use! resp = req.AsyncGetResponse()
    use stream = resp.GetResponseStream()
    return! asyncReadToEnd stream }

  /// Download an HTTP web resource from the specified URL synchronously
  /// (allows specifying query string parameters and HTTP headers including
  /// headers that have to be handled specially - such as Accept)
  static member Request(url:string, ?query, ?headers, ?meth, ?body) = 
    Http.AsyncRequest(url, ?headers=headers, ?query=query, ?meth=meth, ?body=body)
    |> Async.RunSynchronously
