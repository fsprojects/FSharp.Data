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

    let private (++) a b = Path.Combine(a,b)

    let private referenceAssembliesPath = 
        Environment.GetFolderPath Environment.SpecialFolder.ProgramFilesX86 
        ++ "Reference Assemblies" 
        ++ "Microsoft" 

    let private fsharpPortableAssembliesPath = 
        referenceAssembliesPath
        ++ "FSharp" 
        ++ "3.0" 
        ++ "Runtime" 
        ++ ".NETPortable"

    let private frameworkPortableAssembliesPath = 
        referenceAssembliesPath
        ++ "Framework" 
        ++ ".NETPortable" 
        ++ "v4.0" 
        ++ "Profile" 
        ++ "Profile47" 

    let private silverlightAssembliesPath = 
        referenceAssembliesPath
        ++ "Framework" 
        ++ "Silverlight" 
        ++ "v5.0" 

    let private silverlightSdkAssembliesPath = 
        Environment.GetFolderPath Environment.SpecialFolder.ProgramFilesX86 
        ++ "Microsoft SDKs" 
        ++ "Silverlight" 
        ++ "v5.0" 
        ++ "Libraries"
        ++ "Client"

    let private fullAssemblies = 
        AppDomain.CurrentDomain.GetAssemblies()
        |> Seq.map (fun asm -> asm.GetName().Name, asm)
        // If there are dups, Map.ofSeq will take the last one. When the portable version
        // is already loaded, it will be the last one and replace the full version on the
        // map. We don't want that, so we use distinct to only keep the first version of
        // each assembly (assumes CurrentDomain.GetAssemblies() returns assemblies in
        // load order, must check if that's also true for Mono)
        |> Seq.distinctBy fst 
        |> Map.ofSeq

    let private getAssembly (asmName:AssemblyName) reflectionOnly = 
        let folder = 
            let version = 
                if asmName.Version = null // version is null when trying to load the log4net assembly when running tests inside NUnit
                then "" else asmName.Version.ToString()
            match asmName.Name, version with
            | "FSharp.Core", "2.3.5.0" -> fsharpPortableAssembliesPath
            | "System.Xml.Linq", "5.0.5.0" -> silverlightSdkAssembliesPath
            | _, "5.0.5.0" -> silverlightAssembliesPath
            | _, "2.0.5.0" -> frameworkPortableAssembliesPath
            | _, _ -> null
        if folder = null then 
            null
        else
            let assemblyPath = folder ++ (asmName.Name + ".dll")
            if File.Exists assemblyPath then
                if reflectionOnly then Assembly.ReflectionOnlyLoadFrom assemblyPath
                else Assembly.LoadFrom assemblyPath 
            else null

    let mutable private initialized = false    

    let init (cfg : TypeProviderConfig) = 

        if not initialized then
            initialized <- true
            AppDomain.CurrentDomain.add_AssemblyResolve(fun _ args -> getAssembly (AssemblyName args.Name) false)
            AppDomain.CurrentDomain.add_ReflectionOnlyAssemblyResolve(fun _ args -> getAssembly (AssemblyName args.Name) true)
        
        let isPortable = cfg.SystemRuntimeAssemblyVersion = new Version(2, 0, 5, 0)
        let isSilverlight = cfg.SystemRuntimeAssemblyVersion = new Version(5, 0, 5, 0)
                
        let runtimeAssembly = 
            if isSilverlight then Assembly.ReflectionOnlyLoadFrom cfg.RuntimeAssembly
            else Assembly.LoadFrom cfg.RuntimeAssembly

        let runtimeAssemblyPair = Assembly.GetExecutingAssembly(), runtimeAssembly

        let asmMappings = 
            if isPortable || isSilverlight then
                let portableAsmsPairs = 
                    runtimeAssembly.GetReferencedAssemblies()
                    |> Seq.filter (fun asmName -> asmName.Name <> "mscorlib")
                    |> Seq.choose (fun asmName -> 
                        fullAssemblies.TryFind asmName.Name
                        |> Option.bind (fun fullAsm ->
                            // Ideally we would always use reflectionOnly as true, but that creates problems in Windows 8 apps with the System.Core.dll version
                            let portableAsm = getAssembly asmName isSilverlight
                            if portableAsm <> null && portableAsm.FullName <> fullAsm.FullName then Some (fullAsm, portableAsm)
                            else None))
                    |> Seq.toList
                if portableAsmsPairs = [] then
                    failwithf "Something went wrong when creating the assembly mappings"
                runtimeAssemblyPair::portableAsmsPairs
            else
                [runtimeAssemblyPair]

        runtimeAssembly, AssemblyReplacer.create asmMappings

#endif
