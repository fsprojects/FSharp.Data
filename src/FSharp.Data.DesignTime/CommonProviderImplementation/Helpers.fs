// Copyright 2011-2015, Tomas Petricek (http://tomasp.net), Gustavo Guerra (http://functionalflow.co.uk), and other contributors
// Licensed under the Apache License, Version 2.0, see LICENSE.md in this project
//
// Helpers for writing type providers

namespace ProviderImplementation

open System
open System.Collections.Generic
open System.Reflection
open System.Text
open System.Runtime.CompilerServices
open FSharp.Core.CompilerServices
open FSharp.Quotations
open FSharp.Data.Runtime
open FSharp.Data.Runtime.IO
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference
open ProviderImplementation
open ProviderImplementation.ProvidedTypes

// ----------------------------------------------------------------------------------------------

[<AutoOpen>]
module internal PrimitiveInferedValueExtensions =

    type PrimitiveInferedValue with

        member x.TypeWithMeasure =
            match x.UnitOfMeasure with
            | None -> x.RuntimeType
            | Some unit ->
                if supportsUnitsOfMeasure x.RuntimeType then
                    ProvidedMeasureBuilder.AnnotateType(x.RuntimeType, [ unit ])
                else
                    failwithf "Units of measure not supported by type %s" x.RuntimeType.Name


// ----------------------------------------------------------------------------------------------

[<AutoOpen>]
module internal ActivePatterns =

    /// Helper active pattern that can be used when constructing InvokeCode
    /// (to avoid writing pattern matching or incomplete matches):
    ///
    ///    p.InvokeCode <- fun (Singleton self) -> <@ 1 + 2 @>
    ///
    let (|Singleton|) =
        function
        | [ l ] -> l
        | _ -> failwith "Parameter mismatch"

    /// Takes a map and succeeds if it is empty
    let (|EmptyMap|_|) result (map: Map<_, _>) =
        if map.IsEmpty then Some result else None

    /// Takes a map and succeeds if it contains exactly one value
    let (|SingletonMap|_|) (map: Map<_, _>) =
        if map.Count <> 1 then
            None
        else
            let (KeyValue (k, v)) = Seq.head map
            Some(k, v)

// ----------------------------------------------------------------------------------------------

module internal ReflectionHelpers =

    open FSharp.Quotations
    open UncheckedQuotations

    let makeDelegate (exprfunc: Expr -> Expr) argType =
        let var = Var("t", argType)
        let convBody = exprfunc (Expr.Var var)
        Expr.NewDelegateUnchecked(typedefof<Func<_, _>>.MakeGenericType (argType, convBody.Type), [ var ], convBody)

// ----------------------------------------------------------------------------------------------

type DisposableTypeProviderForNamespaces(config, ?assemblyReplacementMap) as x =
    inherit TypeProviderForNamespaces(config, ?assemblyReplacementMap = assemblyReplacementMap)

    let disposeActions = ResizeArray()

    static let mutable idCount = 0

    let id = idCount
    let filesToWatch = Dictionary()

    do idCount <- idCount + 1

    let dispose typeNameOpt =
        lock disposeActions (fun () ->
            for i = disposeActions.Count - 1 downto 0 do
                let disposeAction = disposeActions.[i]
                let discard = disposeAction typeNameOpt
                if discard then disposeActions.RemoveAt(i))

    do
        log (sprintf "Creating TypeProviderForNamespaces %O [%d]" x id)

        x.Disposing.Add(fun _ ->
            use _holder = logTime "DisposingEvent" (sprintf "%O [%d]" x id)
            dispose None)

    member _.Id = id

    member _.SetFileToWatch(fullTypeName, path) =
        lock filesToWatch (fun () -> filesToWatch.[fullTypeName] <- path)

    member _.GetFileToWath(fullTypeName) =
        lock filesToWatch (fun () ->
            match filesToWatch.TryGetValue(fullTypeName) with
            | true, path -> Some path
            | _ -> None)

    member _.AddDisposeAction action =
        lock disposeActions (fun () -> disposeActions.Add action)

    member _.InvalidateOneType typeName =
        (use _holder = logTime "InvalidateOneType" (sprintf "%s in %O [%d]" typeName x id)
         dispose (Some typeName)
         log (sprintf "Calling invalidate for %O [%d]" x id))

        base.Invalidate()

