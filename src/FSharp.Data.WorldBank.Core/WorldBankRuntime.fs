// --------------------------------------------------------------------------------------
// WorldBank type provider - runtime components
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Runtime.WorldBank

open System
open System.Collections
open System.Diagnostics
open System.Globalization
open System.Net
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Data.Runtime.Caching

/// <exclude />
[<AutoOpen>]
module Implementation =

    let private retryCount = 5
    let private parallelIndicatorPageDownloads = 8

    type internal IndicatorRecord =
        { Id: string
          Name: string
          TopicIds: string list
          Source: string
          Description: string }

    type internal CountryRecord =
        { Id: string
          Name: string
          CapitalCity: string
          Region: string }
        member x.IsRegion = x.Region = "Aggregates"

    type internal TopicRecord =
        { Id: string
          Name: string
          Description: string }

    type internal ServiceConnection(restCache: ICache<_, _>, serviceUrl: string, sources) =

        let worldBankUrl (functions: string list) (props: (string * string) list) =
            let url =
                serviceUrl
                :: (List.map Uri.EscapeUriString functions)
                |> String.concat "/"

            let query = [ "per_page", "1000"; "format", "json" ] @ props
            Http.AppendQueryToUrl(url, query)

        // The WorldBank data changes very slowly indeed (monthly updates to values, rare updates to schema), hence caching it is ok.

        let rec worldBankRequest attempt funcs args : Async<string> =
            async {
                let url = worldBankUrl funcs args

                match restCache.TryRetrieve(url) with
                | Some res -> return res
                | None ->
                    Debug.WriteLine(sprintf "[WorldBank] downloading (%d): %s" attempt url)

                    try
                        let! doc =
                            Http.AsyncRequestString(
                                url,
                                headers =
                                    [ HttpRequestHeaders.UserAgent "FSharp.Data WorldBank Type Provider"
                                      HttpRequestHeaders.Accept HttpContentTypes.Json ]
                            )

                        Debug.WriteLine(
                            sprintf
                                "[WorldBank] got text: %s"
                                (if isNull doc then "null"
                                 elif doc.Length > 50 then doc.[0..49] + "..."
                                 else doc)
                        )

                        if not (String.IsNullOrEmpty doc) then
                            restCache.Set(url, doc)

                        return doc
                    with e ->
                        Debug.WriteLine(sprintf "[WorldBank] error: %s" (e.ToString()))

                        if attempt > 0 then
                            return! worldBankRequest (attempt - 1) funcs args
                        else
                            return! failwithf "Failed to request '%s'. Error: %O" url e
            }

        let rec getDocuments funcs args page parallelPages =
            async {
                let! docs =
                    Async.Parallel
                        [ for i in 0 .. parallelPages - 1 ->
                              worldBankRequest retryCount funcs (args @ [ "page", string (page + i) ]) ]

                let docs = docs |> Array.map JsonValue.Parse
                Debug.WriteLine(sprintf "[WorldBank] geting page count")
                let pages = docs.[0].[0]?pages.AsInteger()
                Debug.WriteLine(sprintf "[WorldBank] got page count = %d" pages)

                if (pages < page + parallelPages) then
                    return Array.toList docs
                else
                    let! rest = getDocuments funcs args (page + parallelPages) (pages - parallelPages)
                    return Array.toList docs @ rest
            }

        let getIndicators () =
            // Get the indicators in parallel, initially using 'parallelIndicatorPageDownloads' pages
            async {
                let! docs = getDocuments [ "indicator" ] [] 1 parallelIndicatorPageDownloads

                return
                    [ for doc in docs do
                          for ind in doc.[1] do
                              let id = ind?id.AsString()
                              let name = ind?name.AsString().Trim([| '"' |]).Trim()
                              let sourceName = ind?source?value.AsString()

                              if List.isEmpty sources
                                 || sources
                                    |> List.exists (fun source ->
                                        String.Compare(source, sourceName, StringComparison.OrdinalIgnoreCase) = 0) then
                                  let topicIds =
                                      Seq.toList
                                      <| seq {
                                          for item in ind?topics do
                                              match item.TryGetProperty("id") with
                                              | Some id -> yield id.AsString()
                                              | None -> ()
                                      }

                                  let sourceNote = ind?sourceNote.AsString()

                                  yield
                                      { Id = id
                                        Name = name
                                        TopicIds = topicIds
                                        Source = sourceName
                                        Description = sourceNote } ]
            }

        let getTopics () =
            async {
                let! docs = getDocuments [ "topic" ] [] 1 1

                return
                    [ for doc in docs do
                          for topic in doc.[1] do
                              let id = topic?id.AsString()
                              let name = topic?value.AsString()
                              let sourceNote = topic?sourceNote.AsString()

                              yield
                                  { Id = id
                                    Name = name
                                    Description = sourceNote } ]
            }

        let getCountries (args) =
            async {
                let! docs = getDocuments [ "country" ] args 1 1

                return
                    [ for doc in docs do
                          for country in doc.[1] do
                              let region = country?region?value.AsString()
                              let id = country?id.AsString()
                              let name = country?name.AsString()
                              let capitalCity = country?capitalCity.AsString()

                              yield
                                  { Id = id
                                    Name = name
                                    CapitalCity = capitalCity
                                    Region = region } ]
            }

        let getRegions () =
            async {
                let! docs = getDocuments [ "region" ] [] 1 1

                return
                    [ for doc in docs do
                          for ind in doc.[1] do
                              yield ind?code.AsString(), ind?name.AsString() ]
            }

        let getData funcs args (key: string) =
            async {
                let! docs = getDocuments funcs args 1 1

                return
                    [ for doc in docs do
                          for ind in doc.[1] do
                              yield ind.[key].AsString(), ind?value.AsString() ]
            }

        /// At compile time, download the schema
        let topics = lazy (getTopics () |> Async.RunSynchronously)

        let topicsIndexed =
            lazy
                (topics.Force()
                 |> Seq.map (fun t -> t.Id, t)
                 |> dict)

        let indicators =
            lazy
                (getIndicators ()
                 |> Async.RunSynchronously
                 |> List.toSeq
                 |> Seq.distinctBy (fun i -> i.Name)
                 |> Seq.toList)

        let indicatorsIndexed =
            lazy
                (indicators.Force()
                 |> Seq.map (fun i -> i.Id, i)
                 |> dict)

        let indicatorsByTopic =
            lazy
                (indicators.Force()
                 |> Seq.collect (fun i ->
                     i.TopicIds
                     |> Seq.map (fun topicId -> topicId, i.Id))
                 |> Seq.groupBy fst
                 |> Seq.map (fun (topicId, indicatorIds) -> topicId, indicatorIds |> Seq.map snd |> Seq.cache)
                 |> dict)

        let countries = lazy (getCountries [] |> Async.RunSynchronously)

        let countriesIndexed =
            lazy
                (countries.Force()
                 |> Seq.map (fun c -> c.Id, c)
                 |> dict)

        let regions = lazy (getRegions () |> Async.RunSynchronously)
        let regionsIndexed = lazy (regions.Force() |> dict)

        member internal __.Topics = topics.Force()
        member internal __.TopicsIndexed = topicsIndexed.Force()
        member internal __.Indicators = indicators.Force()
        member internal __.IndicatorsIndexed = indicatorsIndexed.Force()
        member internal __.IndicatorsByTopic = indicatorsByTopic.Force()
        member internal __.Countries = countries.Force()
        member internal __.CountriesIndexed = countriesIndexed.Force()
        member internal __.Regions = regions.Force()
        member internal __.RegionsIndexed = regionsIndexed.Force()

        /// At runtime, download the data
        member internal __.GetDataAsync(countryOrRegionCode, indicatorCode) =
            async {
                let! data =
                    getData [ "countries"; countryOrRegionCode; "indicators"; indicatorCode ] [ "date", "" ] "date"

                return
                    seq {
                        for k, v in data do
                            if not (String.IsNullOrEmpty v) then yield int k, float v
                    }
                    // It's a time series - sort it :-)  We should probably also interpolate (e.g. see R time series library)
                    |> Seq.sortBy fst
            }

        member internal x.GetData(countryOrRegionCode, indicatorCode) =
            x.GetDataAsync(countryOrRegionCode, indicatorCode)
            |> Async.RunSynchronously

        member internal __.GetCountriesInRegion region =
            getCountries [ "region", region ]
            |> Async.RunSynchronously

