module FSharp.Data.Tests.FreebaseProvider.WebPageTests

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

open NUnit.Framework
open FsUnit
open System
open FSharp.Data

let data = FreebaseData.GetDataContext()

[<Test>]
let ``Can access the webpages for music composers``() =

    let webPage = 
        data.``Arts and Entertainment``.Music.Composers
        |> Seq.map (fun composer -> String.concat "\n" composer.``Topical webpage``)
        |> Seq.find (not << String.IsNullOrWhiteSpace)

    webPage |> should equal "http://www.discogs.com/artist/John+Barry"

[<Test>]
let ``Can access the webpages of stock exchanges``() =

    let webPage = 
        data.``Products and Services``.Business.``Stock exchanges``
        |> Seq.map (fun exchange -> String.concat "\n" exchange.``Official website``)
        |> Seq.find (not << String.IsNullOrWhiteSpace)

    webPage |> should equal "http://www.nasdaqomx.com/\nhttp://www.nasdaq.com/"