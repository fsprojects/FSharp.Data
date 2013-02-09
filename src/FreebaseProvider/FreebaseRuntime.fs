// --------------------------------------------------------------------------------------
// Freebase type provider - runtime components
// --------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation 2005-2012.
// This sample code is provided "as is" without warranty of any kind. 
// We disclaim all warranties, either express or implied, including the 
// warranties of merchantability and fitness for a particular purpose. 
// --------------------------------------------------------------------------------------

namespace FSharp.Data.RuntimeImplementation.Freebase

open System
open System.Linq
open System.Text
open System.Collections.Generic
open Microsoft.FSharp.Core.CompilerServices
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open FSharp.Data.RuntimeImplementation.Freebase.FreebaseSchema
open FSharp.Data.RuntimeImplementation.Freebase.FreebaseRequests

/// Extension members for operations permitted in queries of the Freebase service
[<AutoOpen>]
module public FreebaseOperators = 
    open System

    type System.String with 
        /// A Freebase query operation that represents a perl-style match of a string, e.g. "book club", "book*", "*book", "*book*", "^book", "book$", "* book *", "book-club", "book\-club". See http://www.freebase.com/docs/mql/ch03.html#directives.
        [<CompiledName("ApproximatelyMatches")>]
        member s.ApproximatelyMatches(_pat:string) : bool = failwith "'ApproximatelyMatches' may only be used in a query executed on the Freebase server."

        /// A Freebase query operation that represents approximately matching one of the given strings. See http://www.freebase.com/docs/mql/ch03.html#directives.
        [<CompiledName("ApproximatelyOneOf")>]
        member s.ApproximatelyOneOf([<ParamArray>] args:string[]) : bool = 
            if args.Length = 0 then false
            else failwith "'ApproximatelyOneOf' may only be used in a query executed on the server. It must be given at least one value."

    type System.Linq.IQueryable<'T> with 
       /// A Freebase query operation returning an approximate count of the items satisfying a query.
       [<CompiledName("ApproximateCount")>]
       member s.ApproximateCount() : int = 
           // Uses the standard LINQ technique to fold the operator into the query
           let m = match <@ Unchecked.defaultof<System.Linq.IQueryable<'T>>.ApproximateCount() @> with Quotations.Patterns.Call(None, mb, _) -> mb | _ -> failwith "unexpected"
           let expr = System.Linq.Expressions.Expression.Call(null,m,[| s.Expression |])
           s.Provider.Execute<int32>(expr)

       /// Synonym for LINQ's Count
       // Included so you don't have to open System.LINQ to use the queries
       [<CompiledName("Count")>]
       member s.Count() : int =  System.Linq.Queryable.Count(s)

       /// Synonym for LINQ's Where
       // Included so you don't have to open System.LINQ to use the queries
       [<CompiledName("Where")>]
       member s.Where(p:System.Linq.Expressions.Expression<System.Func<_,_>>) =  System.Linq.Queryable.Where(s,p)

/// Represents data for a single object
type internal FreebasePropertyBag(dict : IDictionary<string,JsonValue>) =
        
    member s.Dictionary = dict
    member s.Item 
        with get id = 
            if not (dict.ContainsKey id) then failwith (sprintf "property '%s' not found in object property bag, keys = %s" id (String.concat "," [ for k in dict.Keys -> "'" + k + "'"]))
            dict.[id]

/// Represents a data connection to Freebase
type internal FreebaseDataConnection (fb:FreebaseQueries, fbSchema: FreebaseSchemaConnection, useUnits:bool, allowQueryEvaluateOnClientSide: bool) = 

    static let getLimitText objectLimit queryConstraints = 
        match queryConstraints |> List.tryPick (fun (k,v) -> if k = "limit" then Some v else None) with 
        | Some lim -> "",Some (int32 lim)
        | _ when objectLimit = -1 -> "",Some -1
        | _ -> sprintf ", 'limit': %d" objectLimit, None

    // A look aside table for computing schema information for properties.
    let propsById = Dictionary<(string * string),FreebaseProperty>(HashIdentity.Structural) 
    let defaultObjectLimit = 500
       
    member internal __.Connection = fb
    member internal __.Schema = fbSchema
    member val internal Limit = defaultObjectLimit with get, set
    member internal __.UseUnits = useUnits
    member internal __.AllowQueryEvaluateOnClientSide = allowQueryEvaluateOnClientSide
    
    /// Get the FreebaseProperty object that gives schema information for a particular property of a particular type.
    member internal __.TryGetPropertyById(typeId, propId) = 
        match propsById.TryGetValue((typeId, propId)) with
        | true, res -> Some res
        | _ -> 
           match fbSchema.GetTypeByTypeId typeId with 
           | None -> None
           | Some fbType ->
               for p in fbType.Properties do propsById.[(typeId,p.Id)] <- p 
               match propsById.TryGetValue((typeId, propId)) with 
               | true, res -> Some res
               | _ -> None

    member __.QueryFragmentsOfPropertiesOfAllIncludedTypes(typeId:string, queryConstraints ) =
         [ for p in fbSchema.GetAllPropertiesOfAllIncludedTypesOfTypeId typeId do
               if not (queryConstraints |> List.exists (fun (k,_v) -> k = p.Id)) then
                   match p.BasicSystemType, (p.IsUnique || p.IsEnum) with 
                   // Compund, non-unique: not eagerly loaded
                   | Some _, false -> yield sprintf ", '%s' : []" p.Id
                  // Could add this for unique objects with BasicSystemType.IsNone??
                  //| None, true -> sprintf ", '%s' : [{ '/type/object/type' : null, '/type/object/id' : null, '/type/object/name' : null, 'limit':1 }]" p.Id
                   // Compund, unique: not eagerly loaded
                   | Some _, true -> yield sprintf ", '%s' : null" p.Id
                   // Non-compund: eagerly loaded as field
                   | None, _ -> () ]
         |> String.concat ""

    member __.GetInitialDataForObjects (query, explicitLimit) =
        seq { for obj in fb.QuerySequence<IDictionary<string,JsonValue> >(query, dictionaryFromJson, explicitLimit) do
                yield FreebasePropertyBag(obj) } 

    /// Get property bags for all the objects of the given type, at the given type
    member fbDataConn.GetInitialDataForObjectsFromQueryText(queryConstraints:(string * string) list, typeId:string, objectLimit) =
        let fields = fbDataConn.QueryFragmentsOfPropertiesOfAllIncludedTypes (typeId, queryConstraints)
        let queryText = queryConstraints |> List.map (fun (k,v) -> sprintf ", '%s' : %s" k (match v with null -> "null" | s -> s)) |> String.concat ""
        let limitText, explicitLimit = getLimitText objectLimit queryConstraints
        let query = sprintf "[{ '/type/object/type' : '%s' %s %s , '/type/object/id' : null, '/type/object/name' : null %s}]" typeId queryText fields  limitText
        fbDataConn.GetInitialDataForObjects (query, explicitLimit)
    
    member fbDataConn.GetInitialDataForKnownObject(fbTypeId, fbObjId:string) =
        let fields = fbDataConn.QueryFragmentsOfPropertiesOfAllIncludedTypes (fbTypeId, [])
        let query = sprintf "[{ '/type/object/type' : '%s', '/type/object/id' : '%s', '/type/object/name' : null %s }]" fbTypeId fbObjId fields 
        match fbDataConn.GetInitialDataForObjects (query, None) |> Seq.toArray with 
        | [| obj |] -> obj
        | _ -> failwith (sprintf "object id '%s' not available" fbObjId)
    
    /// Get a property bag for a specific object, giving values for the properties of the given type 
    member fbDataConn.GetInitialDataForSpecificObjectOfType(fbTypeId:string, fbId: string) =
        let fields = fbDataConn.QueryFragmentsOfPropertiesOfAllIncludedTypes (fbTypeId, [])
        let query = sprintf "[{ '/type/object/type'  '%s', '/type/object/id' : '%s', '/type/object/name' : null %s }]" fbTypeId fbId fields 
        match fbDataConn.GetInitialDataForObjects (query, None) |> Seq.toArray with 
        | [| obj |] -> obj
        | _ -> failwith (sprintf "object id '%s' not available" fbId)

    /// Get property bags for all the objects in the specific property relation to a given object, giving values for the properties of the given property type 
    member fbDataConn.GetInitialDataForAllObjectsForPropertyOfObject(declaringObjectId:string,declaringTypeId:string,property:FreebaseProperty,fbTypeId:string,objectLimit) =
        let fields = fbDataConn.QueryFragmentsOfPropertiesOfAllIncludedTypes (fbTypeId, [])
        let limitText, explicitLimit = getLimitText objectLimit []
        let query = sprintf "[{'/type/object/id':null, '/type/object/name':null %s, 'optional':true, '/type/object/type':'%s', '!%s': [{'/type/object/id':'%s','/type/object/type':'%s' %s}]}]"  fields property.ExpectedType property.Id declaringObjectId declaringTypeId limitText
        fbDataConn.GetInitialDataForObjects (query, explicitLimit)


