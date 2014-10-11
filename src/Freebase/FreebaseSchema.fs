// --------------------------------------------------------------------------------------
// Freebase type provider 
// --------------------------------------------------------------------------------------
// This file contains an object model for Freebase schema information
//
// This file contains a table of data of units of measure 
// downloaded from www.freebase.com itself. See www.freebase.com 
// for terms and conditions of use.
// --------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation 2005-2012.
// This sample code is provided "as is" without warranty of any kind. 
// We disclaim all warranties, either express or implied, including the 
// warranties of merchantability and fitness for a particular purpose. 
// --------------------------------------------------------------------------------------

module internal FSharp.Data.Runtime.Freebase.FreebaseSchema

open FSharp.Data.Runtime.Freebase.FreebaseRequests
open System
open System.Collections.Generic
open System.Text
open FSharp.Data
open FSharp.Data.JsonExtensions

[<AutoOpen>]
module Utilities = 

    let memoizeLookup (d : Dictionary<_,_>) key f = 
        let mutable res = Unchecked.defaultof<'T>
        let ok = d.TryGetValue(key,&res)
        if ok then res else let v = f key in d.[key] <- v; v

    let memoize f = let t = Dictionary<_,_>() in fun key -> memoizeLookup t key f

    let rec convJsonPrimValue jsonValue = 
        match jsonValue with 
        | JsonValue.String s -> s :> obj
        | JsonValue.Null -> null
        | JsonValue.Boolean b -> b :> obj
        | JsonValue.Number n -> n :> obj
        | JsonValue.Float n -> n :> obj
        | JsonValue.Array a -> Array.map convJsonPrimValue a :> obj
        | _ -> failwith (sprintf "unexpected structured JSON value %+A" jsonValue)

    /// Represents one object's values for all the properties of one type id.
    let dictionaryFromJson (jsonValue:JsonValue) = jsonValue.Properties |> dict

type FreebaseId = 
    | FreebaseId of string
    member x.Id = (match x with FreebaseId(v) -> v)

type FreebaseMachineId = 
    | FreebaseMachineId of string
    member x.MId = (match x with FreebaseMachineId(v) -> v)


type FreebaseDomainId = 
    { DomainId:FreebaseId
      DomainHidden: bool
      DomainName:string }
    static member FromJson(fbr:JsonValue) = 
        { DomainId = FreebaseId(fbr.GetString("/type/object/id"))
          DomainHidden = (fbr.GetString("/freebase/domain_profile/hidden") = "true")
          DomainName = fbr.GetString("/type/object/name")  }
    override x.ToString() = x.DomainName

type FreebaseProperty = 
    { PropertyId:FreebaseId
      MachineId:string
      PropertyName:string
      ExpectedTypeId:FreebaseId
      //MasterProperty:string
      /// Name of the enumeration type. Like /authority/gnis
      EnumerationId:FreebaseId
      Delegated:string
      /// The unit of measure for the property, if any. e.g. /en/kilometer
      UnitOfMeasureId:FreebaseId
      Unique:string }
    static member FromJson(fbr:JsonValue) = 
        { PropertyId = FreebaseId(fbr.GetString("/type/object/id")) 
          MachineId = fbr.GetString("/type/object/mid") 
          PropertyName = fbr.GetString("/type/object/name") 
          ExpectedTypeId = FreebaseId(fbr.GetString("/type/property/expected_type")) 
          //MasterProperty = fbr.GetString("/type/property/master_property") 
          EnumerationId = FreebaseId(fbr.GetString("/type/property/enumeration"))
          Delegated = fbr.GetString("/type/property/delegated") 
          UnitOfMeasureId = FreebaseId(fbr.GetString("/type/property/unit")) 
          Unique = fbr.GetString("/type/property/unique")  }
    member fp.IsUnique = match fp.Unique with "true" | "True" -> true | _ -> false
    member fp.IsEnum = not(String.IsNullOrEmpty fp.EnumerationId.Id)
    member fp.BasicSystemType =
        match fp.ExpectedTypeId.Id with 
        | "/type/enumeration" -> Some (typeof<string>, true)
        | "/type/rawstring" -> Some (typeof<string>, true)
        | "/type/text" -> Some (typeof<string>, true)
        | "/type/uri" ->  Some (typeof<string>, true)
        | "/type/float" -> Some (typeof<double>, false)
        | "/type/int" -> Some (typeof<int>, false)
        | "/type/datetime" -> Some(typeof<string>, true) // Not System.DateTime because, for example, "1776" is a freebase DateTime, as is 9000BC. 
        | "" -> Some(typeof<string>, true) // Tolerate null and treat as string. We can't really do anything else.
        | _ -> None
    override x.ToString() = x.PropertyName

type FreebaseArticle = 
    { ArticleId:FreebaseId }
    static member FromJson(fbr:JsonValue) = 
        { ArticleId = FreebaseId(fbr.GetString("/type/object/id"))  }
      
type FreebaseTypeId = 
    { TypeId:FreebaseId
      DomainId:FreebaseId }
    static member FromJson(fbr:JsonValue) = 
        { TypeId = FreebaseId(fbr.GetString("/type/object/id")) ;
          DomainId = FreebaseId(fbr.GetString("/type/type/domain")) ;}

type FreebaseObjectId = 
    { MachineId:FreebaseMachineId
      ObjectName:string }
    static member FromJson(fbr:JsonValue) = 
        { MachineId = FreebaseMachineId (fbr.GetString("/type/object/mid")) ;
          ObjectName = fbr.GetString("/type/object/name") ;}

type FreebaseTypesSupportedByObject = 
    { TypesSupportedByObject:FreebaseTypeId[] }
    static member FromJson(fbr:JsonValue) = 
        { TypesSupportedByObject = fbr.GetArray("/type/object/type") |> Array.map FreebaseTypeId.FromJson }      

type FreebaseImageInformation = 
    { ImageId:FreebaseId }
    static member FromJson(fbr:JsonValue) = 
        { ImageId = FreebaseId(fbr.GetString("/type/object/id")) }
    static member GetImages(fb:FreebaseQueries, objectId:FreebaseMachineId) = 
        let query = @"[{'/type/object/id':null,'/type/object/type':'/common/image','!/common/topic/image': [{'/type/object/mid':'" + objectId.MId + "'}]}]" 
        fb.Query<FreebaseImageInformation[]>(query, JsonValue.GetArrayVal FreebaseImageInformation.FromJson)
        |> Seq.map (fun image -> fb.GetImageUrl image.ImageId.Id)

type FreebaseType = 
    { TypeId:FreebaseId
      TypeName:string
      Mediator:string
      Deprecated:string
      Domain:FreebaseId
      IncludedTypes: FreebaseTypeId[]
      Properties:FreebaseProperty[] }
    static member FromJson(fbr:JsonValue) = 
        { TypeId = FreebaseId(fbr.GetString("/type/object/id")) 
          TypeName = fbr.GetString("/type/object/name") 
          Mediator = fbr.GetString("/freebase/type_hints/mediator") 
          Deprecated = fbr.GetString("/freebase/type_hints/deprecated")
          Domain = FreebaseId(fbr.GetString("/type/type/domain"))
          IncludedTypes = fbr.GetArray("/freebase/type_hints/included_types") |> Array.map FreebaseTypeId.FromJson
          Properties = fbr.GetArray("/type/type/properties") |> Array.map FreebaseProperty.FromJson }      
    override x.ToString() = x.TypeName

