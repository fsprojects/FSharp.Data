// Copyright 2011-2015, Tomas Petricek (http://tomasp.net), Gustavo Guerra (http://functionalflow.co.uk), and other contributors
// Licensed under the Apache License, Version 2.0, see LICENSE.md in this project
//
// Helpers for writing type providers

namespace ProviderImplementation

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Reflection
open System.Text
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
module internal PrimitiveInferedPropertyExtensions =

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
module internal ActivePatterns =

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

module internal ReflectionHelpers = 

    open Microsoft.FSharp.Quotations
    open UncheckedQuotations

    let makeDelegate (exprfunc:Expr -> Expr) argType = 
        let var = Var("t", argType)
        let convBody = exprfunc (Expr.Var var)
        Expr.NewDelegateUnchecked(typedefof<Func<_,_>>.MakeGenericType(argType, convBody.Type), [var], convBody)

// ----------------------------------------------------------------------------------------------

type DisposableTypeProviderForNamespaces() as x =
    inherit TypeProviderForNamespaces()
  
    let disposeActions = ResizeArray()
  
    static let idCount = ref 0
  
    let id = !idCount
  
    do incr idCount 
  
    do log (sprintf "Creating TypeProviderForNamespaces %O [%d]" x id)

    let addDisposeAction action = lock disposeActions <| fun () -> disposeActions.Add action

    let dispose typeName = lock disposeActions <| fun () -> 
        log (sprintf "Disposing %s in TypeProviderForNamespaces %O [%d]" typeName x id)
        for dispose in disposeActions do
            dispose (Some typeName)

    let disposeAll() = lock disposeActions <| fun () ->
        log (sprintf "Disposing all types in TypeProviderForNamespaces %O [%d]" x id)
        for dispose in disposeActions do
            dispose None

    do
        x.Disposing.Add(fun _ -> disposeAll())
              
    interface IDisposableTypeProvider with
        member __.InvalidateOneType typeName = dispose typeName; ``base``.Invalidate()
        member __.AddDisposeAction action = addDisposeAction action
        member __.Id = id

// ----------------------------------------------------------------------------------------------

