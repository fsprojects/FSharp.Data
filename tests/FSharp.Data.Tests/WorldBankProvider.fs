module FSharp.Data.Tests.WorldBankProvider

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
    try
        data.Countries.``United Kingdom``.Indicators.``GNI growth (annual %)``.[1900] |> should equal Double.NaN
        data.Countries.``United Kingdom``.Indicators.``GNI growth (annual %)``.[2012] |> should not' (equal Double.NaN)
        data.Countries.``United Kingdom``.Indicators.``GNI growth (annual %)``.TryGetValueAt 1900 |> should equal None
        data.Countries.``United Kingdom``.Indicators.``GNI growth (annual %)``.TryGetValueAt 2012 |> should not' (equal None)
    with ex
        when ex.ToString().Contains("The server has encountered an error which prevents it from fulfilling your request. Please contact the system administrator")
        || ex.ToString().Contains("Timeout exceeded while getting response")
        || ex.ToString().Contains("504 Gateway Time-out") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

