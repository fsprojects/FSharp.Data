// --------------------------------------------------------------------------------------
// Helpers for writing type providers
// ----------------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.IO
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes

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

  /// Helper active pattern that can be used when constructing InvokeCode
  /// (to avoid writing pattern matching or incomplete matches):
  ///
  ///    p.InvokeCode <- fun (Singleton self) -> <@ 1 + 2 @>
  ///
  let (|Singleton|) = function [l] -> l | _ -> failwith "Parameter mismatch"