module ProviderImplementation.AssemblyResolver

open System
open System.IO
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices

let private (++) a b = Path.Combine(a,b)

let private referenceAssembliesPath = 
    Environment.GetFolderPath Environment.SpecialFolder.ProgramFilesX86 
    ++ "Reference Assemblies" 
    ++ "Microsoft" 

let private fsharp30Portable47AssembliesPath = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ "3.0" 
    ++ "Runtime" 
    ++ ".NETPortable"

let private fsharp31Portable7AssembliesPath = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ ".NETCore" 
    ++ "3.3.1.0" 

let private fsharp30AssembliesPath = 
    referenceAssembliesPath
   ++ "FSharp" 
    ++ ".NETFramework" 
    ++ "v4.0"
    ++ "4.3.0.0"
(*    ++ "FSharp" 
    ++ "3.0" 
    ++ "Runtime" 
    ++ "v4.0" *)

let private fsharp31AssembliesPath = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ ".NETFramework" 
    ++ "v4.0"
    ++ "4.3.1.0"

let private portable47AssembliesPath = 
    referenceAssembliesPath
    ++ "Framework" 
    ++ ".NETPortable" 
    ++ "v4.0" 
    ++ "Profile" 
    ++ "Profile47" 

let private designTimeAssemblies = 
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
        | "FSharp.Core", "4.3.0.0" -> fsharp30AssembliesPath
        | "FSharp.Core", "4.3.1.0" -> fsharp31AssembliesPath
        | "FSharp.Core", "2.3.5.0" -> fsharp30Portable47AssembliesPath
        | "FSharp.Core", "3.3.1.0" -> fsharp31Portable7AssembliesPath
        | _, "2.0.5.0" -> portable47AssembliesPath
        | _, _ -> null
    if folder = null then 
        if reflectionOnly then Assembly.ReflectionOnlyLoad asmName.FullName
        else null
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
    
    let isPortable47 = cfg.SystemRuntimeAssemblyVersion = Version(2, 0, 5, 0)
    //portable7 has SystemRuntimeAssemblyVersion = 4.0.0.0, so we can't detect it, but it's only supported in F# 3.1, so there's no problem
    let isFSharp31 = typedefof<option<_>>.Assembly.GetName().Version = Version(4, 3, 1, 0)

    let differentFramework = isPortable47 || isFSharp31
    let useReflectionOnly = differentFramework

    let runtimeAssembly = 
        if useReflectionOnly then Assembly.ReflectionOnlyLoadFrom cfg.RuntimeAssembly
        else Assembly.LoadFrom cfg.RuntimeAssembly

    let mainRuntimeAssemblyPair = Assembly.GetExecutingAssembly(), runtimeAssembly

    let asmMappings = 
        if differentFramework then
            let runtimeAsmsPairs = 
                runtimeAssembly.GetReferencedAssemblies()
                |> Seq.filter (fun asmName -> asmName.Name <> "mscorlib")
                |> Seq.choose (fun asmName -> 
                    designTimeAssemblies.TryFind asmName.Name
                    |> Option.bind (fun designTimeAsm ->
                        let targetAsm = getAssembly asmName useReflectionOnly
                        if targetAsm <> null && (targetAsm.FullName <> designTimeAsm.FullName ||
                                                 targetAsm.ReflectionOnly <> designTimeAsm.ReflectionOnly) then 
                          Some (designTimeAsm, targetAsm)
                        else None))
                |> Seq.toList
            if runtimeAsmsPairs = [] then
                failwithf "Something went wrong when creating the assembly mappings"
            mainRuntimeAssemblyPair::runtimeAsmsPairs
        else
            [mainRuntimeAssemblyPair]

    runtimeAssembly, AssemblyReplacer.create asmMappings
