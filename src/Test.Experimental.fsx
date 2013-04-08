#if INTERACTIVE
#load "SetupTesting.fsx"
SetupTesting.generateSetupScript __SOURCE_DIRECTORY__ "FSharp.Data.Experimental.DesignTime"
#load "__setup__FSharp.Data.Experimental.DesignTime__.fsx"
#endif

open System
open System.IO
open ProviderImplementation

let (++) a b = Path.Combine(a, b)
let resolutionFolder = __SOURCE_DIRECTORY__ ++ ".." ++ "samples" ++ "docs"
let assemblyName = "FSharp.Data.Experimental.dll"
let runtimeAssembly = __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ assemblyName

let signatureOnly = false
let ignoreOutput = false

let generate (inst:TypeProviderInstantiation) = inst.generateType resolutionFolder runtimeAssembly
let prettyPrint t = Debug.prettyPrint signatureOnly ignoreOutput t
let prettyPrintWithMaxDepth maxDepth t = Debug.prettyPrintWithMaxDepth signatureOnly ignoreOutput maxDepth t

Apiary { ApiName = "themoviedb" }
|> generate |> prettyPrint |> Console.WriteLine

Apiary { ApiName = "fssnip" }
|> generate |> prettyPrint |> Console.WriteLine
