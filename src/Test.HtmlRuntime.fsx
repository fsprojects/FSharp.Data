#if INTERACTIVE
#load "Net/UriUtils.fs"
#load "Net/Http.fs"
#load "CommonRuntime/IO.fs"
#load "CommonRuntime/TextConversions.fs"
#load "CommonRuntime/TextRuntime.fs"
#load "CommonRuntime/Pluralizer.fs"
#load "CommonRuntime/NameUtils.fs"
#load "CommonRuntime/StructuralTypes.fs"
#load "CommonRuntime/StructuralInference.fs"
#load "Html/HtmlCharRefs.fs"
#load "Html/HtmlParser.fs"
#load "Html/HtmlOperations.fs"
#load "Html/HtmlInference.fs"
#load "Html/HtmlRuntime.fs"
#else
module internal Test.HtmlRuntime
#endif

open FSharp.Data
open FSharp.Data.Html
open FSharp.Data.Runtime

let printTables includeLayout (url:string) = 
    for table in HtmlRuntime.getTables includeLayout (HtmlDocument.Load url) do
        printfn "%s" table.Name
        printfn "%s" (table |> HtmlRuntime.formatTable)
        printfn "+++++++++++++++++++++++++++++++++++++"

type PrintableContent =
    | Element of string * HtmlAttribute list * (PrintableContent list)
    | Text of string
    | Comment of string
    static member ofHtmlNode(x) =
        match x with
        | HtmlElement(_, name, attrs, content) -> Element(name, attrs, content |> List.map PrintableContent.ofHtmlNode)
        | HtmlText(_,content) -> Text(content)
        | HtmlComment(_, content) -> Comment(content)

fsi.AddPrintTransformer(PrintableContent.ofHtmlNode >> box)
    
//Working sensibly
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

//Interesting table structure, with col and row spans.
printTables false "http://en.wikipedia.org/wiki/List_of_Presidents_of_the_United_States"
 

