// Copyright (c) Microsoft Corporation 2005-2012.
// This sample code is provided "as is" without warranty of any kind. 
// We disclaim all warranties, either express or implied, including the 
// warranties of merchantability and fitness for a particular purpose. 

/// Provides Freebase schema and data as provided types, properties and methods
namespace ProviderImplementation

open System
open System.Collections.Generic
open System.Globalization
open System.Reflection
open System.Linq
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.Caching
open FSharp.Data.Runtime.Freebase
open FSharp.Data.Runtime.Freebase.FreebaseRequests
open FSharp.Data.Runtime.Freebase.FreebaseSchema

type RFreebaseDomain = FSharp.Data.Runtime.Freebase.FreebaseDomain
type RFreebaseDomainCategory = FSharp.Data.Runtime.Freebase.FreebaseDomainCategory

module List = 
    /// Split a non-empty list into a pair consisting of
    /// its head and its tail
    let headAndTail l = 
        match l with 
        | [] -> invalidArg "l" "empty list" 
        | head::tail -> (head, tail)

    /// Split a non-empty list into a list with all elements 
    /// except for the last one and the last element
    let frontAndBack l = 
        let rec loop acc l = 
            match l with
            | [] -> invalidArg "l" "empty list" 
            | [singleton] -> List.rev acc, singleton
            | head::tail -> loop  (head::acc) tail
        loop [] l

/// Find the handles in the Freebase type provider runtime DLL. 
type internal FreebaseRuntimeInfo (config : TypeProviderConfig) =

    let asm, version, replacer = AssemblyResolver.init config

    member val FreebaseDataContextType =     typeof<FreebaseDataContext>     |> replacer.ToRuntime
    member val IFreebaseDataContextType =    typeof<IFreebaseDataContext>    |> replacer.ToRuntime
    member val FreebaseIndividualsType =     typeof<FreebaseIndividuals>     |> replacer.ToRuntime
    member val IFreebaseIndividualsType =    typeof<IFreebaseIndividuals>    |> replacer.ToRuntime
    member val IFreebaseObjectType =         typeof<IFreebaseObject>         |> replacer.ToRuntime
    member val FreebaseDomainType =          typeof<RFreebaseDomain>         |> replacer.ToRuntime
    member val IFreebaseDomainType =         typeof<IFreebaseDomain>         |> replacer.ToRuntime
    member val FreebaseDomainCategoryType =  typeof<RFreebaseDomainCategory> |> replacer.ToRuntime
    member val IFreebaseDomainCategoryType = typeof<IFreebaseDomainCategory> |> replacer.ToRuntime

    member this.RuntimeAssembly = asm
    member this.RuntimeVersion = version

type internal DomainId = KnownDomain of FreebaseId | UnknownDomain

