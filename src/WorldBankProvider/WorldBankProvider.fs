// --------------------------------------------------------------------------------------
// The World Bank type provider 
// --------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.Net
open System.Xml.Linq
open System.Web
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes
open FSharp.Data.Caching
open FSharp.Data.WorldBank.Runtime

// --------------------------------------------------------------------------------------

[<TypeProvider>]
type public WorldBankProvider(cfg:TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    let asm, isPortable, replacer = AssemblyResolver.init cfg
    let ns = "FSharp.Data" 

    let defaultServiceUrl = "http://api.worldbank.org"
    let restCache = createInternetFileCache "WorldBankSchema" (TimeSpan.FromDays(7.0))

    let createTypesForSources(sources, worldBankTypeName, asynchronous) = 

        let connection = ServiceConnection(restCache, defaultServiceUrl, sources)
 
        let resTy = ProvidedTypeDefinition(asm, ns, worldBankTypeName, baseType=Some typeof<obj>, HideObjectMethods=true)

        let conv (expr:Expr->Expr) (args:Expr list) = let arg0 = replacer.ToDesignTime args.[0] in replacer.ToRuntime (expr arg0)

        let serviceTypesType = 
            let t = ProvidedTypeDefinition("ServiceTypes", baseType=Some typeof<obj>, HideObjectMethods=true)
            t.AddXmlDoc("<summary>Contains the types that describe the data service</summary>")
            resTy.AddMember t
            t

        let indicatorsType =
            let t = ProvidedTypeDefinition("Indicators", baseType=Some (replacer.ToRuntime typeof<Indicators>), HideObjectMethods=true)
            t.AddMembersDelayed (fun () -> 
                [ for indicator in connection.Indicators do
                      let indicatorIdVal = indicator.Id
                      let prop = 
                        if asynchronous then 
                          let t = replacer.ToRuntime typeof<Async<Indicator>>
                          ProvidedProperty
                            ( indicator.Name, t, IsStatic=false,
                              GetterCode = conv (fun arg -> <@@ (%%arg : Indicators)._AsyncGetIndicator(indicatorIdVal) @@>))
                        else
                          let t = replacer.ToRuntime typeof<Indicator>
                          ProvidedProperty
                            ( indicator.Name, t, IsStatic=false,
                              GetterCode = conv (fun arg -> <@@ (%%arg : Indicators)._GetIndicator(indicatorIdVal) @@>))

                      if not (String.IsNullOrEmpty indicator.Description) then prop.AddXmlDoc(indicator.Description)
                      yield prop ] )
            serviceTypesType.AddMember t
            t

        let countryType =
            let t = ProvidedTypeDefinition("Country", baseType=Some (replacer.ToRuntime typeof<Country>), HideObjectMethods=true)
            t.AddMembersDelayed (fun () -> 
                [ let prop = ProvidedProperty("Indicators", indicatorsType, IsStatic=false,
                              GetterCode = conv (fun arg -> <@@ (%%arg : Country)._GetIndicators() @@>))
                  prop.AddXmlDoc("<summary>The indicators for the country</summary>")
                  yield prop ] )
            serviceTypesType.AddMember t
            t

        let countriesType =
            let countryCollectionType = ProvidedTypeBuilder.MakeGenericType(replacer.ToRuntime typedefof<CountryCollection<_>>, [ countryType ])
            let t = ProvidedTypeDefinition("Countries", baseType=Some countryCollectionType, HideObjectMethods=true)
            t.AddMembersDelayed (fun () -> 
                [ for country in connection.Countries do
                    let countryIdVal = country.Id
                    let name = country.Name
                    let prop = 
                        ProvidedProperty
                          ( name, countryType, IsStatic=false,
                            GetterCode = conv (fun arg -> <@@ (%%arg : CountryCollection<Country>)._GetCountry(countryIdVal, name) @@>))
                    prop.AddXmlDoc (sprintf "The data for '%s'" country.Name)
                    yield prop ])
            serviceTypesType.AddMember t
            t

        let regionType =
            let t = ProvidedTypeDefinition("Region", baseType=Some (replacer.ToRuntime typeof<Region>), HideObjectMethods=true)
            t.AddMembersDelayed (fun () -> 
                [ let prop = ProvidedProperty("Indicators", indicatorsType, IsStatic=false,
                               GetterCode = conv (fun arg -> <@@ (%%arg : Region)._GetIndicators() @@>))
                  prop.AddXmlDoc("<summary>The indicators for the region</summary>")
                  yield prop 
                  let prop = ProvidedProperty("Countries", countriesType, IsStatic=false,
                               GetterCode = conv (fun arg -> <@@ (%%arg : Region)._GetCountries() @@>))
                  prop.AddXmlDoc("<summary>The indicators for the region</summary>")
                  yield prop ] )
            serviceTypesType.AddMember t
            t

        let regionsType =
            let regionCollectionType = ProvidedTypeBuilder.MakeGenericType(replacer.ToRuntime typedefof<RegionCollection<_>>, [ regionType ])
            let t = ProvidedTypeDefinition("Regions", baseType=Some regionCollectionType, HideObjectMethods=true)
            t.AddMembersDelayed (fun () -> 
                [ for (code, name) in connection.Regions do
                    let prop = 
                        ProvidedProperty
                          ( name, regionType, IsStatic=false,
                            GetterCode = conv (fun arg -> <@@ (%%arg : RegionCollection<Region>)._GetRegion(code) @@>)) 
                    prop.AddXmlDoc (sprintf "The data for region '%s'" name)
                    yield prop ])
            serviceTypesType.AddMember t
            t
  
        let worldBankDataServiceType =
            let t = ProvidedTypeDefinition("WorldBankDataService", baseType=Some (replacer.ToRuntime typeof<WorldBankData>), HideObjectMethods=true)
            t.AddMembersDelayed (fun () -> 
                [ yield ProvidedProperty("Countries", countriesType, IsStatic=false, GetterCode = conv (fun arg -> <@@ (%%arg : WorldBankData)._GetCountries() @@>)) 
                  yield ProvidedProperty("Regions", regionsType, IsStatic=false, GetterCode = conv (fun arg -> <@@ (%%arg : WorldBankData)._GetRegions() @@>)) ])
            serviceTypesType.AddMember t
            t

        // TODO: show topics

        resTy.AddMembersDelayed (fun () -> 
            [ let urlVal = defaultServiceUrl
              let sourcesVal = sources |> String.concat ";"
              yield ProvidedMethod ("GetDataContext", [], worldBankDataServiceType, IsStaticMethod=true,
                                    InvokeCode = (fun _ -> replacer.ToRuntime <@@ WorldBankData(urlVal, sourcesVal) @@>)) 
              //TODO: overload to provide serviceUrl
            ])

        resTy

    do if not isPortable then

        // ASSUMPTION: Follow www.worldbank.org and only show these sources by default. The others are very sparsely populated.
        let defaultSources = [ "World Development Indicators"; "Global Development Finance"]

        let worldBankType = createTypesForSources(defaultSources, "WorldBank", false)

        let paramWorldBankType = 
            let t = ProvidedTypeDefinition(asm, ns, "WorldBankProvider", Some(typeof<obj>), HideObjectMethods = true)
        
            let defaultSourcesStr = String.Join(";", defaultSources)
            let helpText = "<summary>Typed representation of WorldBank data with additional configuration parameters</summary>
                            <param name='Sources'>The World Bank data sources to include, separated by semicolons. Defaults to \"" + defaultSourcesStr + "\"</param>
                            <param name='Asynchronous'>Generate asynchronous calls. Defaults to false</param>"
            t.AddXmlDoc(helpText)

            let parameters =
                [ ProvidedStaticParameter("Sources", typeof<string>, defaultSourcesStr)
                  ProvidedStaticParameter("Asynchronous", typeof<bool>, false) ]

            t.DefineStaticParameters(parameters, fun typeName providerArgs -> 
                let sources = (providerArgs.[0] :?> string).Split([| ';' |], StringSplitOptions.RemoveEmptyEntries) |> Array.toList
                let isAsync = providerArgs.[1] :?> bool
                createTypesForSources(sources, typeName, isAsync))
            t
        
        this.AddNamespace(ns, [ worldBankType; paramWorldBankType ])
