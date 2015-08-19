#if INTERACTIVE
#load "SetupTesting.fsx"
SetupTesting.generateSetupScript __SOURCE_DIRECTORY__ "FSharp.Data.DesignTime"
#load "__setup__FSharp.Data.DesignTime__.fsx"
#else
module internal Test
#endif

open System
open System.Globalization
open System.IO
open ProviderImplementation
open FSharp.Data
open FSharp.Data.Runtime

let (++) a b = Path.Combine(a, b)
let resolutionFolder = __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.Tests" ++ "Data"
let outputFolder = __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.DesignTime.Tests" ++ "expected"
let assemblyName = "FSharp.Data.dll"

type Platform = Net40 | Portable7 | Portable47

let dump signatureOnly ignoreOutput platform saveToFileSystem (inst:TypeProviderInstantiation) =
    let runtimeAssembly =
        match platform with
        | Net40 -> __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ assemblyName
        | Portable7 -> __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ "portable7" ++ assemblyName
        | Portable47 -> __SOURCE_DIRECTORY__ ++ ".." ++ "bin" ++ "portable47" ++ assemblyName
    inst.Dump resolutionFolder (if saveToFileSystem then outputFolder else "") runtimeAssembly signatureOnly ignoreOutput
    |> Console.WriteLine

let dumpAll inst =
    dump false false Net40 false inst
    //dump false false Portable7 false inst
    dump false false Portable47 false inst

Html { Sample = "doctor_who2.html"
       PreferOptionals = false
       IncludeLayoutTables = false
       MissingValues = "NaN,NA,N/A,#N/A,:,-,TBA,TBD"
       Culture = "" 
       Encoding = ""
       ResolutionFolder = ""
       EmbeddedResource = "" }
|> dumpAll

Json { Sample = "optionals.json"
       SampleIsList = false
       RootName = ""
       Culture = ""
       Encoding = ""
       ResolutionFolder = ""
       EmbeddedResource = ""
       InferTypesFromValues = true }
|> dumpAll

Xml { Sample = "JsonInXml.xml"
      SampleIsList = true
      Global = false
      Culture = ""
      Encoding = ""
      ResolutionFolder = ""
      EmbeddedResource = ""
      InferTypesFromValues = true }
|> dumpAll

Csv { Sample = "AirQuality.csv"
      Separators = ";"
      InferRows = Int32.MaxValue
      Schema = ""
      HasHeaders = true
      IgnoreErrors = false
      SkipRows = 0
      AssumeMissingValues = false
      PreferOptionals = false
      Quote = '"'
      MissingValues = "NaN,NA,N/A,#N/A,:,-,TBA,TBD"
      CacheRows = true
      Culture = ""
      Encoding = ""
      ResolutionFolder = ""
      EmbeddedResource = "" }
|> dumpAll

let testCases =
    __SOURCE_DIRECTORY__ ++ ".." ++ "tests" ++ "FSharp.Data.DesignTime.Tests" ++ "SignatureTestCases.config"
    |> File.ReadAllLines
    |> Array.map (TypeProviderInstantiation.Parse >> snd)

for testCase in testCases do
    dump false false Net40 true testCase

let printParsed (html:string) = 
    html
    |> HtmlDocument.Parse
    |> printfn "%A"

printParsed """<a href="/url?q=http://fsharp.github.io/FSharp.Data/&amp;sa=U&amp;ei=sv1jU_3bMMmk0QX33YGQBw&amp;ved=0CB4QFjAA&amp;usg=AFQjCNF_2exXvCWzixA0Uj58KLThvXYUwA"><b>F# Data</b>: Library for Data Access - F# Open Source Group @ GitHub</a>"""

let parameters : HtmlInference.Parameters = 
    { MissingValues = TextConversions.DefaultMissingValues
      CultureInfo = CultureInfo.InvariantCulture
      UnitsOfMeasureProvider = StructuralInference.defaultUnitsOfMeasureProvider
      PreferOptionals = false }

let printTables includeLayout (url:string) = 
    url
    |> HtmlDocument.Load
    |> HtmlRuntime.getTables (Some parameters) includeLayout
    |> List.iter (printfn "+++++++++++++++++++++++++++++++++++++\n%O")

let printLists (url:string) = 
    url
    |> HtmlDocument.Load
    |> HtmlRuntime.getLists
    |> List.iter (printfn "+++++++++++++++++++++++++++++++++++++\n%O")

printLists "https://en.wikipedia.org/wiki/Doctor_Who"
printTables false "http://en.wikipedia.org/wiki/List_of_Presidents_of_the_United_States"
printTables false "http://en.wikipedia.org/wiki/The_Championships,_Wimbledon"
printTables false "http://www.fifa.com/u17womensworldcup/statistics/index.html"
printTables false "http://en.wikipedia.org/wiki/Athletics_at_the_2012_Summer_Olympics_%E2%80%93_Women's_heptathlon"
printTables false "http://www.imdb.com/chart/top?sort=ir,desc"
printTables false "https://www.nuget.org/packages/FSharp.Data"
printTables false "http://www.rottentomatoes.com"
printTables false "http://www.orbitz.com/shop/home?airDA=true&ar.rt.leaveSlice.dest.dl=LGW_AIRPORT&search=Continue&ar.rt.carriers%5B1%5D=&ar.rt.narrowSel=0&type=air&ar.rt.returnSlice.time=Anytime&ar.rt.leaveSlice.originRadius=0&ar.rt.flexAirSearch=0&ar.rt.numAdult=1&ar.rt.numChild=0&ar.rt.child%5B4%5D=&ar.rt.leaveSlice.orig.key=SFO&ar.rt.child%5B2%5D=&strm=true&ar.rt.child%5B0%5D=&ar.rt.leaveSlice.time=Anytime&ar.rt.child%5B6%5D=&ar.rt.carriers%5B0%5D=&ar.rt.numSenior=0&ar.rt.returnSlice.date=05%2F28%2F14&ar.rt.narrow=airlines&ar.rt.carriers%5B2%5D=&ar.rt.leaveSlice.dest.key=LONDON&ar.rt.leaveSlice.date=04%2F22%2F14&ar.rt.nonStop=0&ar.rt.cabin=C&ar.rt.leaveSlice.destinationRadius=0&ar.type=roundTrip&ar.rt.child%5B3%5D=&ar.rt.child%5B5%5D=&ar.rt.child%5B7%5D=&ar.rt.child%5B1%5D="
printTables false "http://www.sherdog.com/stats/fightfinder?SearchTxt=silva"
printTables false "http://www.ebay.com/sch/i.html?_nkw=cars"
printTables false "http://www.ebay.com/sch/i.html?_nkw=cars&_sacat=0&_from=R40"
printTables false "http://www.ebay.com/sch/i.html?_trksid=p2050601.m570.l1311.R1.TR11.TRC1.A0.H0.Xcar&_nkw=cars&_sacat=0&_from=R40"
