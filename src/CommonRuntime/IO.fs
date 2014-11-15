/// Helper functions called from the generated code for working with files
module FSharp.Data.Runtime.IO

open System
open System.IO
open System.Text
open FSharp.Data

type UriResolutionType =
    | DesignTime
    | Runtime
    | RuntimeInFSI

let isWeb (uri:Uri) = uri.IsAbsoluteUri && not uri.IsUnc && uri.Scheme <> "file"

type UriResolver = 
    
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

/// Opens a stream to the uri using the uriResolver resolution rules
/// It the uri is a file, uses shared read, so it works when the file locked by Excel or similar tools,
/// and sets up a filesystem watcher that calls the invalidate function whenever the file changes
let asyncReadText (_watchForChanges:Uri->unit) (uriResolver:UriResolver) formatName encodingStr (uri:Uri) =
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
        return! Http.AsyncRequestString(uri.OriginalString, headers = headers, responseEncodingOverride = encodingStr)
    }
  else
#if FX_NO_LOCAL_FILESYSTEM
    failwith "Only web locations are supported"
#else
    let path = uri.OriginalString.Replace(Uri.UriSchemeFile + "://", "")
    async {
        use file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        _watchForChanges uri
        let encoding = if encodingStr = "" then Encoding.UTF8 else HttpEncodings.getEncoding encodingStr
        use sr = new StreamReader(file, encoding)
        return sr.ReadToEnd()
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
    async {
        let! text = asyncReadText ignore resolver formatName encodingStr uri
        return new StringReader(text) :> TextReader
    }

/// Returns a TextReader for the uri using the designtime resolution rules
let asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr uri = 
  withUri uri <| fun uri ->
    let resolver = UriResolver.Create(DesignTime, defaultResolutionFolder, resolutionFolder)
    async {
        let! text = asyncReadText ignore resolver formatName encodingStr uri
        return new StringReader(text) :> TextReader
    }