/// This is the Freebase type provider.    
[<TypeProvider>]
type public FreebaseTypeProvider(config : TypeProviderConfig) as this = 
    inherit DisposableTypeProviderForNamespaces()

    let fbRuntimeInfo = FreebaseRuntimeInfo(config)

    /// Root namespace of Freebase types
    let rootNamespace = "FSharp.Data"
    let createTypes(apiKey, serviceUrl, rootTypeName, numIndividuals, useUnits, usePluralize, snapshotDate, useLocalCache, allowQueryEvaluateOnClientSide, useRefinedTypesForItems) = 

        ProviderHelpers.getOrCreateProvidedType this rootTypeName fbRuntimeInfo.RuntimeVersion FreebaseRequests.cacheDuration <| fun () ->

        let fb = new FreebaseQueries(apiKey, serviceUrl, "FreebaseSchema", snapshotDate, useLocalCache)
        let fbSchema = new FreebaseSchemaConnection(fb)
        let tidyName(value:string) = value.Replace("&amp;","&")
        //fb.SendingRequest.Add(fun arg -> printfn "MD: %s" arg.AbsoluteUri)

        let firstCap (s:string) = s.[0..0].ToUpperInvariant() + s.[1..]

        let typeNameForDomainObjects(domainName:string) = 
            // If the domain already contains spaces then we might as well make its type have a space before Domain since quoting will be required anyway.
            (if domainName.Contains(" ") then domainName+" Domain" else domainName+"Domain") |> tidyName

    #if FX_NO_SECURITY_ELEMENT_ESCAPE
        let xmlDoc (text:string) = "<summary>" + text + "</summary>"
    #else
        let xmlDoc text = "<summary>" + System.Security.SecurityElement.Escape text + "</summary>"
    #endif

        let blurbOfId id = fbSchema.GetBlurbById id |> String.concat " "  
        let blurbOfMachineId id = fbSchema.GetBlurbByMachineId id |> String.concat " "  

        let createDataContext = fbRuntimeInfo.FreebaseDataContextType.GetMethod "_Create"
        let getDomainCategoryById = fbRuntimeInfo.IFreebaseDataContextType.GetMethod "GetDomainCategoryById"
        let getDomainById = fbRuntimeInfo.IFreebaseDomainCategoryType.GetMethod "GetDomainById"
        let getObjectsOfTypeId = fbRuntimeInfo.IFreebaseDomainType.GetMethod "GetObjectsOfTypeId"
        let getIndividualsObject = fbRuntimeInfo.FreebaseIndividualsType.GetMethod "_GetIndividualsObject"
        let getIndividualById = fbRuntimeInfo.IFreebaseIndividualsType.GetMethod "GetIndividualById"

        let domains = 
            lazy 
                [ for nsk in fbSchema.GetDomainStructure().NamespaceKeys do 
                    if nsk.Namespace.Hidden <> "true" then
                      if nsk.Namespace.NamespaceKinds |> Array.exists (fun s -> s = "/type/domain") then
                        yield nsk.Namespace ]
                |> Seq.distinctBy (fun nsp -> (nsp.DomainId.Id, nsp.DomainName))

        let domainCategories = lazy fbSchema.GetDomainCategories()  

        let domainTypes = 
           lazy 
              let allRealDomains = 
                  [ for domain in domains.Force() do
                       if not (String.IsNullOrWhiteSpace domain.DomainId.Id) then 
                           yield (KnownDomain domain.DomainId, lazy (fbSchema.GetAllTypesInDomainSansProperties(domain.DomainId)) ) ]
              dict 
                [ yield! allRealDomains
                  yield (UnknownDomain, 
                         lazy 
                                let hashSet = HashSet()
                                for (_, lazyTypesInDomain) in allRealDomains do 
                                    for ty in lazyTypesInDomain.Force() do
                                       hashSet.Add ty.TypeId |> ignore
                                [ for ty in fbSchema.GetAllTypesInAllDomainsSansProperties() do
                                      if not (hashSet.Contains ty.TypeId) then
                                          yield ty ] :> seq<_>) ]

        let getDomains() = domains.Force()
        let getDomainCategories() = domainCategories.Force()
        let getTypesOfDomain(domainId) = domainTypes.Force().[domainId].Force()

        let theServiceTypesClass = ProvidedTypeDefinition("ServiceTypes",baseType=Some typeof<obj>,HideObjectMethods=true)
        theServiceTypesClass.AddXmlDoc (xmlDoc "Contains the types defined in the web data store")

        let pluralize = if usePluralize then NameUtils.pluralize else id
    
        let containerTypeNameForDomainTypes (fbDomainIdOpt:DomainId) = 
            match fbDomainIdOpt with 
            | UnknownDomain -> "Uncategorized"
            | KnownDomain fbDomainId -> fbDomainId.Id.TrimStart('/').Replace('/',' ') |> firstCap

        let pathToTypeForFreebaseTypeId (fbDomainId:FreebaseId, fbTypeId:FreebaseId) = 
            let domainPath = [containerTypeNameForDomainTypes  (match fbDomainId.Id with null | "" -> UnknownDomain | _ -> KnownDomain fbDomainId)]
            let path, final = fbTypeId.Id.Split '/' |> List.ofArray |> List.frontAndBack 
            match path with
            | [] -> failwith "Unexpected 9078543"
            | [_hd] -> failwith "Unexpected 0984509"
            | _empty::path -> List.map firstCap  domainPath @ List.map firstCap path @ [firstCap final + "Data"]

        /// Given a description of a Freebase type, return the path where the corresponding provided type lives 
        /// under FreebaseData.DataTypes.
        let pathToTypeForFreebaseType (fbType:FreebaseType) = 
            pathToTypeForFreebaseTypeId (fbType.Domain, fbType.TypeId)

        /// Given a description of a Freebase type, find the corresponding provided type that lives 
        /// under FreebaseData.DataTypes.
        let tryFindTypeForFreebaseType (root:System.Type, path:string list) = 
            // The first fragment has theServiceTypesClass as parent
            (Some root, path) ||> List.fold (fun parent name -> 
                match parent with 
                | None -> None 
                | Some parent -> 
                    match parent.GetNestedType (name, BindingFlags.Public ||| BindingFlags.NonPublic) with 
                    | :? ProvidedTypeDefinition as p -> Some (p  :> System.Type)
                    | _ -> None ) 

        let tryFindRefinedTypeForFreebaseType fbType = 
            let path = pathToTypeForFreebaseType fbType 
            tryFindTypeForFreebaseType (theServiceTypesClass , path)

        let tryFindRefinedTypeForFreebaseTypeId fbTypeId = 
            let path = pathToTypeForFreebaseTypeId fbTypeId 
            tryFindTypeForFreebaseType (theServiceTypesClass , path)

        let tryFindRefinedCollectionTypeForFreebaseType fbType = 
            let path = pathToTypeForFreebaseType fbType 
            let f,b = List.frontAndBack path
            let path = f @ [b + "Collection"]
            tryFindTypeForFreebaseType (theServiceTypesClass, path)

        // PARAMETER: Unit normalization
        let refinedFSharpTypeOfFreebaseProperty (fp: FreebaseProperty) =
            // NOTE: if you alter this mapping, see GetProperty in FreebaseRuntime.fs
            match fp.ExpectedTypeId.Id with 
            | "/type/float" -> 
                match fp.UnitOfMeasureId.Id with 
                | u when useUnits && units.ContainsKey u -> 
                        let (measureAnnotation,_multipler,_offset) = units.[u]
                        let rec trans u = 
                            match u with 
                            | SI s -> ProvidedMeasureBuilder.Default.SI s
                            | Prod(u1,u2) -> ProvidedMeasureBuilder.Default.Product (trans u1, trans u2)
                            | Div(u1,u2) -> ProvidedMeasureBuilder.Default.Ratio (trans u1, trans u2)
                            | One -> ProvidedMeasureBuilder.Default.One
                        let floatWithMeasureAnnotation = ProvidedMeasureBuilder.Default.AnnotateType(typeof<double>,[trans measureAnnotation])
                        Some (floatWithMeasureAnnotation, false)
                | _ -> 
                    Some (typeof<double>, false)
            | _ -> None
        let makeDesignTimeNullableTy (ty:Type) = ProvidedTypeBuilder.MakeGenericType(typedefof<Nullable<_>>, [ ty ])
        let makeDesignTimeSeqTy (ty:Type) = ProvidedTypeBuilder.MakeGenericType(typedefof<seq<_>>, [ ty ])
        let makeDesignTimeQueryableTy (ty:Type) = ProvidedTypeBuilder.MakeGenericType(typedefof<IQueryable<_>>, [ ty ])

        let makeMemberForFreebaseProperty (typeWithProperties: FreebaseType, property: FreebaseProperty, alwaysThere: bool) =
            [ if not (String.IsNullOrEmpty property.PropertyName) then 
                let staticPropertyType = property.FSharpPropertyType(fbSchema, refinedFSharpTypeOfFreebaseProperty, tryFindRefinedTypeForFreebaseType, makeDesignTimeNullableTy, makeDesignTimeSeqTy, alwaysThere)
                let runtimePropertyType = property.FSharpPropertyRuntimeType(fbSchema, fbRuntimeInfo.IFreebaseObjectType,alwaysThere)
                let (?) = QuotationBuilder.(?)
                let p = ProvidedProperty(property.PropertyName, staticPropertyType,
                                            GetterCode = 
                                                ((fun args -> 
                                                  if alwaysThere then 
                                                    fbRuntimeInfo.IFreebaseObjectType?GetRequiredPropertyByIdTyped (runtimePropertyType)
                                                                                                                            (args.[0], typeWithProperties.TypeId.Id, property.PropertyId.Id)
                                                   else
                                                    fbRuntimeInfo.IFreebaseObjectType?GetPropertyByIdTyped (runtimePropertyType)
                                                                                                                            (args.[0], typeWithProperties.TypeId.Id, property.PropertyId.Id))))
                p.AddXmlDocDelayed(fun () -> blurbOfId property.PropertyId |> xmlDoc)
                yield p
                ]

        /// Given a description of a Freebase type, make the members for the corresponding provided type that lives 
        /// under FreebaseData.DataTypes.  
        let makeMembersForFreebaseType (fbType: FreebaseType) =
            [ let typeWithProperties = fbSchema.GetTypeByTypeId fbType.TypeId
              match typeWithProperties with
              | None -> ()
              | Some typeWithProperties -> 
                  for property in typeWithProperties.Properties do
                      yield! makeMemberForFreebaseProperty(typeWithProperties, property, false) ]

        let getRefinedTypeOfItem (itemName, itemType, fbObj: FreebaseObjectId, placeToPutType: ProvidedTypeDefinition) = 
            if useRefinedTypesForItems then 
                // This is a bit cheeky as the item type is added after the "ServiceTypes" collection may have been iterated.
                // TODO: add the type using a delayed structure and dereference/link to the type here, rather than creating it here.
                let itemRefinedType = ProvidedTypeDefinition(itemName + " Item",baseType = Some (itemType :> Type), HideObjectMethods = true)
                itemRefinedType.AddXmlDocDelayed(fun () -> "<summary>The type representing the refined view of item '" + itemName + "'</summary>")

                let allTypesForItemLazy = lazy fbSchema.GetAllTypesOfObject(fbObj.MachineId).TypesSupportedByObject 
                // Make this object inherit from all types that this specific object supports
                itemRefinedType.AddInterfaceImplementationsDelayed(fun () -> 
                    let allTypesForItem = allTypesForItemLazy.Force()
                    [ for ity in allTypesForItem do 
                        match tryFindRefinedTypeForFreebaseTypeId (ity.DomainId, ity.TypeId) with 
                        | Some i -> yield i
                        | None -> 
                            //System.Diagnostics.Debug.Assert(false,"included type not found")
                            () ])

                // Add obsolete members for all the properties which are not available for this item. This hides them
                // and gives a warning if they are used.
                itemRefinedType.AddMembersDelayed(fun () -> 
                    //  Get all the types supported by the item
                    let allTypesForItem = allTypesForItemLazy.Force()
                    let allTypeIdsForitem = 
                        [ for oneTypeForItem in allTypesForItem do 
                            match fbSchema.GetTypeByTypeId(oneTypeForItem.TypeId) with 
                            | None -> () // If getting the type failed for some reason then don't supply its properties
                            | Some fbOneTypeForItem -> 
                            match tryFindRefinedTypeForFreebaseTypeId (oneTypeForItem.DomainId, oneTypeForItem.TypeId) with 
                            | None -> () // If the type for the item doesn't exist in our view of the world, then don't supply its properties
                            | Some _ -> 
                               yield fbOneTypeForItem ]
                    let fbObjData = fbSchema.GetDataExistenceForSpecificObject(allTypeIdsForitem, fbObj.MachineId)
                    [ for (fbOneTypeForItem, fbProps) in fbObjData do
                       for (fbProp, hasData) in fbProps do    
                        // Make the properties based on strong knowlege of the presence of data
                        for p in makeMemberForFreebaseProperty (fbOneTypeForItem, fbProp, true) do 
                            //printfn "property %s of type %s, hasData = %b" fbProp.PropertyName fbOneTypeForItem.TypeName hasData
                            if not hasData then 
                                p.AddObsoleteAttribute("The property '" + fbProp.PropertyName + "' is not available for the item '" + itemName + "'", isError=true) 
                            yield p ])


                placeToPutType.AddMember itemRefinedType
                itemRefinedType
            else
                itemType

        let insertFreebaseTypesForOneDomain (theDataTypesClassForDomain,domainId) = 
        
            //printfn "FreebaseProvider: inserting types for domain '%+A'" domainId
            let allTypesForDomainSansProperties = getTypesOfDomain domainId
            // Collect up the immediate nested types for the domain type
            let theNestedTypesForTheDataTypesClassForDomain = ResizeArray<_>()
            /// Holds all enclosing type definitions we've created so far in this domain.
            let enclosingTypeHash = Dictionary<(ProvidedTypeDefinition * string),ProvidedTypeDefinition>(HashIdentity.Structural)  
    
            /// Find an enclosing type on the path through the type space, create it if it is not created yet
            let findOrCreateEnclosingType (parentType:ProvidedTypeDefinition) name =
                let key = (parentType,name)
                match enclosingTypeHash.TryGetValue key with 
                | false,_ -> 
                    let t = ProvidedTypeDefinition(name, baseType=Some typeof<obj>,HideObjectMethods=true)
                    t.HideObjectMethods <- true
                    t.AddXmlDoc (xmlDoc "Contains a subset of the types defined in the web data store")
                    enclosingTypeHash.Add(key, t)
                    if Object.ReferenceEquals(parentType, theDataTypesClassForDomain) then 
                        theNestedTypesForTheDataTypesClassForDomain.Add t
                    else
                        parentType.AddMember t
                    t
                | _,t -> 
                    t

            for fbType in allTypesForDomainSansProperties do
                let fullPath = pathToTypeForFreebaseType fbType
                let path, typeName = List.frontAndBack fullPath
                let _domain, path = List.headAndTail path
                let declaringType = (theDataTypesClassForDomain, path) ||> List.fold findOrCreateEnclosingType
                //printfn "FreebaseProvider: creating item type, typeName='%A', fullPath='%A', domainId '%+A', declaringType.Name = '%s'" typeName fullPath domainId declaringType.Name
                let itemType = 
                    let t = ProvidedTypeDefinition(typeName, baseType=Some fbRuntimeInfo.IFreebaseObjectType, HideObjectMethods=true)
                    t.SetAttributes (TypeAttributes.Public ||| TypeAttributes.Interface ||| enum (int32 TypeProviderTypeAttributes.IsErased))
                    t.AddInterfaceImplementationsDelayed(fun () -> [fbRuntimeInfo.IFreebaseObjectType])
                    t.AddMembersDelayed (fun () -> makeMembersForFreebaseType fbType)
                    t.AddInterfaceImplementationsDelayed(fun () -> 
                      [ for ity in fbType.IncludedTypes do 
                          match tryFindRefinedTypeForFreebaseTypeId (ity.DomainId, ity.TypeId) with 
                          | Some i -> yield i
                          | None -> 
                              //System.Diagnostics.Debug.Assert(false,"included type not found")
                              () ])

                    t.AddXmlDocDelayed (fun () -> blurbOfId fbType.TypeId |> xmlDoc)
                    t

                //printfn "FreebaseProvider: creating individuals type, typeName='%A', fullPath='%A', domainId '%+A', declaringType.Name = '%s'" typeName fullPath domainId declaringType.Name
                let individualsTypeOpt = 
                    let membersForIndividuals(t, n, prefix) = 

                        [ for fbObj in fbSchema.GetAllObjectsOfType(fbType, n, prefix) |> Seq.distinctBy (fun x -> x.ObjectName) do 
                            let itemName = fbObj.ObjectName
                            let itemRefinedType =  getRefinedTypeOfItem (itemName, itemType, fbObj, t)
                            let p = ProvidedProperty(itemName, itemRefinedType,
                                                    GetterCode = (fun args -> Expr.Call(args.[0], getIndividualById,[Expr.Value fbType.TypeId.Id;Expr.Value fbObj.MachineId.MId])))
                            p.AddXmlDocDelayed(fun () -> blurbOfMachineId fbObj.MachineId |> xmlDoc)
                            yield (p :> MemberInfo) ]

                    if numIndividuals > 0 then 
                        
                        let t1 = ProvidedTypeDefinition(itemType.Name + "Individuals", baseType=Some fbRuntimeInfo.FreebaseIndividualsType,HideObjectMethods=true)
                        t1.AddXmlDoc (xmlDoc (sprintf "Represents a sample set of specific named individuals of type '%s' in the web data store" fbType.TypeName))
                        t1.AddMembersDelayed(fun () -> membersForIndividuals(t1, numIndividuals, None))

                        let tAZ = ProvidedTypeDefinition(itemType.Name + "IndividualsAZ", baseType=Some fbRuntimeInfo.FreebaseIndividualsType,HideObjectMethods=true)
                        tAZ.AddXmlDoc (xmlDoc (sprintf "Represents an indexing of specific named individuals of type '%s' in the web data store" fbType.TypeName))

                        // Also provide an index 
                        //    .IndividualsIndexed.A
                        //    ...
                        //    .IndividualsIndexed.Z
                        //
                        // where each list contains 5x as many entries
                        tAZ.AddMembersDelayed(fun () -> 

                                // Make the types for the A-Z properties
                                let tCs = 
                                    [ for c in [ for c1 in 'A' .. 'Z' do 
                                                    yield string c1 ] (* @ [ for c in '0' .. '9' -> string c  ] @  [ "Other" ] *) do 
                                        let tC = ProvidedTypeDefinition(itemType.Name + "IndividualsIndexed" + c, baseType=Some fbRuntimeInfo.FreebaseIndividualsType,HideObjectMethods=true)
                                        tC.AddXmlDoc (xmlDoc (sprintf "Represents an indexing of specific named individuals of type '%s' in the web data store" fbType.TypeName))
                                        tC.AddMembersDelayed(fun () ->  membersForIndividuals(tC, numIndividuals*10, Some c))
                                        yield (c, tC)
                                    ]
                                [ for (c,tC) in tCs do 
                                    let pC = ProvidedProperty(c, tC, GetterCode = (fun args -> args.[0]))
                                    pC.AddXmlDoc (xmlDoc (sprintf "An indexing of specific named individuals of type '%s' in the web data store" fbType.TypeName))
                                    yield (pC :> MemberInfo) 
                                    yield (tC :> MemberInfo) ])

                        let t10 = ProvidedTypeDefinition(itemType.Name + "Individuals10", baseType=Some fbRuntimeInfo.FreebaseIndividualsType,HideObjectMethods=true)
                        t10.AddXmlDoc (xmlDoc (sprintf "Represents a 10x larger sample of specific named individuals of type '%s' in the web data store" fbType.TypeName))
                        t10.AddMembersDelayed(fun () -> membersForIndividuals(t10, numIndividuals*10, None))

                        let t100 = ProvidedTypeDefinition(itemType.Name + "Individuals100", baseType=Some fbRuntimeInfo.FreebaseIndividualsType,HideObjectMethods=true)
                        t100.AddXmlDoc (xmlDoc (sprintf "Represents a 100x larger sample of specific named individuals of type '%s' in the web data store" fbType.TypeName))
                        t100.AddMembersDelayed(fun () -> membersForIndividuals(t100, numIndividuals*100, None))

                        Some (t1, tAZ, t10, t100)
                    else 
                        None

                //printfn "FreebaseProvider: creating collection type, typeName='%A', fullPath='%A', domainId '%+A', declaringType.Name = '%s'" typeName fullPath domainId declaringType.Name
                let collectionsType = 
                    let t = ProvidedTypeDefinition(itemType.Name + "Collection", baseType=Some typeof<obj>,HideObjectMethods=true)
                    t.HideObjectMethods <- true
                    t.AddInterfaceImplementationsDelayed (fun () -> [ makeDesignTimeQueryableTy itemType ])
                    t.AddXmlDoc (xmlDoc (sprintf "Represents the collection of all individuals of type '%s' in the web data store" fbType.TypeName))                
                    t.AddMembersDelayed(fun () -> 

                        [ match individualsTypeOpt with 
                          | None -> ()
                          | Some (t1, tAZ, t10, t100) -> 
                            let pIndividuals = ProvidedProperty("Individuals", t1, GetterCode = (fun args -> Expr.Call(getIndividualsObject, [ Expr.Coerce(args.[0], typeof<obj>) ])))
                            pIndividuals.AddXmlDocDelayed(fun () -> sprintf "A sample set of named individuals (up to %d in size) of type '%s' in the web data store" numIndividuals fbType.TypeName |> xmlDoc)
                            yield pIndividuals

                            let pIndividualsAZ = ProvidedProperty("IndividualsAZ", tAZ, GetterCode = (fun args -> Expr.Call(getIndividualsObject, [ Expr.Coerce(args.[0], typeof<obj>) ])))
                            pIndividualsAZ.AddXmlDocDelayed(fun () -> sprintf "A sample of named individuals of type '%s', (bucketed by first character with each bucket up to %d in size). The buckets may be slow to explore" fbType.TypeName (numIndividuals*10) |> xmlDoc)
                            yield pIndividualsAZ

                            let pIndividuals10 = ProvidedProperty("Individuals10", t10, GetterCode = (fun args -> Expr.Call(getIndividualsObject, [ Expr.Coerce(args.[0], typeof<obj>) ])))
                            pIndividuals10.AddXmlDocDelayed(fun () -> sprintf "A 10x larger sample set of named individuals of type '%s' in the web data store. This property may be slower to explore" fbType.TypeName |> xmlDoc)
                            yield pIndividuals10

                            let pIndividuals100 = ProvidedProperty("Individuals100", t100, GetterCode = (fun args -> Expr.Call(getIndividualsObject, [ Expr.Coerce(args.[0], typeof<obj>) ])))
                            pIndividuals100.AddXmlDocDelayed(fun () -> sprintf "A 100x larger sample set of named individuals of type '%s' in the web data store. This property may be very slow to explore" fbType.TypeName |> xmlDoc)
                            yield pIndividuals100]
                        )
                    t

                //printfn "FreebaseProvider: adding types as members, typeName='%A', fullPath='%A', domainId '%+A', declaringType.Name = '%s'" typeName fullPath domainId declaringType.Name
                declaringType.AddMember itemType
                match individualsTypeOpt with 
                | None -> ()
                | Some (t1, tAZ, t10, t100) ->
                   declaringType.AddMember t1
                   declaringType.AddMember tAZ
                   declaringType.AddMember t10
                   declaringType.AddMember t100
                declaringType.AddMember collectionsType

            //printfn "FreebaseProvider: done inserting types for domain '%+A'" domainId
            theNestedTypesForTheDataTypesClassForDomain |> Seq.toArray

        /// Lazily populate all the entries under FreebaseData.ServiceTypes. 
        do theServiceTypesClass.AddMembersDelayed (fun () -> 

            let makeTypeForFreebaseDomainTypes(domainId:DomainId, domainName:string) = 
                let domainTypeName = containerTypeNameForDomainTypes domainId
                let theDataTypesClassForDomain = ProvidedTypeDefinition(domainTypeName,baseType=Some typeof<obj>,HideObjectMethods=true)
                theDataTypesClassForDomain.AddXmlDoc (xmlDoc "Contains the types defined in the web data store for domain '" + domainName + "'")
                theDataTypesClassForDomain.AddMembersDelayed(fun () -> insertFreebaseTypesForOneDomain (theDataTypesClassForDomain,domainId) |> Array.toList) 
                theDataTypesClassForDomain

            try
                [ for domain in getDomains() do 
                    yield makeTypeForFreebaseDomainTypes (KnownDomain domain.DomainId, domain.DomainName)
                  yield makeTypeForFreebaseDomainTypes (UnknownDomain, "Uncategorized") ]
            with _ -> [])

        /// Make the type that corresponds to a Freebase domain. The type lives under DomainObjects. Under FreebaseData you will
        /// also find a single property whose type is this domain type.
        let makeTypeForFreebaseDomainObjects(domainInfo: FreebaseDomain) = 
    
            let domainTypeName = typeNameForDomainObjects domainInfo.DomainName

            let t = ProvidedTypeDefinition(domainTypeName,baseType=Some fbRuntimeInfo.FreebaseDomainType,HideObjectMethods=true)
            t.AddXmlDocDelayed (fun () -> blurbOfId domainInfo.DomainId |> xmlDoc)
            t.HideObjectMethods <- true

            t.AddMembersDelayed(fun () -> 
    
                [ for childType in getTypesOfDomain (KnownDomain domainInfo.DomainId) do
                      // Note, don't include mediator types in the all-objects-categorized-by-type presentation
                      if not(String.IsNullOrEmpty(childType.TypeName)) && childType.Mediator <> "true" then
                          match tryFindRefinedCollectionTypeForFreebaseType childType with 
                          | None -> ()
                          | Some collectionType -> 
                        
                              let p = ProvidedProperty(pluralize childType.TypeName, collectionType, 
                                                        GetterCode = (fun args -> Expr.Call(args.[0],getObjectsOfTypeId,[Expr.Value childType.TypeId.Id])))
                              if childType.Deprecated="true" then 
                                  p.AddObsoleteAttribute "This type is marked 'deprecated' in the data store"
                              p.AddXmlDocDelayed (fun () -> blurbOfId childType.TypeId |> xmlDoc) 
                              yield p ])
                    
            t

        /// Make the class that holds all the nested classes that hold all the objects in different Freebase domains categorized by type.
        let theDomainObjectsClass =
            let t = ProvidedTypeDefinition("DomainObjects",baseType=Some typeof<obj>,HideObjectMethods=true)
            t.AddXmlDoc (xmlDoc "Contains the domains of the domain categories defined in the web data store")
            t.AddMembersDelayed(fun () -> 
                        [ for domainInfo in getDomains() do
                              yield makeTypeForFreebaseDomainObjects domainInfo ]) 
            t

        let theServiceType = ProvidedTypeDefinition("FreebaseService",baseType=Some fbRuntimeInfo.FreebaseDataContextType, HideObjectMethods=true)
        do theServiceType.AddXmlDoc "Represents the information available in the web data store. See www.freebase.com for terms and conditions."

        /// Populate the root type (FreebaseData) with properties, one for each Freebase domain. Also include the DomainObjects 
        /// and DataTypes classes.
        do theServiceType.AddMembersDelayed (fun () -> 
            let domainCategories = getDomainCategories()
            try
                [ for domainCategory in domainCategories do
                    if domainCategory.Domains |> Array.exists (fun c -> not c.DomainHidden) then 
                      if not (domainCategory.DomainCategoryId.Id.StartsWith("/user/")) &&
                         not (domainCategory.Domains |> Array.forall (fun c -> c.DomainId.Id.StartsWith("/user/"))) then
                        let domainCategoryName = domainCategory.Name.Replace("&amp;", "and")
                        let t = ProvidedTypeDefinition(domainCategoryName,baseType=Some fbRuntimeInfo.FreebaseDomainCategoryType,HideObjectMethods=true)
                        t.AddXmlDoc (xmlDoc (sprintf "Represents the objects of the domain category '%s' defined in the web data store organized by type" domainCategory.Name))
                        t.AddMembersDelayed(fun () -> 
                            [ for domainInfo in domainCategory.Domains do
                                if not domainInfo.DomainHidden then 
                                  let domainName = domainInfo.DomainName
                                  let domainTypeName = typeNameForDomainObjects domainName
                                  let domainType = theDomainObjectsClass.GetNestedType (domainTypeName, BindingFlags.Public ||| BindingFlags.NonPublic)
                                  if domainType <> null then
                                    let propertyName = tidyName domainName
                                    let pi = ProvidedProperty(propertyName, domainType, IsStatic=false, 
                                                              GetterCode = (fun args -> Expr.Call(args.[0], getDomainById,[Expr.Value(domainInfo.DomainId.Id)])))
                                    pi.AddXmlDocDelayed (fun () -> blurbOfId domainInfo.DomainId |> xmlDoc)
                                    yield pi]) 
                        theDomainObjectsClass.AddMember t
                        let p = ProvidedProperty(domainCategoryName, t, IsStatic=false, 
                                                 GetterCode = (fun args -> Expr.Call(args.[0], getDomainCategoryById,[Expr.Value(domainCategory.DomainCategoryId.Id)])))
                        p.AddXmlDocDelayed (fun () -> xmlDoc (sprintf "Contains the objects of the domain category '%s' defined in the web data store organized by type" domainCategory.Name))
                        yield p ]
                with e -> 
                    let errorMessage = e.Message
                    let propertyName = 
                        match e with
                        | :? FreebaseWebException as e when e.Domain = "usageLimits" && e.Reason = "keyInvalid" -> "Invalid API Key"
                        | _ -> "Error"
                    let errorProp = ProvidedProperty(propertyName, typeof<string>, GetterCode = (fun _ -> <@@ failwith errorMessage @@>))
                    errorProp.AddXmlDoc errorMessage
                    [errorProp] )

        theServiceTypesClass.AddMembers [theServiceType; theDomainObjectsClass ]

        let theRootType = ProvidedTypeDefinition(fbRuntimeInfo.RuntimeAssembly,rootNamespace,rootTypeName,baseType=Some typeof<obj>, HideObjectMethods=true)
        theRootType.AddXmlDoc "<summary>Typed representation of Freebase data. See http://www.freebase.com for terms and conditions.</summary>"
        theRootType.AddMembers [ theServiceTypesClass  ]
        theRootType.AddMembersDelayed (fun () -> 
            [ yield ProvidedMethod ("GetDataContext", [], theServiceType, IsStaticMethod=true,
                                    InvokeCode = (fun _args -> Expr.Call(createDataContext, [  Expr.Value apiKey; Expr.Value serviceUrl; Expr.Value useUnits; Expr.Value snapshotDate; Expr.Value useLocalCache; Expr.Value allowQueryEvaluateOnClientSide  ])))
            ])

        theRootType

    let defaultNumIndividuals = 1000
    let defaultUseUnits = true
    let defaultPluralize = true
    let defaultSnapshotDate = "none"
    let defaultServiceUrl = FreebaseQueries.DefaultServiceUrl
    let defaultLocalSchemaCache = true
    let defaultApiKey = "none"
    let defaultAllowQueryEvaluateOnClientSide = true
    let defaultUseRefinedTypesForItems = true
    let freebaseType = createTypes(defaultApiKey, defaultServiceUrl, "FreebaseData", defaultNumIndividuals, defaultUseUnits,defaultPluralize, defaultSnapshotDate, defaultLocalSchemaCache, defaultAllowQueryEvaluateOnClientSide, defaultUseRefinedTypesForItems)
    let paramFreebaseType   = ProvidedTypeDefinition(fbRuntimeInfo.RuntimeAssembly, rootNamespace, "FreebaseDataProvider", Some(typeof<obj>), HideObjectMethods = true)
    let apiKeyParam = ProvidedStaticParameter("Key",    typeof<string>, defaultApiKey)
    let serviceUrlParam   = ProvidedStaticParameter("ServiceUrl",      typeof<string>, defaultServiceUrl)
    let numIndividualsParam = ProvidedStaticParameter("NumIndividuals",    typeof<int>, defaultNumIndividuals)
    let useUnitsParam       = ProvidedStaticParameter("UseUnitsOfMeasure", typeof<bool>,defaultUseUnits)
    let pluralizeParam      = ProvidedStaticParameter("Pluralize",         typeof<bool>,defaultPluralize)
    let snapshotDateParam   = ProvidedStaticParameter("SnapshotDate",      typeof<string>, defaultSnapshotDate)
    let localCacheParam   = ProvidedStaticParameter("LocalCache",      typeof<bool>, defaultLocalSchemaCache)
    let allowQueryEvaluateOnClientSideParam   = ProvidedStaticParameter("AllowLocalQueryEvaluation",      typeof<bool>, defaultAllowQueryEvaluateOnClientSide)
    let useRefinedTypesForItemsParam = ProvidedStaticParameter("UseRefinedTypes",      typeof<bool>, defaultUseRefinedTypesForItems)

    let helpText = "<summary>Typed representation of Freebase data with additional configuration parameters. See http://www.freebase.com for terms and conditions.</summary>
                    <param name='Key'>The API key for the MQL metadata service.</param>
                    <param name='ServiceUrl'>The service URL for the MQL metadata service (default: `" + defaultServiceUrl + "`).</param>
                    <param name='NumIndividuals'>The maximum number of sample individuals for each Freebase type (default: `" + string defaultNumIndividuals + "`).</param>
                    <param name='UseUnitsOfMeasure'>Use the unit-of-measure annotations from the data source metadata (default: " + sprintf "%b" defaultUseUnits + ").</param>
                    <param name='Pluralize'>Use adhoc rules to pluralize the names of types when forming names of collections (default: " + sprintf "%b" defaultPluralize + ").</param>
                    <param name='SnapshotDate'>Use a snapshot of the web data store at the given date and/or time in ISO8601 format, e.g., 2012-01-18, 2012-09-15T21:11:32. A value of `now` indicates the compile time of the code. (default: no snapshot).</param>
                    <param name='LocalCache'>Use a persistent local cache for schema requests. Also provides the default for whether a persistent local cache is used at runtime. A per-session cache is always used for schema data but it will not persist if this is set to false (default: true).</param>
                    <param name='AllowLocalQueryEvaluation'>Allow local evalution of some parts of a query. If false, then an exception will be raised if a query can't be evaluated fully on the server. If true, data sets may be implicitly brought to the client for processing (default: " + (sprintf "%b" defaultAllowQueryEvaluateOnClientSide) + ").</param>
                    <param name='UseRefinedTypes'>Use refined types for individual entities (default: " + (sprintf "%b" defaultUseRefinedTypesForItems) + ").</param>"
    do paramFreebaseType.AddXmlDoc(helpText)
    do paramFreebaseType.DefineStaticParameters([apiKeyParam;serviceUrlParam;numIndividualsParam;useUnitsParam;pluralizeParam;snapshotDateParam;localCacheParam;allowQueryEvaluateOnClientSideParam;useRefinedTypesForItemsParam], fun typeName providerArgs -> 
          let apiKey = (providerArgs.[0] :?> string)
          let serviceUrl = (providerArgs.[1] :?> string)
          let numIndividuals = (providerArgs.[2] :?> int)
          let useUnits = (providerArgs.[3] :?> bool)
          let usePluralize = (providerArgs.[4] :?> bool)
          let snapshotDate = (providerArgs.[5] :?> string)
          let snapshotDate = 
              match snapshotDate with 
              | "now" -> DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss") 
              | null | "" | "none"  -> snapshotDate
              | _ -> try ignore(DateTime.Parse(snapshotDate, CultureInfo.InvariantCulture, DateTimeStyles.None)); snapshotDate with e -> failwith ("invalid snapshot date" + e.Message)

          let useLocalCache = (providerArgs.[6] :?> bool)
          let allowQueryEvaluateOnClientSide =  (providerArgs.[7] :?> bool)
          let useRefinedTypesForItems =  (providerArgs.[8] :?> bool)
          createTypes(apiKey, serviceUrl, typeName, numIndividuals, useUnits, usePluralize, snapshotDate, useLocalCache, allowQueryEvaluateOnClientSide, useRefinedTypesForItems))
    do 
      this.AddNamespace(rootNamespace, [ freebaseType ])
      this.AddNamespace(rootNamespace, [ paramFreebaseType ])