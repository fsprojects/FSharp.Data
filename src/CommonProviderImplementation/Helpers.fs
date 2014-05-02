﻿// --------------------------------------------------------------------------------------
// Helpers for writing type providers
// ----------------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open FSharp.Data.Runtime
open FSharp.Data.Runtime.IO
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference
open ProviderImplementation
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
    
    /// Takes a map and succeeds if it is empty
    let (|EmptyMap|_|) result (map:Map<_,_>) = if map.IsEmpty then Some result else None
    
    /// Takes a map and succeeds if it contains exactly one value
    let (|SingletonMap|_|) (map:Map<_,_>) = 
        if map.Count <> 1 then None else
            let (KeyValue(k, v)) = Seq.head map
            Some(k, v)

// ----------------------------------------------------------------------------------------------

module ReflectionHelpers = 

    open Microsoft.FSharp.Quotations
    
    let makeDelegate (exprfunc:Expr -> Expr) argType = 
        let var = Var("t", argType)
        let convBody = exprfunc (Expr.Var var)
        Expr.NewDelegate(typedefof<Func<_,_>>.MakeGenericType(argType, convBody.Type), [var], convBody)

// ----------------------------------------------------------------------------------------------

type DisposableTypeProviderForNamespaces() as x =
    inherit TypeProviderForNamespaces()
  
    let mutable disposeActionsByTypeName = Dictionary()
  
    static let idCount = ref 0
  
    let id = !idCount
  
    do incr idCount 
  
    do log (sprintf "Creating TypeProviderForNamespaces %O [%d]" x id)
  
    let addDisposeAction typeName action = lock disposeActionsByTypeName <| fun () ->
        match disposeActionsByTypeName.TryGetValue typeName with
        | false, _ -> 
            let disposeActions = ResizeArray()
            disposeActions.Add action
            disposeActionsByTypeName.Add(typeName, disposeActions)
        | true, disposeActions -> disposeActions.Add action

    let dispose typeName = lock disposeActionsByTypeName <| fun () ->
        log (sprintf "Disposing %s in TypeProviderForNamespaces %O [%d]" typeName x id)
        match disposeActionsByTypeName.TryGetValue typeName with
        | true, disposeActions ->
            disposeActionsByTypeName.Remove typeName |> ignore
            for action in disposeActions do
                action()
        | false, _ -> ()

    let disposeAll() = lock disposeActionsByTypeName <| fun () ->
        log (sprintf "Disposing all types in TypeProviderForNamespaces %O [%d]" x id)
        for typeName in Seq.toArray disposeActionsByTypeName.Keys do
            dispose typeName

    interface IDisposable with 
        member __.Dispose() = disposeAll()
              
    interface IDisposableTypeProvider with
        member __.Invalidate typeName = dispose typeName; ``base``.Invalidate()
        member __.AddDisposeAction typeName action = addDisposeAction typeName action
        member __.Id = id

// ----------------------------------------------------------------------------------------------

