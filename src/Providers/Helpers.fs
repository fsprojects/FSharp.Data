// --------------------------------------------------------------------------------------
// Helpers for writing type providers
// ----------------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.IO
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open FSharp.Data.RuntimeImplementation.StructuralTypes
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference

// ----------------------------------------------------------------------------------------------

module Seq = 

  /// Merge two sequences by pairing elements for which
  /// the specified predicate returns the same key
  ///
  /// (If the inputs contain the same keys, then the order
  /// of the elements is preserved.)
  let pairBy f first second = 
    let vals1 = [ for o in first -> f o, o ]
    let vals2 = [ for o in second -> f o, o ]
    let d1, d2 = dict vals1, dict vals2
    let k1, k2 = set d1.Keys, set d2.Keys
    let keys = List.map fst vals1 @ (List.ofSeq (k2 - k1))
    let asOption = function true, v -> Some v | _ -> None
    [ for k in keys -> 
        k, asOption (d1.TryGetValue(k)), asOption (d2.TryGetValue(k)) ]

module List = 

  /// Split a non-empty list into a pair consisting of
  /// its head and its tail
  let headAndTail l = 
    match l with 
    | [] -> invalidArg "l" "empty list" 
    | head::tail -> (head, tail)

  /// Split a non-empty list into a list with all elements 
  /// except for the last one and the last element
  let frontAndBack l = 
    let rec loop acc l = 
      match l with
      | [] -> invalidArg "l" "empty list" 
      | [singleton] -> List.rev acc, singleton
      | head::tail -> loop  (head::acc) tail
    loop [] l

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

module internal ReflectionHelpers = 

  open Microsoft.FSharp.Quotations

  let makeDelegate (exprfunc:Expr -> Expr) argType = 
    let var = Var.Global("t", argType)
    let convBody = exprfunc (Expr.Var var)
    convBody.Type, Expr.NewDelegate(typedefof<Func<_,_>>.MakeGenericType [| argType; convBody.Type |], [var], convBody)
        
// ----------------------------------------------------------------------------------------------

