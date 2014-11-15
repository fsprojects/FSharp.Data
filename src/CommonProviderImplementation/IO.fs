module ProviderImplementation.IO

open System
open System.IO

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
let inline internal log _ = ()
let inline internal logWithStackTrace _ = ()
let inline internal logTime _ _ = dummyDisposable

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
            let typeProviders = Seq.toArray typeProviders
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
let internal watchForChanges (((tp:IDisposableTypeProvider), typeName) as key) (uri:Uri) =

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