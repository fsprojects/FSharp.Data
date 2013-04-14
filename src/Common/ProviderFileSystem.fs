// --------------------------------------------------------------------------------------
// Utilities for working with network, downloading resources with specified headers etc.
// --------------------------------------------------------------------------------------

module FSharp.Data.RuntimeImplementation.ProviderFileSystem

open System
open System.Collections.Generic
open System.IO
open System.Net
open System.Text
open System.Reflection

#if FX_NO_LOCAL_FILESYSTEM
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

let internal isWeb (uri:Uri) = uri.IsAbsoluteUri && not uri.IsUnc && uri.Scheme <> "file"

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
let private resolveUri
    //note: don't remove the type annotations, as some parameters aren't used in the portable version and will become generic, making the type generation fail
    (designTime:bool) (isHosted:bool, defaultResolutionFolder:string) (resolutionFolder:string) (uri:Uri) =

#if FX_NO_LOCAL_FILESYSTEM
  if isWeb uri then
      uri, true
  else
      failwith "Only web locations are supported"
#else
  match uri with
  | url when isWeb url -> url, true
  | fullPath when uri.IsAbsoluteUri -> fullPath, false
  | relative ->
      let root = 
        if designTime then
          if not (String.IsNullOrEmpty(resolutionFolder)) then resolutionFolder
          else defaultResolutionFolder
        elif isHosted then defaultResolutionFolder
        else AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/')
      Uri(Path.Combine(root, relative.OriginalString), UriKind.Absolute), false
#endif

/// Given a type provider configuration and a name passed by user, open 
/// the uri and return it as a stream
let internal asyncOpenStreamInProvider 
    designTime cfg (invalidate:(Unit->Unit) option) resolutionFolder uri = async {

  let resolvedUri, isWeb = resolveUri designTime cfg resolutionFolder uri

  if isWeb then
    let req = WebRequest.Create(resolvedUri)
    let! resp = req.AsyncGetResponse() 
    return resp.GetResponseStream()
  else
#if FX_NO_LOCAL_FILESYSTEM
    return failwith "Only web locations are supported"
#else
    // Open the file, even if it is already opened by another application
    let file = File.Open(resolvedUri.OriginalString, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    invalidate |> Option.iter (fun f -> watchForChanges f resolvedUri.OriginalString)
    return file :> Stream
#endif
}

/// Resolve a location of a file (or a web location) and open it for shared
/// read at runtime (do not monitor file changes and use runtime resolution rules)
let readTextAtRunTime isHosted defaultResolutionFolder resolutionFolder uri = 
  match Uri.TryCreate(uri, UriKind.RelativeOrAbsolute) with
  | false, _ -> failwithf "Invalid uri: %s" uri
  | true, uri ->
      let stream = 
        asyncOpenStreamInProvider false (isHosted, defaultResolutionFolder) None resolutionFolder uri
        |> Async.RunSynchronously
      new StreamReader(stream)