[<AllowNullLiteral>]
/// Represents a single object drawn from Freebase. 
type public IFreebaseObject = 
    /// The ID of this item
    abstract Id: string
    /// The name of this item
    abstract Name: string
    /// The main image associated with this item
    abstract MainImage : string
    /// The Blurb text for this item, if any
    abstract Blurb : string list
    /// Images associated with this item. 
    abstract GetImages : unit -> seq<string>
    /// Get a property by identifier, with a strong type
    abstract GetPropertyByIdTyped<'T> : declaringTypeId: string * propertyId:string -> 'T 
    /// Get a property by identifier, with a strong type
    abstract GetPropertyById : declaringTypeId: string * propertyId:string -> obj

module private RuntimeConversion = 
    // The handwritten JSON deserializer uses 'JsonValue.N of decimal | JsonValue.D of double' for numbers, but the schema typing expects 'double | Nullable<double>'
    //   - convJsonPrimValue has converted this to (null | boxed-decimal | boxed-double)
    let convertUnits (useUnits:bool) (isIncoming:bool) (fbProp: FreebaseProperty) (fv:double)  = 
        let u = fbProp.UnitOfMeasure
        if useUnits && units.ContainsKey u  then 
            let (_measureAnnotation,conversionFactor,offset) = units.[u]
            let offset = match offset with Some x -> x | None -> 0.0
            if isIncoming then (fv + offset) * conversionFactor
            else (fv / conversionFactor) - offset
        else
            fv

    let convertOne (useUnits:bool) (isIncoming:bool) (rawResult:obj) (targetType: Type) (fbProp: FreebaseProperty) = 
        let v = 
            match rawResult with 
            | null                      when targetType = typeof<bool> -> false |> box
            | null                      when targetType = typeof<int> -> 0 |> box 
            | null                      when targetType = typeof<double> -> 0.0 |> box 
            | :? bool                   when targetType = typeof<bool> -> rawResult
            | :? decimal           as x when targetType = typeof<int> -> int x |> box 
            | :? decimal           as x when targetType = typeof<double> -> double x |> box 
            | :? double            as x when targetType = typeof<double> -> x |> box 
            | null                      when targetType = typeof<Nullable<bool>> -> Nullable<bool>() |> box 
            | null                      when targetType = typeof<Nullable<int>> -> Nullable<int>() |> box 
            | null                      when targetType = typeof<Nullable<double>> -> Nullable<double>() |> box 
            | :? bool              as x when targetType = typeof<Nullable<bool>> -> Nullable<bool>(x) |> box
            | :? decimal           as x when targetType = typeof<Nullable<int>> -> Nullable<int>(int x) |> box 
            | :? decimal           as x when targetType = typeof<Nullable<double>> -> Nullable<double>(double x) |> box 
            | :? double            as x when targetType = typeof<Nullable<double>> -> Nullable<double>(x) |> box 
            | v -> v
        // convert unit at runtime
        let v = 
            match v with 
            | (:? Nullable<double> as d) when d.HasValue -> Nullable<double>(convertUnits useUnits isIncoming fbProp d.Value) |> box
            | (:? double as d) -> convertUnits useUnits isIncoming fbProp d |> box
            | _ -> v
        v


