namespace ProviderImplementation

open System
open System.IO
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProvidedTypesTesting
open FSharp.Data.Runtime

type CsvProviderArgs =
    { Sample : string
      Separators : string
      InferRows : int
      Schema : string
      HasHeaders : bool
      IgnoreErrors : bool
      SkipRows : int
      AssumeMissingValues : bool
      PreferOptionals : bool
      Quote : char
      MissingValues : string
      CacheRows : bool
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string }

type XmlProviderArgs =
    { Sample : string
      SampleIsList : bool
      Global : bool
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string
      InferTypesFromValues : bool }

type JsonProviderArgs =
    { Sample : string
      SampleIsList : bool
      RootName : string
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string
      InferTypesFromValues : bool }

type HtmlProviderArgs =
    { Sample : string
      PreferOptionals : bool
      IncludeLayoutTables : bool
      MissingValues : string
      Culture : string
      Encoding : string
      ResolutionFolder : string
      EmbeddedResource : string }

type WorldBankProviderArgs =
    { Sources : string
      Asynchronous : bool }

type Platform = Net40 | Portable7 | Portable47 | Portable259

module private RuntimeAssemblies =

    let (++) a b = Path.Combine(a, b)

    let runningOnMono = Type.GetType("Mono.Runtime") <> null
    let osxMonoRoot = "/Library/Frameworks/Mono.framework/Versions/Current/lib/mono"
    let linuxMonoRoot = "/usr/lib/mono"

    let monoRoot =
        match System.Environment.OSVersion.Platform with
        // /usr/bin/osascript is the applescript interpreter for osx
        | PlatformID.MacOSX | PlatformID.Unix when System.IO.File.Exists "/usr/bin/osascript"-> osxMonoRoot
        | PlatformID.Unix -> linuxMonoRoot
        | _ -> ""

    let referenceAssembliesPath =
        (if runningOnMono then monoRoot else Environment.GetFolderPath Environment.SpecialFolder.ProgramFilesX86)
        ++ "Reference Assemblies"
        ++ "Microsoft"

    let fsharp31PortableAssembliesPath profile =
         match profile with
         | 47 -> referenceAssembliesPath ++ "FSharp" ++ ".NETPortable" ++ "2.3.5.1" ++ "FSharp.Core.dll"
         | 7 -> referenceAssembliesPath ++ "FSharp" ++ ".NETCore" ++ "3.3.1.0" ++ "FSharp.Core.dll"
         | 259 -> referenceAssembliesPath ++ "FSharp" ++ ".NETCore" ++ "3.259.3.1" ++ "FSharp.Core.dll"
         | _ -> failwith "unimplemented portable profile"

    let fsharp31AssembliesPath =
        if runningOnMono then monoRoot ++ "gac" ++ "FSharp.Core" ++ "4.3.1.0__b03f5f7f11d50a3a"
        else referenceAssembliesPath ++ "FSharp" ++ ".NETFramework" ++ "v4.0" ++ "4.3.1.0"

    let net45AssembliesPath =
        if runningOnMono then monoRoot ++ "4.5"
        else referenceAssembliesPath ++ "Framework" ++ ".NETFramework" ++ "v4.5"

    let portableAssembliesPath profile =
        let portableRoot = if runningOnMono then monoRoot ++ "xbuild-frameworks" else referenceAssembliesPath ++ "Framework"
        match profile with
        | 47 -> portableRoot ++ ".NETPortable" ++ "v4.0" ++ "Profile" ++ "Profile47"
        | 7 | 259 -> portableRoot ++ ".NETPortable" ++ "v4.5" ++ "Profile" ++ (sprintf "Profile%d" profile)
        | _ -> failwith "unimplemented portable profile"

    let net40FSharp31Refs = [net45AssembliesPath ++ "mscorlib.dll"; net45AssembliesPath ++ "System.Xml.dll"; net45AssembliesPath ++ "System.Core.dll"; net45AssembliesPath ++ "System.Xml.Linq.dll"; net45AssembliesPath ++ "System.dll"; fsharp31AssembliesPath ++ "FSharp.Core.dll"]
    let portable47FSharp31Refs = [portableAssembliesPath 47 ++ "mscorlib.dll"; portableAssembliesPath 47 ++ "System.Xml.Linq.dll"; fsharp31PortableAssembliesPath 47]

    let portableCoreFSharp31Refs profile =
        [ for asm in [ "System.Runtime"; "mscorlib"; "System.Collections"; "System.Core"; "System"; "System.Globalization"; "System.IO"; "System.Linq"; "System.Linq.Expressions";
                       "System.Linq.Queryable"; "System.Net"; "System.Net.NetworkInformation"; "System.Net.Primitives"; "System.Net.Requests"; "System.ObjectModel"; "System.Reflection";
                       "System.Reflection.Extensions"; "System.Reflection.Primitives"; "System.Resources.ResourceManager"; "System.Runtime.Extensions";
                       "System.Runtime.InteropServices.WindowsRuntime"; "System.Runtime.Serialization"; "System.Threading"; "System.Threading.Tasks"; "System.Xml"; "System.Xml.Linq"; "System.Xml.XDocument";
                       "System.Runtime.Serialization.Json"; "System.Runtime.Serialization.Primitives"; "System.Windows" ] do
             yield portableAssembliesPath profile ++ asm + ".dll"
          yield fsharp31PortableAssembliesPath profile ]

