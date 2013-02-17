#if INTERACTIVE
#load "SetupTesting.fsx"
SetupTesting.generateSetupScript __SOURCE_DIRECTORY__
#load "__setup__.fsx"
#endif

open System
open System.IO
open ProviderImplementation

let (++) a b = Path.Combine(a, b)
let resolutionFolder = __SOURCE_DIRECTORY__ ++ ".." ++ "samples" ++ "docs"
let runtimeAssembly = __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ "FSharp.Data.dll"
//let runtimeAssembly = __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ "portable" ++ "FSharp.Data.dll"

let generate args = Debug.generate resolutionFolder runtimeAssembly args

let generateCsv sample separator culture inferRows = 
    generate (fun cfg -> new CsvProvider(cfg)) [| box sample; box separator; box culture; box inferRows; null |] 

let generateXml sample globl sampleList culture = 
    generate (fun cfg -> new XmlProvider(cfg)) [| box sample; box globl; box sampleList; box culture; null |] 

let generateJson sample sampleList culture = 
    generate (fun cfg -> new JsonProvider(cfg)) [| box sample; box sampleList; box culture; null |] 

let generateWorldBank sources asynchronous = 
    generate (fun cfg -> new WorldBankProvider(cfg)) [| box sources; box asynchronous |] 

let generateFreebase apiKeyParam serviceUrlParam numIndividualsParam useUnitsParam pluralizeParam snapshotDateParam localCacheParam allowQueryEvaluateOnClientSideParam = 
    generate (fun cfg -> new FreebaseTypeProvider(cfg)) [| box apiKeyParam ; box serviceUrlParam ; box numIndividualsParam ; box useUnitsParam ; box pluralizeParam ; box snapshotDateParam ; box localCacheParam ; box allowQueryEvaluateOnClientSideParam |] 

let signatureOnly = true
//let signatureOnly = false

let prettyPrint t = Debug.prettyPrint signatureOnly t
let prettyPrintWithMaxDepth maxDepth t = Debug.prettyPrintWithMaxDepth signatureOnly maxDepth t

generateCsv "SmallTest.csv" "" "" Int32.MaxValue
|> prettyPrint |> Console.WriteLine

generateCsv "MSFT.csv" "" "" Int32.MaxValue
|> prettyPrint |> Console.WriteLine

generateCsv "AirQuality.csv" ";" "" Int32.MaxValue
|> prettyPrint |> Console.WriteLine

generateXml "Writers.xml" false false ""
|> prettyPrint |> Console.WriteLine

generateXml "HtmlBody.xml" true false ""
|> prettyPrint |> Console.WriteLine

generateXml "http://tomasp.net/blog/rss.aspx" false false ""
|> prettyPrint |> Console.WriteLine

generateJson "WorldBank.json" false ""
|> prettyPrint |> Console.WriteLine

generateJson "TwitterStream.json" true ""
|> prettyPrint |> Console.WriteLine

generateWorldBank "" false
|> prettyPrintWithMaxDepth 2 |> Console.WriteLine

generateWorldBank "" true
|> prettyPrintWithMaxDepth 2 |> Console.WriteLine

generateFreebase "none" "https://www.googleapis.com/freebase/v1" 10 true true "now" true true
|> prettyPrintWithMaxDepth 2
|> Console.WriteLine
