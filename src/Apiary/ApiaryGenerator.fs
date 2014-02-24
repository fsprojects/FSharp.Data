// --------------------------------------------------------------------------------------
// Apiary type provider - type builder
// --------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.Globalization
open Microsoft.FSharp.Quotations
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open ProviderImplementation.ProvidedTypes

type internal ApiaryGenerationContext =
  { TypeProviderType : ProvidedTypeDefinition
    Replacer : AssemblyReplacer 
    // to nameclash type names
    UniqueNiceName : string -> string 
    ApiName : string 
    ApiaryContextSelector : Expr -> Expr<InternalApiaryContext>
    JsonContext : JsonGenerationContext 
    SpecialNames : Map<string, string> }
  static member Create(apiName, tpType, replacer, specialNames) =
    let uniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName
    { TypeProviderType = tpType
      ApiName = apiName
      Replacer = replacer 
      UniqueNiceName = uniqueNiceName
      ApiaryContextSelector = fun e -> <@ (%%e:ApiaryContext) :> InternalApiaryContext @> 
      JsonContext = JsonGenerationContext.Create("", tpType, typeof<ApiaryDocument>, replacer, uniqueNiceName) 
      SpecialNames = specialNames } 

module internal ApiaryTypeBuilder = 

  let join parentName name =
    let name = NameUtils.nicePascalName name
    if name = parentName
    then name
    else parentName + name

  /// Given a specification (returned by the apiary.io service) 
  /// infer structure of JSON and generate a type for the result
  ///
  /// TODO: Lots of room for improvement here (pattern matching based
  /// on error codes, handle other file formats like XML...)
  let generateMembersForJsonResult (ctx:ApiaryGenerationContext) name spec =

    let samples = 
      [ for example in spec?responses do
          if example?status.AsInteger() = 200 then 
            match example.TryGetProperty "body" with
            | Some body -> 
                let source = body.InnerText
                yield JsonValue.ParseSample source
            | None -> () ]

    let result =
      [ for item in samples -> JsonInference.inferType CultureInfo.InvariantCulture (*allowEmptyValues*)false name item ]
      |> Seq.fold (StructuralInference.subtypeInfered (*allowEmptyValues*)false) InferedType.Top
      |> JsonTypeBuilder.generateJsonType ctx.JsonContext (*canPassAllConversionCallingTypes*)false (*optionalityHandledByParent*)false
    result.ConvertedType, result.GetConverter ctx.JsonContext

  let ensureGeneratedType ctx parentName (entityTy:Type) = 
    match entityTy with
    | :? ProvidedTypeDefinition as entityTy -> entityTy
    | _ ->
      let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName parentName, Some(typeof<obj>))
      ctx.TypeProviderType.AddMember objectTy
      let prop = ProvidedProperty("Value", entityTy)
      prop.GetterCode <- fun (Singleton self) -> Expr.Coerce(self, entityTy)
      objectTy.AddMember(prop)
      objectTy
  
  /// Generates formatted string with headers that must be
  /// passed to a call according to apiary.io specification
  let generateHeadersForCall spec =
    [ for h, v in spec?request?headers.Properties -> h, v.AsString() ]
    |> ApiaryUtils.formatHeaders
  
  /// Generates method that invokes a specified operation or retrieves
  /// an entity given some arguments (which can contain additional methods)
  /// This code also generates an asynchronous version of the operation
  let generateOperations ctx name (args, meth, path, spec) resultTy bodyResConv =

    /// Arguments that are appended to all methods (to allow passing additional info)
    let additionalArgs = 
      [ ProvidedParameter("query", ctx.Replacer.ToRuntime typeof<list<string*string>>, optionalValue=null)
        ProvidedParameter("headers", ctx.Replacer.ToRuntime typeof<list<string*string>>, optionalValue=null) ] 

    // Headers required by the apiary specification
    let reqHeaders = generateHeadersForCall spec
                    
    // Combine arguments with optional ?query and ?headers
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
            let apiCtx = %(ctx.ApiaryContextSelector self)
            (apiCtx :> ApiaryOperations), 
            { Method = meth; Path = path; Arguments = %%argsArray;
              Headers = headers; Query = query } @>

    // Generate two versions of the method - synchronous and asynchronous
    let asyncResTy = typedefof<Async<_>>.MakeGenericType [| resultTy |]
    let asyncM = ProvidedMethod("Async" + name, providedArgs, asyncResTy)
    let normalM = ProvidedMethod(name, providedArgs, resultTy)
    normalM.InvokeCode <- fun parameters ->
      let parameters = parameters |> Seq.map ctx.Replacer.ToDesignTime 
      <@@ let apiCtx, args = %(makeInvokeCode parameters)
          apiCtx.InvokeOperation(args) @@>
      |> bodyResConv

    // Generating code for async is more tricky, because we need to perform
    // the mapping not on the result (as above) but inside async block.
    asyncM.InvokeCode <- fun parameters ->
      let parameters = parameters |> Seq.map ctx.Replacer.ToDesignTime 
      let asyncInvoke = <@ let apiCtx, args = %(makeInvokeCode parameters)
                           apiCtx.AsyncInvokeOperation(args) @>
      ProviderHelpers.asyncMap ctx.Replacer resultTy asyncInvoke bodyResConv

    match spec.TryGetProperty "description" with
    | None -> ()
    | Some description ->
        let xmlDoc = description.AsString() |> NameUtils.trimHtml 
        asyncM.AddXmlDoc xmlDoc
        normalM.AddXmlDoc xmlDoc

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
  let rec generateSchema ctx parentName (parent:ProvidedTypeDefinition) = function

    | Module(name, [NormalizedFunction ctx.SpecialNames (name2, meth, args, servicePath, apiaryPath)]) when name = name2 ->
        
        // Generate method that calls the function
        let spec = ApiarySchema.downloadSpecification ctx.ApiName meth apiaryPath
        let methResTy, methResConv = generateMembersForJsonResult ctx (join parentName name) spec

        generateOperations ctx (NameUtils.nicePascalName name) (args, meth, servicePath, spec) methResTy methResConv
        |> Seq.iter parent.AddMember
        
    | NormalizedFunction ctx.SpecialNames (name, meth, args, servicePath, apiaryPath) ->

        // Generate method that calls the function
        let spec = ApiarySchema.downloadSpecification ctx.ApiName meth apiaryPath
        let methResTy, methResConv = generateMembersForJsonResult ctx (join parentName name) spec

        generateOperations ctx (NameUtils.nicePascalName name) (args, meth, servicePath, spec) methResTy methResConv
        |> Seq.iter parent.AddMember

    | Module(name, nested) ->

        // Generate new type for the nested module
        let nestedTyp = ProvidedTypeDefinition(ctx.UniqueNiceName name, Some(ctx.Replacer.ToRuntime typeof<InternalApiaryContext>))
        ctx.TypeProviderType.AddMember(nestedTyp)
        // Add the new module as nested property of the parent
        let p = ProvidedProperty(NameUtils.nicePascalName name, nestedTyp)
        p.GetterCode <- fun (Singleton self) -> ctx.Replacer.ToRuntime (ctx.ApiaryContextSelector (ctx.Replacer.ToDesignTime self))
        parent.AddMember(p) 
        // Add all nested operations to this type
        let ctx = { ctx with ApiaryContextSelector = Expr.Cast }
        nested |> Seq.iter (generateSchema ctx (join parentName name) nestedTyp)

    | Entity(name, FindMethod ctx.SpecialNames "GET" (args, (meth, path)), nested) ->

        // Generate new type representing the entity
        // (but it needs to be generated type because we want to add members)
        let spec = ApiarySchema.downloadSpecification ctx.ApiName meth path
        let entityTy, entityConv = generateMembersForJsonResult ctx (join parentName name) spec
        let entityTy = ensureGeneratedType ctx name entityTy  
        
        // Generate method that obtains the entity
        generateOperations ctx ("Get" + NameUtils.nicePascalName name) (args, meth, path, spec) entityTy entityConv        
        |> Seq.iter parent.AddMember

        // Add all nested operations to this type
        let ctx = { ctx with ApiaryContextSelector = fun self -> <@ ((%%self:IJsonDocument) :?> ApiaryDocument).Context @> }
        nested |> Seq.iter (generateSchema ctx (join parentName name) entityTy)

    | _-> 

        // Silently ignore...
        ()