type FreebaseDomain = 
    { DomainId:FreebaseId
      DomainName:string    
      NamespaceKinds:string[]
      Hidden:string    }
    static member FromJson(fbr:JsonValue) = 
        { DomainId = FreebaseId(fbr.GetString("/type/object/id")) 
          DomainName = fbr.GetString("/type/object/name") 
          NamespaceKinds = fbr.GetArray("/type/object/type") |> Array.map (fun j -> j.AsString())
          Hidden = fbr.GetString("/freebase/domain_profile/hidden") }
    override x.ToString() = x.DomainName

type FreebaseNamespaceKey = 
    { Value:string
      Namespace:FreebaseDomain }
    static member FromJson(fbr:JsonValue) = 
        { Value = fbr.GetString("value") 
          Namespace = fbr?``namespace`` |> FreebaseDomain.FromJson }
    override x.ToString() = x.Namespace.ToString()

/// The element type returned by GetDomainStructure.
type FreebaseDomainStructure = 
    { NamespaceKeys:FreebaseNamespaceKey[] }
    static member FromJson(fbr:JsonValue) = 
        { NamespaceKeys = fbr.GetArray("/type/namespace/keys") |> Array.map FreebaseNamespaceKey.FromJson }

/// The element type returned by GetDomainCategories
type FreebaseDomainCategory = 
    { DomainCategoryId:FreebaseId
      Name:string
      Domains:FreebaseDomainId[] }
    static member FromJson(fbr:JsonValue) = 
        { DomainCategoryId = FreebaseId(fbr.GetString("/type/object/id")) 
          Name = fbr.GetString("/type/object/name") 
          Domains = fbr.GetArray("/freebase/domain_category/domains") |> Array.map FreebaseDomainId.FromJson }
    override x.ToString() = x.Name

type FreebaseDocumentation = 
    { Articles: FreebaseArticle[] 
      Tip:string  }
    static member FromJson(fbr:JsonValue) = 
        { Articles = fbr.GetArray("/common/topic/article") |> Array.map FreebaseArticle.FromJson
          Tip = fbr.GetString("/freebase/documented_object/tip") }

    static member GetDocs (fb: FreebaseQueries, query:string) =
        let fbDoc = fb.Query<FreebaseDocumentation>(query, FreebaseDocumentation.FromJson)
        [ match fbDoc.Articles with
          | null -> ()
          | articles ->
              for a in articles do
                  match fb.GetBlurbByArticleId a.ArticleId.Id with
                  | Some v -> yield v
                  | None -> () 

          match fbDoc.Tip with
          | null -> ()
          | tip -> yield tip ]

    static member GetBlurbById (fb: FreebaseQueries, objectId: FreebaseId) =
        let query = "{ '/type/object/id' : '" + objectId.Id + "', '/common/topic/article' : [{ '/type/object/id' : null, 'optional' : true}], '/freebase/documented_object/tip' : null}" 
        FreebaseDocumentation.GetDocs(fb, query)

    static member GetBlurbByMachineId (fb: FreebaseQueries, objectId: FreebaseMachineId) : string list =
        let query = "{ '/type/object/mid' : '" + objectId.MId + "', '/common/topic/article' : [{ '/type/object/id' : null, 'optional' : true}], '/freebase/documented_object/tip' : null}" 
        FreebaseDocumentation.GetDocs(fb, query)

type FreebaseSchemaConnection(fb:FreebaseQueries) = 
    let getTypeQuery(typeId:string,typeName:string,domainId:string,includeProperties) = 
        let properties = @"[{ '/type/object/id': null, '/type/object/mid': null, '/type/object/name': null, '/type/property/expected_type': null, '/type/property/master_property': null, '/type/property/enumeration': null, '/type/property/delegated': null, '/type/property/unit': null, '/type/property/unique': null, 'optional':true}]"
        let properties = if includeProperties then "'/type/type/properties':"+properties+"," else ""
        sprintf @"[{'/type/object/type':'/type/type', '/type/object/id':%s, '/type/object/name':%s, '/type/type/domain':%s, '/freebase/type_hints/deprecated' : null, '/freebase/type_hints/mediator' : null, '/freebase/type_hints/included_types' : [ {'/type/object/id':null,'/type/type/domain':null,'optional':true } ], %s '/freebase/type_profile/instance_count': null, 'limit':10000 }]" typeId typeName domainId properties

    /// Cache policy for type properties. This will be used at runtime.
    let typeIdToType = Dictionary<FreebaseId,_>()
    let quote s = "'"+s+"'"

    /// Get the types that correspond to type id. The properties of the type are filled in.
    let getTypeByTypeId typeId =
        match typeIdToType.TryGetValue typeId with
        | true, res -> res
        | _ ->   
            let query = getTypeQuery(quote typeId.Id, "null", "null", true)
            let result = fb.Query<FreebaseType[]>(query, JsonValue.GetArrayVal FreebaseType.FromJson)
            let fbType = match result with [|ft|] -> Some ft | _ -> None
            typeIdToType.[typeId] <- fbType
            fbType

    let rec allIncludedTypesOfTypeId (typeId:FreebaseId) =
        seq { match getTypeByTypeId typeId with
              | Some ty -> yield ty; for ity in ty.IncludedTypes do  yield! allIncludedTypesOfTypeId ity.TypeId
              | None -> () }

    let getAllIncludedTypesOfTypeId = memoize (allIncludedTypesOfTypeId >> Seq.distinct >> Seq.toArray)
    let getProperties (fbType:FreebaseType) = match fbType.Properties with | null -> [| |] | props -> props
    let getAllPropertiesOfAllIncludedTypesOfTypeId = memoize (getAllIncludedTypesOfTypeId >> Array.collect getProperties)

    member __.GetAllPropertiesOfType(fbType:FreebaseType) = getProperties fbType
    member __.GetAllPropertiesOfAllIncludedTypesOfTypeId typeId = getAllPropertiesOfAllIncludedTypesOfTypeId typeId 
    member __.QueryConnection = fb
    /// Query the structure of common domains and all the type types in that domain.  Design-time only.
    member __.GetDomainStructure () = fb.Query(@"{ '/type/object/id': '/', '/type/namespace/keys': [{ 'value': null, 'namespace': { '/type/object/id': null, '/type/object/name': null, '/type/object/type': [], '/freebase/domain_profile/hidden': null }, 'optional':true, 'limit':20000 }] }", FreebaseDomainStructure.FromJson)
            
    /// Query the structure of common domains and all the type types in that domain. 
    member __.GetDomainCategories () = fb.QuerySequence(@"[{ '/type/object/type': '/freebase/domain_category', '/type/object/id': null, '/type/object/name': null, '/freebase/domain_category/domains': [{  '/type/object/id': null, '/type/object/name': null, '/freebase/domain_profile/hidden': null, 'optional':true, 'limit':20000 }] }]", FreebaseDomainCategory.FromJson, None)
            
    /// Return all typesin a domain. Design-time only.
    member __.GetAllTypesInDomainSansProperties(domainId:FreebaseId) = 
        fb.QuerySequence<FreebaseType>(getTypeQuery("null","null",quote domainId.Id,false), FreebaseType.FromJson, None)

    /// Return all types. Design-time only.
    member __.GetAllTypesInAllDomainsSansProperties() = fb.QuerySequence<FreebaseType>(getTypeQuery("null","null","null",false), FreebaseType.FromJson, None)

    /// Get the types that correspond to type id.   The properties of the type are filled in.
    member __.GetTypeByTypeId typeId = getTypeByTypeId typeId

    /// Get the 'blurb' text for this topic ID 
    member __.GetBlurbById objectId = FreebaseDocumentation.GetBlurbById(fb, objectId)

    /// Get the 'blurb' text for this topic ID 
    member __.GetBlurbByMachineId objectId = FreebaseDocumentation.GetBlurbByMachineId(fb, objectId)

    /// Get property bags for all the objects of the given type, at the given type
    member __.GetAllObjectsOfType(fbType:FreebaseType, limit:int, prefixName) =
        let nameCx = 
            match prefixName with 
            | None -> "'/type/object/name' : null"
            | Some s -> "'/type/object/name' : null, '/type/object/name~=' : '^" + s + "*'"
        
        let query = 
            "[{ '/type/object/type' : '" + fbType.TypeId.Id + 
            "', '/type/object/mid' : null, " + 
            nameCx  + 
            ", 'limit': " + string limit  +
            " }]" 
        fb.QuerySequence<FreebaseObjectId>(query, FreebaseObjectId.FromJson, Some limit)

    member __.GetAllTypesOfObject(fbObjId:FreebaseMachineId) =
        let query = "{ '/type/object/type' : [{ '/type/object/id': null, '/type/type/domain': null }], '/type/object/mid' : '" + fbObjId.MId + "' }" 
        fb.Query(query, FreebaseTypesSupportedByObject.FromJson) 

    // Perform one query to determine the existence of data in the properties of a specific object
    member fbSchema.GetDataExistenceForSpecificObject(fbTypes, fbObjId:FreebaseMachineId) =
        let fbProps = 
            [ for fbType in fbTypes do 
                yield (fbType, getProperties fbType) ]

        let fields = 
            [ for (_fbType, props)  in fbProps do 
               for p in props do
                match p.IsUnique, p.BasicSystemType with 
                | true, Some _ -> yield ", '" + p.PropertyId.Id + "' : null"  
                | _ -> 
                    yield ", '" + p.PropertyId.Id + "' : [ ]" ]
                    //yield ", '" + p.Id + "' : [ {  'type': [], 'limit': 1 } ]" ]
            |> String.Concat

        let query = sprintf "{ '/type/object/mid' : '" + fbObjId.MId + "'  " + fields  + " }"   

        let data =  fb.Query<IDictionary<string,JsonValue> >(query, dictionaryFromJson) 

        [ for (fbType, props)  in fbProps do 
            let ps = 
             [ for fbProp in props do
                let hasData = 
                    match data.TryGetValue(fbProp.PropertyId.Id) with 
                    | true, JsonValue.Array [| |] -> false
                    | true, JsonValue.Null -> false
                    | true, _ -> true
                    | false, _ -> false
                yield (fbProp, hasData) ]
            yield (fbType, ps) ]