/// Represents a single object drawn from Freebase. 
//
// Note: A Freebase object may have multiple types. In this (somewhat awkward) implementation, the object 
// is biased towards one of these types ('firstType'). This is typically the static type for the object related to the 
// program point where the object is created. The non-compound properties of the object are eagerly fetched and expanded 
// in 'objProps'.  Other properties, including all compound properties and simple properties on other types (e.g. base types) are fetched on-demand.
// This bias also extends to how the object is presented via ToString and the ICustomTypeDescriptor
// property descriptions, since only the properties from 'firstType' are returned. 
[<StructuredFormatDisplay("{DisplayText}")>]
type public FreebaseObject internal (fb:FreebaseDataConnection, objProps:FreebasePropertyBag, firstTypeId:string) = 
    // Some properties are computed on-demand. This is a lookaside table for those properties.
    let objPropsOnDemand = Dictionary<string,obj>(HashIdentity.Structural)

    let rawResultToStringSequence (rawResult: obj) = 
        seq { match rawResult with 
              | null -> ()
              | :? System.Collections.IEnumerable as e -> for o in e do yield (string o) 
              | _ -> invalidArg "rawResult" "not a sequence"  }

    // This is the public entry point
    member public fbo.GetPropertyByIdTyped<'T>(declaringTypeId: string, propertyId:string) : 'T = 
        fbo.GetPropertyById (declaringTypeId, propertyId) |> unbox

    // Get a non-compund property. If it is eagerly populated then we fetch from 'objProps'
    // directly. Otherwise, the property must be populated on-demand.
    member internal this.GetSimplePropertyById(declaringTypeId:string, propertyId:string) : obj = 
        let extractPrimValue v = 
            match v with 
            // Some constraints cause Freebase primitives to be extracted to { 'value' : 3 }
            | JsonValue.Object map ->
                match Map.toList map with
                | [ "value", v ] -> convJsonPrimValue v
                | _ -> convJsonPrimValue v
            | v -> convJsonPrimValue v
        
        if objProps.Dictionary.ContainsKey propertyId then 
            extractPrimValue objProps.[propertyId]
        else
           match objPropsOnDemand.TryGetValue(propertyId) with
           | true, res -> res
           | _ -> 
               //printf "lazily populating properties for type '%s' for object '%s'" declaringTypeId this.Id 
               match fb.Schema.GetTypeByTypeId declaringTypeId with 
               | None -> null
               | Some fbDeclaringType ->
                   let obj = fb.GetInitialDataForSpecificObjectOfType(fbDeclaringType.Id, this.Id)
                   for (KeyValue(k,v)) in obj.Dictionary do
                       objPropsOnDemand.[k] <- extractPrimValue v
                   match objPropsOnDemand.TryGetValue(propertyId) with
                   | true, res -> res
                   | _ -> failwith (sprintf "could not lazily populate property '%s' for type '%s' for object '%s', keys = %s" propertyId declaringTypeId this.Id (String.concat "," [ for k in obj.Dictionary.Keys -> "'" + k + "'"]))

    member internal this.GetPropertyById(declaringTypeId:string, propertyId:string) : obj = 
        if propertyId = "/type/object/id" then box objProps.["/type/object/id"]
        elif propertyId = "/type/object/name" then box objProps.["/type/object/name"]
        else
            let fbPropOpt = fb.TryGetPropertyById((declaringTypeId, propertyId))
            match fbPropOpt with 
            | None -> failwith (sprintf "couldn't find information for property '%s' of type '%s'" propertyId declaringTypeId)
            | Some fbProp ->
            let isUnique = fbProp.IsUnique || fbProp.IsEnum
            match fbProp.BasicSystemType with
            // Unique or sequence of compound type: no basic system type
            | None -> 
                let propTypeOpt = fb.Schema.GetTypeByTypeId fbProp.ExpectedType
                match propTypeOpt with 
                | Some propType ->
                  memoizeLookup objPropsOnDemand propertyId (fun _propertyId -> 
                    let results = 
                        seq { for objData in fb.GetInitialDataForAllObjectsForPropertyOfObject(this.Id,declaringTypeId,fbProp,propType.Id,fb.Limit) do
                                 yield FreebaseObject(fb,objData,propType.Id) }
                    if fbProp.IsUnique then 
                        match results |> Seq.toList with
                        | objData :: _ -> objData |> box
                        | [ ] -> null 
                    else
                       box results)
                | _ ->
                    let raw = this.GetSimplePropertyById(declaringTypeId, propertyId)
                    if fbProp.IsUnique then  
                        // Unpublished types get a string 
                       raw
                    else
                        // Unpublished types get a string sequence
                       raw |> rawResultToStringSequence |> box 

            // Unique or sequence of primitve results: create array, apply unit transformations
            | Some (basicType,supportsNull) ->

                let rawResult = this.GetSimplePropertyById(declaringTypeId, propertyId)
                if isUnique then
                    let targetType = if supportsNull then basicType else makeRuntimeNullableTy basicType
                    RuntimeConversion.convertOne fb.UseUnits true rawResult targetType fbProp
                elif basicType=typeof<string> then
                    this.GetSimplePropertyById(declaringTypeId, propertyId)
                    |> rawResultToStringSequence 
                    |> box 
                else
                    let rawResult = this.GetSimplePropertyById(declaringTypeId, propertyId)
                    let rawResults = rawResult :?> obj[]
                    let arr = System.Array.CreateInstance(basicType, rawResults.Length)
                    for i in 0 .. rawResults.Length - 1 do 
                        arr.SetValue(RuntimeConversion.convertOne fb.UseUnits true rawResults.[i] basicType fbProp, i)
                    arr |> box

    
    /// Images associated with this item. 
    member this.GetImages() = FreebaseImageInformation.GetImages(fb.Connection,this.Id)

    /// The ID of this item
    member this.Id = match objProps.["/type/object/id"] with JsonValue.String s -> s | _ -> failwith "id was not a string"

    /// The Name of this item
    member this.Name = match objProps.["/type/object/name"] with JsonValue.String s -> s | JsonValue.Null -> null |  _ -> failwith "name was not a string"

    /// The Blurb text for this item, if any
    member this.Blurb = fb.Schema.GetBlurbById this.Id

    /// The main image associated with this item, if any
    // It seems like there's not a good way at bind-time to pick the first out of the Images array.
    // Randomly choosing the first one.
    member this.MainImage = this.GetImages().FirstOrDefault()

    interface IFreebaseObject with 
        member x.Id = x.Id
        member x.Name = x.Name
        member x.MainImage = x.MainImage
        member x.Blurb = x.Blurb
        member x.GetImages() = x.GetImages()
        member x.GetPropertyByIdTyped<'T>(declaringTypeId, propertyId) = x.GetPropertyByIdTyped<'T>(declaringTypeId, propertyId)
        member x.GetPropertyById(declaringTypeId, propertyId) = x.GetPropertyById(declaringTypeId, propertyId)

    member this.DisplayText = this.ToString()
    override this.ToString() =
        match box dict with
        | null -> "null:" + firstTypeId
        | _ -> 
            match this.Name with 
            | null -> this.Id + ":" + firstTypeId
            | nm -> nm
