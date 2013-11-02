// --------------------------------------------------------------------------------------
// Utilities for working with network, downloading resources with specified headers etc.
// --------------------------------------------------------------------------------------

/// [omit]
module FSharp.Data.Runtime.IO

open System
open System.IO
open System.Net
open FSharp.Net

type internal UriResolutionType =
    | DesignTime
    | Runtime
    | RuntimeInFSI

let internal isWeb (uri:Uri) = uri.IsAbsoluteUri && not uri.IsUnc && uri.Scheme <> "file"

type internal UriResolver = 
    
    { ResolutionType : UriResolutionType
      DefaultResolutionFolder : string
      ResolutionFolder : string }
    
    static member Create(resolutionType, defaultResolutionFolder, resolutionFolder) =
      { ResolutionType = resolutionType
        DefaultResolutionFolder = defaultResolutionFolder       
        ResolutionFolder = resolutionFolder }

    /// Resolve the absolute location of a file (or web URL) according to the rules
    /// used by standard F# type providers as described here:
    /// https://github.com/fsharp/fsharpx/issues/195#issuecomment-12141785
    ///
    ///  * if it is web resource, just return it
    ///  * if it is full path, just return it
    ///  * otherwise.
    ///
    ///    At design-time:
    ///      * if the user specified resolution folder, use that
    ///      * otherwise use the default resolution folder
    ///    At run-time:
    ///      * if the user specified resolution folder, use that
    ///      * if it is running in F# interactive (config.IsHostedExecution) 
    ///        use the default resolution folder
    ///      * otherwise, use 'CurrentDomain.BaseDirectory'
    /// returns an absolute uri * isWeb flag
    member x.Resolve(uri:Uri) = 
      if uri.IsAbsoluteUri then uri, isWeb uri
      else
#if FX_NO_LOCAL_FILESYSTEM
        failwith "Only web locations are supported"
#else
        let root = 
          match x.ResolutionType with
          | DesignTime -> if String.IsNullOrEmpty x.ResolutionFolder
                          then x.DefaultResolutionFolder
                          else x.ResolutionFolder
          | RuntimeInFSI -> x.DefaultResolutionFolder
          | Runtime -> AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/')
        Uri(Path.Combine(root, uri.OriginalString), UriKind.Absolute), false
#endif


#if FX_NO_LOCAL_FILESYSTEM
#else
// sets up a filesystem watcher that calls the invalidate function whenever the file changes
// adds the filesystem watcher to the list of objects to dispose by the type provider
let private watchForChanges (uri:Uri) (invalidate, addDisposer:IDisposable->unit) =
    let watcher = 
        let path = Path.GetDirectoryName(uri.OriginalString)
        let name = Path.GetFileName(uri.OriginalString)
        new FileSystemWatcher(Filter = name, 
                              Path = path, 
                              EnableRaisingEvents = true)
    watcher.Changed.Add(fun _ -> invalidate())
    watcher.Deleted.Add(fun _ -> invalidate())
    watcher.Renamed.Add(fun _ -> invalidate())
    addDisposer watcher
#endif

/// Opens a stream to the uri using the uriResolver resolution rules
/// It the uri is a file, uses shared read, so it works when the file locked by Excel or similar tools,
/// and sets up a filesystem watcher that calls the invalidate function whenever the file changes
let internal asyncOpenStream (invalidate:((unit->unit)*(IDisposable->unit)) option) (uriResolver:UriResolver) (uri:Uri) = async {
  let uri, isWeb = uriResolver.Resolve uri
  if isWeb then
    let! stream = Http.InnerRequest(uri.OriginalString, fun _ _ _ _ _ stream -> stream)
    return stream :> Stream
  else
#if FX_NO_LOCAL_FILESYSTEM
    return failwith "Only web locations are supported"
#else
    // Open the file, even if it is already opened by another application
    // unlike in the web case, we don't consume the whole file into memory first
    let file = File.Open(uri.OriginalString, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    invalidate |> Option.iter (watchForChanges uri)
    return file :> Stream
#endif
}

let private withUri uri f =
  match Uri.TryCreate(uri, UriKind.RelativeOrAbsolute) with
  | false, _ -> failwithf "Invalid uri: %s" uri
  | true, uri -> f uri

/// Returns a TextReader for the uri using the runtime resolution rules
let asyncReadTextAtRuntime forFSI defaultResolutionFolder resolutionFolder uri = 
  withUri uri <| fun uri ->
    let resolver = UriResolver.Create((if forFSI then RuntimeInFSI else Runtime), 
                                      defaultResolutionFolder, resolutionFolder)
    async { let! stream = asyncOpenStream None resolver uri
            return new StreamReader(stream) :> TextReader }

/// Returns a TextReader for the uri using the designtime resolution rules
let asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder uri = 
  withUri uri <| fun uri ->
    let resolver = UriResolver.Create(DesignTime, defaultResolutionFolder, resolutionFolder)
    async { let! stream = asyncOpenStream None resolver uri
            return new StreamReader(stream) :> TextReader }
