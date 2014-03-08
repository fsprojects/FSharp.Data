// --------------------------------------------------------------------------------------
// Utilities for working with network, downloading resources with specified headers etc.
// --------------------------------------------------------------------------------------

/// [omit]
module FSharp.Data.Runtime.IO

open System
open System.IO
open System.Net
open FSharp.Data

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

#if false

let private logLock = obj()

let private appendToLogMultiple logFile lines = lock logLock <| fun () ->
    let path = __SOURCE_DIRECTORY__ + "/../../" + logFile
    use stream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)
    use writer = new StreamWriter(stream)
    for (line:string) in lines do
        writer.WriteLine(line.Replace("\r", null).Replace("\n","\\n"))
    writer.Flush()

let private appendToLog logFile line = 
    appendToLogMultiple logFile [line]

let internal log str =
    "[" + DateTime.Now.TimeOfDay.ToString() + "] " + str
    |> appendToLog "log.txt"

let internal logWithStackTrace (str:string) =
    let stackTrace = 
        Environment.StackTrace.Split '\n'
        |> Seq.skip 3
        |> Seq.truncate 5
        |> Seq.map (fun s -> s.TrimEnd())
        |> Seq.toList
    str::stackTrace |> appendToLogMultiple "log.txt"

open System.Diagnostics
  
let internal logTime category (instance:string) =

    log (sprintf "Started %s %s" category instance)

    let s = Stopwatch()
    s.Start()

    { new IDisposable with
        member __.Dispose() =
            s.Stop()
            log (sprintf "Ended %s %s" category instance)
            let instance = instance.Replace("\r", null).Replace("\n","\\n")
            sprintf "%s|%s|%d" category instance s.ElapsedMilliseconds
            |> appendToLog "log.csv" }

#else

let internal dummyDisposable = { new IDisposable with member __.Dispose() = () }
let inline internal log _ = ()
let inline internal logWithStackTrace _ = ()
let inline internal logTime _ _ = dummyDisposable

#endif
#endif

type internal IDisposableTypeProvider =
    abstract Invalidate : unit -> unit
    abstract AddDisposeAction : (unit -> unit) -> unit
    abstract Id : int

#if FX_NO_LOCAL_FILESYSTEM
#else

type private Watcher(uri:Uri) =

    let typeProviders = ResizeArray<IDisposableTypeProvider>()

    let getLastWrite() = File.GetLastWriteTime uri.OriginalString 
    let lastWrite = ref (getLastWrite())
    
    let watcher = 
        let path = Path.GetDirectoryName uri.OriginalString
        let name = Path.GetFileName uri.OriginalString
        new FileSystemWatcher(Filter = name, Path = path, EnableRaisingEvents = true)

    let checkForChanges _ =
        let curr = getLastWrite()
    
        if !lastWrite <> curr then
            log ("Invalidated " + uri.OriginalString)
            lastWrite := curr
            for tp in typeProviders do
                tp.Invalidate()

    do
        watcher.Changed.Add checkForChanges
        watcher.Renamed.Add checkForChanges
        watcher.Deleted.Add checkForChanges

    member __.Add = typeProviders.Add
    member __.Remove (tp:IDisposableTypeProvider) = 
        log (sprintf "Removing [%d] from watcher %s" tp.Id uri.OriginalString) 
        typeProviders.Remove tp |> ignore
        if typeProviders.Count = 0 then
            log ("Disposing watcher " + uri.OriginalString) 
            watcher.Dispose()
            true
        else
            false 

open System.Collections.Generic

let private watchers = Dictionary()

// sets up a filesystem watcher that calls the invalidate function whenever the file changes
// adds the filesystem watcher to the list of objects to dispose by the type provider
let private watchForChanges (uri:Uri) (tp:IDisposableTypeProvider) =

    let watcher = 

        match watchers.TryGetValue uri.OriginalString with
        | true, watcher ->

            log (sprintf "Reusing watcher %s for [%d]" uri.OriginalString tp.Id)
            watcher

        | false, _ ->
                   
            log (sprintf "Setting up watcher %s for [%d]" uri.OriginalString tp.Id)
            let watcher = Watcher uri
            watchers.Add(uri.OriginalString, watcher)
            watcher

    watcher.Add tp
    
    tp.AddDisposeAction <| fun () -> 

        if watcher.Remove tp then
            watchers.Remove uri.OriginalString |> ignore
            
#endif
    
/// Opens a stream to the uri using the uriResolver resolution rules
/// It the uri is a file, uses shared read, so it works when the file locked by Excel or similar tools,
/// and sets up a filesystem watcher that calls the invalidate function whenever the file changes
let internal asyncOpenStream (_tp:IDisposableTypeProvider option) (uriResolver:UriResolver) (uri:Uri) =
  let uri, isWeb = uriResolver.Resolve uri
  if isWeb then
    async {
        let! response = Http.AsyncRequestStream uri.OriginalString
        return response.ResponseStream
    }
  else
#if FX_NO_LOCAL_FILESYSTEM
    failwith "Only web locations are supported"
#else
    let path = uri.OriginalString.Replace(Uri.UriSchemeFile + "://", "")
    let file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite) :> Stream
    _tp |> Option.iter (watchForChanges uri)
    async.Return file
#endif

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
