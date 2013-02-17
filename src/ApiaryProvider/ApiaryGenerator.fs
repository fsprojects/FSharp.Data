// --------------------------------------------------------------------------------------
// Apiary type provider - runtime components and type builder
// --------------------------------------------------------------------------------------
//#nowarn "58"
namespace ProviderImplementation

open System
open FSharp.Net
open FSharp.Data.Json

// ----------------------------------------------------------------------------------------------
// Runtime components used by the generated Apiary code
// ----------------------------------------------------------------------------------------------

module ApiaryUtils =
  let formatHeaders headers = 
    [ for h, v in headers -> h + ":" + v ] |> String.concat "\n"
  let parseHeaders (headers:string) = 
    [ for h in headers.Split('\n') do
        if String.IsNullOrEmpty(h) |> not then
          match h.Split(':') with
          | [| h; v |] -> yield h, v
          | _ -> failwithf "Wrong headers: '%s'" headers ]
  let emptyIfNull (l:'T list) = 
    if System.Object.Equals(Unchecked.defaultof<'T list>, l) then [] else l

type OperationArguments = 
  { Method:string
    Path:string
    Arguments:(string * string)[]
    Headers:(string * string)[]
    Query:(string * string)[] }

/// Underlying representation of the generated JSON types
type ApiaryDocument private (json:JsonValue, context:InternalApiaryContext option) =
  member x.JsonValue = json
  member x.Context = context.Value
  static member Create(json:JsonValue) =
    ApiaryDocument(json, None)
  static member Create(json:JsonValue, context) =
    ApiaryDocument(json, Some context)

and ApiaryOperations = 
  abstract InvokeOperation : OperationArguments -> ApiaryDocument
  abstract AsyncInvokeOperation : OperationArguments -> Async<ApiaryDocument>

and InternalApiaryContext private 
    (rootUrl:string, queries:seq<_>, headers:seq<_>, arguments:seq<string * string>) = 
  let globalQuery = ResizeArray<_>(queries)
  let globalHeaders = ResizeArray<_>(headers)
  let globalArguments = arguments
  
  new (rootUrl) = 
    InternalApiaryContext(rootUrl, [], [], [])

  member internal x.GlobalQuery = globalQuery
  member internal x.GlobalHeaders = globalHeaders
  interface ApiaryOperations with
    member x.AsyncInvokeOperation
      ({ Method = meth; Path = path; Arguments = arguments;
         Headers = headers; Query = query }) = async {

      // Replace parameters in the path with actual arguments
      let allArguments = Seq.concat [globalArguments; Seq.ofArray arguments]
      let path = allArguments |> Seq.fold (fun (path:string) (key, value) -> 
        path.Replace(key, value)) path

      // Run the HTTP request
      let allheaders = [ yield! headers; yield! globalHeaders ]
      let allquery = [ yield! query; yield! globalQuery ]
      if String.Compare(meth, "get", true, Globalization.CultureInfo.InvariantCulture) = 0 then
        let! res = Http.AsyncRequest(rootUrl + path, headers = allheaders, query = allquery)

        // Create context that captures all arguments already specified
        let context = InternalApiaryContext(rootUrl, globalQuery, globalHeaders, allArguments)
        return ApiaryDocument.Create(JsonValue.Parse(res), context)
      else
        return failwith "Only GET supported" }

    member x.InvokeOperation(arguments) =
      (x :> ApiaryOperations).AsyncInvokeOperation(arguments) |> Async.RunSynchronously

type ApiaryContext(rootUrl) =
  inherit InternalApiaryContext(rootUrl)
  member x.AddQueryParam(key:string, value:string) =
    x.GlobalQuery.Add( (key, value) )  
  member x.AddHeader(key:string, value:string) =
    x.GlobalHeaders.Add( (key, value) )  

type ApiaryRuntime =
  static member ProcessParameters(reqHeaders, headers, query) =
    let headers = ApiaryUtils.parseHeaders reqHeaders @ (ApiaryUtils.emptyIfNull headers)
    let query = ApiaryUtils.emptyIfNull query
    Array.ofSeq headers, Array.ofSeq query
  
// ----------------------------------------------------------------------------------------------
// Compile-time components that are used to generate Apiary types
// ----------------------------------------------------------------------------------------------

open System
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Quotations
open FSharp.Net
open FSharp.Data.Json
open FSharp.Data.Json.JsonReader

type internal ApiaryGenerationContext =
  { DomainType : ProvidedTypeDefinition
    UniqueNiceName : string -> string 
    ApiName : string 
    ApiaryContextSelector : Expr -> Expr }
  static member Create(apiName, domainTy) =
    { DomainType = domainTy; ApiName = apiName
      UniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName
      ApiaryContextSelector = fun e -> <@@ (%%e : ApiaryContext) :> InternalApiaryContext @@> }
  member x.JsonContext = 
    { JsonGenerationContext.DomainType = x.DomainType
      Representation = typeof<ApiaryDocument>
      Packer = fun e -> <@@ ApiaryDocument.Create(%%e) @@>
      Unpacker = fun e -> <@@ ((%%e):ApiaryDocument).JsonValue @@>
      UniqueNiceName = x.UniqueNiceName }

type ApiaryGenerationHelper = 
  static member AsyncMap<'T, 'R>(work:Async<'T>, f:'T -> 'R) = 
    async { let! v = work in return f v }
    
module internal ApiaryTypeBuilder = 

  /// Given a specification (returned by the apiary.io service) 
  /// infer structure of JSON and generate a type for the result
  ///
  /// TODO: Lots of room for improvement here (pattern matching based
  /// on error codes, handle other file formats like XML...)
  let generateMembersForJsonResult (ctx:ApiaryGenerationContext) spec =
    let samples = 
      [ for example in spec?outgoing do
          if example?status.AsString = "200" then 
            let source = example?body.InnerText
            yield JsonValue.Parse source ]
    [ for itm in samples -> JsonInference.inferType itm ]
    |> Seq.fold StructureInference.subtypeInfered StructureInference.Top
    |> JsonTypeBuilder.generateJsonType ctx.JsonContext

  let ensureGeneratedType ctx (entityTy:Type) = 
    match entityTy with
    | :? ProvidedTypeDefinition as entityTy -> entityTy
    | _ ->
      let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName "Entity", Some(typeof<obj>))
      ctx.DomainType.AddMember(objectTy)
      let prop = ProvidedProperty("Value", entityTy)
      prop.GetterCode <- fun (Singleton self) -> Expr.Coerce(self, entityTy)
      objectTy.AddMember(prop)
      objectTy

  
  /// Generates formatted string with headers that must be
  /// passed to a call according to apiary.io specification
  let generateHeadersForCall spec =
    [ for h, v in spec?incoming?headers.Properties -> h, v.AsString ]
    |> ApiaryUtils.formatHeaders

  /// Arguments that are appended to all methods (to allow passing additional info)
  let additionalArgs = 
    [ ProvidedParameter("query", typeof<list<string*string>>, optionalValue=null)
      ProvidedParameter("headers", typeof<list<string*string>>, optionalValue=null) ] 

  
  /// Generates method that invokes a specified operation or retrieves
  /// an entity given some arguments (which can contain additional methods)
  /// This code also generates an asynchronous version of the operation
  let generateOperations ctx name (args, meth, path, spec) resultTy bodyResConv =

    // Headers required by the apiary specification
    let reqHeaders = generateHeadersForCall spec
                    
    // Combine arguments with opetional ?query and ?headers
    let providedArgs = [ 
      for a in args do 
        yield ProvidedParameter(NameUtils.niceCamelName a, typeof<string>) 
      yield! additionalArgs ] 

    // Given expressions for parameters, generate the invoke code for the method
    let makeInvokeCode parameters =
      let parameters = Array.ofSeq parameters
      if parameters.Length < 3 then
        failwithf "Parameter mismatch in '%s'." meth
      else
        // Parameters are [self; ...other...; query; headers ]
        let self = parameters.[0]
        let query = parameters.[parameters.Length - 2]
        let headers = parameters.[parameters.Length - 1]
              
        // Create array with all arguments
        // we build array such as: [| "{id}", id; ... |]
        let argsInfos = List.zip args [ for i in 1 .. parameters.Length - 3 -> parameters.[i] ]
        let argsExprs = [ for name, arg in argsInfos -> Expr.NewTuple [Expr.Value(name); arg] ]
        let argsArray = Expr.NewArray(typeof<string * string>, argsExprs)

        <@  let headers, query = ApiaryRuntime.ProcessParameters(reqHeaders, %%headers, %%query)
            let apiCtx = (%%(ctx.ApiaryContextSelector self):InternalApiaryContext)
            (apiCtx :> ApiaryOperations), 
            { Method = meth; Path = path; Arguments = %%argsArray;
              Headers = headers; Query = query } @>

    // Generate two versions of the method - synchronous and asynchronous
    let asyncResTy = typedefof<Async<_>>.MakeGenericType [| resultTy |]
    let asyncM = ProvidedMethod("Async" + name, providedArgs, asyncResTy)
    let normalM = ProvidedMethod(name, providedArgs, resultTy)
    normalM.InvokeCode <- fun parameters ->
      <@@ let apiCtx, args = %(makeInvokeCode parameters)
          apiCtx.InvokeOperation(args) @@>
      |> bodyResConv

    // Generating code for async is more tricky, because we need to perform
    // the mapping not on the result (as above) but inside async block. So we
    // generate function and apply 'ApiaryGenerationHelper.AsyncMap(work, f)'
    let asyncMap (asyncWork:Expr) =
      let mi = typeof<ApiaryGenerationHelper>.GetMethod("AsyncMap")
      let resultTy = 
        // It may sound reasonable to use 'resultTy' as the generic type, but then
        // 'MakeGenericMethod' does not work, so we find the erased type
        if resultTy :? ProvidedTypeDefinition then
          resultTy.BaseType else resultTy
      let mi = mi.MakeGenericMethod(typeof<ApiaryDocument>, resultTy)
      let convFuncExpr = 
        let v = Var.Global("doc", typeof<ApiaryDocument>)
        Expr.Lambda(v, bodyResConv (Expr.Var(v)))
      Expr.Call(mi, [asyncWork; convFuncExpr])

    asyncM.InvokeCode <- fun parameters ->
      <@@ let apiCtx, argszz = %(makeInvokeCode parameters)
          apiCtx.AsyncInvokeOperation(argszz) @@>
      |> asyncMap

    asyncM.AddXmlDoc(NameUtils.trimHtml spec?description.AsString)
    normalM.AddXmlDoc(NameUtils.trimHtml spec?description.AsString)
    [asyncM; normalM]

  /// This is the main recursive function that generates type for a "RestApi" specification. 
  ///
  ///  * Modules are turned into nested types (so functions under a module
  ///    become static methods callable via `Module.Function(...)`)
  /// 
  ///  * Functions are turned into static methods that return value of 
  ///    a type that is automatically inferred from the JSON sample
  ///    (ideally we should support other formats too...)
  ///
  ///  * Entities that have "GET" operation are turned into method that returns
  ///    a type representing the entity. All nested functions of the entity
  ///    are added to this type (and at runtime we need to keep arguments around)
  ///
  let rec generateSchema ctx (parent:ProvidedTypeDefinition) = function
    | Module(name, nested) ->          
        // Generate new type for the nested module
        let nestedTyp = ProvidedTypeDefinition(ctx.UniqueNiceName (name + "Type"), Some(typeof<InternalApiaryContext>))
        ctx.DomainType.AddMember(nestedTyp)
        // Add the new module as nested property of the parent
        let p = ProvidedProperty(NameUtils.nicePascalName name, nestedTyp)
        p.GetterCode <- fun (Singleton self) -> ctx.ApiaryContextSelector self
        parent.AddMember(p) 
        // Add all nested operations to this type
        let ctx = { ctx with ApiaryContextSelector = fun self -> <@@ %%self:InternalApiaryContext @@> }
        nested |> Seq.iter (generateSchema ctx nestedTyp)

    | Function(name, (meth, path)) ->
        // Generate method that calls the function
        let spec = ApiarySchema.downloadSpecification ctx.ApiName meth path
        let methResTy, methResConv = generateMembersForJsonResult ctx spec

        generateOperations ctx (NameUtils.nicePascalName name) ([], meth, path, spec) methResTy methResConv
        |> Seq.iter parent.AddMember

    | Entity(name, FindMethod "GET" (args, (meth, path)), nested) ->
        // Generate new type representing the entity
        // (but it needs to be generated type because we want to add members)
        let spec = ApiarySchema.downloadSpecification ctx.ApiName meth path
        let entityTy, entityConv = generateMembersForJsonResult ctx spec
        let entityTy = ensureGeneratedType ctx entityTy  
        
        // Generate method that obtains the entity
        generateOperations ctx ("Get" + NameUtils.nicePascalName name) (args, meth, path, spec) entityTy entityConv        
        |> Seq.iter parent.AddMember

        // Add all nested operations to this type
        let ctx = { ctx with ApiaryContextSelector = fun self -> <@@ (%%self:ApiaryDocument).Context @@> }
        nested |> Seq.iter (generateSchema ctx entityTy)

    | Entity(name, _, nested) -> 
        // Silently ignore...
        ()