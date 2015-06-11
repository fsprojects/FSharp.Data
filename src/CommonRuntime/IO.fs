/// Helper functions called from the generated code for working with files
module FSharp.Data.Runtime.IO

open System
open System.IO
open System.Net
open System.Text
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
        failwith "Only web locations are supported on the PCL versions of F# Data. Please use the full .NET version instead."
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

#if LOGGING_ENABLED

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
let inline internal log (_:string) = ()
let inline internal logWithStackTrace (_:string) = ()
let inline internal logTime (_:string) (_:string) = dummyDisposable

#endif
#endif

type internal IDisposableTypeProvider =
    abstract Invalidate : string -> unit
    abstract AddDisposeAction : string -> (unit -> unit) -> unit
    abstract Id : int

#if FX_NO_LOCAL_FILESYSTEM
#else

type private Watcher(uri:Uri) =

    let typeProviders = ResizeArray<IDisposableTypeProvider*string>()

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
            let typeProviders = typeProviders.ToArray()
            for tp, typeName in typeProviders do
                tp.Invalidate typeName

    do
        watcher.Changed.Add checkForChanges
        watcher.Renamed.Add checkForChanges
        watcher.Deleted.Add checkForChanges

    member __.Add = typeProviders.Add
    member __.Remove (tp:IDisposableTypeProvider) typeName = 
        log (sprintf "Removing %s [%d] from watcher %s" typeName tp.Id uri.OriginalString) 
        typeProviders.Remove (tp, typeName) |> ignore
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
let private watchForChanges (uri:Uri) (((tp:IDisposableTypeProvider), typeName) as key) =

    let watcher = 

        match watchers.TryGetValue uri.OriginalString with
        | true, watcher ->

            log (sprintf "Reusing watcher %s for %s [%d]" typeName uri.OriginalString tp.Id)
            watcher

        | false, _ ->
                   
            log (sprintf "Setting up watcher %s for %s [%d]" typeName uri.OriginalString tp.Id)
            let watcher = Watcher uri
            watchers.Add(uri.OriginalString, watcher)
            watcher

    watcher.Add key
    
    tp.AddDisposeAction typeName <| fun () -> 

        if watcher.Remove tp typeName then
            watchers.Remove uri.OriginalString |> ignore
            
#endif
    
/// Opens a stream to the uri using the uriResolver resolution rules
/// It the uri is a file, uses shared read, so it works when the file locked by Excel or similar tools,
/// and sets up a filesystem watcher that calls the invalidate function whenever the file changes
let internal asyncRead (_tp:(IDisposableTypeProvider*string) option) (uriResolver:UriResolver) formatName encodingStr (uri:Uri) =
  let uri, isWeb = uriResolver.Resolve uri
  if isWeb then
    async {
        let contentTypes =
            match formatName with
            | "CSV" -> [ HttpContentTypes.Csv ]
            | "HTML" -> [ HttpContentTypes.Html ]
            | "JSON" -> [ HttpContentTypes.Json ]
            | "XML" -> [ HttpContentTypes.Xml ]
            | _ -> []
            @ [ HttpContentTypes.Any ]
        let headers = [ HttpRequestHeaders.UserAgent ("F# Data " + formatName + " Type Provider") 
                        HttpRequestHeaders.Accept (String.concat ", " contentTypes) ]
        // Download the whole web resource at once, otherwise with some servers we won't get the full file
        let! text = Http.AsyncRequestString(uri.OriginalString, headers = headers, responseEncodingOverride = encodingStr)
        return new StringReader(text) :> TextReader
    }
  else
#if FX_NO_LOCAL_FILESYSTEM
    failwith "Only web locations are supported"
#else
    let path = uri.OriginalString.Replace(Uri.UriSchemeFile + "://", "")
    async {
        let file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        _tp |> Option.iter (watchForChanges uri)
        let encoding = if encodingStr = "" then Encoding.UTF8 else HttpEncodings.getEncoding encodingStr
        return new StreamReader(file, encoding) :> TextReader
    }
#endif

let private withUri uri f =
  match Uri.TryCreate(uri, UriKind.RelativeOrAbsolute) with
  | false, _ -> failwithf "Invalid uri: %s" uri
  | true, uri -> f uri

/// Returns a TextReader for the uri using the runtime resolution rules
let asyncReadTextAtRuntime forFSI defaultResolutionFolder resolutionFolder formatName encodingStr uri = 
  withUri uri <| fun uri ->
    let resolver = UriResolver.Create((if forFSI then RuntimeInFSI else Runtime), 
                                      defaultResolutionFolder, resolutionFolder)
    asyncRead None resolver formatName encodingStr uri

/// Returns a TextReader for the uri using the designtime resolution rules
let asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr uri = 
  withUri uri <| fun uri ->
    let resolver = UriResolver.Create(DesignTime, defaultResolutionFolder, resolutionFolder)
    asyncRead None resolver formatName encodingStr uri

