// --------------------------------------------------------------------------------------
// Helpers for writing type providers
// ----------------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.IO
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes

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

    let headAndTail l = match l with [] -> invalidArg "l" "empty list" | h::t -> (h,t)

    let frontAndBack l = 
        let rec loop acc l = 
            match l with
            | [] -> invalidArg "l" "empty list" 
            | [h] -> List.rev acc,h
            | h::t -> loop  (h::acc) t
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
  open FSharp.Data.RuntimeImplementation.ProviderFileSystem

  /// Resolve a location of a file (or a web location) and open it for shared
  /// read, and trigger the specified function whenever the file changes
  let readTextAtDesignTime defaultResolutionFolder invalidate resolutionFolder uri = 
    let stream = 
      asyncOpenStreamInProvider true (false, defaultResolutionFolder) (Some invalidate) resolutionFolder uri
      |> Async.RunSynchronously
    new StreamReader(stream)

  let invalidChars = Array.append (Path.GetInvalidPathChars()) (@"{}[],".ToCharArray()) |> set

  let tryGetUri str =
    match Uri.TryCreate(str, UriKind.RelativeOrAbsolute) with
    | false, _ -> None
    | true, uri ->
        if not uri.IsAbsoluteUri && (str |> Seq.exists (fun c -> invalidChars.Contains c)) then
            None
        else
            Some uri

// ----------------------------------------------------------------------------------------------
// Conversions from string to various primitive types
// ----------------------------------------------------------------------------------------------

[<RequireQualifiedAccess>]
type TypeWrapper = None | Option | Nullable

module Conversions = 

  open Microsoft.FSharp.Quotations
  open FSharp.Data.RuntimeImplementation
  open QuotationBuilder

  /// Creates a function that takes Expr<string option> and converts it to 
  /// an expression of other type - the type is specified by `typ` and 
  let convertValue (culture:string) (fieldName:string) typeWrapper (typ, typWithMeasure) (replacer:AssemblyReplacer) = 

    let returnTyp = 
        match typeWrapper with
        | TypeWrapper.None -> typWithMeasure
        | TypeWrapper.Option -> typedefof<option<_>>.MakeGenericType [| typWithMeasure |]
        | TypeWrapper.Nullable -> typedefof<Nullable<_>>.MakeGenericType [| typWithMeasure |]

    returnTyp, fun e ->
      let converted = 
        if typ = typeof<int> then <@@ Operations.ConvertInteger(culture, %%e) @@>
        elif typ = typeof<int64> then <@@ Operations.ConvertInteger64(culture, %%e) @@>
        elif typ = typeof<decimal> then <@@ Operations.ConvertDecimal(culture, %%e) @@>
        elif typ = typeof<float> then <@@ Operations.ConvertFloat(culture, %%e) @@>
        elif typ = typeof<string> then <@@ Operations.ConvertString(%%e) @@>
        elif typ = typeof<bool> then <@@ Operations.ConvertBoolean(culture, %%e) @@>
        elif typ = typeof<DateTime> then <@@ Operations.ConvertDateTime(culture, %%e) @@>
        else failwith "convertValue: Unsupported primitive type"
      match typeWrapper with
      | TypeWrapper.None -> typeof<Operations>?GetNonOptionalValue (typ) (fieldName, converted)
      | TypeWrapper.Option -> converted
      | TypeWrapper.Nullable -> typeof<Operations>?ToNullable (typ) converted
      |> replacer.ToRuntime

// ----------------------------------------------------------------------------------------------

module AssemblyResolver =

#if SILVERLIGHT

    let onUiThread f = 
        if System.Windows.Deployment.Current.Dispatcher.CheckAccess() then 
            f() 
        else
            let resultTask = System.Threading.Tasks.TaskCompletionSource<'T>()
            System.Windows.Deployment.Current.Dispatcher.BeginInvoke(Action(fun () -> try resultTask.SetResult (f()) with err -> resultTask.SetException err)) |> ignore
            resultTask.Task.Result

    let init (cfg : TypeProviderConfig) = 

        let runtimeAssembly = 
            onUiThread (fun () ->
                let assemblyPart = System.Windows.AssemblyPart()
                let FileStreamReadShim(fileName) = 
                    match System.Windows.Application.GetResourceStream(System.Uri(fileName,System.UriKind.Relative)) with 
                    | null -> System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForApplication().OpenFile(fileName, System.IO.FileMode.Open) :> System.IO.Stream 
                    | resStream -> resStream.Stream
                let assemblyStream = FileStreamReadShim cfg.RuntimeAssembly
            
                assemblyPart.Load(assemblyStream))

        runtimeAssembly, AssemblyReplacer.create []

#else

    open System.Reflection
    open System.Runtime.Versioning

    let private (++) a b = Path.Combine(a,b)
    let private portableFSharpCorePath = 
        Environment.GetFolderPath Environment.SpecialFolder.ProgramFilesX86 
        ++ "Reference Assemblies" 
        ++ "Microsoft" 
        ++ "FSharp" 
        ++ "3.0" 
        ++ "Runtime" 
        ++ ".NETPortable" 
        ++ "FSharp.Core.dll"

    let private assemblyResolveHandler = ResolveEventHandler(fun _ args ->
        try 
            let assemName = AssemblyName(args.Name)
            if assemName.Name = "FSharp.Core" && assemName.Version.ToString() = "2.3.5.0" then 
                Assembly.LoadFrom portableFSharpCorePath
            else 
                null
        with e ->  
            null)

    let mutable initialized = false    

    let init (cfg : TypeProviderConfig) = 

        if not initialized then
            initialized <- true
            AppDomain.CurrentDomain.add_AssemblyResolve(assemblyResolveHandler)

        let runtimeAssembly = Assembly.LoadFrom(cfg.RuntimeAssembly)

        let targetFrameworkAttr = runtimeAssembly.GetCustomAttribute<TargetFrameworkAttribute>()
        let isPortable = targetFrameworkAttr.FrameworkName = ".NETPortable,Version=v4.0,Profile=Profile47"

        let asmMappings = [Assembly.GetExecutingAssembly(), runtimeAssembly]
        let asmMappings = 
            if isPortable then
                let fullFSharpCore = typedefof<int list>.Assembly
                let portableFSharpCore = Assembly.LoadFrom portableFSharpCorePath
                (fullFSharpCore, portableFSharpCore)::asmMappings
            else
                asmMappings

        runtimeAssembly, AssemblyReplacer.create asmMappings
#endif