module ProviderHelpers =

  open System.IO
  open FSharp.Data.RuntimeImplementation.Caching
  open FSharp.Data.RuntimeImplementation.ProviderFileSystem

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
  /// * tp -> the type provider
  /// * cfg -> the type provider config
  /// * resolutionFolder -> if the type provider allows to override the resolutionFolder pass it here
  let parseTextAtDesignTime sampleOrSampleUri parseFunc formatName
                            (tp:TypeProviderForNamespaces) (cfg:TypeProviderConfig) resolutionFolder =
  
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
                  use! stream = asyncOpenStream (Some tp.Invalidate) resolver uri
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
  /// * replaceer -> the assemblyReplacer
  /// * resolutionFolder -> if the type provider allows to override the resolutionFolder pass it here
  let generateConstructors formatName sampleOrSampleUri sampleIsList parseSingle parseList getSpecFromSamples
                           (tp:TypeProviderForNamespaces) (cfg:TypeProviderConfig) (replacer:AssemblyReplacer) resolutionFolder =

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
    
    let resultType = spec.RepresentationType
    let resultTypeAsync = typedefof<Async<_>>.MakeGenericType [| spec.RepresentationType |] |> replacer.ToRuntime

    [ // Generate static Parse method
      let args = [ ProvidedParameter("text", typeof<string>) ]
      let m = ProvidedMethod("Parse", args, resultType, IsStaticMethod = true)
      m.InvokeCode <- fun (Singleton text) -> 
        <@ new StringReader(%%text) :> TextReader @>
        |> spec.CreateFromTextReader 
      m.AddXmlDoc <| sprintf "Parses the specified %s string" formatName
      yield m
      
      // Generate static Load stream method
      let args = [ ProvidedParameter("stream", typeof<Stream>) ]
      let m = ProvidedMethod("Load", args, resultType, IsStaticMethod = true)
      m.InvokeCode <- fun (Singleton stream) ->       
        <@ new StreamReader(%%stream:Stream) :> TextReader @>
        |> spec.CreateFromTextReader 
      m.AddXmlDoc <| sprintf "Loads %s from the specified stream" formatName
      yield m

      // Generate static Load reader method
      let args = [ ProvidedParameter("reader", typeof<TextReader>) ]
      let m = ProvidedMethod("Load", args, resultType, IsStaticMethod = true)
      m.InvokeCode <- fun (Singleton reader) -> 
        <@ %%reader:TextReader @>
        |> spec.CreateFromTextReader 
      m.AddXmlDoc <| sprintf "Loads %s from the specified reader" formatName
      yield m
      
      // Generate static Load uri method
      let args = [ ProvidedParameter("uri", typeof<string>) ]
      let m = ProvidedMethod("Load", args, resultType, IsStaticMethod = true)
      m.InvokeCode <- fun (Singleton uri) -> 
        <@ asyncReadTextAtRuntime isRunningInFSI defaultResolutionFolder resolutionFolder %%uri
           |> Async.RunSynchronously @>
        |> spec.CreateFromTextReader 
      m.AddXmlDoc <| sprintf "Loads %s from the specified uri" formatName
      yield m

      // Generate static AsyncLoad uri method
      let args = [ ProvidedParameter("uri", typeof<string>) ]
      let m = ProvidedMethod("AsyncLoad", args, resultTypeAsync, IsStaticMethod = true)
      m.InvokeCode <- fun (Singleton uri) -> 
        <@ asyncReadTextAtRuntime isRunningInFSI defaultResolutionFolder resolutionFolder %%uri @>
        |> spec.AsyncCreateFromTextReader
      m.AddXmlDoc <| sprintf "Loads %s from the specified uri" formatName
      yield m
      
      if sampleIsList then

        let resultTypeArray = spec.RepresentationType.MakeArrayType()
        let resultTypeArrayAsync = typedefof<Async<_>>.MakeGenericType [| resultTypeArray |] |> replacer.ToRuntime

        // Generate static GetSamples method
        let m = ProvidedMethod("GetSamples", [], resultTypeArray, IsStaticMethod = true)
        m.InvokeCode <- fun _ -> 
          (if sampleIsUri 
           then <@ asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder sampleOrSampleUri
                   |> Async.RunSynchronously @>
           else <@ new StringReader(sampleOrSampleUri) :> TextReader @>)
          |> spec.CreateFromTextReaderForSampleList
        yield m 
                
        if sampleIsUri then
          // Generate static AsyncGetSamples method
          let m = ProvidedMethod("AsyncGetSamples", [], resultTypeArrayAsync, IsStaticMethod = true)
          m.InvokeCode <- fun _ -> 
            <@ asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder sampleOrSampleUri @>
            |> spec.AsyncCreateFromTextReaderForSampleList
          yield m

      else 

        let name = if resultType.IsArray then "GetSamples" else "GetSample"

        // Generate static GetSample method
        let m = ProvidedMethod(name, [], resultType, IsStaticMethod = true)
        m.InvokeCode <- fun _ -> 
          (if sampleIsUri 
           then <@ asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder sampleOrSampleUri
                   |> Async.RunSynchronously @>
           else <@ new StringReader(sampleOrSampleUri) :> TextReader @>)
          |> spec.CreateFromTextReader
        yield m 
        
        if sampleIsUri then
          // Generate static AsyncGetSample method
          let m = ProvidedMethod("Async" + name, [], resultTypeAsync, IsStaticMethod = true)
          m.InvokeCode <- fun _ -> 
            <@ asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder sampleOrSampleUri @>
            |> spec.AsyncCreateFromTextReader
          yield m
    ] |> spec.GeneratedType.AddMembers

    spec.GeneratedType

// ----------------------------------------------------------------------------------------------
// Conversions from string to various primitive types
// ----------------------------------------------------------------------------------------------

[<RequireQualifiedAccess>]
type TypeWrapper = None | Option | Nullable

/// Represents type information about primitive property (used mainly in the CSV provider)
/// This type captures the type, unit of measure and handling of missing values (if we
/// infer that the value may be missing, we can generate option<T> or nullable<T>)
type PrimitiveInferedProperty =
  { Name : string
    InferedType : Type
    RuntimeType : Type
    TypeWithMeasure : Type
    TypeWrapper : TypeWrapper }
  static member Create(name, typ, ?typWrapper, ?unit) =
    let runtimeTyp = 
      if typ = typeof<Bit> then typeof<bool>
      elif typ = typeof<Bit0> || typ = typeof<Bit1> then typeof<int>
      else typ
    let typWithMeasure =
      match unit with
      | None -> runtimeTyp
      | Some unit -> 
          if supportsUnitsOfMeasure runtimeTyp
          then ProvidedMeasureBuilder.Default.AnnotateType(runtimeTyp, [unit])
          else failwithf "Units of measure not supported by type %s" runtimeTyp.Name
    { Name = name
      InferedType = typ
      RuntimeType = runtimeTyp
      TypeWithMeasure = typWithMeasure
      TypeWrapper = defaultArg typWrapper TypeWrapper.None }
  static member Create(name, typ, optional) =
    PrimitiveInferedProperty.Create(name, typ, (if optional then TypeWrapper.Option else TypeWrapper.None), ?unit=None)