module internal ProviderHelpers =

    open System.IO
    open Microsoft.FSharp.Reflection
    open FSharp.Data.Runtime.Caching
    open FSharp.Data.Runtime.IO

    let unitsOfMeasureProvider = 
        { new StructuralInference.IUnitsOfMeasureProvider with
            member x.SI(str) = ProvidedMeasureBuilder.Default.SI str
            member x.Product(measure1, measure2) = ProvidedMeasureBuilder.Default.Product(measure1, measure2)
            member x.Inverse(denominator): Type = ProvidedMeasureBuilder.Default.Inverse(denominator) }

    let asyncMap (resultType:Type) (valueAsync:Expr<Async<'T>>) (body:Expr<'T>->Expr) =
        let (?) = ProviderImplementation.QuotationBuilder.(?)
        let convFunc = ReflectionHelpers.makeDelegate (Expr.Cast >> body) typeof<'T>      
        let f = Var("f", convFunc.Type)
        let body = typeof<TextRuntime>?AsyncMap (typeof<'T>, resultType) (valueAsync, Expr.Var f) 
        Expr.Let(f, convFunc, body) 

    let some (typ:Type) arg =
        let unionType = typedefof<option<_>>.MakeGenericType typ
        let meth = unionType.GetMethod("Some")
        Expr.Call(meth, [arg])

    let private cacheDuration = TimeSpan.FromMinutes 30.0
    let private invalidChars = [ for c in "\"|<>{}[]," -> c ] @ [ for i in 0..31 -> char i ] |> set
    let private webUrisCache, _ = createInternetFileCache "DesignTimeURIs" cacheDuration
    
    /// Extract path from message of exception FileNotFoundException or DirectoryNotFoundException 
    let private getPathFrom msg = 
        let pathMatcher = RegularExpressions.Regex(@"[^']+")
        try 
            pathMatcher.Matches(msg).[1].Value
        with e -> ""

    type private ParseTextResult<'T> =
        { TypedSamples : 'T []
          SampleIsUri : bool
          SampleIsWebUri : bool
          SampleIsResource : bool }

    let ReadResource(cfg: TypeProviderConfig, resourceName:string) =
        match resourceName.Split(',') with
        | [| asmName; name |] -> 
            let bindingCtxt = ProvidedTypesContext.Create(cfg)
            match bindingCtxt.TryBindAssembly(AssemblyName(asmName.Trim())) with
            | Choice1Of2 asm -> 
                use sr = new StreamReader(asm.GetManifestResourceStream(name.Trim()))
                Some(sr.ReadToEnd())
            | _ -> None
        | _ -> None

    /// Reads a sample parameter for a type provider, detecting if it is a uri and fetching it if needed
    /// Samples from the web are cached for 30 minutes
    /// Samples from the filesystem are read using shared read, so it works when the file is locked by Excel or similar tools,
    /// and a filesystem watcher that calls the invalidate function whenever the file changes is setup
    /// 
    /// Parameters:
    /// * sampleOrSampleUri - the text which can be a sample or an uri for a sample
    /// * isSampleUri - evaluate whether given string is a uri or not
    /// * parseFunc - receives the file/url extension (or ""  if not applicable) and the text value 
    /// * formatName - the description of what is being parsed (for the error message)
    /// * tp - the type provider
    /// * cfg - the type provider config
    /// * optResource - when specified, we first try to treat read the sample from an embedded resource
    ///     (the value specified assembly and resource name e.g. "MyCompany.MyAssembly, some_resource.json")
    /// * resolutionFolder - if the type provider allows to override the resolutionFolder pass it here
    let private parseTextAtDesignTime sampleOrSampleUri isSampleUri parseFunc formatName (tp:IDisposableTypeProvider) 
                                      (cfg:TypeProviderConfig) encodingStr resolutionFolder optResource fullTypeName maxNumberOfRows =
    
        using (logTime "Loading" sampleOrSampleUri) <| fun _ ->
        let inlineSample = isSampleUri sampleOrSampleUri

        let tryGetResource() = 
            if String.IsNullOrWhiteSpace(optResource)
            then None 
            else ReadResource(cfg, optResource)

        let tryGetUri str =
            match Uri.TryCreate(str, UriKind.RelativeOrAbsolute) with
            | false, _ -> None
            | true, uri ->
                if not inlineSample
                    || str.Trim() = "" 
                    || not uri.IsAbsoluteUri && Seq.exists invalidChars.Contains str
                then None else Some uri
    
        match tryGetResource() with
        | Some res -> { TypedSamples = parseFunc "" res
                        SampleIsUri = false
                        SampleIsWebUri = false
                        SampleIsResource = true }
        | _ -> 

        match tryGetUri sampleOrSampleUri with
        | None -> 
    
            try
                { TypedSamples = parseFunc "" sampleOrSampleUri
                  SampleIsUri = false
                  SampleIsWebUri = false
                  SampleIsResource = false }
            with e -> 
                failwithf "The provided sample is neither a file, nor a well-formed %s: %s" formatName e.Message
    
        | Some uri ->
            let resolver = 
                { ResolutionType = DesignTime
                  DefaultResolutionFolder = cfg.ResolutionFolder
                  ResolutionFolder = resolutionFolder }
            
            let readText() = 
                use reader = 
                    asyncRead (Some (tp, fullTypeName)) resolver formatName encodingStr uri
                    |> Async.RunSynchronously
                match maxNumberOfRows with
                | None -> reader.ReadToEnd()
                | Some max ->
                    let sb = StringBuilder()
                    let max = ref max
                    while !max > 0 do
                        let line = reader.ReadLine() 
                        if line = null then
                            max := 0
                        else
                            line |> sb.AppendLine |> ignore
                            decr max
                    sb.ToString()
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

                { TypedSamples = parseFunc (Path.GetExtension uri.OriginalString) sample
                  SampleIsUri = true
                  SampleIsWebUri = isWeb
                  SampleIsResource = false }
    
            with 
                | :? FileNotFoundException as e when not inlineSample ->
                    failwithf "Cannot read sample %s from '%s'" formatName (getPathFrom e.Message) 
                | :? DirectoryNotFoundException as e when not inlineSample ->
                    failwithf "Cannot read sample %s from '%s'" formatName (getPathFrom e.Message) 
                | e ->
    
                if not uri.IsAbsoluteUri then
                    // even if it's a valid uri, it could be sample text
                    try 
                        { TypedSamples = parseFunc "" sampleOrSampleUri
                          SampleIsUri = false
                          SampleIsWebUri = false
                          SampleIsResource = false }
                    with _ -> 
                        // if not, return the first exception
                        failwithf "Cannot read sample %s from '%s': %s" formatName sampleOrSampleUri e.Message
                else
                    failwithf "Cannot read sample %s from '%s': %s" formatName sampleOrSampleUri e.Message
    
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
    
    type CacheValue = ProvidedTypeDefinition * (string * string * string * Version) 
    //let (|CacheValue|_|) (wr: WeakReference) = match wr.Target with null -> None | v -> Some (v :?> CacheValue)
    //let CacheValue (pair: CacheValue) = System.WeakReference (box pair)
    //let private providedTypesCache = Dictionary<_,WeakReference>()

    let (|CacheValue|_|) (x: CacheValue) = Some x
    let CacheValue (pair: CacheValue) = pair
    let private providedTypesCache = ConcurrentDictionary<_,CacheValue>()
    
    // Cache generated types temporarily during partial invalidation of a type provider.
    let internal getOrCreateProvidedType (cfg: TypeProviderConfig) (tp:IDisposableTypeProvider) (fullTypeName:string) f =
      
      // The fsc.exe and fsi.exe processes don't invalidate, so caching is not useful
      if cfg.IsInvalidationSupported  then 
        let key = fullTypeName
        let fullKey = (fullTypeName, cfg.RuntimeAssembly, cfg.ResolutionFolder, cfg.SystemRuntimeAssemblyVersion)

        match providedTypesCache.TryGetValue key with
        | true, CacheValue (providedType, fullKey2) when fullKey = fullKey2 -> 
            log (sprintf "Reusing saved generation of type %s [%d]" fullTypeName tp.Id)
            providedType
        | _ -> 
            let providedType = f()

            // On disposal of one of the types, temporarily save the type if we know for sure that a different type is being invalidated.
            tp.AddDisposeAction <| fun typeNameBeingDisposedOpt -> 
                match typeNameBeingDisposedOpt with 
                | None -> ()
                | Some typeNameBeingDisposed -> 
                    // Check if a different type is being invalidated
                    if fullTypeName = typeNameBeingDisposed then
                        providedTypesCache.TryRemove key |> ignore
                    else
                        log (sprintf "Saving generation of type %s for 10 seconds awaiting incremental recreation [%d]" fullTypeName tp.Id)
                        providedTypesCache.[key] <- CacheValue (providedType, fullKey)
                        // Remove the cache entry in 10 seconds
                        async { do! Async.Sleep (10000)
                                providedTypesCache.TryRemove(key) |> ignore } |> Async.StartImmediate
            providedType
      else 
          f() 

    
    /// Creates all the constructors for a type provider: (Async)Parse, (Async)Load, (Async)GetSample(s), and default constructor
    /// * sampleOrSampleUri - the text which can be a sample or an uri for a sample
    /// * sampleIsList - true if the sample consists of several samples put together
    /// * isSampleUri - evaluate whether given string is a uri or not
    /// * parseSingle - receives the file/url extension (or ""  if not applicable) and the text value 
    /// * parseList - receives the file/url extension (or ""  if not applicable) and the text value 
    /// * getSpecFromSamples - receives a seq of parsed samples and returns a TypeProviderSpec
    /// * tp -> the type provider
    /// * cfg -> the type provider config
    /// * bindingContext -> the provided types context
    /// * resolutionFolder -> if the type provider allows to override the resolutionFolder pass it here
    /// * optResource - when specified, we first try to treat read the sample from an embedded resource
    ///     (the value specified assembly and resource name e.g. "MyCompany.MyAssembly, some_resource.json")
    /// * typeName -> the full name of the type provider, this will be used for caching
    let generateType formatName sampleOrSampleUri sampleIsList isSampleUri parseSingle parseList getSpecFromSamples (runtimeVersion: AssemblyResolver.FSharpDataRuntimeInfo)
                     (tp:DisposableTypeProviderForNamespaces) (cfg:TypeProviderConfig) (bindingContext:ProvidedTypesContext) 
                     encodingStr resolutionFolder optResource fullTypeName maxNumberOfRows =
    
        let isRunningInFSI = cfg.IsHostedExecution
        let defaultResolutionFolder = cfg.ResolutionFolder
        
        let parse extension (value:string) = using (logTime "Parsing" sampleOrSampleUri) <| fun _ ->
            if sampleIsList then
                parseList extension value
            else
                [| parseSingle extension value |]
        
        getOrCreateProvidedType cfg tp fullTypeName <| fun () ->

        // Infer the schema from a specified uri or inline text
        let parseResult = parseTextAtDesignTime sampleOrSampleUri isSampleUri parse formatName tp cfg encodingStr resolutionFolder optResource fullTypeName maxNumberOfRows
        
        let spec = getSpecFromSamples parseResult.TypedSamples
        
        let resultType = spec.RepresentationType
        let resultTypeAsync = typedefof<Async<_>>.MakeGenericType(resultType) 

        using (logTime "TypeGeneration" sampleOrSampleUri) <| fun _ ->
        
        [ // Generate static Parse method
          let args = [ bindingContext.ProvidedParameter("text", typeof<string>) ]
          let m = bindingContext.ProvidedMethod("Parse", args, resultType, isStatic = true,  
                                    invokeCode  = fun (Singleton text) -> 
                                        <@ new StringReader(%%text) :> TextReader @> 
                                        |> spec.CreateFromTextReader )
          m.AddXmlDoc <| sprintf "Parses the specified %s string" formatName
          yield m :> MemberInfo
          
          // Generate static Load stream method
          let args = [ bindingContext.ProvidedParameter("stream", typeof<Stream>) ]
          let m = bindingContext.ProvidedMethod("Load", args, resultType, isStatic = true, 
                                    invokeCode = fun (Singleton stream) ->   
                                        <@ new StreamReader(%%stream:Stream) :> TextReader @> 
                                        |> spec.CreateFromTextReader)
          m.AddXmlDoc <| sprintf "Loads %s from the specified stream" formatName
          yield m :> _
        
          // Generate static Load reader method
          let args = [ bindingContext.ProvidedParameter("reader", typeof<TextReader>) ]
          let m = bindingContext.ProvidedMethod("Load", args, resultType, isStatic = true, 
                                    invokeCode = fun (Singleton reader) ->  
                                        let reader = reader |> Expr.Cast 
                                        reader |> spec.CreateFromTextReader)
          m.AddXmlDoc <| sprintf "Loads %s from the specified reader" formatName
          yield m :> _
          
          // Generate static Load uri method
          let args = [ bindingContext.ProvidedParameter("uri", typeof<string>) ]
          let m = bindingContext.ProvidedMethod("Load", args, resultType, isStatic = true,  
                                    invokeCode = fun (Singleton uri) -> 
                                         <@ Async.RunSynchronously(asyncReadTextAtRuntime isRunningInFSI defaultResolutionFolder resolutionFolder formatName encodingStr %%uri) @> 
                                         |> spec.CreateFromTextReader)
          m.AddXmlDoc <| sprintf "Loads %s from the specified uri" formatName
          yield m :> _
        
          // Generate static AsyncLoad uri method
          let args = [ bindingContext.ProvidedParameter("uri", typeof<string>) ]
          let m = bindingContext.ProvidedMethod("AsyncLoad", args, resultTypeAsync, isStatic = true,
                                     invokeCode = fun (Singleton uri) -> 
                                         let readerAsync = <@ asyncReadTextAtRuntime isRunningInFSI defaultResolutionFolder resolutionFolder formatName encodingStr %%uri @>
                                         asyncMap resultType readerAsync spec.CreateFromTextReader)
          m.AddXmlDoc <| sprintf "Loads %s from the specified uri" formatName
          yield m :> _
          
          if sampleOrSampleUri <> "" && 
             not (parseResult.SampleIsResource) && 
             (runtimeVersion.SupportsLocalFileSystem || not parseResult.SampleIsUri || parseResult.SampleIsWebUri) then
        
              if sampleIsList then
              
                  // the [][] case needs more work, and it's a weird scenario anyway, so we won't support it
                  if not resultType.IsArray then
                  
                      let resultTypeArray = resultType.MakeArrayType()
                      let resultTypeArrayAsync = typedefof<Async<_>>.MakeGenericType(resultTypeArray) 
                      
                      // Generate static GetSamples method
                      let m = bindingContext.ProvidedMethod("GetSamples", [], resultTypeArray, isStatic = true,
                                                invokeCode = fun _ -> 
                                                  if parseResult.SampleIsUri 
                                                  then <@ Async.RunSynchronously(asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr sampleOrSampleUri) @>
                                                  else <@ new StringReader(sampleOrSampleUri) :> TextReader @>
                                                  |> spec.CreateFromTextReaderForSampleList)
                      yield m :> _
                              
                      if parseResult.SampleIsUri  then
                          // Generate static AsyncGetSamples method
                          let m = bindingContext.ProvidedMethod("AsyncGetSamples", [], resultTypeArrayAsync, isStatic = true,
                                                    invokeCode = fun _ -> 
                                                      let readerAsync = <@ asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr sampleOrSampleUri @>
                                                      spec.CreateFromTextReaderForSampleList 
                                                      |> asyncMap resultTypeArray readerAsync)
                          yield m :> _
              
              else 
              
                let name = if resultType.IsArray then "GetSamples" else "GetSample"
                let getSampleCode _ =
                    if parseResult.SampleIsUri  
                    then <@ Async.RunSynchronously(asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr sampleOrSampleUri) @>
                    else <@ new StringReader(sampleOrSampleUri) :> TextReader @>
                    |> spec.CreateFromTextReader

                // Generate static GetSample method
                yield bindingContext.ProvidedMethod(name, [], resultType, isStatic = true, 
                                        invokeCode = getSampleCode) :> _
                          
                if not sampleIsList && spec.GeneratedType :> Type = spec.RepresentationType then
                    // Generate default constructor
                    yield bindingContext.ProvidedConstructor([], invokeCode = getSampleCode) :> _
              
                if parseResult.SampleIsUri then
                    // Generate static AsyncGetSample method
                    let m = bindingContext.ProvidedMethod("Async" + name, [], resultTypeAsync, isStatic = true, 
                                              invokeCode = fun _ -> 
                                                let readerAsync = <@ asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr sampleOrSampleUri @>
                                                asyncMap resultType readerAsync spec.CreateFromTextReader)
                    yield m :> _
        
        ] |> spec.GeneratedType.AddMembers
        
        spec.GeneratedType
