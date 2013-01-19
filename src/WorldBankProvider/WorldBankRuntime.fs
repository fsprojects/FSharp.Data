namespace FSharp.Data.WorldBank

#if PORTABLE
//TODO PORTABLE
#else

open System
open System.Net
open System.Web
open System.Xml.Linq
open FSharp.Data.Caching

[<AutoOpen>]
module Implementation = 

    type internal IndicatorRecord = { Id: string; Name: string; TopicIds : string list; Source:string;Description: string }
    type internal CountryRecord = { Id: string; Name: string; (* Iso2Code:string; *) Region:string; IsRegion:bool }
    // <wb:iso2Code>AW</wb:iso2Code> <wb:name>Aruba</wb:name> <wb:region id="LCN">Latin America & Caribbean (all income levels)</wb:region> <wb:adminregion id=""/> <wb:incomeLevel id="NOC">High income: nonOECD</wb:incomeLevel> <wb:lendingType id="LNX">Not classified</wb:lendingType> <wb:capitalCity>Oranjestad</wb:capitalCity> <wb:longitude>-70.0167</wb:longitude> <wb:latitude>12.5167</wb:latitude>

    type internal ServiceConnection(restCache:ICache<_>,serviceUrl:string, sources) =

        let xmlSchemaUrl = "http://www.worldbank.org"
        let xattr s (el:XElement) = el.Attribute(XName.Get(s)).Value
        let xelem s (el:XContainer) = el.Element(XName.Get(s, xmlSchemaUrl))
        let xelems s (el:XContainer) = el.Elements(XName.Get(s, xmlSchemaUrl))
        let xvalue (el:XElement) = el.Value
        let xnested path (el:XContainer) = 
            (path |> Seq.fold (fun xn s -> (xelem s xn) :> XContainer) el) :?> XElement
    
        let retryCount = 5 // TODO: make this a parameter
        let parallelIndicatorPageDownloads = 8

        // TODO: frequency=M/Q/Y (monthly, quarterly, yearly)
  
        let worldBankUrl (functions: string list) (props: (string * string) list) = 
            seq { yield serviceUrl
                  for item in functions do
                      yield "/" + HttpUtility.UrlEncode(item:string)
                  yield "?per_page=1000"
                  for key, value in props do
                      yield "&" + key + "=" + HttpUtility.UrlEncode(value:string) } 
            |> String.concat ""

        // The WorldBank data changes very slowly indeed (monthly updates to values, rare updates to schema), hence caching it is ok.

        let rec worldBankRequest attempt funcs args : Async<string> = 
            async { 
                let url = worldBankUrl funcs args
                match restCache.TryRetrieve(url) with
                | Some res -> return res
                | None -> 
                    let wc = new WebClient()
                    printfn "[WorldBank] downloading (%d): %s" attempt url
                    try
                        let! doc = wc.AsyncDownloadString(Uri(url))
                        printfn "[WorldBank] got text: %s" (if doc = null then "null" elif doc.Length > 50 then doc.[0..49] + "..." else doc)
                        if not (System.String.IsNullOrEmpty doc) then 
                            restCache.Set(url, doc)
                        return doc 
                    with e ->
                        printfn "[WorldBank] error: %s" (e.ToString()) 
                        if attempt > 0 then
                            return! worldBankRequest (attempt - 1) funcs args
                        else return! failwith "failed" }

        let rec getDocuments funcs args root page parallelPages = 
            async { let! docs = 
                        Async.Parallel 
                            [ for i in 0 .. parallelPages - 1 -> 
                                  worldBankRequest retryCount funcs (args@["page", string (page+i)]) ]
                    let docs = docs |> Array.map XDocument.Parse
                    printfn "[WorldBank] geting page count" 
                    let pages = docs.[0] |> xnested [ root ] |> xattr "pages" |> int
                    printfn "[WorldBank] got page count = %d" pages 
                    if (pages < page + parallelPages) then 
                        return Array.toList docs
                    else 
                        let! rest = getDocuments funcs args root (page + parallelPages) (pages - parallelPages)
                        return Array.toList docs @ rest }

        let getIndicators() = 
            // Get the indicators in parallel, initially using 'parallelIndicatorPageDownloads' pages
            async { let! docs = getDocuments ["indicator"] [] "indicators" 1 parallelIndicatorPageDownloads
                    return 
                        [ for doc in docs do
                            for ind in doc |> xelem "indicators" |> xelems "indicator" do
                                let id = ind |> xattr "id"
                                let name = ind |> xelem "name" |> xvalue
                                let sourceName = ind |> xelem "source" |> xvalue
                                if sources |> List.exists (fun source -> String.Compare(source, sourceName, StringComparison.CurrentCultureIgnoreCase) = 0) then 
 
                                    let topicIds = ind |> xelem "topics" |> xelems "topic" |> Seq.map (xattr "id") |> Seq.toList
                                    let sourceNote = ind |> xelem "sourceNote" |> xvalue
                                    yield {Id=id;Name=name;TopicIds=topicIds;Source=sourceName;Description=sourceNote} ] }

        let getTopics() = 
            async { let! docs = getDocuments ["topic"] [] "topics" 1 1
                    return 
                        [ for doc in docs do
                            for ind in doc |> xelem "topics" |> xelems "topic" do
                                let id = ind |> xattr "id"
                                let name = ind |> xelem "value" |> xvalue
                                let sourceNote = ind |> xelem "sourceNote" |> xvalue
                                yield (id,name,sourceNote) ] }

        let getCountries(args) = 
            async { let! docs = getDocuments ["country"] args "countries" 1 1
                    return 
                        [ for doc in docs do
                            for ind in doc |> xelem "countries" |> xelems "country" do
                                let region = ind |> xelem "region" |> xvalue
                                let isRegion = (region = "Aggregates")
                                //let region = ""
                                //let isRegion = true
                                yield {Id=ind |> xattr "id"
                                       Name=ind |> xelem "name" |> xvalue
                                       //Iso2Code=ind |> xelem "iso2code" |> xvalue
                                       Region=region
                                       IsRegion=isRegion  } ] }

        let getRegions() = 
            async { let! docs = getDocuments ["region"] [] "regions" 1 1
                    return 
                        [ for doc in docs do
                            for ind in doc |> xelem "regions" |> xelems "region" do
                                yield (ind |> xelem "code" |> xvalue, ind |> xelem "name" |> xvalue) ] }

        let getData funcs args key = 
            async { let! docs = getDocuments funcs args "data" 1 1
                    return
                        [ for doc in docs do
                            for ind in doc |> xelem "data" |> xelems "data" do
                                yield (ind |> xelem key |> xvalue, ind |> xelem "value" |> xvalue) ] }

        /// At compile time, download the schema
        let topics = lazy (getTopics() |> Async.RunSynchronously)
        let indicators = lazy (getIndicators() |> Async.RunSynchronously)
        let indicatorsIndexed = lazy (indicators.Force() |> Seq.map (fun i -> i.Id,i)  |> dict)
        let countries = lazy (getCountries [] |> Async.RunSynchronously)
        let countriesIndexed = lazy (countries.Force() |> Seq.map (fun i -> i.Id, i)  |> dict)
        let regions = lazy (getRegions() |> Async.RunSynchronously)
  
        member internal __.Topics = topics.Force()
        member internal __.Indicators = indicators.Force()
        member internal __.IndicatorsIndexed = indicatorsIndexed.Force()
        member internal __.Countries = countries.Force()
        member internal __.CountriesIndexed = countriesIndexed.Force()
        member internal __.Regions = regions.Force()
        /// At runtime, download the data
        member internal __.GetDataAsync(countryCode, indicatorCode, isRegion) = 
            async { let! data = 
                      getData
                        [ //yield (if isRegion then "regions" else "countries"); 
                          yield "countries"; 
                          yield countryCode
                          yield "indicators"; 
                          yield indicatorCode ]
                        [ "date", "1900:2050" ]
                        "date"
                    return 
                      seq { for (k, v) in data do
                              if not (String.IsNullOrEmpty v) then 
                                 yield int k, float v } 
                      // It's a time series - sort it :-)  We should probably also interpolate (e.g. see R time series library)
                      |> Seq.sortBy fst } 

        member internal x.GetData(countryCode, indicatorCode, isRegion) = 
             x.GetDataAsync(countryCode, indicatorCode, isRegion) |> Async.RunSynchronously
        member internal __._GetCountriesInRegion region = getCountries ["region", region] |> Async.RunSynchronously
  
