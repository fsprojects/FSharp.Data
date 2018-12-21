#if INTERACTIVE
#r "../../bin/lib/net45/FSharp.Data.dll"
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/test/FsUnit/lib/net46/FsUnit.NUnit.dll"
#else
module FSharp.Data.Tests.WorldBankProvider
#endif

open System
open System.Net
open NUnit.Framework
open FsUnit
open FSharp.Data

//alow tests to work when you're behind a proxy
WebRequest.DefaultWebProxy.Credentials <- CredentialCache.DefaultNetworkCredentials

let data = WorldBankData.GetDataContext()

[<Test>]
let ``Should not throw exception for missing year in indicator``() =
    data.Countries.``United Kingdom``.Indicators.``GDP growth (annual %)``.[1900] |> should equal Double.NaN
    data.Countries.``United Kingdom``.Indicators.``GDP growth (annual %)``.[2012] |> should not' (equal Double.NaN)
    data.Countries.``United Kingdom``.Indicators.``GDP growth (annual %)``.TryGetValueAt 1900 |> should equal None
    data.Countries.``United Kingdom``.Indicators.``GDP growth (annual %)``.TryGetValueAt 2012 |> should not' (equal None)
