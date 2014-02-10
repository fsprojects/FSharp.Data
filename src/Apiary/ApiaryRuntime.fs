﻿// --------------------------------------------------------------------------------------
// Apiary type provider - runtime components
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Runtime

open System
open System.ComponentModel
open FSharp.Data

module internal ApiaryUtils =
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

[<StructuredFormatDisplay("{_Print}")>]
type ApiaryDocument private (json:JsonValue, path:string, context:InternalApiaryContext option) =

  new(json, path, context) = ApiaryDocument(json, path, Some context)
  new(json, path) = ApiaryDocument(json, path, None)

  member private x.Json = json
  member private x.Path = path

  interface IJsonDocument with 
    member x.JsonValue = x.Json
    member x.Path = x.Path
    member x.CreateNew(json, pathIncrement) = upcast ApiaryDocument(json, x.Path + pathIncrement)

  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  member x._Print = x.Json.ToString()

  member x.Context = context.Value

  override x.Equals(y) =
    match y with
    | :? ApiaryDocument as y -> x.Json = y.Json && x.Path = y.Path
    | _ -> false 

  override x.GetHashCode() = x.Json.GetHashCode()

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
      if String.Compare(meth, "get", StringComparison.OrdinalIgnoreCase) = 0 then
        let! res = Http.AsyncRequestString(rootUrl + path, headers = allheaders, query = allquery)

        // Create context that captures all arguments already specified
        let context = InternalApiaryContext(rootUrl, globalQuery, globalHeaders, allArguments)
        return ApiaryDocument(JsonValue.Parse(res), "", context)
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