type TypeProviderInstantiation =
    | Csv of CsvProviderArgs
    | Xml of XmlProviderArgs
    | Json of JsonProviderArgs
    | Html of HtmlProviderArgs
    | WorldBank of WorldBankProviderArgs

    member x.GenerateType resolutionFolder runtimeAssembly runtimeAssemblyRefs =
        let f, args =
            match x with
            | Csv x ->
                (fun cfg -> new CsvProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.Separators
                   box x.InferRows
                   box x.Schema
                   box x.HasHeaders
                   box x.IgnoreErrors
                   box x.SkipRows
                   box x.AssumeMissingValues
                   box x.PreferOptionals
                   box x.Quote
                   box x.MissingValues
                   box x.CacheRows
                   box x.Culture
                   box x.Encoding
                   box x.ResolutionFolder
                   box x.EmbeddedResource |]
            | Xml x ->
                (fun cfg -> new XmlProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.SampleIsList
                   box x.Global
                   box x.Culture
                   box x.Encoding
                   box x.ResolutionFolder
                   box x.EmbeddedResource
                   box x.InferTypesFromValues |]
            | Json x ->
                (fun cfg -> new JsonProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.SampleIsList
                   box x.RootName
                   box x.Culture
                   box x.Encoding
                   box x.ResolutionFolder
                   box x.EmbeddedResource
                   box x.InferTypesFromValues |]
            | Html x ->
                (fun cfg -> new HtmlProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sample
                   box x.PreferOptionals
                   box x.IncludeLayoutTables
                   box x.MissingValues
                   box x.Culture
                   box x.Encoding
                   box x.ResolutionFolder
                   box x.EmbeddedResource |]
            | WorldBank x ->
                (fun cfg -> new WorldBankProvider(cfg) :> TypeProviderForNamespaces),
                [| box x.Sources
                   box x.Asynchronous |]

        Testing.GenerateProvidedTypeInstantiation(resolutionFolder, runtimeAssembly, runtimeAssemblyRefs, f, args)

    override x.ToString() =
        match x with
        | Csv x ->
            ["Csv"
             x.Sample
             x.Separators
             x.Schema.Replace(',', ';')
             x.HasHeaders.ToString()
             x.AssumeMissingValues.ToString()
             x.PreferOptionals.ToString()
             x.MissingValues
             x.Culture
             x.Encoding ]
        | Xml x ->
            ["Xml"
             x.Sample
             x.SampleIsList.ToString()
             x.Global.ToString()
             x.Culture
             x.InferTypesFromValues.ToString() ]
        | Json x ->
            ["Json"
             x.Sample
             x.SampleIsList.ToString()
             x.RootName
             x.Culture
             x.InferTypesFromValues.ToString() ]
        | Html x ->
            ["Html"
             x.Sample
             x.PreferOptionals.ToString()
             x.IncludeLayoutTables.ToString()
             x.Culture]
        | WorldBank x ->
            ["WorldBank"
             x.Sources
             x.Asynchronous.ToString()]
        |> String.concat ","

    member x.ExpectedPath outputFolder =
        Path.Combine(outputFolder, (x.ToString().Replace(">", "&gt;").Replace("<", "&lt;").Replace("://", "_").Replace("/", "_") + ".expected"))

    member x.Dump (resolutionFolder, outputFolder, runtimeAssembly, runtimeAssemblyRefs, signatureOnly, ignoreOutput) =
        let replace (oldValue:string) (newValue:string) (str:string) = str.Replace(oldValue, newValue)
        let output =
            let tp = x.GenerateType resolutionFolder runtimeAssembly runtimeAssemblyRefs
            Testing.FormatProvidedType(tp, signatureOnly, ignoreOutput, 10, 100)
            |> replace "FSharp.Data.Runtime." "FDR."
            |> replace resolutionFolder "<RESOLUTION_FOLDER>"
            |> replace "@\"<RESOLUTION_FOLDER>\"" "\"<RESOLUTION_FOLDER>\""
        if outputFolder <> "" then
            File.WriteAllText(x.ExpectedPath outputFolder, output)
        output

    static member Parse (line:string) =
        let args = line.Split [|','|]
        args.[0],
        match args.[0] with
        | "Csv" ->
            Csv { Sample = args.[1]
                  Separators = args.[2]
                  InferRows = Int32.MaxValue
                  Schema = args.[3].Replace(';', ',')
                  HasHeaders = args.[4] |> bool.Parse
                  IgnoreErrors = false
                  SkipRows = 0
                  AssumeMissingValues = args.[5] |> bool.Parse
                  PreferOptionals = args.[6] |> bool.Parse
                  Quote = '"'
                  MissingValues = args.[7]
                  Culture = args.[8]
                  Encoding = args.[9]
                  CacheRows = false
                  ResolutionFolder = ""
                  EmbeddedResource = "" }
        | "Xml" ->
            Xml { Sample = args.[1]
                  SampleIsList = args.[2] |> bool.Parse
                  Global = args.[3] |> bool.Parse
                  Culture = args.[4]
                  Encoding = ""
                  ResolutionFolder = ""
                  EmbeddedResource = ""
                  InferTypesFromValues = args.[5] |> bool.Parse }
        | "Json" ->
            Json { Sample = args.[1]
                   SampleIsList = args.[2] |> bool.Parse
                   RootName = args.[3]
                   Culture = args.[4]
                   Encoding = ""
                   ResolutionFolder = ""
                   EmbeddedResource = ""
                   InferTypesFromValues = args.[5] |> bool.Parse }
        | "Html" ->
            Html { Sample = args.[1]
                   PreferOptionals = args.[2] |> bool.Parse
                   IncludeLayoutTables = args.[3] |> bool.Parse
                   MissingValues = ""
                   Culture = args.[4]
                   Encoding = ""
                   ResolutionFolder = ""
                   EmbeddedResource = "" }
        | "WorldBank" ->
            WorldBank { Sources = args.[1]
                        Asynchronous = args.[2] |> bool.Parse }
        | _ -> failwithf "Unknown: %s" args.[0]

    static member GetRuntimeAssemblyRefs platform =
        match platform with
        | Net40 -> RuntimeAssemblies.net40FSharp31Refs
        | Portable7 -> RuntimeAssemblies.portableCoreFSharp31Refs 7
        | Portable259 -> RuntimeAssemblies.portableCoreFSharp31Refs 259
        | Portable47 -> RuntimeAssemblies.portable47FSharp31Refs

open System.Runtime.CompilerServices

[<assembly:InternalsVisibleToAttribute("FSharp.Data.DesignTime.Tests")>]
do()
