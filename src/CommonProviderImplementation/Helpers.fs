// --------------------------------------------------------------------------------------
// Helpers for writing type providers
// ----------------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.IO
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference
open ProviderImplementation.ProvidedTypes

// ----------------------------------------------------------------------------------------------

[<AutoOpen>]
module PrimitiveInferedPropertyExtensions =
  type PrimitiveInferedProperty with
    member x.TypeWithMeasure =
      match x.UnitOfMeasure with
      | None -> x.RuntimeType
      | Some unit -> 
          if supportsUnitsOfMeasure x.RuntimeType
          then ProvidedMeasureBuilder.Default.AnnotateType(x.RuntimeType, [unit])
          else failwithf "Units of measure not supported by type %s" x.RuntimeType.Name

// ----------------------------------------------------------------------------------------------

[<AutoOpen>]
module ActivePatterns =

  /// Helper active pattern that can be used when constructing InvokeCode
  /// (to avoid writing pattern matching or incomplete matches):
  ///
  ///    p.InvokeCode <- fun (Singleton self) -> <@ 1 + 2 @>
  ///
  let (|Singleton|) = function [l] -> l | _ -> failwith "Parameter mismatch"

  /// Takes dictionary or a map and succeeds if it contains exactly one value
  let (|SingletonMap|_|) map = 
    if Seq.length map <> 1 then None else
      let (KeyValue(k, v)) = Seq.head map 
      Some(k, v)

// ----------------------------------------------------------------------------------------------

module ReflectionHelpers = 

  open Microsoft.FSharp.Quotations

  let makeDelegate (exprfunc:Expr -> Expr) argType = 
    let var = Var.Global("t", argType)
    let convBody = exprfunc (Expr.Var var)
    Expr.NewDelegate(typedefof<Func<_,_>>.MakeGenericType(argType, convBody.Type), [var], convBody)
        
// ----------------------------------------------------------------------------------------------

type DisposableTypeProviderForNamespaces() =
  inherit TypeProviderForNamespaces()

  let mutable childDisposables = ResizeArray<IDisposable>()

  member __.AddChild = childDisposables.Add

  interface IDisposable with 
    member __.Dispose() = 
      let disposables = childDisposables.ToArray()
      childDisposables.Clear()
      for disposable in disposables do 
        try disposable.Dispose() with _ -> ()

// ----------------------------------------------------------------------------------------------