let makeRuntimeNullableTy  (ty:Type) = typedefof<Nullable<_>>.MakeGenericType [|ty|]
let makeRuntimeSeqTy  (ty:Type) = typedefof<seq<_>>.MakeGenericType [|ty|]

type FreebaseProperty with 
    /// Compute the provided or erased runtime type corresponding to the Freebase property.
    /// 'typeReprFunction' indicates if erasure is happening or not.
    member property.FSharpPropertyElementType(fb:FreebaseSchemaConnection, propertyReprFunction, tryTypeReprFunction) =
        match propertyReprFunction property with 
        | Some elementType -> elementType
        | None -> 
        match property.BasicSystemType with
        | Some elementType -> elementType
        | None -> 
        let fbtype = fb.GetTypeByTypeId(property.ExpectedTypeId)
        match fbtype with 
        | Some fbtype -> 
            match tryTypeReprFunction fbtype with 
            | Some ty -> ty, true
            | None -> typeof<string>, true
        | None ->
        // The expected type was unknown to freebase. Tolerate by treating as string.
        (typeof<string>, true)

    /// Compute the provided or erased runtime type corresponding to the Freebase property.
    /// 'typeReprFunction' indicates if erasure is happening or not.
    member property.FSharpPropertyType(fb:FreebaseSchemaConnection, propertyReprFunction, tryTypeReprFunction, makeNullable, makeSeq, alwaysThere) =
        let elementType, supportsNull = property.FSharpPropertyElementType(fb, propertyReprFunction, tryTypeReprFunction)
        match property.IsUnique with
        | true -> if supportsNull || alwaysThere then elementType else makeNullable elementType
        | false -> makeSeq elementType

    member property.FSharpPropertyRuntimeType(fb:FreebaseSchemaConnection, fbCompoundObjTy, alwaysThere) =
        property.FSharpPropertyType(fb,(fun _ -> None), (fun _ -> Some fbCompoundObjTy), makeRuntimeNullableTy, makeRuntimeSeqTy, alwaysThere)

type FreebaseUnit = 
    | SI of string 
    | Prod of FreebaseUnit * FreebaseUnit 
    | Div of FreebaseUnit * FreebaseUnit
    | One
    static member (*) (u1: FreebaseUnit, u2:FreebaseUnit) = Prod (u1, u2)
    static member (/) (u1: FreebaseUnit, u2:FreebaseUnit) = Div (u1, u2)

