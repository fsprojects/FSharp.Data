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
      match typ.GetMember(operation) with 
      | [| :? MethodInfo as mi |] -> 
          let mi = 
            if tyargs = [] then mi
            else mi.MakeGenericMethod(tyargs |> Array.ofList)
          if mi.IsStatic then Expr.Call(mi, args)
          else Expr.Call(List.head args, mi, List.tail args)
      | _ -> failwithf "Constructing call of the '%s' operation failed." operation

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

module internal ProviderHelpers =

  /// Given a type provider configuration and a name passed by user, open 
  /// the file or URL (if it starts with http(s)) and return it as a stream
  let asyncOpenStreamInProvider (cfg:TypeProviderConfig) (fileName:string) = async {

    // Resolve the full path or full HTTP address
    let isWeb =
      fileName.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) ||
      fileName.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase)
    let resolvedFileOrUri = 
      if isWeb then fileName
      else Path.Combine(cfg.ResolutionFolder, fileName)

    // Open network stream or file stream
    if isWeb then
      let req = System.Net.WebRequest.Create(Uri(resolvedFileOrUri))
      let! resp = req.AsyncGetResponse() 
      return resp.GetResponseStream()
    else
      return File.OpenRead(resolvedFileOrUri) :> Stream }

  /// Given a type provider configuration and a name passed by user, open 
  /// the file or URL (if it starts with http(s)) and return it as a stream
  let openStreamInProvider (cfg:TypeProviderConfig) (fileName:string) = 
    asyncOpenStreamInProvider cfg fileName |> Async.RunSynchronously

  /// Read a file passed to a type provider into a string
  /// (if the file is needed to perform some inference)
  let readFileInProvider cfg fileName = 
    use stream = openStreamInProvider cfg fileName
    use reader = new StreamReader(stream)
    reader.ReadToEnd()

  /// Read a file passed to a type provider into a seq of strings
  /// (if the file is needed to perform some inference)
  let readTextInProvider cfg fileName = 
    new StreamReader(openStreamInProvider cfg fileName)

  /// Resolves the config filename
  let findConfigFile resolutionFolder configFileName =
    if Path.IsPathRooted configFileName then configFileName else 
    Path.Combine(resolutionFolder, configFileName)

  /// If the file is web based, setup an file system watcher that 
  /// invalidates the generated type whenever the file changes
  let watchForChanges (ownerType:TypeProviderForNamespaces) (fileName:string) = 
    let isWeb =
      fileName.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) ||
      fileName.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase)

    if not isWeb then
      let path = Path.GetDirectoryName(fileName)
      let name = Path.GetFileName(fileName)
      let watcher = new FileSystemWatcher(Filter = name, Path = path)
      watcher.Changed.Add(fun _ -> ownerType.Invalidate())
      watcher.EnableRaisingEvents <- true


[<AutoOpen>]
module GlobalProviderHelpers =

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
    static member ConvertBoolean = Option.bind (fun (s:string) ->
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