module Conversions = 

  open Microsoft.FSharp.Quotations
  open FSharp.Data.RuntimeImplementation
  open QuotationBuilder

  let getConversionQuotation (missingValues, culture) typ value =
    if typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then <@@ Operations.ConvertInteger(culture, %%value) @@>
    elif typ = typeof<int64> then <@@ Operations.ConvertInteger64(culture, %%value) @@>
    elif typ = typeof<decimal> then <@@ Operations.ConvertDecimal(culture, %%value) @@>
    elif typ = typeof<float> then <@@ Operations.ConvertFloat(culture, missingValues, %%value) @@>
    elif typ = typeof<string> then <@@ Operations.ConvertString(%%value) @@>
    elif typ = typeof<bool> || typ = typeof<Bit> then <@@ Operations.ConvertBoolean(culture, %%value) @@>
    elif typ = typeof<Guid> then <@@ Operations.ConvertGuid(%%value) @@>
    elif typ = typeof<DateTime> then <@@ Operations.ConvertDateTime(culture, %%value) @@>
    else failwith "getConversionQuotation: Unsupported primitive type"

  let getBackConversionQuotation (missingValues, culture) typ value =
    if typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then <@@ Operations.ConvertIntegerBack(culture, %%value) @@>
    elif typ = typeof<int64> then <@@ Operations.ConvertInteger64Back(culture, %%value) @@>
    elif typ = typeof<decimal> then <@@ Operations.ConvertDecimalBack(culture, %%value) @@>
    elif typ = typeof<float> then <@@ Operations.ConvertFloatBack(culture, missingValues, %%value) @@>
    elif typ = typeof<string> then <@@ Operations.ConvertStringBack(%%value) @@>
    elif typ = typeof<bool> then <@@ Operations.ConvertBooleanBack(culture, %%value, false) @@>
    elif typ = typeof<Bit> then <@@ Operations.ConvertBooleanBack(culture, %%value, true) @@>
    elif typ = typeof<Guid> then <@@ Operations.ConvertGuidBack(%%value) @@>
    elif typ = typeof<DateTime> then <@@ Operations.ConvertDateTimeBack(culture, %%value) @@>
    else failwith "getBackConversionQuotation: Unsupported primitive type"

  /// Creates a function that takes Expr<string option> and converts it to 
  /// an expression of other type - the type is specified by `field`
  let convertValue (replacer:AssemblyReplacer) config (field:PrimitiveInferedProperty) = 

    let returnTyp = 
      match field.TypeWrapper with
      | TypeWrapper.None -> field.TypeWithMeasure
      | TypeWrapper.Option -> typedefof<option<_>>.MakeGenericType [| field.TypeWithMeasure |]
      | TypeWrapper.Nullable -> typedefof<Nullable<_>>.MakeGenericType [| field.TypeWithMeasure |]

    let returnTypWithoutMeasure = 
      match field.TypeWrapper with
      | TypeWrapper.None -> field.RuntimeType
      | TypeWrapper.Option -> typedefof<option<_>>.MakeGenericType [| field.RuntimeType |]
      | TypeWrapper.Nullable -> typedefof<Nullable<_>>.MakeGenericType [| field.RuntimeType |]

    let typ = field.InferedType
    let runtimeTyp = field.RuntimeType

    let convert value =
      let converted = getConversionQuotation config typ value
      match field.TypeWrapper with
      | TypeWrapper.None -> typeof<Operations>?GetNonOptionalValue (runtimeTyp) (field.Name, converted, value)
      | TypeWrapper.Option -> converted
      | TypeWrapper.Nullable -> typeof<Operations>?OptionToNullable (runtimeTyp) converted
      |> replacer.ToRuntime

    let convertBack value = 
      let value = 
        match field.TypeWrapper with
        | TypeWrapper.None -> typeof<Operations>?GetOptionalValue (runtimeTyp) value
        | TypeWrapper.Option -> value
        | TypeWrapper.Nullable -> typeof<Operations>?NullableToOption (runtimeTyp) value
        |> replacer.ToDesignTime
      getBackConversionQuotation config typ value |> replacer.ToRuntime

    returnTyp, returnTypWithoutMeasure, convert, convertBack

