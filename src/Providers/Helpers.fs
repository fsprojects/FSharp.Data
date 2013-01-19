// --------------------------------------------------------------------------------------
// Helpers for writing type providers
// ----------------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.IO
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes

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

  /// Take at most the specified number of arguments from the sequence
  let takeMax count input =
    input 
    |> Seq.mapi (fun i v -> i, v)
    |> Seq.takeWhile (fun (i, v) -> i < count)
    |> Seq.map snd

[<AutoOpen>]
module ActivePatterns =

  // Helper active patterns to simplify the inference code
  let (|Trim|) (s:string) = s.Trim()

  let (|StringEquals|_|) (s1:string) s2 = 
    if s1.Equals(s2, StringComparison.InvariantCultureIgnoreCase) 
      then Some () else None

  let (|Parse|_|) func value = 
    match func value with
    | true, v -> Some v
    | _ -> None

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
// Dynamic operator (?) that can be used for constructing quoted F# code without 
// quotations (to simplify constructing F# quotations in portable libraries - where
// we need to pass the System.Type of various types as arguments)
// ----------------------------------------------------------------------------------------------

module QuotationBuilder = 
  open System.Reflection
  open Microsoft.FSharp.Quotations
  open Microsoft.FSharp.Reflection

  let (?) (typ:System.Type) (operation:string) (args1:'T) : 'R = 
    // Arguments are either Expr or other type - in the second case,
    // we treat them as Expr.Value (which will only work for primitives)
    let convertValue (arg:obj) = 
      match arg with
      | :? Expr as e -> e
      | value -> Expr.Value(value, value.GetType())

    let invokeOperation (tyargs:obj, tyargsT) (args:obj, argsT) =
      // To support (e1, e2, ..) syntax, we use tuples - extract tuple arguments
      // First, extract type arguments - a list of System.Type values
      let tyargs = 
        if tyargsT = typeof<unit> then []
        elif FSharpType.IsTuple(tyargsT) then
          [ for f in FSharpValue.GetTupleFields(args) -> f :?> System.Type ]
        else [ tyargs :?> System.Type ]
      // Second, extract arguments (which are either Expr values or primitive constants)
      let args = 
        if argsT = typeof<unit> then []
        elif FSharpType.IsTuple(argsT) then
          [ for f in FSharpValue.GetTupleFields(args) -> convertValue f ]
        else [ convertValue args ]

      // Find a method that we want to call
      let flags = BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.Instance
      match typ.GetMember(operation, MemberTypes.All, flags) with 
      | [| :? MethodInfo as mi |] -> 
          let mi = 
            if tyargs = [] then mi
            else mi.MakeGenericMethod(tyargs |> Array.ofList)
          if mi.IsStatic then Expr.Call(mi, args)
          else Expr.Call(List.head args, mi, List.tail args)
      | [| :? ConstructorInfo as ci |] ->
          if tyargs <> [] then failwith "Constructor cannot be generic!"
          Expr.NewObject(ci, args)
      | options -> failwithf "Constructing call of the '%s' operation failed. Got %A" operation options

    // If the result is a function, we are called with two tuples as arguments
    // and the first tuple represents type arguments for a generic function...
    if FSharpType.IsFunction(typeof<'R>) then
      let domTyp, res = FSharpType.GetFunctionElements(typeof<'R>)
      if res <> typeof<Expr> then failwith "QuotationBuilder: The resulting type must be Expr!"
      FSharpValue.MakeFunction(typeof<'R>, fun args2 ->
        invokeOperation (args1, typeof<'T>) (args2, domTyp) |> box) |> unbox<'R>
    else invokeOperation ((), typeof<unit>) (args1, typeof<'T>) |> unbox<'R>


module internal ReflectionHelpers = 
  open Microsoft.FSharp.Quotations

  let makeFunc (exprfunc:Expr -> Expr) argType = 
    let var = Var.Global("t", argType)
    let convBody = exprfunc (Expr.Var var)
    convBody.Type, Expr.Lambda(var, convBody)
        
  let makeMethodCall (typ:Type) name tyargs args =
    let convMeth = typ.GetMethod(name)
    let convMeth = 
      if tyargs = [] then convMeth else
      convMeth.MakeGenericMethod (Array.ofSeq tyargs)
    Expr.Call(convMeth, args)

// ----------------------------------------------------------------------------------------------

module ProviderHelpers =

  /// If the file is web based, setup an file system watcher that 
  /// invalidates the generated type whenever the file changes
  ///
  /// Asumes that the fileName is a valid file name on the disk
  /// (and not e.g. a web reference)
  let private watchForChanges invalidate (fileName:string) = 
    let path = Path.GetDirectoryName(fileName)
    let name = Path.GetFileName(fileName)
    let watcher = new FileSystemWatcher(Filter = name, Path = path)
    watcher.Changed.Add(fun _ -> invalidate())
    watcher.EnableRaisingEvents <- true

  /// Resolve the absolute location of a file (or web URL) according to the rules
  /// used by standard F# type providers as described here:
  /// https://github.com/fsharp/fsharpx/issues/195#issuecomment-12141785
  ///
  ///  * if it is web resource, just return it
  ///  * if it is full path, just return it
  ///  * otherwise..
  ///
  ///    At design-time:
  ///      * if the user specified resolution folder, use that
  ///      * use the default resolution folder
  ///    At run-time:
  ///      * if the user specified resolution folder, use that
  ///      * if it is running in F# interactive (config.IsHostedExecution) 
  ///        use the default resolution folder
  ///      * otherwise, use 'CurrentDomain.BaseDirectory'
  ///
  /// Returns the resolved file name, together with a flag specifying 
  /// whether it is web based (and we need WebClient to download it)
  let private resolveFileLocation 
      designTime (isHosted, defaultResolutionFolder) resolutionFolder (fileName:string) =
    
    let isWeb =
      fileName.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) ||
      fileName.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase)

    match fileName with
    | url when isWeb -> url, true
    | fullPath when Path.IsPathRooted fullPath -> fullPath, false
    | relative ->
        let root = 
          if designTime then
            if not (String.IsNullOrEmpty(resolutionFolder)) then resolutionFolder
            else defaultResolutionFolder
          elif isHosted then defaultResolutionFolder
          else AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/')
        Path.Combine(root, relative), false

  /// Given a type provider configuration and a name passed by user, open 
  /// the file or URL (if it starts with http(s)) and return it as a stream
  let private asyncOpenStreamInProvider 
      designTime cfg invalidate resolutionFolder (fileName:string) = async {
    let resolvedFileOrUri, isWeb = resolveFileLocation designTime cfg resolutionFolder fileName

    // Open network stream or file stream
    if isWeb then
      let req = System.Net.WebRequest.Create(Uri(resolvedFileOrUri))
      let! resp = req.AsyncGetResponse() 
      return resp.GetResponseStream()
    else
      // Open the file, even if it is already opened by another application
      let file = File.Open(resolvedFileOrUri, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
      invalidate |> Option.iter (fun f -> watchForChanges f resolvedFileOrUri)
      return file :> Stream }

  /// Resolve a location of a file (or a web location) and open it for shared
  /// read, and trigger the specified function whenever the file changes
  let readTextAtDesignTime (cfg:TypeProviderConfig) invalidate resolutionFolder fileName = 
    let stream = 
      asyncOpenStreamInProvider true (false, cfg.ResolutionFolder) (Some invalidate) resolutionFolder fileName 
      |> Async.RunSynchronously
    new StreamReader(stream)

  /// Resolve a location of a file (or a web location) and open it for shared
  /// read at runtime (do not monitor file changes and use runtime resolution rules)
  let readTextAtRunTime isHosted defaultResolutionFolder resolutionFolder fileName = 
    let stream = 
      asyncOpenStreamInProvider false (isHosted, defaultResolutionFolder) None resolutionFolder fileName 
      |> Async.RunSynchronously
    new StreamReader(stream)

// ----------------------------------------------------------------------------------------------
// Conversions from string to various primitive types
// ----------------------------------------------------------------------------------------------

module Conversions = 
  open System
  open QuotationBuilder
  open System.Globalization
  open Microsoft.FSharp.Quotations

  /// Convert the result of TryParse to option type
  let asOption = function true, v -> Some v | _ -> None

  type Operations =
    /// Returns CultureInfor matching the specified culture string
    /// (or InvariantCulture if the argument is null or empty)
    static member GetCulture(culture) =
      if String.IsNullOrEmpty culture then CultureInfo.InvariantCulture else
      Globalization.CultureInfo(culture)

    // Operations that convert string to supported primitive types
    static member ConvertString str = Option.map (fun (s:string) -> s) str
    static member ConvertDateTime(culture:CultureInfo,text) = 
      Option.bind (fun s -> DateTime.TryParse(s, culture, DateTimeStyles.None) |> asOption) text
    static member ConvertInteger(culture:CultureInfo,text) = 
      Option.bind (fun s -> Int32.TryParse(s, NumberStyles.Any, culture) |> asOption) text
    static member ConvertInteger64(culture:CultureInfo,text) = 
      Option.bind (fun s -> Int64.TryParse(s, NumberStyles.Any, culture) |> asOption) text
    static member ConvertDecimal(culture:CultureInfo,text) =
      Option.bind (fun s -> Decimal.TryParse(s, NumberStyles.Any, culture) |> asOption) text
    static member ConvertFloat(culture:CultureInfo,text) = 
      Option.bind (fun (s:string) -> 
          match s.Trim() with
          | StringEquals "#N/A" -> Some Double.NaN
          | _ -> Double.TryParse(s, NumberStyles.Any, culture) |> asOption)
          text
    static member ConvertBoolean b = b |> Option.bind (fun (s:string) ->
        match s.Trim() with
        | StringEquals "true" | StringEquals "yes" -> Some true
        | StringEquals "false" | StringEquals "no" -> Some false
        | _ -> None)

    /// Operation that extracts the value from an option and reports a
    /// meaningful error message when the value is not there
    ///
    /// We could just return defaultof<'T> if the value is None, but that is not
    /// really correct, because this operation is used when the inference engine
    /// inferred that the value is always present. The user should update their
    /// sample to infer it as optional (and get None). If we use defaultof<'T> we
    /// might return 0 and the user would not be able to distinguish between 0
    /// and missing value.
    static member GetNonOptionalAttribute<'T>(name:string, opt:option<'T>) : 'T = 
      match opt with 
      | Some v -> v
      | None when typeof<'T> = typeof<string> -> Unchecked.defaultof<'T>
      | None when typeof<'T> = typeof<DateTime> -> Unchecked.defaultof<'T>
      | _ -> failwithf "Mismatch: %s is missing" name

  /// Creates a function that takes Expr<string option> and converts it to 
  /// an expression of other type - the type is specified by `typ` and 
  let convertValue (culture:string) (message:string) optional typ = 
    let returnTyp = if optional then typedefof<option<_>>.MakeGenericType [| typ |] else typ
    let operationsTyp = typeof<Operations>
    returnTyp, fun e ->
      let converted : Expr = 
        if typ = typeof<int> then operationsTyp?ConvertInteger(operationsTyp?GetCulture(culture),e)
        elif typ = typeof<int64> then operationsTyp?ConvertInteger64(operationsTyp?GetCulture(culture),e)
        elif typ = typeof<decimal> then operationsTyp?ConvertDecimal(operationsTyp?GetCulture(culture),e)
        elif typ = typeof<float> then operationsTyp?ConvertFloat(operationsTyp?GetCulture(culture),e)
        elif typ = typeof<string> then operationsTyp?ConvertString(e)
        elif typ = typeof<bool> then operationsTyp?ConvertBoolean(e)
        elif typ = typeof<DateTime> then operationsTyp?ConvertDateTime(operationsTyp?GetCulture(culture),e)
        else failwith "convertValue: Unsupported primitive type"
      if not optional then 
        operationsTyp?GetNonOptionalAttribute (typ) (message, converted)
      else converted