type Indicator internal (connection:ServiceConnection, countryCode:string, isRegion: bool, indicatorCode:string) = 
    let data = connection.GetData(countryCode, indicatorCode, isRegion) |> Seq.cache
    let dataDict = lazy (dict data)
    /// Get the code for the country for the indicator
    member x.Code =  countryCode
    /// Get the code for the indicator
    member x.IndicatorCode = indicatorCode
    /// Get the name of the indicator
    member x.Name = connection.IndicatorsIndexed.[indicatorCode].Name
    /// Get the source of the indicator
    member x.Source = connection.IndicatorsIndexed.[indicatorCode].Source
    /// Get the description of the indicator
    member x.Description = connection.IndicatorsIndexed.[indicatorCode].Description
    /// Get a value for a year for the indicator
    member x.Item with get idx = dataDict.Force().[idx]
    /// Get the years for which the indicator has values
    member x.Years = dataDict.Force().Keys
    /// Get the values for the indicator (without years)
    member x.Values = dataDict.Force().Values
    interface seq<int * float> with member x.GetEnumerator() = data.GetEnumerator()
    interface System.Collections.IEnumerable with member x.GetEnumerator() = (data.GetEnumerator() :> _)
    //override x.ToString() = x.Name
    member x.GetValueAtOrZero(time:int) = 
        x |> Seq.tryPick (fun (x,y) -> if time = x then Some y else None)
          |> function None -> 0.0 | Some x -> x

