// --------------------------------------------------------------------------------------
// The World Bank type provider 
// --------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.Net
open System.Xml.Linq
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.Caching
open FSharp.Data.Runtime.WorldBank

// --------------------------------------------------------------------------------------

[<TypeProvider>]
type public WorldBankProvider(cfg:TypeProviderConfig) as this = 
    inherit DisposableTypeProviderForNamespaces()

    let bindingCtxt, asm, version, replacer = AssemblyResolver.init cfg // TODO: make this respect the binding context
    let ns = "FSharp.Data" 

    let defaultServiceUrl = "http://api.worldbank.org"
    let cacheDuration = TimeSpan.FromDays 30.0
    let restCache, _ = createInternetFileCache "WorldBankSchema" cacheDuration

    let createTypesForSources(sources, worldBankTypeName, asynchronous) = 

        ProviderHelpers.getOrCreateProvidedType this worldBankTypeName version cacheDuration <| fun () ->

        let connection = ServiceConnection(restCache, defaultServiceUrl, sources)
 
        let resTy = replacer.ProvidedTypeDefinition(asm, ns, worldBankTypeName, typeof<obj>, hideObjectMethods = true, nonNullable = true)

        let serviceTypesType = 
            let t = replacer.ProvidedTypeDefinition("ServiceTypes", typeof<obj>, hideObjectMethods = true, nonNullable = true)
            t.AddXmlDoc("<summary>Contains the types that describe the data service</summary>")
            resTy.AddMember t
            t

        let indicatorsType =
            let t = replacer.ProvidedTypeDefinition("Indicators", typeof<Indicators>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for indicator in connection.Indicators do
                      let indicatorIdVal = indicator.Id
                      let prop = 
                        if asynchronous then 
                          replacer.ProvidedProperty
                            ( indicator.Name, typeof<Async<Indicator>> , 
                              getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Indicators) :> IIndicators).AsyncGetIndicator(indicatorIdVal) @@>))
                        else
                          replacer.ProvidedProperty
                            ( indicator.Name, typeof<Indicator>, 
                              getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Indicators) :> IIndicators).GetIndicator(indicatorIdVal) @@>))

                      if not (String.IsNullOrEmpty indicator.Description) then prop.AddXmlDoc(indicator.Description)
                      yield prop ] )
            serviceTypesType.AddMember t
            t

        let indicatorsDescriptionsType =
            let t = replacer.ProvidedTypeDefinition("IndicatorsDescriptions", typeof<IndicatorsDescriptions> , hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for indicator in connection.Indicators do
                      let indicatorIdVal = indicator.Id
                      let prop = 
                          replacer.ProvidedProperty
                            ( indicator.Name, 
                              typeof<IndicatorDescription> , 
                              getterCode = (fun (Singleton arg) -> <@@ ((%%arg : IndicatorsDescriptions) :> IIndicatorsDescriptions).GetIndicator(indicatorIdVal) @@>))
                      if not (String.IsNullOrEmpty indicator.Description) then prop.AddXmlDoc(indicator.Description)
                      yield prop ] )
            serviceTypesType.AddMember t
            t

        let countryType =
            let t = replacer.ProvidedTypeDefinition("Country", baseType=(typeof<Country>), hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ let prop = replacer.ProvidedProperty("Indicators", indicatorsType, 
                              getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Country) :> ICountry).GetIndicators() @@>))
                  prop.AddXmlDoc("<summary>The indicators for the country</summary>")
                  yield prop ] )
            serviceTypesType.AddMember t
            t

        let countriesType =
            let countryCollectionType = ProvidedTypeBuilder.MakeGenericType(typedefof<CountryCollection<_>>, [ countryType ])
            let t = replacer.ProvidedTypeDefinition("Countries", countryCollectionType, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for country in connection.Countries do
                    let countryIdVal = country.Id
                    let name = country.Name
                    let prop = 
                        replacer.ProvidedProperty
                          ( name, countryType, 
                            getterCode = (fun (Singleton arg) -> <@@ ((%%arg : CountryCollection<Country>) :> ICountryCollection).GetCountry(countryIdVal, name) @@>))
                    prop.AddXmlDoc (sprintf "The data for country '%s'" country.Name)
                    yield prop ])
            serviceTypesType.AddMember t
            t

        let regionType =
            let t = replacer.ProvidedTypeDefinition("Region", typeof<Region>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ let prop = replacer.ProvidedProperty("Indicators", indicatorsType, 
                               getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Region) :> IRegion).GetIndicators() @@>))
                  prop.AddXmlDoc("<summary>The indicators for the region</summary>")
                  yield prop 
                  let prop = replacer.ProvidedProperty("Countries", countriesType, 
                               getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Region) :> IRegion).GetCountries() @@>))
                  prop.AddXmlDoc("<summary>The indicators for the region</summary>")
                  yield prop ] )
            serviceTypesType.AddMember t
            t

        let regionsType =
            let regionCollectionType = ProvidedTypeBuilder.MakeGenericType(typedefof<RegionCollection<_>>, [ regionType ])
            let t = replacer.ProvidedTypeDefinition("Regions", regionCollectionType, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for code, name in connection.Regions do
                    let prop = 
                        replacer.ProvidedProperty
                          ( name, regionType, 
                            getterCode = (fun (Singleton arg) -> <@@ ((%%arg : RegionCollection<Region>) :> IRegionCollection).GetRegion(code) @@>)) 
                    prop.AddXmlDoc (sprintf "The data for region '%s'" name)
                    yield prop ])
            serviceTypesType.AddMember t
            t
  
        let topicType =
            let t = replacer.ProvidedTypeDefinition("Topic", typeof<Topic>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ let prop = replacer.ProvidedProperty("Indicators", indicatorsDescriptionsType, 
                              getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Topic) :> ITopic).GetIndicators() @@>))
                  prop.AddXmlDoc("<summary>The indicators for the topic</summary>")
                  yield prop ] )
            serviceTypesType.AddMember t
            t

        let topicsType =
            let topicCollectionType = ProvidedTypeBuilder.MakeGenericType(typedefof<TopicCollection<_>>, [ topicType ])
            let t = replacer.ProvidedTypeDefinition("Topics", topicCollectionType, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for topic in connection.Topics do
                    let topicIdVal = topic.Id
                    let prop = 
                        replacer.ProvidedProperty
                          ( topic.Name, topicType, 
                            getterCode = (fun (Singleton arg) -> <@@ ((%%arg : TopicCollection<Topic>) :> ITopicCollection).GetTopic(topicIdVal) @@>))
                    if not (String.IsNullOrEmpty topic.Description) then prop.AddXmlDoc(topic.Description)
                    yield prop ])
            serviceTypesType.AddMember t
            t

        let worldBankDataServiceType =
            let t = replacer.ProvidedTypeDefinition("WorldBankDataService", typeof<WorldBankData>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ yield replacer.ProvidedProperty("Countries", countriesType,  getterCode = (fun (Singleton arg) -> <@@ ((%%arg : WorldBankData) :> IWorldBankData).GetCountries() @@>))
                  yield replacer.ProvidedProperty("Regions", regionsType,  getterCode = (fun (Singleton arg) -> <@@ ((%%arg : WorldBankData) :> IWorldBankData).GetRegions() @@>))
                  yield replacer.ProvidedProperty("Topics", topicsType,  getterCode = (fun (Singleton arg) -> <@@ ((%%arg : WorldBankData) :> IWorldBankData).GetTopics() @@>)) ])
            serviceTypesType.AddMember t
            t

        resTy.AddMembersDelayed (fun () -> 
            [ let urlVal = defaultServiceUrl
              let sourcesVal = sources |> String.concat ";"
              yield replacer.ProvidedMethod ("GetDataContext", [], worldBankDataServiceType, isStatic=true,
                                       invokeCode = (fun _ -> <@@ WorldBankData(urlVal, sourcesVal) @@>)) 
            ])

        resTy

    // ASSUMPTION: Follow www.worldbank.org and only show these sources by default. The others are very sparsely populated.
    let defaultSources = [ "World Development Indicators"; "Global Financial Development" ]

    let worldBankType = createTypesForSources(defaultSources, "WorldBankData", false)
    do worldBankType.AddXmlDoc "<summary>Typed representation of WorldBank data. See http://www.worldbank.org for terms and conditions.</summary>"

    let paramWorldBankType = 
        let t = replacer.ProvidedTypeDefinition(asm, ns, "WorldBankDataProvider", typeof<obj>, hideObjectMethods = true, nonNullable = true)
        
        let defaultSourcesStr = String.Join(";", defaultSources)
        let helpText = "<summary>Typed representation of WorldBank data with additional configuration parameters. See http://www.worldbank.org for terms and conditions.</summary>
                        <param name='Sources'>The World Bank data sources to include, separated by semicolons. Defaults to `" + defaultSourcesStr + "`.
                        If an empty string is specified, includes all data sources.</param>
                        <param name='Asynchronous'>Generate asynchronous calls. Defaults to false.</param>"
        t.AddXmlDoc(helpText)

        let parameters =
            [ ProvidedStaticParameter("Sources", typeof<string>, defaultSourcesStr)
              ProvidedStaticParameter("Asynchronous", typeof<bool>, false) ]

        t.DefineStaticParameters(parameters, fun typeName providerArgs -> 
            let sources = (providerArgs.[0] :?> string).Split([| ';' |], StringSplitOptions.RemoveEmptyEntries) |> Array.toList
            let isAsync = providerArgs.[1] :?> bool
            createTypesForSources(sources, typeName, isAsync))
        t
        
    do this.AddNamespace(ns, [ worldBankType; paramWorldBankType ])
