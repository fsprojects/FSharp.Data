// --------------------------------------------------------------------------------------
// Utilities for working with network, downloading resources with specified headers etc.
// --------------------------------------------------------------------------------------

namespace FSharp.Data

open System
open System.IO
open System.Net
open System.Text
open System.Reflection
open System.Collections.Generic

module Importing = 

#if PORTABLE
#else
  /// If the file is not web based, setup an file system watcher that 
  /// invalidates the generated type whenever the file changes
  ///
  /// Asumes that the fileName is a valid file name on the disk
  /// (and not e.g. a web reference)
  let private watchForChanges invalidate (fileName:string) = 
    let path = Path.GetDirectoryName(fileName)
    let name = Path.GetFileName(fileName)
    let watcher = new FileSystemWatcher(Filter = name, Path = path)
    watcher.Changed.Add(fun _ -> invalidate())
    watcher.EnableRaisingEvents <- true
#endif

  /// Resolve the absolute location of a file (or web URL) according to the rules
  /// used by standard F# type providers as described here:
  /// https://github.com/fsharp/fsharpx/issues/195#issuecomment-12141785
  ///
  ///  * if it is web resource, just return it
  ///  * if it is full path, just return it
  ///  * otherwise..
  ///
  ///    At design-time:
  ///      * if the user specified resolution folder, use that
  ///      * use the default resolution folder
  ///    At run-time:
  ///      * if the user specified resolution folder, use that
  ///      * if it is running in F# interactive (config.IsHostedExecution) 
  ///        use the default resolution folder
  ///      * otherwise, use 'CurrentDomain.BaseDirectory'
  ///
  /// Returns the resolved file name, together with a flag specifying 
  /// whether it is web based (and we need WebClient to download it)
  let private resolveFileLocation 
      //note: don't remove the type annotations, as some parameters aren't used in the portable version and will become generic, making the type generation fail
      (designTime:bool) (isHosted:bool, defaultResolutionFolder:string) (resolutionFolder:string) (location:string) =
    
    let isWeb =
      location.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
      location.StartsWith("https://", StringComparison.OrdinalIgnoreCase)

#if PORTABLE
    if not isWeb then
        failwith "Only web locations are supported"
    else
        location, true
#else
    match location with
    | url when isWeb -> url, true
    | fullPath when Path.IsPathRooted fullPath -> fullPath, false
    | relative ->
        let root = 
          if designTime then
            if not (String.IsNullOrEmpty(resolutionFolder)) then resolutionFolder
            else defaultResolutionFolder
          elif isHosted then defaultResolutionFolder
          else AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/')
        Path.Combine(root, relative), false
#endif

  /// Given a type provider configuration and a name passed by user, open 
  /// the file or URL (if it starts with http(s)) and return it as a stream
  let asyncOpenStreamInProvider 
      designTime cfg (invalidate:(Unit->Unit) option) resolutionFolder (location:string) = async {

    let resolvedFileOrUri, isWeb = resolveFileLocation designTime cfg resolutionFolder location

    if isWeb then
      let req = System.Net.WebRequest.Create(Uri(resolvedFileOrUri))
      let! resp = req.AsyncGetResponse() 
      return resp.GetResponseStream()
    else
#if PORTABLE
      return failwith "Only web locations are supported"
#else
      // Open the file, even if it is already opened by another application
      let file = File.Open(resolvedFileOrUri, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
      invalidate |> Option.iter (fun f -> watchForChanges f resolvedFileOrUri)
      return file :> Stream
#endif
  }

  /// Resolve a location of a file (or a web location) and open it for shared
  /// read at runtime (do not monitor file changes and use runtime resolution rules)
  let readTextAtRunTime isHosted defaultResolutionFolder resolutionFolder location = 
    let stream = 
      asyncOpenStreamInProvider false (isHosted, defaultResolutionFolder) None resolutionFolder location 
      |> Async.RunSynchronously
    new StreamReader(stream)

// --------------------------------------------------------------------------------------

#if PORTABLE
//TODO PORTABLE
#else
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
    let uri = Uri(uri.OriginalString)
    let paq = uri.PathAndQuery
    let flagsFieldInfo = typeof<Uri>.GetField("m_Flags", BindingFlags.Instance ||| BindingFlags.NonPublic)
    let flags = flagsFieldInfo.GetValue(uri) :?> uint64
    let flags = flags &&& (~~~0x30UL)
    flagsFieldInfo.SetValue(uri, flags)
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

  /// Downlaod an HTTP web resource from the specified URL asynchronously
  static member AsyncRequest(url:string) = async {
    use wc = new WebClient()
    return! wc.AsyncDownloadString(enableUriSlashes(Uri(url))) }

  /// Downlaod an HTTP web resource from the specified URL
  static member Request(url:string) = 
    use wc = new WebClient()
    wc.DownloadString(enableUriSlashes(Uri(url)))

  /// Downlaod an HTTP web resource from the specified URL asynchronously
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
    let req = HttpWebRequest.Create(enableUriSlashes(Uri(url + "?" + query))) 
    let req = req :?> HttpWebRequest
    req.Method <- meth
    
    // Set headers, but look for special headers like Accept
    let invariant = Globalization.CultureInfo.InvariantCulture
    for header, value in headers do
      if String.Compare(header, "accept", true, invariant) = 0 then
        req.Accept <- value
      elif String.Compare(header, "content-type", true, invariant) = 0 then
        req.ContentType <- value
      else
        req.Headers.Add(header, value) 

    // If we want to set some body, encode it with POST data as array of bytes
    match body with 
    | Some (text:string) ->
        let postBytes = Encoding.ASCII.GetBytes(text)
        if headers |> Seq.forall (fun (header, _) ->
          String.Compare(header, "content-type", true, invariant) <> 0) then
          req.ContentType <- "application/x-www-form-urlencoded"
        req.ContentLength <- int64 postBytes.Length
        use reqStream = req.GetRequestStream() 
        reqStream.Write(postBytes, 0, postBytes.Length)
    | _ -> ()
    
    // Send the request and get the response       
    use! resp = req.AsyncGetResponse()
    use stream = resp.GetResponseStream()
    return! asyncReadToEnd stream }

  /// Downlaod an HTTP web resource from the specified URL synchronously
  /// (allows specifying query string parameters and HTTP headers including
  /// headers that have to be handled specially - such as Accept)
  static member Request(url:string, ?query, ?headers, ?meth, ?body) = 
    Http.AsyncRequest(url, ?headers=headers, ?query=query, ?meth=meth, ?body=body)
    |> Async.RunSynchronously
#endif