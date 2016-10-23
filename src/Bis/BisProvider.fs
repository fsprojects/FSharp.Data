// --------------------------------------------------------------------------------------
// The Bank for International Settlements (BIS) type provider 
// --------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation
open ProviderImplementation.ProvidedTypes

open FSharp.Data.Runtime.Bis

// --------------------------------------------------------------------------------------

[<TypeProvider>]
type public BisProvider(cfg:TypeProviderConfig) as this = 
    inherit DisposableTypeProviderForNamespaces()
    
    let asm, version, replacer = AssemblyResolver.init cfg
    let ns = "FSharp.Data"
    let datasetProvider = replacer.ProvidedTypeDefinition(asm, ns, "Bis", typeof<obj>, hideObjectMethods = false, nonNullable = false)
           
    do datasetProvider.DefineStaticParameters([ProvidedStaticParameter("pathToBisFile", typeof<string>)], fun typeName args ->
        let pathToDatasetFile = args.[0] :?> string

        let parser = createPraser pathToDatasetFile

        let dimensionTypes =  
            let dimesionTys = 
                parser.getDataset().dimensions
                    |> Seq.map (fun d -> 
                                    let p = ProvidedTypeDefinition(d.name, Some typeof<obj>, HideObjectMethods = true)
                                    d.members
                                        |> Seq.map (fun m -> ProvidedProperty(m, typeof<string>, IsStatic = true, GetterCode = fun args -> <@@ m.Substring(0, m.IndexOf(':')) @@> ))
                                        |> Seq.iter (fun m -> p.AddMember(m))
                                    p)
                    |> Seq.toList

            dimesionTys
            
//        type Observation(key : string, values : Map<string, option<float>>) =
//        class
//            member this.key = key
//            member this.values = values
//        end

        let observationType =
            let obsTy = ProvidedTypeDefinition("Observation", Some typeof<obj>)
            obsTy.AddMember <| ProvidedProperty("key", typeof<string>, GetterCode = (fun (_) -> <@@ "adf" @@>))
            obsTy

        let filterProvider =
            let set = parser.getDataset()

            // Observation filter type
            let filterTy = ProvidedTypeDefinition("QueryContext", Some typeof<obj>, HideObjectMethods = true)
            filterTy.AddMember <| ProvidedConstructor(parameters = [], InvokeCode = fun args -> <@@ new Dictionary<string, string list>() @@>)

            // Generate property per dataset dimension
            set.dimensions.Select(fun x -> x.name)
                |> Seq.map (fun d -> 
                                ProvidedProperty (
                                    d, 
                                    typeof<string list>, 
                                    IsStatic = false, 
                                    GetterCode = (fun args -> 
                                                    <@@ let dict = ((%%args.[0] : obj) :?> Dictionary<string,string list>)
                                                        if not (dict.ContainsKey d) then dict.Add (d, [])
                                                        dict.[d]
                                                     @@>),
                                    SetterCode = (fun args -> 
                                                    <@@ ((%%args.[0] : obj) :?>Dictionary<string,string list>).[d] <- (%%args.[1] : string list) @@>)))
                |> Seq.toList
                |> filterTy.AddMembers

            let getDatasetPath = ProvidedProperty("DataSource", typeof<string>, IsStatic = false, GetterCode = (fun _ -> <@@ pathToDatasetFile @@>))
            filterTy.AddMember getDatasetPath

            let getFilterMeth = ProvidedMethod("Get", [], typeof<Dictionary<string, string list>>) //Observation 
            getFilterMeth.InvokeCode <- (fun args -> 
                                <@@ 
                                        
                                    let dict = ((%%args.[0] : obj) :?> System.Collections.Generic.Dictionary<string,string list>)
                                    let obsFilter = new Dictionary<string, string list>()
                                    for f in dict.Where((fun d -> d.Value.Length > 0)) do
                                        obsFilter.Add(f.Key, f.Value)
                                    obsFilter
                                 @@>)
            
            filterTy.AddMember (getFilterMeth)
            filterTy

        
        let provider = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
        provider.AddMember <| ProvidedConstructor(parameters = [], InvokeCode = fun args -> <@@ new Dictionary<string, string list>() @@>)
        let alltypes = dimensionTypes.Union([filterProvider]).ToList() 
        alltypes.Add(observationType)
        provider.AddMembers(alltypes |> Seq.toList)
        provider)

    do this.AddNamespace(ns, [datasetProvider])