#if FX_NO_CUSTOMTYPEDESCRIPTOR
#else
    interface System.ComponentModel.ICustomTypeDescriptor with
        member this.GetAttributes() = System.ComponentModel.AttributeCollection.Empty
        member this.GetClassName() = null
        member this.GetComponentName() = null
        member this.GetConverter() = new System.ComponentModel.TypeConverter()
        member this.GetDefaultEvent() = null
        member this.GetDefaultProperty() = null
        member this.GetEditor _ = null
        member this.GetEvents() = System.ComponentModel.EventDescriptorCollection.Empty
        member this.GetEvents _ = System.ComponentModel.EventDescriptorCollection.Empty
        // None of our properties have attributes
        member this.GetProperties _ = System.ComponentModel.PropertyDescriptorCollection.Empty
        member this.GetPropertyOwner _p = this :> obj
        member this.GetProperties() =
            let mangle (s : string) =
                new String(
                    [|  let prevSpace = ref false
                        for c in s do
                            match c with
                            |   ' ' -> prevSpace := true
                            |   c when !prevSpace -> prevSpace := false; yield Char.ToUpperInvariant(c)
                            |   _ -> yield c |])
            let props = 
                System.ComponentModel.PropertyDescriptorCollection(
                    [| match fb.Schema.GetTypeByTypeId firstTypeId with 
                       | Some firstType -> 
                         for p in firstType.Properties do
                            // mangling the property name to make properties available for WPF data binding
                            let propName = p.PropertyName |> mangle
                            // Compute the erased type
                            let typ = p.FSharpPropertyRuntimeType(fb.Schema,typeof<FreebaseObject>)
                            yield
                                { new System.ComponentModel.PropertyDescriptor(propName, [||]) with
                                        override __.IsReadOnly = true
                                        override __.ComponentType = typeof<FreebaseObject>
                                        override __.PropertyType = typ
                                        override __.CanResetValue _ = false
                                        override __.GetValue o = (o :?> FreebaseObject).GetPropertyById(firstType.Id,p.Id) 
                                        override __.ResetValue _ = failwith "Not implemented"
                                        override __.SetValue(_,_) = failwith "Not implemented"
                                        override __.ShouldSerializeValue _ = false }
                       | None -> () |], true)
            props
#endif

