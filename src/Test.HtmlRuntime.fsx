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

let printTables (url:string) = 
    for table in HtmlRuntime.getTables (HtmlDocument.Load url) do
        printfn "%s" table.Name
        printfn "%s" (table |> HtmlRuntime.formatTable)
        printfn "+++++++++++++++++++++++++++++++++++++"

printTables """D:\Appdev\FSharp.Data\tests\FSharp.Data.Tests\Data\wimbledon_wikipedia.html"""

HtmlDocument.Parse """<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd">
<html lang="en" xml:lang="en" xmlns="http://www.w3.org/1999/xhtml"></html>"""
|> printfn "%O" 

HtmlDocument.Load "http://www.imdb.com/chart/top"
|> printfn "%A" 

printTables "http://www.fifa.com/u17womensworldcup/statistics/index.html"
printTables "http://en.wikipedia.org/wiki/Athletics_at_the_2012_Summer_Olympics_%E2%80%93_Women's_heptathlon"
printTables "http://www.imdb.com/chart/top"
printTables "https://www.nuget.org/packages/FSharp.Data"

HtmlDocument.Load "http://www.imdb.com/chart/top"
|> printfn "%O" 