#if LOGGING_ENABLED

    override x.Finalize() = log (sprintf "Finalize %O [%d]" x id)

#endif

// ----------------------------------------------------------------------------------------------

module internal ProviderHelpers =

    open System.IO
    open FSharp.Data.Runtime.Caching

    let unitsOfMeasureProvider =
        { new StructuralInference.IUnitsOfMeasureProvider with
            member x.SI(str) = ProvidedMeasureBuilder.SI str

            member x.Product(measure1, measure2) =
                ProvidedMeasureBuilder.Product(measure1, measure2)

            member x.Inverse(denominator) : Type =
                ProvidedMeasureBuilder.Inverse(denominator) }

    let asyncMap (resultType: Type) (valueAsync: Expr<Async<'T>>) (body: Expr<'T> -> Expr) =
        let (?) = QuotationBuilder.(?)
        let convFunc = ReflectionHelpers.makeDelegate (Expr.Cast >> body) typeof<'T>
        let f = Var("f", convFunc.Type)

        let body =
            typeof<TextRuntime>?(nameof (TextRuntime.AsyncMap)) (typeof<'T>, resultType) (valueAsync, Expr.Var f)

        Expr.Let(f, convFunc, body)

    let some (typ: Type) arg =
        let unionType = typedefof<option<_>>.MakeGenericType typ
        let meth = unionType.GetMethod("Some")
        Expr.Call(meth, [ arg ])

    let private cacheDuration = TimeSpan.FromMinutes 30.0

    let private invalidChars =
        [ for c in "\"|<>{}[]," -> c ]
        @ [ for i in 0..31 -> char i ]
        |> set

    let private webUrisCache = createInternetFileCache "DesignTimeURIs" cacheDuration

    // part of the information needed by generateType
    type TypeProviderSpec<'RuntimeValue> =
        {
            GeneratedType: ProvidedTypeDefinition //the generated type
            //the representation type (what's returned from the constructors, may or may not be the same as Type)
            RepresentationType: Type
            // the constructor from a text reader to the representation
            CreateFromTextReader: Expr<TextReader> -> Expr
            CreateListFromTextReader: (Expr<TextReader> -> Expr) option
            // the constructor from a text reader to an array of the representation
            CreateFromTextReaderForSampleList: Expr<TextReader> -> Expr
            /// Runtime representation of underlying data (e.g. JsonValue) * Mapper function
            CreateFromValue: (Type * (Expr<'RuntimeValue> -> Expr)) option
        }

    type private ParseTextResult<'RuntimeValue> =
        { Spec: TypeProviderSpec<'RuntimeValue>
          IsUri: bool
          IsResource: bool }

    let readResource (tp: DisposableTypeProviderForNamespaces, resourceName: string) =
        match resourceName.Split(',') with
        | [| asmName; name |] ->
            let bindingCtxt = tp.TargetContext

            match bindingCtxt.TryBindSimpleAssemblyNameToTarget(asmName.Trim()) with
            | Choice1Of2 asm ->
                use sr = new StreamReader(asm.GetManifestResourceStream(name.Trim()))
                Some(sr.ReadToEnd())
            | _ -> None
        | _ -> None

    /// <summary>
    /// Reads a sample parameter for a type provider, detecting if it is a uri and fetching it if needed
    /// </summary>
    /// <remarks>
    /// Samples from the web are cached for 30 minutes.
    /// Samples from the filesystem are read using shared read, so it works when the file is locked by Excel or similar tools,
    /// </remarks>
    ///
    /// <param name="valueToBeParsedOrItsUri">the text which can be a sample or an uri for a sample</param>
    /// <param name="parseFunc">receives the file/url extension (or ""  if not applicable) and the text value </param>
    /// <param name="formatName">the description of what is being parsed (for the error message)</param>
    /// <param name="tp">the type provider</param>
    /// <param name="cfg">the type provider config</param>
    /// <param name="resource">when specified, we first try to treat read the sample from an embedded resource
    ///     (the value specified assembly and resource name e.g. "MyCompany.MyAssembly, some_resource.json")</param>
    /// <param name="resolutionFolder">if the type provider allows to override the resolutionFolder pass it here</param>
    /// <param name="encodingStr"></param>
    /// <param name="fullTypeName"></param>
    /// <param name="maxNumberOfRows"></param>
    let private parseTextAtDesignTime
        valueToBeParsedOrItsUri
        parseFunc
        formatName
        (tp: DisposableTypeProviderForNamespaces)
        (cfg: TypeProviderConfig)
        encodingStr
        resolutionFolder
        resource
        fullTypeName
        maxNumberOfRows
        =

        use _holder = logTime "LoadingTextToBeParsed" valueToBeParsedOrItsUri

        let tryGetResource () =
            if resource = "" then None else readResource (tp, resource)

        let tryGetUri str =
            match Uri.TryCreate(str, UriKind.RelativeOrAbsolute) with
            | false, _ -> None
            | true, uri ->
                if str.Trim() = ""
                   || not uri.IsAbsoluteUri
                      && Seq.exists invalidChars.Contains str then
                    None
                else
                    Some uri

        match tryGetResource () with
        | Some res ->
            { Spec = parseFunc "" res
              IsUri = false
              IsResource = true }
        | _ ->

            match tryGetUri valueToBeParsedOrItsUri with
            | None ->

                try
                    { Spec = parseFunc "" valueToBeParsedOrItsUri
                      IsUri = false
                      IsResource = false }
                with e ->
                    failwithf
                        "The provided sample is neither a file, nor a well-formed %s: %s"
                        formatName
                        (e.ToString())

            | Some uri ->

                let resolver =
                    { ResolutionType = DesignTime
                      DefaultResolutionFolder = cfg.ResolutionFolder
                      ResolutionFolder = resolutionFolder }

                let readText () =
                    let reader, toWatch = asyncRead resolver formatName encodingStr uri
                    // Non need to register file watchers in fsc.exe and fsi.exe
                    if cfg.IsInvalidationSupported then
                        toWatch
                        |> Option.iter (fun path -> tp.SetFileToWatch(fullTypeName, path))

                    use reader = reader |> Async.RunSynchronously

                    match maxNumberOfRows with
                    | None -> reader.ReadToEnd()
                    | Some max ->
                        let sb = StringBuilder()
                        let mutable max = max

                        while max > 0 do
                            let line = reader.ReadLine()

                            if isNull line then
                                max <- 0
                            else
                                line |> sb.AppendLine |> ignore
                                max <- max - 1

                        sb.ToString()

                try

                    let sample =
                        if isWeb uri then
                            let text =
                                match webUrisCache.TryRetrieve(uri.OriginalString) with
                                | Some text -> text
                                | None ->
                                    let text = readText ()
                                    webUrisCache.Set(uri.OriginalString, text)
                                    text

                            text
                        else
                            readText ()

                    { Spec = parseFunc (Path.GetExtension uri.OriginalString) sample
                      IsUri = true
                      IsResource = false }

                with e ->

                    if not uri.IsAbsoluteUri then
                        // even if it's a valid uri, it could be sample text
                        try
                            { Spec = parseFunc "" valueToBeParsedOrItsUri
                              IsUri = false
                              IsResource = false }
                        with _ ->
                            // if not, return the first exception
                            failwithf
                                "Cannot read sample %s from '%s': %s"
                                formatName
                                valueToBeParsedOrItsUri
                                (e.ToString())
                    else
                        failwithf
                            "Cannot read sample %s from '%s': %s"
                            formatName
                            valueToBeParsedOrItsUri
                            (e.ToString())

    let private providedTypesCache = createInMemoryCache (TimeSpan.FromSeconds 30.0)
    let private activeDisposeActions = HashSet<_>()

    // Cache generated types for a short time, since VS invokes the TP multiple times
    // Also cache temporarily during partial invalidation since the invalidation of one TP always causes invalidation of all TPs
    let internal getOrCreateProvidedType
        (cfg: TypeProviderConfig)
        (tp: DisposableTypeProviderForNamespaces)
        (fullTypeName: string)
        f
        =

        use _holder =
            logTime "GeneratingProvidedType" (sprintf "%s [%d]" fullTypeName tp.Id)

        let fullKey =
            (fullTypeName, cfg.RuntimeAssembly, cfg.ResolutionFolder, cfg.SystemRuntimeAssemblyVersion)

        let setupDisposeAction providedType fileToWatch =

            if activeDisposeActions.Add(fullTypeName, tp.Id) then

                log "Setting up dispose action"

                let watcher =
                    match fileToWatch with
                    | Some file ->
                        let name = sprintf "%s [%d]" fullTypeName tp.Id
                        // Hold a weak reference to the type provider instance.  If the TP instance is leaked
                        // and not held strongly by anyone else, then don't hold it strongly here.
                        let tpref = WeakReference<_>(tp)

                        let invalidateAction () =
                            match tpref.TryGetTarget() with
                            | true, tp -> tp.InvalidateOneType(fullTypeName)
                            | _ -> ()

                        Some(watchForChanges file (name, invalidateAction))
                    | None -> None

                // On disposal of one of the types, remove that type from the cache, and add all others to the cache
                tp.AddDisposeAction(fun typeNameBeingDisposedOpt ->

                    // might be called more than once for each watcher, but the Dispose action is a NOP the second time
                    watcher
                    |> Option.iter (fun watcher -> watcher.Dispose())

                    match typeNameBeingDisposedOpt with
                    | Some typeNameBeingDisposed when fullTypeName = typeNameBeingDisposed ->
                        providedTypesCache.Remove(fullTypeName)
                        log (sprintf "Dropping dispose action for %s [%d]" fullTypeName tp.Id)
                        // for the case where a file used by two TPs, when the file changes
                        // there will be two invalidations: A and B
                        // when the dispose action is called with A, A is removed from the cache
                        // so we need to remove the dispose action so it will won't be added when disposed is called with B
                        true
                    | _ ->
                        log (sprintf "Caching %s [%d] for 5 minutes" fullTypeName tp.Id)
                        providedTypesCache.Set(fullTypeName, (providedType, fullKey, fileToWatch))
                        // for the case where a file used by two TPs, when the file changes
                        // there will be two invalidations: A and B
                        // when the dispose action is called with A, B is added to the cache
                        // so we need to keep the dispose action around so it will be called with B and the cache is removed
                        false)

        match providedTypesCache.TryRetrieve(fullTypeName, true) with
        | Some (providedType, fullKey2, watchedFile) when fullKey = fullKey2 ->
            log "Retrieved from cache"
            setupDisposeAction providedType watchedFile
            providedType
        | _ ->
            let providedType = f ()
            log "Caching for 5 minutes"
            let fileToWatch = tp.GetFileToWath(fullTypeName)
            providedTypesCache.Set(fullTypeName, (providedType, fullKey, fileToWatch))
            setupDisposeAction providedType fileToWatch
            providedType

    type Source =
        | Sample of string
        | SampleList of string
        | Schema of string

    /// Creates all the constructors for a type provider: (Async)Parse, (Async)Load, (Async)GetSample(s), and default constructor
    /// <param name="source">the sample/sample list/schema from which the types will be generated</param>
    /// <param name="getSpec">receives the file/url extension (or ""  if not applicable) and the text value of the sample or schema</param>
    /// <param name="tp">the type provider</param>
    /// <param name="cfg">the type provider config</param>
    /// <param name="encodingStr">the encoding to be used when reading the sample or schema</param>
    /// <param name="resolutionFolder -> if the type provider allows to override the resolutionFolder pass it here</param>
    /// <param name="resource">when specified, we first try to treat read the sample from an embedded resource</param>
    ///     (the value specifies assembly and resource name e.g. "MyCompany.MyAssembly, some_resource.json")</param>
    /// <param name="fullTypeName">the full name of the type provider, this will be used as the caching key</param>
    /// <param name="maxNumberOfRows">the max number of rows to read from the sample or schema</param>
    let generateType
        formatName
        source
        getSpec
        (tp: DisposableTypeProviderForNamespaces)
        (cfg: TypeProviderConfig)
        encodingStr
        resolutionFolder
        resource
        fullTypeName
        maxNumberOfRows
        =

        getOrCreateProvidedType cfg tp fullTypeName (fun () ->

            let isRunningInFSI = cfg.IsHostedExecution
            let defaultResolutionFolder = cfg.ResolutionFolder

            let valueToBeParsedOrItsUri =
                match source with
                | Sample value -> value
                | SampleList value -> value
                | Schema value -> value

            let parseResult =
                parseTextAtDesignTime
                    valueToBeParsedOrItsUri
                    getSpec
                    formatName
                    tp
                    cfg
                    encodingStr
                    resolutionFolder
                    resource
                    fullTypeName
                    maxNumberOfRows

            let spec = parseResult.Spec

            let resultType = spec.RepresentationType
            let resultTypeAsync = typedefof<Async<_>>.MakeGenericType (resultType)

            use _holder = logTime "CommonTypeGeneration" valueToBeParsedOrItsUri

            [ // Generate static Parse method
              let args = [ ProvidedParameter("text", typeof<string>) ]

              let m =
                  let parseCode (Singleton text: Expr list) =
                      <@ new StringReader(%%text) :> TextReader @>
                      |> spec.CreateFromTextReader

                  ProvidedMethod("Parse", args, resultType, isStatic = true, invokeCode = parseCode)

              m.AddXmlDoc(sprintf "Parses the specified %s string" formatName)
              yield m :> MemberInfo

              match spec.CreateListFromTextReader with
              | None -> ()
              | Some listParser ->
                  let resultTypeList = resultType.MakeArrayType()
                  let args = [ ProvidedParameter("text", typeof<string>) ]

                  let parseListCode (Singleton text: Expr list) =
                      <@ new StringReader(%%text) :> TextReader @>
                      |> listParser

                  let m =
                      ProvidedMethod("ParseList", args, resultTypeList, isStatic = true, invokeCode = parseListCode)

                  m.AddXmlDoc(sprintf "Parses the specified %s string" formatName)
                  yield m :> _

              // Generate static Load stream method
              let args = [ ProvidedParameter("stream", typeof<Stream>) ]

              let loadCode1 (Singleton stream: Expr list) =
                  <@ new StreamReader(%%stream: Stream) :> TextReader @>
                  |> spec.CreateFromTextReader

              let m =
                  ProvidedMethod("Load", args, resultType, isStatic = true, invokeCode = loadCode1)

              m.AddXmlDoc(sprintf "Loads %s from the specified stream" formatName)
              yield m :> _

              // Generate static Load reader method
              let args = [ ProvidedParameter("reader", typeof<TextReader>) ]

              let loadCode2 (Singleton reader: Expr list) =
                  let reader = reader |> Expr.Cast
                  reader |> spec.CreateFromTextReader

              let m =
                  ProvidedMethod("Load", args, resultType, isStatic = true, invokeCode = loadCode2)

              m.AddXmlDoc(sprintf "Loads %s from the specified reader" formatName)
              yield m :> _

              // Generate static Load uri method
              let args = [ ProvidedParameter("uri", typeof<string>) ]

              let loadCode3 (Singleton uri: Expr list) =
                  <@
                      Async.RunSynchronously(
                          asyncReadTextAtRuntime
                              isRunningInFSI
                              defaultResolutionFolder
                              resolutionFolder
                              formatName
                              encodingStr
                              %%uri
                      )
                  @>
                  |> spec.CreateFromTextReader

              let m =
                  ProvidedMethod("Load", args, resultType, isStatic = true, invokeCode = loadCode3)

              m.AddXmlDoc(sprintf "Loads %s from the specified uri" formatName)
              yield m :> _

              // Generate static AsyncLoad uri method
              let args = [ ProvidedParameter("uri", typeof<string>) ]

              let asyncLoadCode (Singleton uri: Expr list) =
                  let readerAsync =
                      <@
                          asyncReadTextAtRuntime
                              isRunningInFSI
                              defaultResolutionFolder
                              resolutionFolder
                              formatName
                              encodingStr
                              %%uri
                      @>

                  asyncMap resultType readerAsync spec.CreateFromTextReader

              let m =
                  ProvidedMethod("AsyncLoad", args, resultTypeAsync, isStatic = true, invokeCode = asyncLoadCode)

              m.AddXmlDoc(sprintf "Loads %s from the specified uri" formatName)
              yield m :> _

              // Generate static Load value method
              match spec.CreateFromValue with
              | None -> ()
              | Some (valueType, valueMapper) ->
                  let args = [ ProvidedParameter("value", valueType) ]

                  let loadCode (Singleton value: Expr list) =
                      let value = value |> Expr.Cast
                      <@ %value @> |> valueMapper

                  let m =
                      ProvidedMethod("Load", args, resultType, isStatic = true, invokeCode = loadCode)

                  m.AddXmlDoc(sprintf "Loads %s from the specified value" formatName)
                  yield m :> _

              if not parseResult.IsResource then

                  match source with
                  | SampleList _ ->

                      // the [][] case needs more work, and it's a weird scenario anyway, so we won't support it
                      if not resultType.IsArray then

                          let resultTypeArray = resultType.MakeArrayType()
                          let resultTypeArrayAsync = typedefof<Async<_>>.MakeGenericType (resultTypeArray)

                          // Generate static GetSamples method
                          let getSamplesCode _ =
                              if parseResult.IsUri then
                                  <@
                                      Async.RunSynchronously(
                                          asyncReadTextAtRuntimeWithDesignTimeRules
                                              defaultResolutionFolder
                                              resolutionFolder
                                              formatName
                                              encodingStr
                                              valueToBeParsedOrItsUri
                                      )
                                  @>
                              else
                                  <@ new StringReader(valueToBeParsedOrItsUri) :> TextReader @>
                              |> spec.CreateFromTextReaderForSampleList

                          let m =
                              ProvidedMethod(
                                  "GetSamples",
                                  [],
                                  resultTypeArray,
                                  isStatic = true,
                                  invokeCode = getSamplesCode
                              )

                          yield m :> _

                          if parseResult.IsUri then
                              // Generate static AsyncGetSamples method
                              let methCode _ =
                                  let readerAsync =
                                      <@
                                          asyncReadTextAtRuntimeWithDesignTimeRules
                                              defaultResolutionFolder
                                              resolutionFolder
                                              formatName
                                              encodingStr
                                              valueToBeParsedOrItsUri
                                      @>

                                  spec.CreateFromTextReaderForSampleList
                                  |> asyncMap resultTypeArray readerAsync

                              let m =
                                  ProvidedMethod(
                                      "AsyncGetSamples",
                                      [],
                                      resultTypeArrayAsync,
                                      isStatic = true,
                                      invokeCode = methCode
                                  )

                              yield m :> _

                  | Sample _ ->

                      let name = if resultType.IsArray then "GetSamples" else "GetSample"

                      let getSampleCode _ =
                          if parseResult.IsUri then
                              <@
                                  Async.RunSynchronously(
                                      asyncReadTextAtRuntimeWithDesignTimeRules
                                          defaultResolutionFolder
                                          resolutionFolder
                                          formatName
                                          encodingStr
                                          valueToBeParsedOrItsUri
                                  )
                              @>
                          else
                              <@ new StringReader(valueToBeParsedOrItsUri) :> TextReader @>
                          |> spec.CreateFromTextReader

                      // Generate static GetSample method
                      yield ProvidedMethod(name, [], resultType, isStatic = true, invokeCode = getSampleCode) :> _

                      if spec.GeneratedType :> Type = spec.RepresentationType then
                          // Generate default constructor
                          yield ProvidedConstructor([], invokeCode = getSampleCode) :> _

                      if parseResult.IsUri then
                          // Generate static AsyncGetSample method
                          let asyncGetSampleCode _ =
                              let readerAsync =
                                  <@
                                      asyncReadTextAtRuntimeWithDesignTimeRules
                                          defaultResolutionFolder
                                          resolutionFolder
                                          formatName
                                          encodingStr
                                          valueToBeParsedOrItsUri
                                  @>

                              asyncMap resultType readerAsync spec.CreateFromTextReader

                          let m =
                              ProvidedMethod(
                                  "Async" + name,
                                  [],
                                  resultTypeAsync,
                                  isStatic = true,
                                  invokeCode = asyncGetSampleCode
                              )

                          yield m :> _

                  | Schema _ ->
                      let getSchemaCode _ =
                          if parseResult.IsUri then
                              <@
                                  Async.RunSynchronously(
                                      asyncReadTextAtRuntimeWithDesignTimeRules
                                          defaultResolutionFolder
                                          resolutionFolder
                                          formatName
                                          encodingStr
                                          valueToBeParsedOrItsUri
                                  )
                              @>
                          else
                              <@ new StringReader(valueToBeParsedOrItsUri) :> TextReader @>
                          |> spec.CreateFromTextReaderForSampleList // hack: this will actually parse the schema

                      // Generate static GetSchema method
                      yield
                          ProvidedMethod(
                              "GetSchema",
                              [],
                              typeof<System.Xml.Schema.XmlSchemaSet>,
                              isStatic = true,
                              invokeCode = getSchemaCode
                          )
                          :> _


              ]
            |> spec.GeneratedType.AddMembers

            spec.GeneratedType)

[<assembly: InternalsVisibleToAttribute("FSharp.Data.Tests, PublicKey=00240000048000001401000006020000002400005253413100080000010001000de370e30996d51c2da4ba3878423843e8553ff8cf95bd0171fe6785d20e2f73c8a54feb5bf55888115de98bdf0f8c0e26ee79e4c0f535201582628313859078ab3be84442114655340980fa0232281badaa21c1c2849c1925d0cfbc3dfa8d22b00ba9800a3d9a6c00c5daf7344e3286c3ed6c3e62d7705db32e2a35ffef84963b8ae0a3fa8a365b4020007d22127bc24783a65602e858680d88f36d4d3ff7567fcbece85143ea5945330eb74e53596d0ead1209c56eaf2c5adbb80a05d70e59ba06b50af250a3b87239dd88b60ed57263ede090ea195f093aac2216897669634235b638fdd47b78fe55c9e34389c2a7cac21250b79c49e3a6e2f78dd3de9487")>]
[<assembly: InternalsVisibleToAttribute("FSharp.Data.DesignTime.Tests, PublicKey=00240000048000001401000006020000002400005253413100080000010001000de370e30996d51c2da4ba3878423843e8553ff8cf95bd0171fe6785d20e2f73c8a54feb5bf55888115de98bdf0f8c0e26ee79e4c0f535201582628313859078ab3be84442114655340980fa0232281badaa21c1c2849c1925d0cfbc3dfa8d22b00ba9800a3d9a6c00c5daf7344e3286c3ed6c3e62d7705db32e2a35ffef84963b8ae0a3fa8a365b4020007d22127bc24783a65602e858680d88f36d4d3ff7567fcbece85143ea5945330eb74e53596d0ead1209c56eaf2c5adbb80a05d70e59ba06b50af250a3b87239dd88b60ed57263ede090ea195f093aac2216897669634235b638fdd47b78fe55c9e34389c2a7cac21250b79c49e3a6e2f78dd3de9487")>]
do ()