module ProviderHelpers =

  open System.IO
  open FSharp.Data.Runtime.Caching
  open FSharp.Data.Runtime.IO

  let private invalidChars = [ for c in "\"|<>{}[]," -> c ] @ [ for i in 0..31 -> char i ] |> set
  let private webUrisCache, _ = createInternetFileCache "DesignTimeURIs" (TimeSpan.FromMinutes 30.0)
  
  /// Reads a sample parameter for a type provider, detecting if it is a uri and fetching it if needed
  /// Samples from the web are cached for 30 minutes
  /// Samples from the filesystem are read using shared read, so it works when the file is locked by Excel or similar tools,
  /// and a filesystem watcher that calls the invalidate function whenever the file changes is setup
  /// 
  /// Parameters:
  /// * sampleOrSampleUri - the text which can be a sample or an uri for a sample
  /// * parseFunc - receives the file/url extension (or ""  if not applicable) and the text value 
  /// * formatName - the description of what is being parsed (for the error message)
  /// * tp - the type provider
  /// * cfg - the type provider config
  /// * resolutionFolder - if the type provider allows to override the resolutionFolder pass it here
  let parseTextAtDesignTime sampleOrSampleUri parseFunc formatName
                            (tp:DisposableTypeProviderForNamespaces) (cfg:TypeProviderConfig) resolutionFolder =
  
      let tryGetUri str =
          match Uri.TryCreate(str, UriKind.RelativeOrAbsolute) with
          | false, _ -> None
          | true, uri ->
              if not uri.IsAbsoluteUri && (str |> Seq.exists (fun c -> invalidChars.Contains c)) 
              then None else Some uri
  
      try
        match tryGetUri sampleOrSampleUri with
        | None -> parseFunc "" sampleOrSampleUri, false
        | Some uri ->

            let resolver = 
              { ResolutionType = DesignTime
                DefaultResolutionFolder = cfg.ResolutionFolder
                ResolutionFolder = resolutionFolder }
            
            let readText() = 
              Async.RunSynchronously <| async {
                  use! stream = asyncOpenStream (Some (tp.Invalidate, tp.AddChild)) resolver uri
                  use reader = new StreamReader(stream)
                  return reader.ReadToEnd()
              } 
            
            let sample = 
              if isWeb uri then
                  match webUrisCache.TryRetrieve uri.OriginalString with
                  | Some value -> value
                  | None ->
                      let value = readText()
                      webUrisCache.Set(uri.OriginalString, value)
                      value
              else readText()
                
            parseFunc (Path.GetExtension uri.OriginalString) sample, true
        
      with e ->
        failwithf "Specified argument is neither a file, nor well-formed %s: %s" formatName e.Message

  // carries part of the information needed by generateConstructors
  type TypeProviderSpec = 
    { //the generated type
      GeneratedType : ProvidedTypeDefinition 
      //the representation type (what's returned from the constructors, may or may not be the same as Type)
      RepresentationType : Type 
      // the constructor from a text reader to the representation
      CreateFromTextReader : Expr<TextReader> -> Expr
      // async version of the constructor from a text reader to the representation
      AsyncCreateFromTextReader : Expr<Async<TextReader>> -> Expr
      // the constructor from a text reader to an array of the representation
      CreateFromTextReaderForSampleList : Expr<TextReader> -> Expr
      // async version of the constructor from a text reader to an array of the representation
      AsyncCreateFromTextReaderForSampleList : Expr<Async<TextReader>> -> Expr }

  /// Creates all the constructors for a type provider: (Async)Parse, (Async)Load, (Async)GetSample(s)
  /// * sampleOrSampleUri - the text which can be a sample or an uri for a sample
  /// * sampleIsList - true if the sample consists of several samples put together
  /// * parseSingle - receives the file/url extension (or ""  if not applicable) and the text value 
  /// * parseList - receives the file/url extension (or ""  if not applicable) and the text value 
  /// * getSpecFromSamples - receives a seq of parsed samples and returns a TypeProviderSpec
  /// * tp -> the type provider
  /// * cfg -> the type provider config
  /// * replacer -> the assemblyReplacer
  /// * resolutionFolder -> if the type provider allows to override the resolutionFolder pass it here
  /// * generateDefaultConstructor -> if true, generates a default constructor that is equivalent to .GetSample(). Only supported when GeneratedType = RepresentationType
  let generateConstructors formatName sampleOrSampleUri sampleIsList parseSingle parseList getSpecFromSamples
                           (tp:DisposableTypeProviderForNamespaces) (cfg:TypeProviderConfig) (replacer:AssemblyReplacer) resolutionFolder generateDefaultConstructor =

    let isRunningInFSI = cfg.IsHostedExecution
    let defaultResolutionFolder = cfg.ResolutionFolder

    let parse extension (value:string) = 
      if sampleIsList then
        try
          parseList extension value
        with _  ->
          value.Split('\n', '\r')
          |> Seq.filter (not << String.IsNullOrWhiteSpace)
          |> Seq.map (parseSingle extension)
      else
        parseSingle extension value |> Seq.singleton

    // Infer the schema from a specified uri or inline text
    let typedSamples, sampleIsUri = parseTextAtDesignTime sampleOrSampleUri parse formatName tp cfg resolutionFolder

    let spec = getSpecFromSamples typedSamples

    if generateDefaultConstructor then        
        assert (not sampleIsList)
        assert (spec.GeneratedType :> Type = spec.RepresentationType)
    
    let resultType = spec.RepresentationType
    let resultTypeAsync = typedefof<Async<_>>.MakeGenericType(resultType) |> replacer.ToRuntime

    [ // Generate static Parse method
      let args = [ ProvidedParameter("text", typeof<string>) ]
      let m = ProvidedMethod("Parse", args, resultType, IsStaticMethod = true)
      m.InvokeCode <- fun (Singleton text) -> 
        <@ new StringReader(%%text) :> TextReader @>
        |> spec.CreateFromTextReader 
      m.AddXmlDoc <| sprintf "Parses the specified %s string" formatName
      yield m :> MemberInfo
      
      // Generate static Load stream method
      let args = [ ProvidedParameter("stream", typeof<Stream>) ]
      let m = ProvidedMethod("Load", args, resultType, IsStaticMethod = true)
      m.InvokeCode <- fun (Singleton stream) ->       
        <@ new StreamReader(%%stream:Stream) :> TextReader @>
        |> spec.CreateFromTextReader 
      m.AddXmlDoc <| sprintf "Loads %s from the specified stream" formatName
      yield m :> _

      // Generate static Load reader method
      let args = [ ProvidedParameter("reader", typeof<TextReader>) ]
      let m = ProvidedMethod("Load", args, resultType, IsStaticMethod = true)
      m.InvokeCode <- fun (Singleton reader) -> 
        <@ %%reader:TextReader @>
        |> spec.CreateFromTextReader 
      m.AddXmlDoc <| sprintf "Loads %s from the specified reader" formatName
      yield m :> _
      
      // Generate static Load uri method
      let args = [ ProvidedParameter("uri", typeof<string>) ]
      let m = ProvidedMethod("Load", args, resultType, IsStaticMethod = true)
      m.InvokeCode <- fun (Singleton uri) -> 
        <@ Async.RunSynchronously(asyncReadTextAtRuntime isRunningInFSI defaultResolutionFolder resolutionFolder %%uri) @>
        |> spec.CreateFromTextReader 
      m.AddXmlDoc <| sprintf "Loads %s from the specified uri" formatName
      yield m :> _

      // Generate static AsyncLoad uri method
      let args = [ ProvidedParameter("uri", typeof<string>) ]
      let m = ProvidedMethod("AsyncLoad", args, resultTypeAsync, IsStaticMethod = true)
      m.InvokeCode <- fun (Singleton uri) -> 
        <@ asyncReadTextAtRuntime isRunningInFSI defaultResolutionFolder resolutionFolder %%uri @>
        |> spec.AsyncCreateFromTextReader
      m.AddXmlDoc <| sprintf "Loads %s from the specified uri" formatName
      yield m :> _
      
      if sampleIsList then

        // the [,] case needs more work, and it's a weird scenario anyway
        if not resultType.IsArray then

          let resultTypeArray = if resultType.IsArray then resultType.GetElementType().MakeArrayType(2) else resultType.MakeArrayType()
          let resultTypeArrayAsync = typedefof<Async<_>>.MakeGenericType(resultTypeArray) |> replacer.ToRuntime
          
          // Generate static GetSamples method
          let m = ProvidedMethod("GetSamples", [], resultTypeArray, IsStaticMethod = true)
          m.InvokeCode <- fun _ -> 
            (if sampleIsUri 
             then <@ Async.RunSynchronously(asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder sampleOrSampleUri) @>
             else <@ new StringReader(sampleOrSampleUri) :> TextReader @>)
            |> spec.CreateFromTextReaderForSampleList
          yield m :> _
                  
          if sampleIsUri then
            // Generate static AsyncGetSamples method
            let m = ProvidedMethod("AsyncGetSamples", [], resultTypeArrayAsync, IsStaticMethod = true)
            m.InvokeCode <- fun _ -> 
              <@ asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder sampleOrSampleUri @>
              |> spec.AsyncCreateFromTextReaderForSampleList
            yield m :> _

      else 

        let name = if resultType.IsArray then "GetSamples" else "GetSample"

        let getSampleCode = fun _ -> 
          (if sampleIsUri 
           then <@ Async.RunSynchronously(asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder sampleOrSampleUri) @>
           else <@ new StringReader(sampleOrSampleUri) :> TextReader @>)
          |> spec.CreateFromTextReader

        // Generate static GetSample method
        yield ProvidedMethod(name, [], resultType, IsStaticMethod = true, InvokeCode = getSampleCode) :> _
        
        // Generate default constructor
        if generateDefaultConstructor then
          yield ProvidedConstructor([], InvokeCode = getSampleCode) :> _

        if sampleIsUri then
          // Generate static AsyncGetSample method
          let m = ProvidedMethod("Async" + name, [], resultTypeAsync, IsStaticMethod = true)
          m.InvokeCode <- fun _ -> 
            <@ asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder sampleOrSampleUri @>
            |> spec.AsyncCreateFromTextReader
          yield m :> _

    ] |> spec.GeneratedType.AddMembers

    spec.GeneratedType
