module ProviderImplementation.AssemblyResolver

open System
open System.IO
open System.Net
open System.Reflection
open System.Xml.Linq
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation

let runningOnMono = Type.GetType("Mono.Runtime") <> null

let private (++) a b = Path.Combine(a,b)

let private referenceAssembliesPath = 
    if runningOnMono
    then "/Library/Frameworks/Mono.framework/Versions/CurrentVersion/lib/mono/"
    else Environment.GetFolderPath Environment.SpecialFolder.ProgramFilesX86 
    ++ "Reference Assemblies" 
    ++ "Microsoft" 

let private fsharp30Portable47AssembliesPath1 = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ "3.0" 
    ++ "Runtime" 
    ++ ".NETPortable"

let private fsharp30Portable47AssembliesPath2 = 
     referenceAssembliesPath
     ++ "FSharp" 
     ++ ".NETPortable" 
     ++ "2.3.5.0"

let private fsharp31Portable47AssembliesPath = 
     referenceAssembliesPath
     ++ "FSharp" 
     ++ ".NETPortable" 
     ++ "2.3.5.1"

let private fsharp31Portable7AssembliesPath = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ ".NETCore" 
    ++ "3.3.1.0" 

let private fsharp30AssembliesPath1 = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ "3.0" 
    ++ "Runtime" 
    ++ "v4.0"

let private fsharp30AssembliesPath2 = 
    referenceAssembliesPath
    ++ "FSharp" 
    ++ ".NETFramework" 
    ++ "v4.0"
    ++ "4.3.0.0"

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

let private safeFileExists path = 
    try File.Exists path with _ -> false

let private nullToOption x = match x with null -> None | _ -> Some x

let private getAssembly (asmName:AssemblyName) reflectionOnly fsharpDataPaths = 
    let folders = 
        let version = 
            if asmName.Version = null // version is null when trying to load the log4net assembly when running tests inside NUnit
            then "" else asmName.Version.ToString()
        match asmName.Name, version with
        | "FSharp.Core", "4.3.0.0" -> [fsharp30AssembliesPath1; fsharp30AssembliesPath2]
        | "FSharp.Core", "4.3.1.0" -> [fsharp31AssembliesPath]
        | "FSharp.Core", "2.3.5.0" -> [fsharp30Portable47AssembliesPath1; fsharp30Portable47AssembliesPath2]
        | "FSharp.Core", "2.3.5.1" -> [fsharp31Portable47AssembliesPath]
        | "FSharp.Core", "3.3.1.0" -> [fsharp31Portable7AssembliesPath]
        | "FSharp.Data", _ -> fsharpDataPaths
        | _, "2.0.5.0" -> [portable47AssembliesPath]
        | _, _ -> []
    let asm = 
        folders |> List.tryPick (fun folder -> 
          try
            let assemblyPath = folder ++ (asmName.Name + ".dll")
            if safeFileExists assemblyPath then
                if reflectionOnly then nullToOption (Assembly.ReflectionOnlyLoadFrom assemblyPath)
                else nullToOption (Assembly.LoadFrom assemblyPath)
            else None 
          with _ -> None)
    match asm with 
    | Some asm -> asm
    | None -> 
        if reflectionOnly then 
          let redirectedName = AppDomain.CurrentDomain.ApplyPolicy(asmName.FullName)
          Assembly.ReflectionOnlyLoad redirectedName
        else null

let mutable private initialized = false    

[<RequireQualifiedAccess>]
type FSharpDataRuntimeVersion =
    | Net40
    | Portable7 //net45+win8
    | Portable47 //net45+wp8+win8
    member x.SupportsLocalFileSystem = 
        match x with
        | Net40 -> true
        | _ -> false