/// <summary>Indicator data</summary>
/// <namespacedoc>
///   <summary>Support types for the WorldBank type provider.</summary>
/// </namespacedoc>
[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
type Indicator internal (connection: ServiceConnection, countryOrRegionCode: string, indicatorCode: string) =
    let data =
        connection.GetData(countryOrRegionCode, indicatorCode)
        |> Seq.cache

    let dataDict = lazy (dict data)

    /// Get the code for the country or region of the indicator
    member _.Code = countryOrRegionCode

    /// Get the code for the indicator
    member _.IndicatorCode = indicatorCode

    /// Get the name of the indicator
    member _.Name = connection.IndicatorsIndexed.[indicatorCode].Name

    /// Get the source of the indicator
    member _.Source = connection.IndicatorsIndexed.[indicatorCode].Source

    /// Get the description of the indicator
    member _.Description = connection.IndicatorsIndexed.[indicatorCode].Description

    /// Get the indicator value for the given year. If there's no data for that year, NaN is returned
    member _.Item
        with get year =
            match dataDict.Force().TryGetValue year with
            | true, value -> value
            | _ -> Double.NaN

    /// Get the indicator value for the given year, if present
    member _.TryGetValueAt year =
        match dataDict.Force().TryGetValue year with
        | true, value -> Some value
        | _ -> None

    /// Get the years for which the indicator has values
    member _.Years = dataDict.Force().Keys

    /// Get the values for the indicator (without years)
    member _.Values = dataDict.Force().Values

    interface seq<int * float> with
        member _.GetEnumerator() = data.GetEnumerator()

    interface IEnumerable with
        member _.GetEnumerator() = (data.GetEnumerator() :> _)

/// Metadata for an Indicator
[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
type IndicatorDescription internal (connection: ServiceConnection, topicCode: string, indicatorCode: string) =
    /// Get the code for the topic of the indicator
    member _.Code = topicCode

    /// Get the code for the indicator
    member _.IndicatorCode = indicatorCode

    /// Get the name of the indicator
    member _.Name = connection.IndicatorsIndexed.[indicatorCode].Name

    /// Get the source of the indicator
    member _.Source = connection.IndicatorsIndexed.[indicatorCode].Source

    /// Get the description of the indicator
    member _.Description = connection.IndicatorsIndexed.[indicatorCode].Description

/// <exclude />
type IIndicators =
    abstract GetIndicator: indicatorCode: string -> Indicator
    abstract AsyncGetIndicator: indicatorCode: string -> Async<Indicator>

/// <exclude />
type Indicators internal (connection: ServiceConnection, countryOrRegionCode) =
    let indicators =
        seq { for indicator in connection.Indicators -> Indicator(connection, countryOrRegionCode, indicator.Id) }

    interface IIndicators with
        member _.GetIndicator(indicatorCode) =
            Indicator(connection, countryOrRegionCode, indicatorCode)

        member _.AsyncGetIndicator(indicatorCode) =
            async { return Indicator(connection, countryOrRegionCode, indicatorCode) }

    interface seq<Indicator> with
        member _.GetEnumerator() = indicators.GetEnumerator()

    interface IEnumerable with
        member _.GetEnumerator() = indicators.GetEnumerator() :> _

/// <exclude />
type IIndicatorsDescriptions =
    abstract GetIndicator: indicatorCode: string -> IndicatorDescription

/// <exclude />
type IndicatorsDescriptions internal (connection: ServiceConnection, topicCode) =
    let indicatorsDescriptions =
        seq {
            for indicatorId in connection.IndicatorsByTopic.[topicCode] ->
                IndicatorDescription(connection, topicCode, indicatorId)
        }

    interface IIndicatorsDescriptions with
        member _.GetIndicator(indicatorCode) =
            IndicatorDescription(connection, topicCode, indicatorCode)

    interface seq<IndicatorDescription> with
        member _.GetEnumerator() = indicatorsDescriptions.GetEnumerator()

    interface IEnumerable with
        member _.GetEnumerator() =
            indicatorsDescriptions.GetEnumerator() :> _

/// <exclude />
type ICountry =
    abstract GetIndicators: unit -> Indicators

/// Metadata for a Country
[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
type Country internal (connection: ServiceConnection, countryCode: string) =
    let indicators = new Indicators(connection, countryCode)

    /// Get the WorldBank code of the country
    member _.Code = countryCode

    /// Get the name of the country
    member _.Name = connection.CountriesIndexed.[countryCode].Name

    /// Get the capital city of the country
    member _.CapitalCity = connection.CountriesIndexed.[countryCode].CapitalCity

    /// Get the region of the country
    member _.Region = connection.CountriesIndexed.[countryCode].Region

    interface ICountry with
        member _.GetIndicators() = indicators

/// <exclude />
type ICountryCollection =
    abstract GetCountry: countryCode: string * countryName: string -> Country

/// <exclude />
type CountryCollection<'T when 'T :> Country> internal (connection: ServiceConnection, regionCodeOpt) =
    let items =
        seq {
            let countries =
                match regionCodeOpt with
                | None -> connection.Countries
                | Some r -> connection.GetCountriesInRegion(r)

            for country in countries do
                if not country.IsRegion then
                    yield Country(connection, country.Id) :?> 'T
        }

    interface seq<'T> with
        member _.GetEnumerator() = items.GetEnumerator()

    interface IEnumerable with
        member _.GetEnumerator() = (items :> IEnumerable).GetEnumerator()

    interface ICountryCollection with
        member _.GetCountry(countryCode (*this parameter is only here to help FunScript*) , _countryName) =
            Country(connection, countryCode)

/// <exclude />
type IRegion =
    abstract GetCountries<'T when 'T :> Country> : unit -> CountryCollection<'T>
    abstract GetIndicators: unit -> Indicators

/// Metadata for a Region
[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
type Region internal (connection: ServiceConnection, regionCode: string) =
    let indicators = new Indicators(connection, regionCode)
    /// Get the WorldBank code for the region
    member _.RegionCode = regionCode
    /// Get the name of the region
    member _.Name = connection.RegionsIndexed.[regionCode]

    interface IRegion with
        member _.GetCountries() =
            CountryCollection(connection, Some regionCode)

        member _.GetIndicators() = indicators

/// <exclude />
type IRegionCollection =
    abstract GetRegion: regionCode: string -> Region

/// <exclude />
type RegionCollection<'T when 'T :> Region> internal (connection: ServiceConnection) =
    let items =
        seq { for (code, _) in connection.Regions -> Region(connection, code) :?> 'T }

    interface seq<'T> with
        member _.GetEnumerator() = items.GetEnumerator()

    interface IEnumerable with
        member _.GetEnumerator() = (items :> IEnumerable).GetEnumerator()

    interface IRegionCollection with
        member _.GetRegion(regionCode) = Region(connection, regionCode)

/// <exclude />
type ITopic =
    abstract GetIndicators: unit -> IndicatorsDescriptions

/// Metadata for a Topic
[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
type Topic internal (connection: ServiceConnection, topicCode: string) =
    let indicatorsDescriptions = new IndicatorsDescriptions(connection, topicCode)

    /// Get the WorldBank code of the topic
    member _.Code = topicCode

    /// Get the name of the topic
    member _.Name = connection.TopicsIndexed.[topicCode].Name

    /// Get the description of the topic
    member _.Description = connection.TopicsIndexed.[topicCode].Description

    interface ITopic with
        member _.GetIndicators() = indicatorsDescriptions

/// <exclude />
type ITopicCollection =
    abstract GetTopic: topicCode: string -> Topic

/// <exclude />
type TopicCollection<'T when 'T :> Topic> internal (connection: ServiceConnection) =
    let items =
        seq { for topic in connection.Topics -> Topic(connection, topic.Id) :?> 'T }

    interface seq<'T> with
        member _.GetEnumerator() = items.GetEnumerator()

    interface IEnumerable with
        member _.GetEnumerator() = (items :> IEnumerable).GetEnumerator()

    interface ITopicCollection with
        member _.GetTopic(topicCode) = Topic(connection, topicCode)

/// <exclude />
type IWorldBankData =
    abstract GetCountries<'T when 'T :> Country> : unit -> seq<'T>
    abstract GetRegions<'T when 'T :> Region> : unit -> seq<'T>
    abstract GetTopics<'T when 'T :> Topic> : unit -> seq<'T>

/// <exclude />
type WorldBankData(serviceUrl: string, sources: string) =
    let sources =
        sources.Split([| ';' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.toList

    let restCache = createInternetFileCache "WorldBankRuntime" (TimeSpan.FromDays 30.0)
    let connection = new ServiceConnection(restCache, serviceUrl, sources)

    interface IWorldBankData with
        member _.GetCountries() =
            CountryCollection(connection, None) :> seq<_>

        member _.GetRegions() = RegionCollection(connection) :> seq<_>
        member _.GetTopics() = TopicCollection(connection) :> seq<_>