module internal QueryImplementation = 
    open System.Linq
    open System.Linq.Expressions
    open System.Reflection

    /// TODO: make this a parameter
    let evaluateOnClientSideWhereNecessary = true
    let (|MethodWithName|_|) (s:string) (m:MethodInfo) =  if s = m.Name then Some () else None
    let (|PropertyWithName|_|) (s:string) (m:PropertyInfo) =  if s = m.Name then Some () else None

    let (|MethodCall|_|) (e:Expression) = 
        match e.NodeType, e with 
        | ExpressionType.Call, (:? MethodCallExpression as e) ->  
            Some ((match e.Object with null -> None | obj -> Some obj), e.Method, Seq.toList e.Arguments)
        | _ -> None

    let (|AsType|_|) (e:Expression) = 
        match e.NodeType, e with 
        | ExpressionType.TypeAs, (:? UnaryExpression as e) ->  Some (e.Operand, e.Type)
        | _ -> None

    let (|NewArray|_|) (e:Expression) = 
        match e.NodeType, e with 
        | ExpressionType.NewArrayInit, (:? NewArrayExpression as e) ->  Some (Seq.toList e.Expressions)
        | _ -> None

    let (|PropertyGet|_|) (e:Expression) = 
        match e.NodeType, e with 
        | ExpressionType.MemberAccess, ( :? MemberExpression as e) -> 
            match e.Member with 
            | :? PropertyInfo as p -> 
                 Some ((match e.Expression with null -> None | obj -> Some obj), p)
            | _ -> None
        | _ -> None

    let (|Constant|_|) (e:Expression) = 
        match e.NodeType, e with 
        | ExpressionType.Constant, (:? ConstantExpression as ce) ->  Some (ce.Value, ce.Type)
        | _ -> None

    let (|String|_|) = function | Constant((:? string as s),_) -> Some s | _ -> None
    let (|Int32|_|) = function | Constant((:? int as s),_) -> Some s | _ -> None
    let (|Null|_|) = function Constant(null,_) -> Some () | _ -> None
    let (|Double|_|) = function Constant((:? double as s),_) -> Some s | _ -> None
    let (|Decimal|_|) = function Constant((:? decimal as s),_) -> Some s | _ -> None

    let (|Convert|_|) (e:Expression) = 
        match e.NodeType, e with 
        | ExpressionType.Convert, (:? UnaryExpression as ce) ->  Some (ce.Operand, ce.Type)
        | _ -> None
    
    let (|Var|_|) (e:Expression) = 
        match e.NodeType, e with 
        | ExpressionType.Parameter, (:? ParameterExpression as ce) ->  Some ce
        | _ -> None

    let (|Lambda|_|) (e:Expression) = 
        match e.NodeType, e with 
        | ExpressionType.Lambda, (:? LambdaExpression as ce) ->  Some (Seq.toList ce.Parameters, ce.Body)
        | _ -> None

    let (|LetExpr|_|) (e:System.Linq.Expressions.Expression) = 
        match e with 
        | MethodCall(Some (Lambda([v],body)), m, [arg]) when m.Name = "Invoke" ->  Some(v,arg,body)
        | _ -> None

    let (|OptionalQuote|) (e:Expression) = 
        match e.NodeType, e with 
        | ExpressionType.Quote, (:? UnaryExpression as ce) ->  ce.Operand
        | _ -> e

    let (|FreebaseRelOp|_|) (e:Expression) = 
        match e.NodeType, e with 
        | ExpressionType.Equal, (:? BinaryExpression as ce) ->  Some ("=", ce.Left, ce.Right)
        | ExpressionType.LessThan, (:? BinaryExpression as ce) ->  Some ("<", ce.Left, ce.Right)
        | ExpressionType.LessThanOrEqual, (:? BinaryExpression as ce) ->  Some ("<=", ce.Left, ce.Right)
        | ExpressionType.GreaterThan, (:? BinaryExpression as ce) ->  Some (">", ce.Left, ce.Right)
        | ExpressionType.GreaterThanOrEqual, (:? BinaryExpression as ce) ->  Some (">=", ce.Left, ce.Right)
        | ExpressionType.NotEqual, (:? BinaryExpression as ce) ->  Some ("!=", ce.Left, ce.Right)
        | _, MethodCall(_,MethodWithName "ApproximatelyMatches",[l;r]) ->  Some ("~=", l, r)
        | _ -> None

    let (|FreebaseConstant|_|) (e:Expression) = 
        match e with 
        | String s -> box s  |> Some
        | Convert(Int32 i, _)  -> box (Nullable<int>(i)) |> Some
        | Convert(Double i, _) -> box (Nullable<double>(i)) |> Some
        | Convert(Decimal i, _) -> box (Nullable<decimal>(i)) |> Some
        | Int32 i  -> box i |> Some
        | Double i -> box i |> Some
        | Decimal i -> box i |> Some
        | _ -> None //failwithf "unrecognized query constant '%A'" e

    let (|FreebaseConstants|_|) (es:Expression list) = 
        let es2 = es |> List.map (|FreebaseConstant|_|) 
        if List.forall Option.isSome es2 then Some (List.map Option.get es2) else None

    let (|FreebaseApproximatelyOneOfOp|_|) (e:Expression) = 
        match e with 
        | MethodCall(_,MethodWithName "ApproximatelyOneOf",[arg; NewArray (FreebaseConstants args)]) ->  Some (arg, args)
        | _ -> None

    let rec (|FreebasePropertyGet|_|) e = 
        match e with 
        | AsType(FreebasePropertyGet(v),_) -> Some v //look through 'Coerce' nodes
        | MethodCall(Some e, (MethodWithName "GetPropertyByIdTyped"), [String typeId; String propId]) -> Some (e, typeId, propId)
        | PropertyGet(Some e, PropertyWithName "Name") -> Some(e, "/type/object", "/type/object/name")
        | PropertyGet(Some e, PropertyWithName "Id") -> Some(e, "/type/object", "/type/object/id")
        | LetExpr(v,e,FreebasePropertyGet(Var v2, typeId, propId)) when v = v2 -> Some (e, typeId, propId)
        | _ -> None

    // A fetch of 'Value' on a Nullable value can be looked through
    let rec (|FreebasePropertyGetWithPossibleNullableValueFetch|_|) e = 
        match e with 
        | FreebasePropertyGet (e,typeId,propId) -> Some (e,typeId,propId) 
        | PropertyGet(Some (FreebasePropertyGet (e,typeId,propId)), PropertyWithName "Value") -> Some (e, typeId, propId)
        | LetExpr(v,FreebasePropertyGet (e,typeId,propId),PropertyGet(Some (Var v2), PropertyWithName "Value")) when v = v2 -> Some (e, typeId, propId)
        | _ -> None

    // Detect a series of gets, e.g.
    //   isotope.``Isotope of``.``Atomic number``.Value

    let (|FreebasePropertyGets|_|) e = 
        match e with 
        | FreebasePropertyGetWithPossibleNullableValueFetch(e, typeId, propId) -> 
            let rec more e = 
                match e with 
                | FreebasePropertyGetWithPossibleNullableValueFetch(e, typeId, propId) -> 
                    let (e,vs) = more e 
                    (e,(typeId, propId)::vs) 
                | _ -> (e,[])
            let (e,vs) = more e
            Some(e,List.rev ((typeId, propId)::vs))
        | _ -> None

    let rec (|FreebaseUniquePropertyGets|_|) (conn: FreebaseDataConnection) e = 
        match e with 
        | FreebasePropertyGets(e,props) -> 
            let (typeId, propId) = List.head (List.rev props)
            match conn.TryGetPropertyById(typeId, propId) with 
            | Some prop when prop.IsUnique || prop.IsEnum -> Some (e, props, prop)
            | _ -> None
        | _ -> None

    /// The algebra of different supported  qualifications
    type FreebasePropAccess = (string * string) list  
    
    type FreebaseQueryQualification = 
        | UniquePropertyNotNull of FreebasePropAccess * bool
        | PropertyOpConstant of FreebasePropAccess * string * obj
        | PropertyOpConstants of FreebasePropAccess * string * obj list

    type FreebaseQueryData = 
        | Base of string 
        | TailSelect of FreebaseQueryData * (obj -> obj)
        | Filter of FreebaseQueryData * FreebaseQueryQualification
        | Take of FreebaseQueryData * int
        | Sort of FreebaseQueryData * (string * FreebasePropAccess) list

    let nonTailSelect() = failwith "only one select or yield operation is allowed, at the end of a query"
    let badQueryOperation (methName:string) = failwith (sprintf "Unrecognized operation '%s' in Freebase query. Supported operations are 'for', 'select', 'where', 'sortBy', 'sortByDescending', 'thenBy', 'thenByDescending', 'take'" methName)
    let quote s = "'" + s + "'"

    let formatQueryConstant (fb:FreebaseDataConnection) (fbPropOpt:FreebaseProperty option) (qc:obj) = 
        // Reverse-convert unitized values sent back to Freebase
        let convert (v:double) = 
            match fbPropOpt with 
            | None -> v
            | Some fbProp -> RuntimeConversion.convertUnits fb.UseUnits false fbProp v
        match qc with 
        | null -> null
        | :? string as s -> quote s
        | :? Nullable<int> as i -> if i.HasValue then string i.Value else null
        | :? int as i -> string i
        | :? Nullable<decimal> as i -> if i.HasValue then string i.Value else null
        | :? decimal as i -> string i
        | :? Nullable<double> as i -> if i.HasValue then string (convert i.Value) else null
        | :? double as i -> string (convert i)
        | qc -> failwithf "unexpected constant %A" qc

    //
    // Nested accesses become nested queries expanding the returned data
    // e.g. 
    //   isotope.``Isotope of``.``Atomic number``.Value <= 4
    // becomes:
    //     "/chemistry/isotope/isotope_of": { "/chemistry/chemical_element/atomic_number<=": 4 + ... }
    /// with all the non-compound fields filled in.
    //
    // The data is used to populate probed compund field of the object
    let rec formatPropAccessCx (fbDataConn:FreebaseDataConnection) ps op constraintText =
         match ps with 
         | [(_typeId, propId)] -> propId + op, constraintText 
         | (_typeId1, propId1) :: rest -> 
            //let fields1 = fbDataConn.QueryFragmentsOfPropertiesOfAllIncludedTypes (typeId1, [])
            let subPropId,subCx = formatPropAccessCx fbDataConn rest op constraintText
            propId1, "{ '" + subPropId + "': " + subCx + ", 'limit':1 }"  
         | _ -> failwith "unreachable"

    // In sorts, property access just uses the '.' notation
    let formatPropAccessSort ps = String.concat "." (List.map snd ps)

    let formatQueryCondition (fbDataConn:FreebaseDataConnection)  qc = 
        match qc with 
        | UniquePropertyNotNull (propAccess,isCompound) -> 
            let constraintText = 
                if isCompound then 
                    "{ '/type/object/id' : null, 'limit':1 }"
                else 
                    "{ 'value':null, 'limit':1}"
            formatPropAccessCx fbDataConn propAccess "" constraintText
        | PropertyOpConstant (propAccess, op, o) -> 
            let (typeId, propId) = List.head (List.rev propAccess)
            let fbPropOpt = fbDataConn.TryGetPropertyById(typeId, propId) 
            (formatPropAccessCx fbDataConn propAccess (if op = "=" then "" else op) (formatQueryConstant fbDataConn fbPropOpt o))
        | PropertyOpConstants (propAccess, op, objs) -> 
            let (typeId, propId) = List.head (List.rev propAccess)
            let fbPropOpt = fbDataConn.TryGetPropertyById(typeId, propId) 
            (formatPropAccessCx fbDataConn propAccess (if op = "=" then "" else op) ("[" + (objs |> List.map (formatQueryConstant fbDataConn fbPropOpt) |> String.concat ",") + "]" ))

    /// Format as query text for MQL
    let rec formatQueryData fbDataConn q = 
        match q with 
        | Base typeId -> [("type", quote typeId)]
        | Filter (xs,q) -> formatQueryData fbDataConn xs  @ [ formatQueryCondition fbDataConn q ]
        | Take (xs,n) -> formatQueryData fbDataConn xs  @ [ ("limit", string n) ]
        | Sort (xs,[(direction,propIds)]) -> 
            let rest = formatQueryData fbDataConn xs
            // Skip 'sort' when counting
            rest @ [ (formatPropAccessSort propIds, null); ("sort", quote (direction + formatPropAccessSort propIds)) ]
        | Sort (xs,propIds) -> 
            let rest = formatQueryData fbDataConn xs
            // Skip 'sort' when counting
            rest 
              @ (propIds |> List.map (fun (_,propAccess) -> (formatPropAccessSort propAccess, null))) 
              @ [ ("sort", sprintf "[%s]" (propIds |> List.map (fun (direction,propAccess) -> quote (direction + formatPropAccessSort propAccess)) |> String.concat ",")) ]
        | TailSelect _ -> nonTailSelect()

    let getBaseTypeId queryData = 
        let rec loop = function Base info -> info | Filter (xs,_) | Take (xs,_) | Sort(xs,_) -> loop xs | TailSelect _ -> nonTailSelect() 
        loop queryData

    let rec queryDataAsEnumerable (fbDataConn:FreebaseDataConnection) (queryData:FreebaseQueryData) =
        match queryData with 
        | TailSelect (preQuery,f) -> (preQuery  |> queryDataAsEnumerable fbDataConn |> Seq.cast |> Seq.map f) :> System.Collections.IEnumerable
        | _ -> 
            let queryConstraints = formatQueryData fbDataConn queryData
            let typeId = getBaseTypeId queryData
            seq { for objData in fbDataConn.GetInitialDataForObjectsFromQueryText(queryConstraints,typeId,fbDataConn.Limit) do 
                      yield FreebaseObject(fbDataConn,objData,typeId) :> IFreebaseObject } :> System.Collections.IEnumerable

    type IWithFreebaseQueryData = 
        abstract FreebaseQueryData : FreebaseQueryData
        abstract FreebaseDataConnection : FreebaseDataConnection

    let (|SourceWithQueryData|_|) (source:Expression) = 
        match source with 
        | Constant ((:? IWithFreebaseQueryData as bd), _) -> Some bd
        | _ -> None 

    let isNullableTy (ty:Type) = ty.IsGenericType && ty.GetGenericTypeDefinition() = typedefof<Nullable<_>>
    let whenAllElseFails (e:Expression) : 'TResult = 
        match e with 
        | MethodCall(None, meth, (SourceWithQueryData sourceData :: args)) when evaluateOnClientSideWhereNecessary ->
            let sourceObj = ((queryDataAsEnumerable sourceData.FreebaseDataConnection sourceData.FreebaseQueryData) :?> seq<IFreebaseObject>).AsQueryable()
            let sourceExpr = Expression.Constant(sourceObj, typeof<IQueryable<IFreebaseObject>>) :> Expression
            let replacementExpr = Expression.Call(null, meth, [| yield sourceExpr; yield! args |])
            let fDelegate = (Expression.Lambda(replacementExpr,[| |])).Compile()
            try fDelegate.DynamicInvoke() :?> 'TResult with :? TargetInvocationException as e -> raise e 
        | MethodCall(_, meth, _) -> badQueryOperation meth.Name
        | _ -> failwithf "Unrecognized operation '%A' in web data query" e

    type FreebaseQueryable<'T>(fbDataConn,data:FreebaseQueryData) = 
    
        static member Create(typeId, fbDataConn) = 
            FreebaseQueryable<'T>(fbDataConn,Base (typeId)) :> IQueryable<'T>

        interface IQueryable<'T> 
        interface IQueryable with 
             member x.Provider = FreebaseQueryableStatics.Provider
             member x.Expression =  Expression.Constant(x,typeof<IQueryable<'T>>) :> Expression 
             member x.ElementType = typeof<'T>
        interface seq<'T> with 
             member x.GetEnumerator() = (Seq.cast<'T> (queryDataAsEnumerable fbDataConn data)).GetEnumerator()
        interface System.Collections.IEnumerable with 
             member x.GetEnumerator() = (x :> seq<'T>).GetEnumerator() :> System.Collections.IEnumerator
        interface IWithFreebaseQueryData with 
             member x.FreebaseQueryData = data
             member x.FreebaseDataConnection = fbDataConn

    and FreebaseOrderedQueryable<'T>(fbDataConn, data:FreebaseQueryData) = 
    
        static member Create(typeId, fbDataConn) = FreebaseQueryable<'T>(fbDataConn, Base (typeId)) :> IQueryable<'T>
        interface IOrderedQueryable<'T> 
        interface IQueryable<'T> 
        interface IQueryable with 
             member x.Provider = FreebaseQueryableStatics.Provider
             member x.Expression =  Expression.Constant(x,typeof<IOrderedQueryable<'T>>) :> Expression 
             member x.ElementType = typeof<'T>
        interface seq<'T> with member x.GetEnumerator() = (Seq.cast<'T> (queryDataAsEnumerable fbDataConn data)).GetEnumerator()
        interface System.Collections.IEnumerable with member x.GetEnumerator() = (x :> seq<'T>).GetEnumerator() :> System.Collections.IEnumerator
        interface IWithFreebaseQueryData with 
             member x.FreebaseQueryData = data
             member x.FreebaseDataConnection = fbDataConn

    and FreebaseQueryableStatics() = 

        static let createQueryable(isOrdered,fbDataConn,data:FreebaseQueryData,ty:System.Type) : obj = 
            let qty = (if isOrdered then typedefof<FreebaseOrderedQueryable<_>> else typedefof<FreebaseQueryable<_>>).MakeGenericType [| ty |]
            qty.GetConstructors().[0].Invoke [|fbDataConn;data|]

        static let translationFailure (fb: FreebaseDataConnection) msg = 
            if fb.AllowQueryEvaluateOnClientSide then None else failwith msg 

        static member val Provider = 
         { new System.Linq.IQueryProvider with 
            member provider.CreateQuery(e:Expression) : IQueryable = failwithf "CreateQuery, e = %A" e

            /// This member is called by LINQ's .Where, .Select, etc.
            member provider.CreateQuery<'T>(e:Expression) : IQueryable<'T> = 
                let resultsOpt = 
                    match e with 
                    | MethodCall(None, (MethodWithName "Where" as meth), [ SourceWithQueryData source; OptionalQuote qual ]) ->
                
                        // Convert the qualification
                        let dataOpt = 
                            match qual with 
                            // Detect the compiled version of "where (freebaseObject.FreebaseProperty = constant)"
                            | Lambda([ v1 ], FreebaseRelOp(op,FreebasePropertyGets(Var v2, propAccess), FreebaseConstant qc)) when v1 = v2 ->  
                                Some (FreebaseQueryData.Filter(source.FreebaseQueryData, FreebaseQueryQualification.PropertyOpConstant (propAccess, op, qc)))

                            // Detect the compiled version of "where (freebaseObject.FreebaseProperty <> null )"
                            // Becomes 'prop : { ... "limit":1}' 
                            // This currently only works for unique compound properties like dated_money_value, e.g. /meteorology/tropical_cyclone/damages

                            | Lambda([ v1 ], FreebaseRelOp("!=",FreebaseUniquePropertyGets source.FreebaseDataConnection (Var v2, propAccess, prop),  Null)) 
                                  when v1 = v2  ->
                                let isCompound = prop.BasicSystemType.IsNone
                                Some (FreebaseQueryData.Filter(source.FreebaseQueryData, FreebaseQueryQualification.UniquePropertyNotNull (propAccess, isCompound)))

                            | Lambda([ v1 ], PropertyGet(Some (FreebaseUniquePropertyGets source.FreebaseDataConnection (Var v2, propAccess, prop)), hasValueProp)) 
                                  when v1 = v2  && hasValueProp.Name = "HasValue" ->
                                let isCompound = prop.BasicSystemType.IsNone
                                Some (FreebaseQueryData.Filter(source.FreebaseQueryData, FreebaseQueryQualification.UniquePropertyNotNull (propAccess, isCompound)))

                            // Detect the compiled version of "where (freebaseObject.FreebaseProperty.ApproximatelyOneOf(constant1, constant2, ...)"
                            | Lambda([ v1 ], FreebaseApproximatelyOneOfOp(FreebasePropertyGets(Var v2, propAccess), qcs)) when qcs.Length > 0 && v1 = v2 ->  
                                Some (FreebaseQueryData.Filter(source.FreebaseQueryData, FreebaseQueryQualification.PropertyOpConstants (propAccess, "|=", qcs)))
                            | _ -> 
                                translationFailure source.FreebaseDataConnection (sprintf "unknown qualification - not a lambda - %A, %A, %A" qual qual.NodeType (qual :? LambdaExpression))

                        match dataOpt with 
                        | Some data -> 
                            let argTy = meth.GetGenericArguments().[0]
                            Some (source.FreebaseDataConnection, false, data, argTy)
                        | None -> None

                    // Take(n)
                    | MethodCall(None, (MethodWithName "Take" as meth), [ SourceWithQueryData source; OptionalQuote (Int32 n) ]) ->
                        let argTy = meth.GetGenericArguments().[0]
                        let data = FreebaseQueryData.Take(source.FreebaseQueryData, n)
                        Some (source.FreebaseDataConnection, false, data, argTy)

                    // "sort":"name"
                    | MethodCall(None, ((MethodWithName "OrderBy" | MethodWithName "OrderByDescending") as meth), [ SourceWithQueryData source; OptionalQuote qual ]) ->
                        let dataOpt = 
                            match qual with 
                            // Detect the compiled version of "(fun v -> v.FreebaseProperty)"
                            | Lambda([ v1 ], FreebasePropertyGets(Var v2, propAccess)) when v1 = v2 ->  
                                let direction = if meth.Name = "OrderBy" then "" else "-"
                                Some (FreebaseQueryData.Sort(source.FreebaseQueryData, [(direction, propAccess)]))
                            | _ -> 
                                translationFailure source.FreebaseDataConnection (sprintf "unknown qualification - not a lambda - %A, %A, %A" qual qual.NodeType (qual :? LambdaExpression))
                        match dataOpt with 
                        | Some data -> 
                            let argTy = meth.GetGenericArguments().[0]
                            Some (source.FreebaseDataConnection, true, data, argTy)
                        | None -> None 

                    | MethodCall(None, ((MethodWithName "ThenBy" | MethodWithName "ThenByDescending") as meth), [ SourceWithQueryData source; OptionalQuote qual ]) ->
                        let dataOpt = 
                            match qual with 
                            // Detect the compiled version of "(fun v -> v.FreebaseProperty)"
                            | Lambda([ v1 ], FreebasePropertyGets(Var v2, propId)) when v1 = v2 ->  
                                let direction = if meth.Name = "ThenBy" then "" else "-"
                                match source.FreebaseQueryData with 
                                | FreebaseQueryData.Sort(sourceDataOrig, propIds) -> Some (FreebaseQueryData.Sort(sourceDataOrig, propIds @ [(direction,propId)]))
                                | _ -> translationFailure source.FreebaseDataConnection (sprintf "'thenBy' operations must come immediately after a 'sortBy' operation in a query")
                            | _ -> translationFailure source.FreebaseDataConnection (sprintf "unknown qualification - not a lambda - %A, %A, %A" qual qual.NodeType (qual :? LambdaExpression))

                        match dataOpt with 
                        | Some data -> 
                            let argTy = meth.GetGenericArguments().[0]
                            Some (source.FreebaseDataConnection, true, data, argTy)
                        | None -> None 

                    // Select(fun x -> ...)
                    | MethodCall(None, (MethodWithName "Select" as meth), [ SourceWithQueryData source; OptionalQuote (Lambda([ v1 ], e) as lambda) ]) ->
                        let argTy = meth.GetGenericArguments().[1]
                        match e with 
                        | Var v2 when v1 = v2 -> 
                            Some (source.FreebaseDataConnection, false, source.FreebaseQueryData, argTy)
                        | _ -> 
                            // TODO: consider sanitizing the "e" to check that only non-server-query operations 
                            // are used, i.e. simple property projections, to present cascading queries
                            let fDelegate = (lambda :?> LambdaExpression).Compile()
                            let f (x:obj) = try fDelegate.DynamicInvoke(x) with :? TargetInvocationException as e -> raise e
                            Some (source.FreebaseDataConnection, false, FreebaseQueryData.TailSelect (source.FreebaseQueryData, f), argTy)
                    | _ -> None

                match resultsOpt with 
                | Some (fbDataConn, isOrdered, newQueryData, newQueryTy) ->
                    let combinedObj = createQueryable (isOrdered, fbDataConn, newQueryData, newQueryTy)
                    (combinedObj :?> IQueryable<'T>)
                | None ->
                    whenAllElseFails e

            member provider.Execute(e:Expression) : obj = failwith "Execute, untyped: nyi"
            member provider.Execute<'T>(e:Expression) : 'T = 
                match e with 
                | MethodCall(None, ((MethodWithName "Count" | MethodWithName "ApproximateCount") as meth), [ SourceWithQueryData source ]) ->
                    //printfn "count/estimate-count"
                    let queryData = source.FreebaseQueryData
                    let extraConstraint = [ ("return", quote (if meth.Name = "Count" then "count" else "estimate-count"))  ]
                    let queryConstraints = formatQueryData source.FreebaseDataConnection queryData @ extraConstraint
                    let queryText = queryConstraints |> List.map (fun (k,v) -> sprintf ", '%s' : %s" k (match v with null -> "null" | s -> s)) |> String.concat ""
                    let typeId = getBaseTypeId queryData
                    let query = sprintf "{ '/type/object/type' : '%s' %s }" typeId queryText 
                    let count = source.FreebaseDataConnection.Connection.Query<int>(query, fun j -> j.AsInteger())
                    box count  :?> 'T
                | _ ->
                    whenAllElseFails e }

/// Represents the contents of a Freebase namespace
type public FreebaseDomain internal (fbDataConn,domainId:string) =
    member fs._Id = domainId
    /// Get all the Freebase objects which have the given Freebase type id.
    member public __._GetObjectsOfTypeId (typeId:string) : IQueryable<IFreebaseObject> =
        QueryImplementation.FreebaseQueryable.Create (typeId, fbDataConn)

/// Represents the contents of a Freebase namespace
type public FreebaseDomainCategory internal (fbDataConn, domainCategoryId) =
    /// Get all the Freebase objects which have the given Freebase type id.
    /// Get the object which represents the Freebase domain with the given object id.
    member public __._GetDomainById(domainId:string) : FreebaseDomain = FreebaseDomain(fbDataConn, domainId)
    member fs._Id = domainCategoryId

type FreebaseIndividuals internal (fbDataConn: FreebaseDataConnection) = 
    /// Get all the Freebase objects which have the given type id and object id.
    member public __._GetIndividualById (typeId:string,objId:string) : IFreebaseObject =
        let objData = fbDataConn.GetInitialDataForKnownObject(typeId,objId)
        FreebaseObject(fbDataConn,objData,typeId) :> IFreebaseObject

    /// Get all the Freebase objects which have the given Freebase type id.
    static member public _GetIndividualsObject (collectionObj:obj) =
        match collectionObj with 
        | :? QueryImplementation.IWithFreebaseQueryData as qd -> FreebaseIndividuals(qd.FreebaseDataConnection)
        | _ -> failwith "expected query information on collection"

type FreebaseSendingRequestArgs(uri: System.Uri) = 
    member x.RequestUri = uri

/// Contains public entry points called by provided code.
type public FreebaseDataContext internal (apiKey:string, serviceUrl:string, useUnits:bool, snapshotDate:string, useLocalCache: bool, allowQueryEvaluateOnClientSide: bool) = 
    let localCacheName = "FreebaseRuntime"
    let fbQueries = new FreebaseQueries(apiKey, serviceUrl, localCacheName, snapshotDate, useLocalCache)
    let fbSchema = new FreebaseSchemaConnection(fbQueries)
    let fbDataConn = new FreebaseDataConnection(fbQueries, fbSchema, useUnits, allowQueryEvaluateOnClientSide)
    let settings = FreebaseDataContextSettings (fbQueries,fbDataConn)
    member __.DataContext = settings
    /// Create a data context
    static member _Create(apiKey, serviceUrl, useUnits, snapshotDate, useLocalCache, allowQueryEvaluateOnClientSide) = FreebaseDataContext(apiKey, serviceUrl, useUnits, snapshotDate, useLocalCache, allowQueryEvaluateOnClientSide)
    /// Get the object which represents the Freebase domain with the given object id.
    member public __._GetDomainCategoryById(domainCategoryId:string) : FreebaseDomainCategory = FreebaseDomainCategory(fbDataConn, domainCategoryId)
    
and FreebaseDataContextSettings internal (fbQueries,fbDataConn) = 
    let sendingRequest = fbQueries.SendingRequest  |> Event.map (fun uri -> FreebaseSendingRequestArgs(uri))

    [<CLIEvent>]
    member __.SendingRequest = sendingRequest
    member __.ServiceUrl with get() = fbQueries.ServiceUrl and set v = fbQueries.ServiceUrl <- v
    member __.Limit with get() = fbDataConn.Limit and set v = fbDataConn.Limit <- v
    member __.LocalCacheLocation = fbQueries.LocalCacheLocation
    member __.UseLocalCache with get() = fbQueries.UseLocalCache and set v = fbQueries.UseLocalCache <- v

#if NO_FSHARP_CORE_TYPE_PROVIDER_ASSEMBLY_ATTRIBUTE
// Attach the TypeProviderAssemblyAttribute to the runtime assembly
namespace Microsoft.FSharp.Core.CompilerServices

open System
open System.Reflection

[<AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)>]
type internal TypeProviderAssemblyAttribute(assemblyName : string) = 
    inherit System.Attribute()
    new () = TypeProviderAssemblyAttribute(null)
    member __.AssemblyName = assemblyName

#endif

