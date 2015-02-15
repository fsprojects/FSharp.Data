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

    let asm, version, replacer = AssemblyResolver.init cfg
    let ns = "FSharp.Data" 

    let defaultServiceUrl = "http://api.worldbank.org"
    let cacheDuration = TimeSpan.FromDays 30.0
    let restCache, _ = createInternetFileCache "WorldBankSchema" cacheDuration

    let createTypesForSources(sources, worldBankTypeName, asynchronous) = 

        ProviderHelpers.getOrCreateProvidedType this worldBankTypeName version cacheDuration <| fun () ->

        let connection = ServiceConnection(restCache, defaultServiceUrl, sources)
 
        let resTy = ProvidedTypeDefinition(asm, ns, worldBankTypeName, baseType=Some typeof<obj>, HideObjectMethods = true, NonNullable = true)

        let conv (expr:Expr->Expr) (args:Expr list) = let arg0 = replacer.ToDesignTime args.[0] in replacer.ToRuntime (expr arg0)

        let serviceTypesType = 
            let t = ProvidedTypeDefinition("ServiceTypes", baseType=Some typeof<obj>)
            t.AddXmlDoc("<summary>Contains the types that describe the data service</summary>")
            resTy.AddMember t
            t

        let indicatorsType =
            let t = ProvidedTypeDefinition("Indicators", baseType=Some (replacer.ToRuntime typeof<Indicators>), HideObjectMethods = true, NonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for indicator in connection.Indicators do
                      let indicatorIdVal = indicator.Id
                      let prop = 
                        if asynchronous then 
                          let t = replacer.ToRuntime typeof<Async<Indicator>>
                          ProvidedProperty
                            ( indicator.Name, t, IsStatic=false,
                              GetterCode = conv (fun arg -> <@@ ((%%arg : Indicators) :> IIndicators).AsyncGetIndicator(indicatorIdVal) @@>))
                        else
                          let t = replacer.ToRuntime typeof<Indicator>
                          ProvidedProperty
                            ( indicator.Name, t, IsStatic=false,
                              GetterCode = conv (fun arg -> <@@ ((%%arg : Indicators) :> IIndicators).GetIndicator(indicatorIdVal) @@>))

                      if not (String.IsNullOrEmpty indicator.Description) then prop.AddXmlDoc(indicator.Description)
                      yield prop ] )
            serviceTypesType.AddMember t
            t

        let indicatorsDescriptionsType =
            let t = ProvidedTypeDefinition("IndicatorsDescriptions", baseType=Some (replacer.ToRuntime typeof<IndicatorsDescriptions>), HideObjectMethods = true, NonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for indicator in connection.Indicators do
                      let indicatorIdVal = indicator.Id
                      let prop = 
                          let t = replacer.ToRuntime typeof<IndicatorDescription>
                          ProvidedProperty
                            ( indicator.Name, t, IsStatic=false,
                              GetterCode = conv (fun arg -> <@@ ((%%arg : IndicatorsDescriptions) :> IIndicatorsDescriptions).GetIndicator(indicatorIdVal) @@>))
                      if not (String.IsNullOrEmpty indicator.Description) then prop.AddXmlDoc(indicator.Description)
                      yield prop ] )
            serviceTypesType.AddMember t
            t

        let countryType =
            let t = ProvidedTypeDefinition("Country", baseType=Some (replacer.ToRuntime typeof<Country>), HideObjectMethods = true, NonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ let prop = ProvidedProperty("Indicators", indicatorsType, IsStatic=false,
                              GetterCode = conv (fun arg -> <@@ ((%%arg : Country) :> ICountry).GetIndicators() @@>))
                  prop.AddXmlDoc("<summary>The indicators for the country</summary>")
                  yield prop ] )
            serviceTypesType.AddMember t
            t

        let countriesType =
            let countryCollectionType = ProvidedTypeBuilder.MakeGenericType(replacer.ToRuntime typedefof<CountryCollection<_>>, [ countryType ])
            let t = ProvidedTypeDefinition("Countries", baseType=Some countryCollectionType, HideObjectMethods = true, NonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for country in connection.Countries do
                    let countryIdVal = country.Id
                    let name = country.Name
                    let prop = 
                        ProvidedProperty
                          ( name, countryType, IsStatic=false,
                            GetterCode = conv (fun arg -> <@@ ((%%arg : CountryCollection<Country>) :> ICountryCollection).GetCountry(countryIdVal, name) @@>))
                    prop.AddXmlDoc (sprintf "The data for country '%s'" country.Name)
                    yield prop ])
            serviceTypesType.AddMember t
            t

        let regionType =
            let t = ProvidedTypeDefinition("Region", baseType=Some (replacer.ToRuntime typeof<Region>), HideObjectMethods = true, NonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ let prop = ProvidedProperty("Indicators", indicatorsType, IsStatic=false,
                               GetterCode = conv (fun arg -> <@@ ((%%arg : Region) :> IRegion).GetIndicators() @@>))
                  prop.AddXmlDoc("<summary>The indicators for the region</summary>")
                  yield prop 
                  let prop = ProvidedProperty("Countries", countriesType, IsStatic=false,
                               GetterCode = conv (fun arg -> <@@ ((%%arg : Region) :> IRegion).GetCountries() @@>))
                  prop.AddXmlDoc("<summary>The indicators for the region</summary>")
                  yield prop ] )
            serviceTypesType.AddMember t
            t

        let regionsType =
            let regionCollectionType = ProvidedTypeBuilder.MakeGenericType(replacer.ToRuntime typedefof<RegionCollection<_>>, [ regionType ])
            let t = ProvidedTypeDefinition("Regions", baseType=Some regionCollectionType, HideObjectMethods = true, NonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for code, name in connection.Regions do
                    let prop = 
                        ProvidedProperty
                          ( name, regionType, IsStatic=false,
                            GetterCode = conv (fun arg -> <@@ ((%%arg : RegionCollection<Region>) :> IRegionCollection).GetRegion(code) @@>)) 
                    prop.AddXmlDoc (sprintf "The data for region '%s'" name)
                    yield prop ])
            serviceTypesType.AddMember t
            t
  
        let topicType =
            let t = ProvidedTypeDefinition("Topic", baseType=Some (replacer.ToRuntime typeof<Topic>), HideObjectMethods = true, NonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ let prop = ProvidedProperty("Indicators", replacer.ToRuntime indicatorsDescriptionsType, IsStatic=false,
                              GetterCode = conv (fun arg -> <@@ ((%%arg : Topic) :> ITopic).GetIndicators() @@>))
                  prop.AddXmlDoc("<summary>The indicators for the topic</summary>")
                  yield prop ] )
            serviceTypesType.AddMember t
            t

        let topicsType =
            let topicCollectionType = ProvidedTypeBuilder.MakeGenericType(replacer.ToRuntime typedefof<TopicCollection<_>>, [ topicType ])
            let t = ProvidedTypeDefinition("Topics", baseType=Some topicCollectionType, HideObjectMethods = true, NonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for topic in connection.Topics do
                    let topicIdVal = topic.Id
                    let prop = 
                        ProvidedProperty
                          ( topic.Name, topicType, IsStatic=false,
                            GetterCode = conv (fun arg -> <@@ ((%%arg : TopicCollection<Topic>) :> ITopicCollection).GetTopic(topicIdVal) @@>))
                    if not (String.IsNullOrEmpty topic.Description) then prop.AddXmlDoc(topic.Description)
                    yield prop ])
            serviceTypesType.AddMember t
            t

        let worldBankDataServiceType =
            let t = ProvidedTypeDefinition("WorldBankDataService", baseType=Some (replacer.ToRuntime typeof<WorldBankData>), HideObjectMethods = true, NonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ yield ProvidedProperty("Countries", countriesType, IsStatic=false, GetterCode = conv (fun arg -> <@@ ((%%arg : WorldBankData) :> IWorldBankData).GetCountries() @@>))
                  yield ProvidedProperty("Regions", regionsType, IsStatic=false, GetterCode = conv (fun arg -> <@@ ((%%arg : WorldBankData) :> IWorldBankData).GetRegions() @@>))
                  yield ProvidedProperty("Topics", topicsType, IsStatic=false, GetterCode = conv (fun arg -> <@@ ((%%arg : WorldBankData) :> IWorldBankData).GetTopics() @@>)) ])
            serviceTypesType.AddMember t
            t

        resTy.AddMembersDelayed (fun () -> 
            [ let urlVal = defaultServiceUrl
              let sourcesVal = sources |> String.concat ";"
              yield ProvidedMethod ("GetDataContext", [], worldBankDataServiceType, IsStaticMethod=true,
                                    InvokeCode = (fun _ -> replacer.ToRuntime <@@ WorldBankData(urlVal, sourcesVal) @@>)) 
            ])

        resTy

    // ASSUMPTION: Follow www.worldbank.org and only show these sources by default. The others are very sparsely populated.
    let defaultSources = [ "World Development Indicators"; "Global Financial Development" ]

    let worldBankType = createTypesForSources(defaultSources, "WorldBankData", false)
    do worldBankType.AddXmlDoc "<summary>Typed representation of WorldBank data. See http://www.worldbank.org for terms and conditions.</summary>"

    let paramWorldBankType = 
        let t = ProvidedTypeDefinition(asm, ns, "WorldBankDataProvider", Some typeof<obj>)
        
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
