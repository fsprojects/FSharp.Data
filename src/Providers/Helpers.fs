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
  let readLinesInProvider cfg fileName = 
    seq {use textReader = new StreamReader(openStreamInProvider cfg fileName)
         while not textReader.EndOfStream do
             yield textReader.ReadLine()}

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


module Conversions = 
  open System
  open System.Globalization
  open Microsoft.FSharp.Quotations

  /// Convert the result of TryParse to option type
  let asOption = function true, v -> Some v | _ -> None

  type Operations =
    // Operations that convert string to supported primitive types
    static member ConvertString = Option.map (fun (s:string) -> s)
    static member ConvertDateTime = Option.bind (fun s -> 
      DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None) |> asOption)
    static member ConvertInteger = Option.bind (fun s -> 
      Int32.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture) |> asOption)
    static member ConvertInteger64 = Option.bind (fun s -> 
      Int64.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture) |> asOption)
    static member ConvertDecimal = Option.bind (fun s -> 
      Decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture) |> asOption)
    static member ConvertFloat = Option.bind (fun s -> 
      match s with
      | StringEquals "#N/A" -> Some Double.NaN
      | _ -> Double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture) |> asOption)
    static member ConvertBoolean = Option.bind (function 
        | StringEquals "true" | StringEquals "yes" -> Some true
        | StringEquals "false" | StringEquals "no" -> Some false
        | _ -> None)

    /// Operation that extracts the value from an option and reports a
    /// meaningful error message when the value is not there
    static member GetNonOptionalAttribute<'T>(name, opt:option<'T>) : 'T = 
      match opt with 
      | Some v -> v
      | None -> Unchecked.defaultof<'T>
      | _ -> failwithf "Mismatch: %s is missing" name

  /// Creates a function that takes Expr<string option> and converts it to 
  /// an expression of other type - the type is specified by `typ` and 
  let convertValue message optional typ = 
    let returnTyp = if optional then typedefof<option<_>>.MakeGenericType [| typ |] else typ
    returnTyp, fun e ->
      let converted = 
        if typ = typeof<int> then <@@ Operations.ConvertInteger(%%e) @@>
        elif typ = typeof<int64> then <@@ Operations.ConvertInteger64(%%e) @@>
        elif typ = typeof<decimal> then <@@ Operations.ConvertDecimal(%%e) @@>
        elif typ = typeof<float> then <@@ Operations.ConvertFloat(%%e) @@>
        elif typ = typeof<string> then <@@ Operations.ConvertString(%%e) @@>
        elif typ = typeof<bool> then <@@ Operations.ConvertBoolean(%%e) @@>
        elif typ = typeof<DateTime> then <@@ Operations.ConvertDateTime(%%e) @@>
        else failwith "convertValue: Unsupported primitive type"
      if not optional then 
        ReflectionHelpers.makeMethodCall typeof<Operations> "GetNonOptionalAttribute"
          [ typ ] [ Expr.Value message; converted]
      else converted