let init (cfg : TypeProviderConfig) = 

    let init (_:SaveOptions) = 
        WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials
        AppDomain.CurrentDomain.add_AssemblyResolve(fun _ args -> getAssembly (AssemblyName args.Name) false [])
        AppDomain.CurrentDomain.add_ReflectionOnlyAssemblyResolve(fun _ args -> getAssembly (AssemblyName args.Name) true [])
        ProvidedTypes.ProvidedTypeDefinition.Logger := Some FSharp.Data.Runtime.IO.log

    if not initialized then
        initialized <- true
        // the following parameter is just here to force System.Xml.Linq to load
        init SaveOptions.None
    
    let useReflectionOnly = true

    let runtimeAssembly = 
        if useReflectionOnly then Assembly.ReflectionOnlyLoadFrom cfg.RuntimeAssembly
        else Assembly.LoadFrom cfg.RuntimeAssembly

    let runtimeAssemblyName = runtimeAssembly.GetName()

    let runtimeVersion =
        match runtimeAssemblyName.Version.Revision with
        | 0 -> FSharpDataRuntimeVersion.Net40
        | 7 -> FSharpDataRuntimeVersion.Portable7
        | 47 -> FSharpDataRuntimeVersion.Portable47
        | _ -> failwithf "Unexpected version of %s.dll: %O [Looking for revision 0, 7 or 47]" runtimeAssemblyName.Name runtimeAssemblyName.Version

    let runtimeAssemblyPair = Assembly.GetCallingAssembly(), runtimeAssembly

    let referencedAssembliesPairs = 
        runtimeAssembly.GetReferencedAssemblies()
        |> Seq.filter (fun asmName -> asmName.Name <> "mscorlib")
        |> Seq.choose (fun asmName -> 
            let designTimeAsmName =
                match asmName.Name with
                | "FSharp.Data" -> "FSharp.Data.DesignTime" // this applies when this code is being used by another assembly that depends on FSharp.Data, like ApiaryProvider
                | "System.Runtime" | "System.IO" | "System.Threading.Tasks" -> "mscorlib"
                | "System.Xml.XDocument" -> "System.Xml.Linq"
                | asmName -> asmName
            designTimeAssemblies.TryFind designTimeAsmName
            |> Option.bind (fun designTimeAsm ->
                let targetAsm = 
                    if asmName.Name = "FSharp.Data" then                        
                        let runtimeAssemblyPath = Path.GetDirectoryName runtimeAssembly.Location
                        let fsharpDataPaths = [ runtimeAssemblyPath
                                                runtimeAssemblyPath.Replace(sprintf "%s.%d.%d.%d" runtimeAssemblyName.Name 
                                                                                                  runtimeAssemblyName.Version.Major 
                                                                                                  runtimeAssemblyName.Version.Minor 
                                                                                                  runtimeAssemblyName.Version.Build,
                                                                            sprintf "FSharp.Data.%d.%d.%d" asmName.Version.Major
                                                                                                           asmName.Version.Minor
                                                                                                           asmName.Version.Build) ]
                        // for cases when there is a version mismatch
                        let alternativeFsharpDataPaths = 
                            let mutable packagesFolder = runtimeAssemblyPath
                            let suffixes = ResizeArray<_>()
                            while packagesFolder <> null && not ("packages".Equals(DirectoryInfo(packagesFolder).Name, StringComparison.OrdinalIgnoreCase)) do
                                suffixes.Add (DirectoryInfo packagesFolder).Name
                                let parent = Directory.GetParent packagesFolder
                                packagesFolder <- if parent = null then null else parent.FullName
                            if packagesFolder = null then [] 
                            else
                                let suffix = 
                                    suffixes 
                                    |> Seq.truncate (suffixes.Count - 1)
                                    |> Seq.toArray
                                    |> Array.rev 
                                    |> String.concat (string Path.DirectorySeparatorChar)
                                Directory.GetDirectories(packagesFolder, "FSharp.Data.*")
                                |> Array.sort 
                                |> Array.rev
                                |> Array.map (fun fsharpDataFolder -> Path.Combine(fsharpDataFolder, suffix))
                                |> Array.toList
                        getAssembly asmName useReflectionOnly (fsharpDataPaths @ alternativeFsharpDataPaths)
                    else
                        getAssembly asmName useReflectionOnly []
                if targetAsm <> null && (targetAsm.FullName <> designTimeAsm.FullName || targetAsm.ReflectionOnly <> designTimeAsm.ReflectionOnly) then 
                    Some (designTimeAsm, targetAsm)
                else None))
        |> Seq.toList
    
    runtimeAssembly, runtimeVersion, AssemblyReplacer.create (runtimeAssemblyPair::referencedAssembliesPairs)