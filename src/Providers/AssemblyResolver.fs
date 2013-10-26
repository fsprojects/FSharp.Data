module ProviderImplementation.AssemblyResolver

open System
open System.IO
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices

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

let private (++) a b = Path.Combine(a,b)

let private referenceAssembliesPath = 
    Environment.GetFolderPath Environment.SpecialFolder.ProgramFilesX86 
    ++ "Reference Assemblies" 
    ++ "Microsoft" 

let private fsharp30PortableAssembliesPath = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ "3.0" 
    ++ "Runtime" 
    ++ ".NETPortable"

let private fsharp30Net40AssembliesPath = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ "3.0" 
    ++ "Runtime" 
    ++ "v4.0"

let private net40AssembliesPath = 
    referenceAssembliesPath
    ++ "Framework" 
    ++ ".NETFramework" 
    ++ "v4.0" 

let private portable40AssembliesPath = 
    referenceAssembliesPath
    ++ "Framework" 
    ++ ".NETPortable" 
    ++ "v4.0" 
    ++ "Profile" 
    ++ "Profile47" 

let private silverlight5AssembliesPath = 
    referenceAssembliesPath
    ++ "Framework" 
    ++ "Silverlight" 
    ++ "v5.0" 

let private silverlight5SdkAssembliesPath = 
    Environment.GetFolderPath Environment.SpecialFolder.ProgramFilesX86 
    ++ "Microsoft SDKs" 
    ++ "Silverlight" 
    ++ "v5.0" 
    ++ "Libraries"
    ++ "Client"

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
        | "FSharp.Core", "4.3.0.0" -> fsharp30Net40AssembliesPath
        | "FSharp.Core", "2.3.5.0" -> fsharp30PortableAssembliesPath
        | _, "4.0.0.0" -> net40AssembliesPath
        | _, "2.0.5.0" -> portable40AssembliesPath
        | "System.Xml.Linq", "5.0.5.0" -> silverlight5SdkAssembliesPath
        | _, "5.0.5.0" -> silverlight5AssembliesPath
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
    
    let isPortable = cfg.SystemRuntimeAssemblyVersion = Version(2, 0, 5, 0)
    let isSilverlight = cfg.SystemRuntimeAssemblyVersion = Version(5, 0, 5, 0)
    let isFSharp31 = typedefof<option<_>>.Assembly.GetName().Version = Version(4, 3, 1, 0)

    let differentFramework = isPortable || isSilverlight || isFSharp31
    let useReflectionOnly =
        differentFramework &&
        // Ideally we would always use reflectionOnly, but that creates problems in Windows 8
        // apps with the System.Core.dll version, so we set to false for portable
        (not isPortable || isFSharp31)

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

#endif
