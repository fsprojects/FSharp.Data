module FSharp.Data.Tests.WorldBankProvider

open System
open System.Collections.Generic
open System.Linq
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
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

[<Test>]
let ``Indicator should have valid properties``() =
    try
        let indicator = data.Countries.``United Kingdom``.Indicators.``GDP (current US$)``
        indicator.Code |> should equal "GBR"
        indicator.IndicatorCode |> should not' (be EmptyString)
        indicator.Name |> should not' (be EmptyString)
        indicator.Source |> should not' (be EmptyString)
        indicator.Description |> should not' (be EmptyString)
    with ex
        when ex.ToString().Contains("The server has encountered an error")
        || ex.ToString().Contains("Timeout exceeded")
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

[<Test>]
let ``Indicator should be enumerable``() =
    try
        let indicator = data.Countries.``United Kingdom``.Indicators.``GDP (current US$)``
        let dataPoints = indicator |> Seq.toList
        dataPoints |> should not' (be Empty)
        dataPoints |> List.forall (fun (year, _value) -> year > 1900 && year < 2030) |> should be True
    with ex
        when ex.ToString().Contains("The server has encountered an error")
        || ex.ToString().Contains("Timeout exceeded")
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

[<Test>]
let ``Indicator should have years and values collections``() =
    try
        let indicator = data.Countries.``United Kingdom``.Indicators.``GDP (current US$)``
        let years = indicator.Years |> Seq.toList
        let values = indicator.Values |> Seq.toList
        years |> should not' (be Empty)
        values |> should not' (be Empty)
        years.Length |> should equal values.Length
    with ex
        when ex.ToString().Contains("The server has encountered an error")
        || ex.ToString().Contains("Timeout exceeded")
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

[<Test>]
let ``Country should have valid properties``() =
    try
        let country = data.Countries.``United Kingdom``
        country.Code |> should equal "GBR"
        country.Name |> should equal "United Kingdom"
        country.CapitalCity |> should equal "London"
        country.Region |> should not' (be EmptyString)
    with ex
        when ex.ToString().Contains("The server has encountered an error")
        || ex.ToString().Contains("Timeout exceeded")
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

[<Test>]
let ``Country should provide indicators``() =
    try
        let country = data.Countries.``United Kingdom``
        let indicators = country.Indicators
        indicators |> should not' (be Null)
        let indicatorsList = indicators |> Seq.take 5 |> Seq.toList
        indicatorsList |> should not' (be Empty)
    with ex
        when ex.ToString().Contains("The server has encountered an error")
        || ex.ToString().Contains("Timeout exceeded")
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

[<Test>]
let ``Countries collection should be enumerable``() =
    try
        let countries = data.Countries |> Seq.take 10 |> Seq.toList
        countries |> should not' (be Empty)
        countries |> List.forall (fun c -> not (String.IsNullOrEmpty(c.Code))) |> should be True
        countries |> List.forall (fun c -> not (String.IsNullOrEmpty(c.Name))) |> should be True
    with ex
        when ex.ToString().Contains("The server has encountered an error")
        || ex.ToString().Contains("Timeout exceeded")
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

[<Test>]
let ``Regions collection should be enumerable``() =
    try
        let regions = data.Regions |> Seq.take 5 |> Seq.toList
        regions |> should not' (be Empty)
        regions |> List.forall (fun r -> not (String.IsNullOrEmpty(r.RegionCode))) |> should be True
        regions |> List.forall (fun r -> not (String.IsNullOrEmpty(r.Name))) |> should be True
    with ex
        when ex.ToString().Contains("The server has encountered an error")
        || ex.ToString().Contains("Timeout exceeded")
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

[<Test>]
let ``Region should provide countries``() =
    try
        let regions = data.Regions |> Seq.toList
        if regions.Length > 0 then
            let region = regions.[0]
            let countries = region.Countries |> Seq.take 3 |> Seq.toList
            // Some regions might have no countries, so we just test structure
            countries |> should not' (be Null)
    with ex
        when ex.ToString().Contains("The server has encountered an error")
        || ex.ToString().Contains("Timeout exceeded")
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

[<Test>]
let ``Region should provide indicators``() =
    try
        let regions = data.Regions |> Seq.toList
        if regions.Length > 0 then
            let region = regions.[0]
            let indicators = region.Indicators
            indicators |> should not' (be Null)
    with ex
        when ex.ToString().Contains("The server has encountered an error")
        || ex.ToString().Contains("Timeout exceeded")
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

[<Test>]
let ``Topics collection should be enumerable``() =
    try
        let topics = data.Topics |> Seq.take 5 |> Seq.toList
        topics |> should not' (be Empty)
        topics |> List.forall (fun t -> not (String.IsNullOrEmpty(t.Code))) |> should be True
        topics |> List.forall (fun t -> not (String.IsNullOrEmpty(t.Name))) |> should be True
    with ex
        when ex.ToString().Contains("The server has encountered an error")
        || ex.ToString().Contains("Timeout exceeded")
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

[<Test>]
let ``Topic should provide indicator descriptions``() =
    try
        let topics = data.Topics |> Seq.toList
        if topics.Length > 0 then
            let topic = topics.[0]
            topic.Description |> should not' (be Null)
            let indicatorDescriptions = topic.Indicators
            indicatorDescriptions |> should not' (be Null)
    with ex
        when ex.ToString().Contains("The server has encountered an error")
        || ex.ToString().Contains("Timeout exceeded")
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

[<Test>]
let ``IndicatorDescription should have valid properties``() =
    try
        let topics = data.Topics |> Seq.toList
        if topics.Length > 0 then
            let topic = topics.[0]
            let indicators = topic.Indicators |> Seq.take 1 |> Seq.toList
            if indicators.Length > 0 then
                let indicator = indicators.[0]
                indicator.Name |> should not' (be EmptyString)
                indicator.Description |> should not' (be EmptyString)
    with ex
        when ex.ToString().Contains("The server has encountered an error")
        || ex.ToString().Contains("Timeout exceeded")
        || ex.ToString().Contains("504 Gateway Time-out")
        || ex.ToString().Contains("400") ->
            Assert.Inconclusive($"Worldbank api is having issues: {ex}")