/// This table of data was downloaded from www.freebase.com itself. 
/// See www.freebase.com for term and conditions of use.
let units = 
   dict [
       ("/en/astronomical_unit", (* "Astronomical unit", *) (SI "metre", 1.49598e+11, None))
       ("/en/centimeter", (* "Centimeter", *) (SI "metre", 0.01, None))
       ("/en/furlong", (* "Furlong", *) (SI "metre", 201.168, None))
       ("/en/geographical_mile", (* "Geographical mile", *) (SI "metre", 1855.0, None))
       ("/en/inch", (* "Inch", *) (SI "metre", 0.0254, None))
       ("/en/meter", (* "Meter", *) (SI "metre", 1.0, None))
       ("/en/mile", (* "Mile", *) (SI "metre", 1609.34, None))
       ("/m/055v9", (* "Micrometer", *) (SI "metre", 1e-06, None))
       ("/en/nautical_mile", (* "Nautical mile", *) (SI "metre", 1852.0, None))
       ("/en/parsec", (* "Parsec", *) (SI "metre", 3.08568e+16, None))
       ("/en/yard", (* "Yard", *) (SI "metre", 0.9144, None))
       ("/en/kilometer", (* "Kilometer", *) (SI "metre", 1000.0, None))
       ("/en/millimeter", (* "Millimeter", *) (SI "metre", 0.001, None))
       ("/en/earth_radius", (* "Earth radius", *) (SI "metre", 6.37814e+06, None))
       ("/en/smoot", (* "Smoot", *) (SI "metre", 1.7, None))
       ("/en/angstrom", (* "Ångström", *) (SI "metre", 1e-10, None))
       ("/en/fathom", (* "Fathom", *) (SI "metre", 1.8288, None))
       ("/en/bohr_radius", (* "Bohr radius", *) (SI "metre", 5.29177e-11, None))
       ("/en/light_second", (* "Light second", *) (SI "metre", 2.99792e+08, None))
       ("/en/international_foot", (* "Foot", *) (SI "metre", 0.3048, None))
       ("/m/01g8lv", (* "Hand", *) (SI "metre", 0.1016, None))
       ("/en/decimetre", (* "Decimetre", *) (SI "metre", 0.1, None))
       ("/en/decametre", (* "Decametre", *) (SI "metre", 10.0, None))
       ("/en/shaku", (* "Shaku", *) (SI "metre", 0.303, None))
       ("/en/solar_radius", (* "Solar radius", *) (SI "metre", 6.96e+08, None))
       ("/m/02mr9z", (* "Rod", *) (SI "metre", 5.029, None))
       ("/en/megametre", (* "Megametre", *) (SI "metre", 1e+06, None))
       ("/en/hectometre", (* "Hectometre", *) (SI "metre", 100.0, None))
       ("/en/light-year", (* "Light-year", *) (SI "metre", 9.46073e+15, None))
       ("/en/megalithic_yard", (* "Megalithic yard", *) (SI "metre", 0.8297, None))
       ("/m/0355rq", (* "Chain", *) (SI "metre", 20.117, None))
       ("/en/light_hour", (* "Light hour", *) (SI "metre", 1.07925e+12, None))
       ("/en/planck_length", (* "Planck length", *) (SI "metre", 0.0, None))
       ("/en/light_week", (* "Light week", *) (SI "metre", 1.81314e+14, None))
       ("/en/light_day", (* "Light day", *) (SI "metre", 2.59021e+13, None))
       ("/en/light_minute", (* "Light minute", *) (SI "metre", 1.79875e+10, None))
       ("/en/light_month", (* "Light month", *) (SI "metre", 7.77062e+14, None))
       ("/m/051v_f", (* "Span", *) (SI "metre", 0.2286, None))
       ("/m/055vq1", (* "Li", *) (SI "metre", 500.0, None))
       ("/m/05b1qt", (* "Digit", *) (SI "metre", 0.01905, None))
       ("/m/05b1sy", (* "Finger", *) (SI "metre", 0.022225, None))
       ("/m/05b1tz", (* "Palm", *) (SI "metre", 0.0762, None))
       ("/en/shaftment", (* "Shaftment", *) (SI "metre", 0.1524, None))
       ("/en/ell", (* "Ell", *) (SI "metre", 1.143, None))
       ("/m/05gwxt", (* "Link", *) (SI "metre", 0.201, None))
       ("/en/arpent", (* "Arpent", *) (SI "metre", 58.47, None))
       ("/en/siriometer", (* "Siriometer", *) (SI "metre", 1.49598e+17, None))
       ("/en/spat", (* "Spat", *) (SI "metre", 1e+12, None))
       ("/en/beard-second", (* "Beard-second", *) (SI "metre", 5e-09, None))
       ("/en/toise", (* "Toise", *) (SI "metre", 1.949, None))
       ("/m/0fhs97", (* "Thou", *) (SI "metre", 2.54e-05, None))
       ("/en/picometre", (* "Picometre", *) (SI "metre", 1e-12, None))
       ("/en/cham_am", (* "Cham am", *) (SI "metre", 0.25, None))
       ("/m/027tq8y", (* "Bahar", *) (SI "metre", 0.0325, None))
       ("/en/nanometre", (* "Nanometre", *) (SI "metre", 1e-09, None))
       ("/m/03yvsyr", (* "Jupiter radius", *) (SI "metre", 7.1492e+06, None))
       ("/m/0cv2t5d", (* "moot", *) (SI "metre", 1.7, None))
       ("/en/acre", (* "Acre", *) (Prod (SI "metre",SI "metre"), 4046.86, None))
       ("/en/hectare", (* "Hectare", *) (Prod (SI "metre",SI "metre"), 10000.0, None))
       ("/en/barn_measure", (* "Barn", *) (Prod (SI "metre",SI "metre"), 1e-27, None))
       ("/en/square_kilometer", (* "Square kilometer", *) (Prod (SI "metre",SI "metre"), 1e+06, None))
       ("/en/square_mile", (* "Square mile", *) (Prod (SI "metre",SI "metre"), 2.58999e+06, None))
       ("/en/square_foot", (* "Square foot", *) (Prod (SI "metre",SI "metre"), 0.092903, None))
       ("/en/square_inch", (* "Square inch", *) (Prod (SI "metre",SI "metre"), 0.00064516, None))
       ("/en/square_yard", (* "Square yard", *) (Prod (SI "metre",SI "metre"), 0.836127, None))
       ("/en/square_meter", (* "Square meter", *) (Prod (SI "metre",SI "metre"), 1.0, None))
       ("/en/us_survey_acre", (* "US survey acre", *) (Prod (SI "metre",SI "metre"), 4046.87, None))
       ("/en/iraqi_dunam", (* "Iraqi dunam", *) (Prod (SI "metre",SI "metre"), 2500.0, None))
       ("/en/metric_dunam", (* "Metric dunam", *) (Prod (SI "metre",SI "metre"), 1000.0, None))
       ("/en/old_dunam", (* "Old dunam", *) (Prod (SI "metre",SI "metre"), 919.3, None))
       ("/en/cypriot_dunam", (* "Cypriot dunam", *) (Prod (SI "metre",SI "metre"), 1337.8, None))
       ("/en/british_thermal_unit", (* "British thermal unit", *) (SI "joule", 1055.06, None))
       ("/en/calorie", (* "Calorie", *) (SI "joule", 4.184, None))
       ("/en/electronvolt", (* "Electronvolt", *) (SI "joule", 1.60218e-19, None))
       ("/en/erg", (* "Erg", *) (SI "joule", 7e-07, None))
       ("/en/joule", (* "Joule", *) (SI "joule", 1.0, None))
       ("/en/hartree_energy", (* "Hartree energy", *) (SI "joule", 4.35974e-18, None))
       ("/en/watt-hour", (* "Watt-hour", *) (SI "joule", 3600.0, None))
       ("/en/barrel_of_oil_equivalent", (* "Barrel of oil equivalent", *) (SI "joule", 6.11786e+09, None))
       ("/en/kilowatt_hour", (* "Kilowatt-hour", *) (SI "joule", 3.6e+06, None))
       ("/en/kilocalorie", (* "Kilocalorie", *) (SI "joule", 4184.0, None))
       ("/en/kilojoule", (* "Kilojoule", *) (SI "joule", 1000.0, None))
       ("/en/ton_of_tnt", (* "Ton of TNT", *) (SI "joule", 4.184e+09, None))
       ("/en/kiloton_of_tnt", (* "Kiloton of TNT", *) (SI "joule", 4.184e+12, None))
       ("/en/megaton_of_tnt", (* "Megaton of TNT", *) (SI "joule", 4.184e+15, None))
       ("/en/milliton_of_tnt", (* "Milliton of TNT", *) (SI "joule", 4.184e+06, None))
       ("/en/microton_of_tnt", (* "Microton of TNT", *) (SI "joule", 4184.0, None))
       ("/en/megaelectronvolt", (* "Megaelectronvolt", *) (SI "joule", 1.60218e-13, None))
       ("/en/pound-force_per_square_inch", (* "Pound-force per square inch", *) (SI "pascal", 6894.76, None))
       ("/en/torr", (* "Torr", *) (SI "pascal", 133.322, None))
       ("/en/pascal", (* "Pascal", *) (SI "pascal", 1.0, None))
       ("/en/bar", (* "Bar", *) (SI "pascal", 100000.0, None))
       ("/en/atmosphere_measure", (* "Atmosphere", *) (SI "pascal", 101325.0, None))
       ("/en/centimetre_of_water", (* "Centimetre of water", *) (SI "pascal", 98.0638, None))
       ("/en/barye", (* "Barye", *) (SI "pascal", 0.1, None))
       ("/en/pieze", (* "Pièze", *) (SI "pascal", 1000.0, None))
       ("/en/technical_atmosphere", (* "Technical atmosphere", *) (SI "pascal", 98066.5, None))
       ("/en/inch_of_mercury", (* "Inch of mercury", *) (SI "pascal", 3386.39, None))
       ("/en/decibars", (* "Decibars", *) (SI "pascal", 10000.0, None))
       ("/en/kilogram-force_per_square_centimetre", (* "Kilogram-force per square centimetre", *) (SI "pascal", 98066.5, None))
       ("/en/megapascal", (* "Megapascal", *) (SI "pascal", 1e+06, None))
       ("/en/millibar", (* "Millibar", *) (SI "pascal", 100.0, None))
       ("/en/gigapascal", (* "Gigapascal", *) (SI "pascal", 1e+09, None))
       ("/en/kilonewton_per_metre_squared", (* "KiloNewton per metre squared", *) (SI "pascal", 1000.0, None))
       ("/en/kilopound_force_per_square_inch", (* "Kilopound-force per square inch", *) (SI "pascal", 6.89476e+06, None))
       ("/en/kilopascal", (* "Kilopascal", *) (SI "pascal", 1000.0, None))
       ("/en/watt", (* "Watt", *) (SI "watt", 1.0, None))
       ("/en/solar_luminosity", (* "Solar luminosity", *) (SI "watt", 3.827e+26, None))
       ("/en/mechanical_horsepower", (* "Mechanical horsepower", *) (SI "watt", 745.7, None))
       ("/en/metric_horsepower", (* "Metric horsepower", *) (SI "watt", 735.499, None))
       ("/en/boiler_horsepower", (* "Boiler horsepower", *) (SI "watt", 9809.5, None))
       ("/en/electrical_horsepower", (* "Electrical horsepower", *) (SI "watt", 746.0, None))
       ("/en/kilowatt_measure", (* "Kilowatt", *) (SI "watt", 1000.0, None))
       ("/en/milliwatt", (* "Milliwatt", *) (SI "watt", 0.001, None))
       ("/en/megawatt", (* "Megawatt", *) (SI "watt", 1e+06, None))
       ("/en/metre_per_second", (* "Metre per second", *) (Div (SI "metre",SI "second"), 1.0, None))
       ("/en/miles_per_hour", (* "Miles per hour", *) (Div (SI "metre",SI "second"), 0.44704, None))
       ("/en/knot_measure", (* "Knot", *) (Div (SI "metre",SI "second"), 0.514444, None))
       ("/en/kilometres_per_hour", (* "Kilometres per hour", *) (Div (SI "metre",SI "second"), 0.27778, None))
       ("/en/kilometres_per_second", (* "Kilometres per second", *) (Div (SI "metre",SI "second"), 1000.0, None))
       ("/en/gallon", (* "Gallon (US)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.00378541, None))
       ("/en/liter", (* "Liter", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.001, None))
       ("/en/teaspoon", (* "Teaspoon", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 5e-06, None))
       ("/en/tablespoon", (* "Tablespoon", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 1.5e-05, None))
       ("/en/peck", (* "Peck (Imperial)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.00727373, None))
       ("/en/pint_us", (* "Pint (US)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.000473176, None))
       ("/en/cubic_foot", (* "Cubic foot", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.0283168, None))
       ("/en/quart_us", (* "Quart (US)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.000946353, None))
       ("/en/fluid_ounce_us", (* "Fluid ounce (US)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 2.95735e-05, None))
       ("/en/dessert_spoon", (* "Dessert spoon", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 1e-05, None))
       ("/en/gill_measure", (* "Gill", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.000118294, None))
       ("/en/cubic_mile", (* "Cubic mile", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 4.16818e+09, None))
       ("/en/cubic_metre", (* "Cubic metre", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 1.0, None))
       ("/en/jigger", (* "Jigger", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 44.36, None))
       ("/en/mutchkin", (* "Mutchkin", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 8.48e-07, None))
       ("/en/chopin_measure", (* "Chopin", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.000848, None))
       ("/en/milliliter", (* "Milliliter", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 1e-06, None))
       ("/en/gallon_imperial", (* "Gallon (Imperial)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.00454609, None))
       ("/en/fluid_ounce_imperial", (* "Fluid ounce (imperial)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 2.84131e-05, None))
       ("/en/pint_imperial", (* "Pint (Imperial)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.000568261, None))
       ("/en/quart_imperial", (* "Quart (Imperial)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.00113652, None))
       ("/en/cubic_centimetre", (* "Cubic centimetre", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 1e-06, None))
       ("/en/joug", (* "Joug", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.001696, None))
       ("/en/gallon_scots", (* "Gallon (Scots)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.013568, None))
       ("/en/cubic_kilometer", (* "Cubic kilometer", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 1e+09, None))
       ("/en/hectoliter", (* "Hectoliter", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.1, None))
       ("/en/fluid_dram_us", (* "Fluid Dram US", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 3.69669e-06, None))
       ("/en/tablespoon_au", (* "Tablespoon (AU)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 2e-05, None))
       ("/en/cup_imperial", (* "Cup (Imperial)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.000284131, None))
       ("/en/cup_metric", (* "Cup (Metric)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.00025, None))
       ("/en/cup_us", (* "Cup (US)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.00024, None))
       ("/en/cup_japan", (* "Cup (Japan)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.0002, None))
       ("/en/pint_us_dry", (* "Pint (US dry)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.00055061, None))
       ("/en/pint_metric", (* "Pint (metric)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.0005, None))
       ("/en/quart_us_dry", (* "Quart (US dry)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.00110121, None))
       ("/en/gallon_us_dry", (* "Gallon (US dry)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.00440488, None))
       ("/en/gill_imperial", (* "Gill (Imperial)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.000142065, None))
       ("/en/centiliter", (* "Centiliter", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 1e-05, None))
       ("/en/deciliter", (* "Deciliter", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.0001, None))
       ("/en/peck_us", (* "Peck (US)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.0090921, None))
       ("/en/bushel_us", (* "Bushel (US)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.0352391, None))
       ("/en/bushel_imperial", (* "Bushel (Imperial)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.0363687, None))
       ("/en/beer_barrel_us", (* "Beer barrel (US)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.117348, None))
       ("/en/beer_barrel_uk", (* "Beer barrel (UK)", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.163659, None))
       ("/en/oil_barrel", (* "Oil barrel", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.158987, None))
       ("/m/0h8pr0v", (* "Coles Lime 1lt", *) (Prod (Prod (SI "metre",SI "metre"),SI "metre"), 0.001, None))
       ("/en/kilogram_per_cubic_metre", (* "Kilogram per cubic metre", *) (Div (SI "kilogram",Prod (Prod (SI "metre",SI "metre"),SI "metre")), 1.0, None))
       ("/en/gram_per_cubic_centimeter", (* "Gram per cubic centimeter", *) (Div (SI "kilogram",Prod (Prod (SI "metre",SI "metre"),SI "metre")), 1000.0, None))
       ("/en/calendar_year", (* "Year", *) (SI "second", 3.1536e+07, None))
       ("/en/day", (* "Day", *) (SI "second", 86400.0, None))
       ("/en/hour", (* "Hour", *) (SI "second", 3600.0, None))
       ("/en/leap_year", (* "Leap year", *) (SI "second", 3.16224e+07, None))
       ("/en/minute", (* "Minute", *) (SI "second", 60.0, None))
       ("/en/month", (* "Month", *) (SI "second", 2.628e+06, None))
       ("/en/second", (* "Second", *) (SI "second", 1.0, None))
       ("/en/tropical_year", (* "Tropical year", *) (SI "second", 3.15569e+07, None))
       ("/en/week", (* "Week", *) (SI "second", 604800.0, None))
       ("/en/planck_time", (* "Planck time", *) (SI "second", 0.0, None))
       ("/en/millennium", (* "Millennium", *) (SI "second", 3.15567e+10, None))
       ("/en/julian_year", (* "Julian year", *) (SI "second", 3.15576e+07, None))
       ("/en/centisecond", (* "Centisecond", *) (SI "second", 0.01, None))
       ("/en/jiffy", (* "Jiffy", *) (SI "second", 0.01, None))
       ("/en/nanosecond", (* "Nanosecond", *) (SI "second", 1e-09, None))
       ("/en/millisecond", (* "Millisecond", *) (SI "second", 0.001, None))
       ("/en/microsecond", (* "Microsecond", *) (SI "second", 1e-06, None))
       ("/en/yottasecond", (* "yottasecond", *) (SI "second", 1e+24, None))
       ("/en/zettasecond", (* "zettasecond", *) (SI "second", 1e+21, None))
       ("/en/exasecond", (* "exasecond", *) (SI "second", 1e+18, None))
       ("/en/petasecond", (* "petasecond", *) (SI "second", 1e+15, None))
       ("/en/terasecond", (* "terasecond", *) (SI "second", 1e+12, None))
       ("/en/gigasecond", (* "gigasecond", *) (SI "second", 1e+09, None))
       ("/en/megasecond", (* "megasecond", *) (SI "second", 1e+06, None))
       ("/en/kilosecond", (* "kilosecond", *) (SI "second", 1000.0, None))
       ("/en/hectosecond", (* "hectosecond", *) (SI "second", 100.0, None))
       ("/en/decasecond", (* "decasecond", *) (SI "second", 10.0, None))
       ("/en/decisecond", (* "decisecond", *) (SI "second", 0.1, None))
       ("/en/picosecond", (* "picosecond", *) (SI "second", 1e-12, None))
       ("/en/femtosecond", (* "femtosecond", *) (SI "second", 1e-15, None))
       ("/en/attosecond", (* "attosecond", *) (SI "second", 1e-18, None))
       ("/en/zeptosecond", (* "zeptosecond", *) (SI "second", 1e-21, None))
       ("/en/yoctosecond", (* "yoctosecond", *) (SI "second", 1e-24, None))
       ("/en/million_years", (* "Million years", *) (SI "second", 3.15576e+13, None))
       ("/en/julian_century", (* "Julian Century", *) (SI "second", 3.15576e+09, None))
       ("/en/myriad_measure", (* "Myriad", *) (SI "second", 3.15576e+11, None))
       ("/en/carat", (* "Carat", *) (SI "kilogram", 0.0002, None))
       ("/en/kilogram", (* "Kilogram", *) (SI "kilogram", 1.0, None))
       ("/en/pound", (* "Pound", *) (SI "kilogram", 0.453592, None))
       ("/en/tonne", (* "Tonne", *) (SI "kilogram", 1000.0, None))
       ("/en/atomic_mass_unit", (* "Atomic mass unit", *) (SI "kilogram", 1.7e-27, None))
       ("/en/tael", (* "Tael", *) (SI "kilogram", 0.0377994, None))
       ("/en/grain_measure", (* "Grain", *) (SI "kilogram", 6.47989e-05, None))
       ("/en/planck_mass", (* "Planck mass", *) (SI "kilogram", 2.17645e-08, None))
       ("/en/solar_mass", (* "Solar mass", *) (SI "kilogram", 1.9891e+30, None))
       ("/en/ounce", (* "Ounce", *) (SI "kilogram", 0.0283495, None))
       ("/en/pennyweight", (* "Pennyweight", *) (SI "kilogram", 0.00155517, None))
       ("/en/long_ton", (* "Long ton", *) (SI "kilogram", 1016.05, None))
       ("/en/hundredweight", (* "Hundredweight", *) (SI "kilogram", 50.8023, None))
       ("/en/short_ton", (* "Short ton", *) (SI "kilogram", 907.185, None))
       ("/en/stone_measure", (* "Stone", *) (SI "kilogram", 6.35029, None))
       ("/en/dram", (* "Dram", *) (SI "kilogram", 0.00177185, None))
       ("/en/catty", (* "Catty", *) (SI "kilogram", 0.60479, None))
       ("/en/candareen", (* "Candareen", *) (SI "kilogram", 0.000377994, None))
       ("/en/mace_measure", (* "Mace", *) (SI "kilogram", 0.00377994, None))
       ("/en/grave_measure", (* "Grave", *) (SI "kilogram", 1.0, None))
       ("/en/milligram_measure", (* "Milligram", *) (SI "kilogram", 1e-06, None))
       ("/en/gram", (* "Gram", *) (SI "kilogram", 0.001, None))
       ("/en/microgram", (* "Microgram", *) (SI "kilogram", 1e-09, None))
       ("/en/picul", (* "Picul", *) (SI "kilogram", 60.479, None))
       ("/en/cash_measure", (* "Cash", *) (SI "kilogram", 3.77994e-06, None))
       ("/en/jupiter_mass", (* "Jupiter mass", *) (SI "kilogram", 1.8986e+27, None))
       ("/en/long_quarter", (* "Long quarter", *) (SI "kilogram", 12.7006, None))
       ("/en/cental", (* "Cental", *) (SI "kilogram", 45.3592, None))
       ("/en/troy_ounce", (* "Troy ounce", *) (SI "kilogram", 0.0311035, None))
       ("/en/troy_pound", (* "Troy pound", *) (SI "kilogram", 0.373242, None))
       ("/en/electron_rest_mass", (* "Electron rest mass", *) (SI "kilogram", 0.0, None))
       ("/en/short_quarter", (* "Short quarter", *) (SI "kilogram", 11.3398, None))
       ("/en/earth_mass", (* "Earth mass", *) (SI "kilogram", 5.9736e+28, None))
       ("/en/lunar_mass", (* "Lunar mass", *) (SI "kilogram", 7.3477e+22, None))
       ("/en/mev_c", (* "MeV/c²", *) (SI "kilogram", 1.6e-27, None))
       ("/en/gray", (* "Gray", *) (SI "gray", 1.0, None))
       ("/en/rad_measure", (* "Rad", *) (SI "gray", 0.01, None))
       ("/en/gray_per_second", (* "Gray per second", *) (Div (SI "gray",SI "second"), 1.0, None))
       ("/en/gal", (* "Gal", *) (Div (SI "metre",Prod (SI "second",SI "second")), 0.01, None))
       ("/en/metre_per_second_squared", (* "Metre per second squared", *) (Div (SI "metre",Prod (SI "second",SI "second")), 1.0, None))
       ("/en/g_force_measure", (* "g-force", *) (Div (SI "metre",Prod (SI "second",SI "second")), 9.80665, None))
       ("/en/mole_per_cubic_metre", (* "Mole per cubic metre", *) (Div (SI "mole",Prod (Prod (SI "metre",SI "metre"),SI "metre")), 1.0, None))
       ("/en/molar_measure", (* "Molar", *) (Div (SI "mole",Prod (Prod (SI "metre",SI "metre"),SI "metre")), 0.001, None))
       ("/en/nanomolar", (* "Nanomolar", *) (Div (SI "mole",Prod (Prod (SI "metre",SI "metre"),SI "metre")), 1e-09, None))
       ("/en/micromolar", (* "Micromolar", *) (Div (SI "mole",Prod (Prod (SI "metre",SI "metre"),SI "metre")), 1e-06, None))
       ("/en/mole_measure", (* "Mole", *) (SI "mole", 1.0, None))
       ("/en/micromole", (* "Micromole", *) (SI "mole", 1e-06, None))
       ("/en/millimole", (* "Millimole", *) (SI "mole", 0.001, None))
       ("/en/nanomole", (* "Nanomole", *) (SI "mole", 1e-08, None))
       ("/en/pound_mole", (* "Pound mole", *) (SI "mole", 4.53592e+39, None))
       ("/en/radian_per_second_squared", (* "Radian per second squared", *) (Div (One,Prod (SI "second",SI "second")), 1.0, None))
       ("/en/revolutions_per_minute", (* "Revolutions per minute", *) (Div (One,SI "second"), 0.10472, None))
       ("/en/radian_per_second", (* "Radian per second", *) (Div (One,SI "second"), 1.0, None))
       ("/en/farad", (* "Farad", *) (SI "farad", 1.0, None))
       ("/en/katal", (* "Katal", *) (SI "katal", 1.0, None))
       ("/en/enzyme_unit", (* "Enzyme unit", *) (SI "katal", 1.667e-08, None))
       ("/en/katal_per_cubic_metre", (* "Katal per cubic metre", *) (Div (SI "katal",Prod (Prod (SI "metre",SI "metre"),SI "metre")), 1.0, None))
       ("/en/coulomb", (* "Coulomb", *) (SI "coulomb", 1.0, None))
       ("/en/statcoulomb", (* "Statcoulomb", *) (SI "coulomb", 3.3356e-10, None))
       ("/en/elementary_charge", (* "Elementary charge", *) (SI "coulomb", 1.60218e-19, None))
       ("/en/planck_charge", (* "Planck charge", *) (SI "coulomb", 1.87555e-18, None))
       ("/en/abcoulomb", (* "Abcoulomb", *) (SI "coulomb", 10.0, None))
       ("/en/ampere-hour", (* "Ampere-hour", *) (SI "coulomb", 3600.0, None))
       ("/en/siemens", (* "Siemens", *) (SI "siemens", 1.0, None))
       ("/en/microsiemens", (* "Microsiemens", *) (SI "siemens", 1e-06, None))
       ("/en/millisiemens", (* "Millisiemens", *) (SI "siemens", 0.001, None))
       ("/en/siemens_per_meter", (* "Siemens per meter", *) (Div (SI "siemens",SI "metre"), 1.0, None))
       ("/en/microsiemens_per_centimeter", (* "Microsiemens per centimeter", *) (Div (SI "siemens",SI "metre"), 0.01, None))
       ("/en/ampere_per_square_metre", (* "Ampere per square metre", *) (Div (SI "ampere",Prod (SI "metre",SI "metre")), 1.0, None))
       ("/en/sievert", (* "Sievert", *) (SI "sievert", 1.0, None))
       ("/en/coulomb_per_cubic_metre", (* "Coulomb per cubic metre", *) (Div (SI "coulomb",Prod (Prod (SI "metre",SI "metre"),SI "metre")), 1.0, None))
       ("/en/ampere", (* "Ampere", *) (SI "ampere", 1.0, None))
       ("/en/abampere", (* "Abampere", *) (SI "ampere", 10.0, None))
       ("/en/volt_per_metre", (* "Volt per metre", *) (Div (SI "volt",SI "metre"), 1.0, None))
       ("/en/newtons_per_coulomb", (* "Newtons per coulomb", *) (Div (SI "volt",SI "metre"), 1.0, None))
       ("/en/coulomb_per_square_metre", (* "Coulomb per square metre", *) (Div (SI "coulomb",Prod (SI "metre",SI "metre")), 1.0, None))
       ("/en/joule_per_cubic_metre", (* "Joule per cubic metre", *) (Div (SI "joule",Prod (Prod (SI "metre",SI "metre"),SI "metre")), 1.0, None))
       ("/en/coulomb_per_kilogram", (* "Coulomb per kilogram", *) (Div (SI "coulomb",SI "kilogram"), 1.0, None))
       ("/en/dyne", (* "Dyne", *) (SI "newton", 1e-05, None))
       ("/en/newton", (* "Newton", *) (SI "newton", 1.0, None))
       ("/en/pound-force", (* "Pound-force", *) (SI "newton", 4.44822, None))
       ("/en/kilogram-force", (* "Kilogram-force", *) (SI "newton", 9.80665, None))
       ("/en/poundal", (* "Poundal", *) (SI "newton", 0.138255, None))
       ("/en/kilonewton", (* "KiloNewton", *) (SI "newton", 1000.0, None))
       ("/en/joule_per_kelvin", (* "Joule per kelvin", *) (Div (SI "joule",SI "kelvin"), 1.0, None))
       ("/en/lux", (* "Lux", *) (SI "lux", 1.0, None))
       ("/en/henry_measure", (* "Henry", *) (SI "henry", 1.0, None))
       ("/en/inverse_kelvin", (* "Inverse Kelvin", *) (Div (One,SI "kelvin"), 1.0, None))
       ("/en/micro_per_degree_c", (* "micro per degree C", *) (Div (One,SI "kelvin"), 1e-06, None))
       ("/en/micro_per_degree_f", (* "micro per degree F", *) (Div (One,SI "kelvin"), 5.556e-07, None))
       ("/en/watt_per_square_metre", (* "Watt per square metre", *) (Div (SI "watt",Prod (SI "metre",SI "metre")), 1.0, None))
       ("/en/footlambert", (* "Footlambert", *) (Div (SI "candela",Prod (SI "metre",SI "metre")), 3.42626, None))
       ("/en/lambert_measure", (* "Lambert", *) (Div (SI "candela",Prod (SI "metre",SI "metre")), 3183.1, None))
       ("/en/candela_per_square_metre", (* "Candela per square metre", *) (Div (SI "candela",Prod (SI "metre",SI "metre")), 1.0, None))
       ("/en/stilb", (* "Stilb", *) (Div (SI "candela",Prod (SI "metre",SI "metre")), 10000.0, None))
       ("/en/lumen_measure", (* "Lumen", *) (SI "lumen", 1.0, None))
       ("/en/candela", (* "Candela", *) (SI "candela", 1.0, None))
       ("/en/oersted", (* "Oersted", *) (Div (SI "ampere",Prod (SI "metre",SI "metre")), 79.5775, None))
       ("/en/ampere_per_metre", (* "Ampere per metre", *) (Div (SI "ampere",Prod (SI "metre",SI "metre")), 1.0, None))
       ("/en/weber", (* "Weber", *) (SI "weber", 1.0, None))
       ("/en/gauss_measure", (* "Gauss", *) (SI "tesla", 0.0001, None))
       ("/en/tesla_measure", (* "Tesla", *) (SI "tesla", 1.0, None))
       ("/en/joule_per_mole", (* "Joule per mole", *) (Div (SI "joule",SI "mole"), 1.0, None))
       ("/en/kilojoule_per_mole", (* "Kilojoule per mole", *) (Div (SI "joule",SI "mole"), 1000.0, None))
       ("/en/joule_per_mole_per_kelvin", (* "Joule per mole per kelvin", *) (Div (Div (SI "joule",SI "mole"),SI "kelvin"), 1.0, None))
       ("/en/cubic_metre_per_mole", (* "Cubic metre per mole", *) (Div (Prod (Prod (SI "metre",SI "metre"),SI "metre"),SI "mole"), 1.0, None))
       ("/en/newton_metre", (* "Newton metre", *) (Prod (SI "newton",SI "metre"), 1.0, None))
       ("/en/foot-pound_force", (* "Foot-pound force", *) (Prod (SI "newton",SI "metre"), 1.35582, None))
       ("/en/ounce_force_inch", (* "Ounce-force inch", *) (Prod (SI "newton",SI "metre"), 0.00706155, None))
       ("/en/henry_per_metre", (* "Henry per metre", *) (Div (SI "henry",SI "metre"), 1.0, None))
       ("/en/farad_per_metre", (* "Farad per metre", *) (Div (SI "farad",SI "metre"), 1.0, None))
       ("/en/volt", (* "Volt", *) (SI "volt", 1.0, None))
       ("/en/watt_per_square_metre_per_steradian", (* "Watt per square metre per steradian", *) (Div (Div (SI "watt",Prod (SI "metre",SI "metre")),One), 1.0, None))
       ("/en/watt_per_steradian", (* "Watt per steradian", *) (Div (SI "watt",One), 1.0, None))
       ("/en/curie", (* "Curie", *) (SI "becquerel", 3.7e+10, None))
       ("/en/becquerel", (* "Becquerel", *) (SI "becquerel", 1.0, None))
       ("/en/ohm", (* "Ohm", *) (SI "ohm", 1.0, None))
       ("/en/ohm_meter", (* "Ohm meter", *) (Prod (SI "ohm",SI "metre"), 1.0, None))
       ("/en/ohm_centimeter", (* "Ohm centimeter", *) (Prod (SI "ohm",SI "metre"), 0.01, None))
       ("/en/joule_per_kilogram", (* "Joule per kilogram", *) (Div (SI "joule",SI "kilogram"), 1.0, None))
       ("/en/joule_per_gram", (* "Joule per gram", *) (Div (SI "joule",SI "kilogram"), 0.001, None))
       ("/en/joule_per_kilogram_per_kelvin", (* "Joule per kilogram per kelvin", *) (Div (Div (SI "joule",SI "kilogram"),SI "kelvin"), 1.0, None))
       ("/en/joule_per_gram_per_kelvin", (* "Joule per gram per kelvin", *) (Div (Div (SI "joule",SI "kilogram"),SI "kelvin"), 1000.0, None))
       ("/en/btu_per_pound_mass_per_degree_f", (* "Btu per pound-mass per degree F", *) (Div (Div (SI "joule",SI "kilogram"),SI "kelvin"), 4.1868, None))
       ("/en/cubic_metre_per_kilogram", (* "Cubic metre per kilogram", *) (Div (Prod (Prod (SI "metre",SI "metre"),SI "metre"),SI "kilogram"), 1.0, None))
       ("/en/kilogram_per_square_metre", (* "Kilogram per square metre", *) (Div (SI "kilogram",Prod (SI "metre",SI "metre")), 1.0, None))
       ("/en/newton_per_metre", (* "Newton per metre", *) (Div (SI "newton",SI "metre"), 1.0, None))
       ("/en/celsius", (* "Degree Celsius", *) (SI "kelvin", 1.0, Some 273.15))
       ("/en/fahrenheit", (* "Degree Fahrenheit", *) (SI "kelvin", 0.55555, Some 255.372))
       ("/en/kelvin", (* "Kelvin", *) (SI "kelvin", 1.0, Some 0.0))
       ("/en/rankine", (* "Degree Rankine", *) (SI "kelvin", 0.55555, Some 0.0))
       ("/en/watt_per_metre_per_kelvin", (* "Watt per meter per kelvin", *) (Div (Div (SI "watt",SI "metre"),SI "kelvin"), 1.0, None))
       ("/en/pascal_second_measure", (* "Pascal second", *) (Prod (SI "pascal",SI "second"), 1.0, None))
       ("/en/cubic_metres_per_second", (* "Cubic metres per second", *) (Div (Prod (Prod (SI "metre",SI "metre"),SI "metre"),SI "second"), 1.0, None))
       ("/en/barrel_petroleum_per_day", (* "Barrel (petroleum) per day", *) (Div (Prod (Prod (SI "metre",SI "metre"),SI "metre"),SI "second"), 1.84e-06, None))
       ("/en/reciprocal_metre", (* "Reciprocal metre", *) (Div (One,SI "metre"), 1.0, None))
       ("/en/hertz", (* "Hertz", *) (SI "hertz", 1.0, None))
       ("/en/daily", (* "Daily", *) (SI "hertz", 1.15741e-05, None))
       ("/en/biweekly", (* "Once every two weeks", *) (SI "hertz", 8.27e-07, None))
       ("/en/weekly", (* "Weekly", *) (SI "hertz", 1.653e-06, None))
       ("/en/megahertz", (* "Megahertz", *) (SI "hertz", 1e+06, None))
       ("/en/gigahertz", (* "Gigahertz", *) (SI "hertz", 1e+09, None))
       ("/en/hourly", (* "Hourly", *) (SI "hertz", 0.000277778, None))
       ("/en/kilohertz_measure", (* "Kilohertz", *) (SI "hertz", 1000.0, None))
  ]