module ProviderHelpers =

    open System.IO
    open Microsoft.FSharp.Reflection
    open FSharp.Data.Runtime.Caching
    open FSharp.Data.Runtime.IO
    
    let asyncMap (replacer:AssemblyReplacer) (resultType:Type) (valueAsync:Expr<Async<'T>>) (body:Expr<'T>->Expr) =
        let (?) = ProviderImplementation.QuotationBuilder.(?)
        let convFunc = ReflectionHelpers.makeDelegate (Expr.Cast >> body) typeof<'T>      
        let f = Var("f", convFunc.Type)
        let body = typeof<TextRuntime>?AsyncMap (typeof<'T>, resultType) (valueAsync, Expr.Var f) :> Expr
        Expr.Let(f, convFunc, body) |> replacer.ToRuntime

    let some (typ:Type) arg =
        let unionCase = 
            FSharpType.GetUnionCases(typedefof<option<_>>.MakeGenericType typ)
            |> Seq.find (fun x -> x.Name = "Some")
        Expr.NewUnionCase(unionCase, [ arg ])

    let private cacheDuration = TimeSpan.FromMinutes 30.0
    let private invalidChars = [ for c in "\"|<>{}[]," -> c ] @ [ for i in 0..31 -> char i ] |> set
    let private webUrisCache, _ = createInternetFileCache "DesignTimeURIs" cacheDuration
    
    let(|Url|_|) str =
        match Uri.TryCreate(str, UriKind.Absolute) with
        | (true, url) when url.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || url.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) -> Some(url)
        | _ -> None
      
    let isValidUrl str = 
        match str with
        | Url _ -> true
        | _ -> false

    let hasAuthorizationPart url =
        if isValidUrl url then
            match(new Uri(url)).UserInfo.Split([|':'|]) with
            | [|_; _|] -> true
            | _ -> false
        else
            false

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
    let private parseTextAtDesignTime sampleOrSampleUri parseFunc formatName (tp:IDisposableTypeProvider) 
                                      (cfg:TypeProviderConfig) resolutionFolder fullTypeName =
    
        using (logTime "Loading" sampleOrSampleUri) <| fun _ ->
    
        let tryGetUri str =
            match Uri.TryCreate(str, UriKind.RelativeOrAbsolute) with
            | false, _ -> None
            | true, uri ->
                if str.Trim() = "" || not uri.IsAbsoluteUri && Seq.exists invalidChars.Contains str
                then None else Some uri
    
        match tryGetUri sampleOrSampleUri with
        | None -> 
    
            try lazy (parseFunc "" sampleOrSampleUri), false, false
            with e -> failwithf "The provided sample is neither a file, nor a well-formed %s: %s" formatName e.Message
    
        | Some uri ->
    
            let resolver = 
                { ResolutionType = DesignTime
                  DefaultResolutionFolder = cfg.ResolutionFolder
                  ResolutionFolder = resolutionFolder }
            
            let readText() = 
                Async.RunSynchronously <| async {
                    use! stream = asyncOpenStream (Some (tp, fullTypeName)) resolver formatName uri
                    use reader = new StreamReader(stream)
                    return reader.ReadToEnd()
                } 
    
            try
              
                let sample, isWeb = 
                    if isWeb uri then
                        match webUrisCache.TryRetrieve uri.OriginalString with
                        | Some value -> value, true
                        | None ->
                            let value = readText()
                            webUrisCache.Set(uri.OriginalString, value)
                            value, true
                    else readText(), false
                    
                lazy (parseFunc (Path.GetExtension uri.OriginalString) sample), true, isWeb
    
            with e ->
    
                if not uri.IsAbsoluteUri then
                    // even if it's a valid uri, it could be sample text
                    try 
                        lazy (parseFunc "" sampleOrSampleUri), false, false
                    with _ -> 
                        // if not, return the first exception
                        failwithf "Cannot read sample %s from %s: %s" formatName sampleOrSampleUri e.Message
                else
                    failwithf "Cannot read sample %s from %s: %s" formatName sampleOrSampleUri e.Message
    
    // carries part of the information needed by generateType
    type TypeProviderSpec = 
        { //the generated type
          GeneratedType : ProvidedTypeDefinition 
          //the representation type (what's returned from the constructors, may or may not be the same as Type)
          RepresentationType : Type 
          // the constructor from a text reader to the representation
          CreateFromTextReader : Expr<TextReader> -> Expr
          // the constructor from a text reader to an array of the representation
          CreateFromTextReaderForSampleList : Expr<TextReader> -> Expr }
    
    let private providedTypesCache = Dictionary()
    
    // Caches the generated types by name for up to 30 minutes, the same duration of web caches
    // If there's a file invalidation, this cache is also invalidated
    let internal getOrCreateProvidedType (tp:IDisposableTypeProvider) (fullTypeName:string) (runtimeVersion:AssemblyResolver.FSharpDataRuntimeVersion) cacheDuration f =
      
        let key = fullTypeName, runtimeVersion

        match providedTypesCache.TryGetValue key with
        | true, (providedType, time) when DateTime.Now - time < cacheDuration -> 
            log (sprintf "Reusing cache for %s [%d]" fullTypeName tp.Id)
            providedType
        | _ -> 
            let providedType = f()
            log (sprintf "Creating cache for %s [%d]" fullTypeName tp.Id)
            providedTypesCache.[key] <- (providedType, DateTime.Now)
            tp.AddDisposeAction fullTypeName <| fun () -> 
                log (sprintf "Invalidating cache for %s [%d]" fullTypeName tp.Id )
                providedTypesCache.Remove key |> ignore
            providedType
    
    /// Creates all the constructors for a type provider: (Async)Parse, (Async)Load, (Async)GetSample(s), and default constructor
    /// * sampleOrSampleUri - the text which can be a sample or an uri for a sample
    /// * sampleIsList - true if the sample consists of several samples put together
    /// * parseSingle - receives the file/url extension (or ""  if not applicable) and the text value 
    /// * parseList - receives the file/url extension (or ""  if not applicable) and the text value 
    /// * getSpecFromSamples - receives a seq of parsed samples and returns a TypeProviderSpec
    /// * tp -> the type provider
    /// * cfg -> the type provider config
    /// * replacer -> the assemblyReplacer
    /// * resolutionFolder -> if the type provider allows to override the resolutionFolder pass it here
    /// * typeName -> the full name of the type provider, this will be used for caching
    let generateType formatName sampleOrSampleUri sampleIsList parseSingle parseList getSpecFromSamples runtimeVersion
                     (tp:DisposableTypeProviderForNamespaces) (cfg:TypeProviderConfig) (replacer:AssemblyReplacer) resolutionFolder fullTypeName =
    
        let isRunningInFSI = cfg.IsHostedExecution
        let defaultResolutionFolder = cfg.ResolutionFolder
        
        let parse extension (value:string) = using (logTime "Parsing" sampleOrSampleUri) <| fun _ ->
            if sampleIsList then
                parseList extension value
            else
                parseSingle extension value |> Seq.singleton
        
        getOrCreateProvidedType tp fullTypeName runtimeVersion cacheDuration <| fun () ->

        // Infer the schema from a specified uri or inline text
        let typedSamples, sampleIsUri, sampleIsWebUri = parseTextAtDesignTime sampleOrSampleUri parse formatName tp cfg resolutionFolder fullTypeName
        
        let spec = getSpecFromSamples typedSamples.Value
        
        let resultType = spec.RepresentationType
        let resultTypeAsync = typedefof<Async<_>>.MakeGenericType(resultType) |> replacer.ToRuntime
        
        using (logTime "TypeGeneration" sampleOrSampleUri) <| fun _ ->
        
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
              reader |> Expr.Cast |> spec.CreateFromTextReader
          m.AddXmlDoc <| sprintf "Loads %s from the specified reader" formatName
          yield m :> _
          
          // Generate static Load uri method
          let args = [ ProvidedParameter("uri", typeof<string>) ]
          let m = ProvidedMethod("Load", args, resultType, IsStaticMethod = true)
          m.InvokeCode <- fun (Singleton uri) -> 
              <@ Async.RunSynchronously(asyncReadTextAtRuntime isRunningInFSI defaultResolutionFolder resolutionFolder formatName %%uri) @>
              |> spec.CreateFromTextReader 
          m.AddXmlDoc <| sprintf "Loads %s from the specified uri" formatName
          yield m :> _
        
          // Generate static AsyncLoad uri method
          let args = [ ProvidedParameter("uri", typeof<string>) ]
          let m = ProvidedMethod("AsyncLoad", args, resultTypeAsync, IsStaticMethod = true)
          m.InvokeCode <- fun (Singleton uri) -> 
              let readerAsync = <@ asyncReadTextAtRuntime isRunningInFSI defaultResolutionFolder resolutionFolder formatName %%uri @>
              asyncMap replacer resultType readerAsync spec.CreateFromTextReader
          m.AddXmlDoc <| sprintf "Loads %s from the specified uri" formatName
          yield m :> _
          
          if sampleOrSampleUri <> "" && (runtimeVersion.SupportsLocalFileSystem || not sampleIsUri || sampleIsWebUri && not (hasAuthorizationPart sampleOrSampleUri)) then
        
              if sampleIsList then
              
                  // the [][] case needs more work, and it's a weird scenario anyway, so we won't support it
                  if not resultType.IsArray then
                  
                      let resultTypeArray = resultType.MakeArrayType()
                      let resultTypeArrayAsync = typedefof<Async<_>>.MakeGenericType(resultTypeArray) |> replacer.ToRuntime
                      
                      // Generate static GetSamples method
                      let m = ProvidedMethod("GetSamples", [], resultTypeArray, IsStaticMethod = true)
                      m.InvokeCode <- fun _ -> 
                          if sampleIsUri 
                          then <@ Async.RunSynchronously(asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName sampleOrSampleUri) @>
                          else <@ new StringReader(sampleOrSampleUri) :> TextReader @>
                          |> spec.CreateFromTextReaderForSampleList
                      yield m :> _
                              
                      if sampleIsUri then
                          // Generate static AsyncGetSamples method
                          let m = ProvidedMethod("AsyncGetSamples", [], resultTypeArrayAsync, IsStaticMethod = true)
                          m.InvokeCode <- fun _ -> 
                              let readerAsync = <@ asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName sampleOrSampleUri @>
                              asyncMap replacer resultTypeArray readerAsync spec.CreateFromTextReaderForSampleList
                          yield m :> _
              
              else 
              
                let name = if resultType.IsArray then "GetSamples" else "GetSample"
              
                let getSampleCode = fun _ -> 
                    if sampleIsUri 
                    then <@ Async.RunSynchronously(asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName sampleOrSampleUri) @>
                    else <@ new StringReader(sampleOrSampleUri) :> TextReader @>
                    |> spec.CreateFromTextReader
              
                // Generate static GetSample method
                yield ProvidedMethod(name, [], resultType, IsStaticMethod = true, InvokeCode = getSampleCode) :> _
                          
                if not sampleIsList && spec.GeneratedType :> Type = spec.RepresentationType then
                    // Generate default constructor
                    yield ProvidedConstructor([], InvokeCode = getSampleCode) :> _
              
                if sampleIsUri then
                    // Generate static AsyncGetSample method
                    let m = ProvidedMethod("Async" + name, [], resultTypeAsync, IsStaticMethod = true)
                    m.InvokeCode <- fun _ -> 
                        let readerAsync = <@ asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName sampleOrSampleUri @>
                        asyncMap replacer resultType readerAsync spec.CreateFromTextReader
                    yield m :> _
        
        ] |> spec.GeneratedType.AddMembers
        
        spec.GeneratedType