type Indicators internal (connection:ServiceConnection, countryCode, isRegion) = 
    let indicators = seq { for indicator in connection.Indicators -> Indicator(connection, countryCode, false, indicator.Id) }
    /// Get the specified indicator for the country
    member x._GetIndicator(indicatorCode:string) = Indicator(connection, countryCode, isRegion, indicatorCode)
    member x._AsyncGetIndicator(indicatorCode:string) = async { return Indicator(connection, countryCode, isRegion, indicatorCode) }
    interface System.Collections.Generic.IEnumerable<Indicator>  with member x.GetEnumerator() = indicators.GetEnumerator()
    interface System.Collections.IEnumerable  with member x.GetEnumerator() = indicators.GetEnumerator() :> _


type Country internal (connection:ServiceConnection, countryCode:string) = 
    let indicators = new Indicators(connection, countryCode, false)
    /// Get the WorldBank code of the country
    member x.Code = countryCode
    /// Get the name of the country 
    member x.Name = connection.CountriesIndexed.[countryCode].Name
    //member x.Iso2Code = connection.CountriesIndexed.[countryCode].Iso2Code
    /// Get the indicators or the country
    member x._GetIndicator(indicatorCode:string) = indicators._GetIndicator(indicatorCode)
    member x._GetIndicators() = indicators
    //override x.ToString() = x.Name

type CountryCollection<'T when 'T :> Country> internal (connection: ServiceConnection, regionCodeOpt) = 
    let items = 
        seq { let countries = 
                  match regionCodeOpt with 
                  | None -> connection.Countries 
                  | Some r -> connection._GetCountriesInRegion(r)
              for country in countries  do 
                  if not country.IsRegion then 
                      yield Country(connection, country.Id) :?> 'T }  
    interface seq<'T> with member x.GetEnumerator() = items.GetEnumerator()
    interface System.Collections.IEnumerable with member x.GetEnumerator() = (items :> System.Collections.IEnumerable).GetEnumerator()
    member x._GetCountry(countryCode: string, name:string) = Country(connection, countryCode)

type Region internal (connection:ServiceConnection, regionCode:string) = 
    let indicators = new Indicators(connection, regionCode, true)
    /// Get the WorldBank code for the region
    member x.RegionCode = regionCode
    /// Get the countries in the region
    member x._GetCountries() = CountryCollection(connection,Some regionCode)
    /// Get the name of the region
    member x._GetIndicator(indicatorCode:string) = indicators._GetIndicator(indicatorCode)
    member x._GetIndicators() = indicators
    member x.Name = connection.Regions |> List.find (fun (code, _) -> code = regionCode) |> snd
    //override x.ToString() = x.Name

type RegionCollection<'T when 'T :> Region> internal (connection: ServiceConnection) = 
    let items = seq { for (code,_name) in connection.Regions  -> Region(connection, code) :?> 'T } 
    interface seq<'T> with member x.GetEnumerator() = items.GetEnumerator()
    interface System.Collections.IEnumerable with member x.GetEnumerator() = (items :> System.Collections.IEnumerable).GetEnumerator()
    member x._GetRegion(regionCode: string) = Region(connection, regionCode)

type WorldBankData(serviceUrl:string, sources:string) = 
    let sources = sources.Split([| ';' |], StringSplitOptions.RemoveEmptyEntries) |> Array.toList
    let restCache = createInternetFileCache "WorldBankRuntime" (TimeSpan.FromDays(7.0))
    let connection = new ServiceConnection(restCache,serviceUrl, sources)
    member x.ServiceLocation = serviceUrl
    //member x._GetCountry(countryCode: string) = Country(connection, countryCode)
    member x._GetCountries() = CountryCollection(connection, None) :> seq<_>
    member x._GetRegions() = RegionCollection(connection) :> seq<_>
    //member x._GetRegion(regionCode: string) = Region(connection, regionCode)

#endif