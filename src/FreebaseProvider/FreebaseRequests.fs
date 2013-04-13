// --------------------------------------------------------------------------------------
// Freebase type provider 
// --------------------------------------------------------------------------------------
// This file contains functions to help perform basic REST and JSON 
// query accesses to the Freebase MQL data service.
// --------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation 2005-2012.
// This sample code is provided "as is" without warranty of any kind. 
// We disclaim all warranties, either express or implied, including the 
// warranties of merchantability and fitness for a particular purpose. 
// --------------------------------------------------------------------------------------

module internal FSharp.Data.RuntimeImplementation.Freebase.FreebaseRequests

open System
open System.Diagnostics
open System.IO
open System.Net 
open System.Collections.Generic
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open FSharp.Data.RuntimeImplementation.Caching
open FSharp.Net

[<AutoOpen>]
module Utilities = 

    type JsonValue with

        member this.GetStringValWithKey s = 
            this.GetProperty(s).AsString()

        member this.GetOptionalStringValWithKey(s, ?dflt) = 
            let strOption = this.TryGetProperty(s) |> Option.map (fun j -> j.AsString())
            defaultArg strOption (defaultArg dflt "")

        member this.GetArrayValWithKey s = 
            this.GetProperty(s).AsArray()

        member this.GetOptionalArrayValWithKey s = 
            let arrayOption = this.TryGetProperty(s) |> Option.map (fun j -> j.AsArray())
            defaultArg arrayOption [| |]

        static member GetArrayVal f (j:JsonValue) = 
            j.AsArray() |> Array.map f
    
type FreebaseResult<'TResult> = 
    { Code:string
      Cursor:string
      Result:'TResult
      Message:string }
    static member FromJson f (fbr:JsonValue) = 
        { Code = fbr.GetOptionalStringValWithKey "code"
          Cursor = fbr.GetOptionalStringValWithKey("cursor", "false")
          Result = f fbr?result
          Message = fbr.GetOptionalStringValWithKey "message" }

type FreebaseWebException(e:WebException, domain, reason, message, extendedHelp) = 
    inherit WebException(
        (if String.IsNullOrEmpty extendedHelp then
             sprintf "%s (Domain='%s' Reason='%s')" message domain reason
         else
             sprintf "%s (Domain='%s' Reason='%s' ExtendedHelp='%s')" message domain reason extendedHelp),
        e, 
        e.Status, 
        e.Response)
    member __.Domain = domain
    member __.Reason = reason
    member __.FreebaseMessage = message
    member __.ExtendedHelp = extendedHelp

let isStringNone s = String.IsNullOrEmpty s || s = "none"        

type FreebaseQueries(apiKey: string, serviceUrl:string, localCacheName: string, snapshotDate:string, useLocalCache) = 
    let snapshotDate = if isStringNone snapshotDate then None else Some snapshotDate
    let sendingRequest = Event<Uri>()
    let localCache, localCacheLocation = createInternetFileCache localCacheName (TimeSpan.FromDays 30.0)
    let noLocalCache = createNonCachingCache()
    let mutable useLocalCache = useLocalCache
    let mutable serviceUrl = serviceUrl
    let getCache() = if useLocalCache then localCache else noLocalCache
    let freebaseV0 = match serviceUrl with | "http://freebaseread.com/api" -> true | _ -> false

        /// Create a query url from the given query string.
    let createQueryUrl(query:string,cursor:string option) : string =
        let query = query.Replace("'","\"")
        if freebaseV0 then  // old freebase API
            // add the cursor
            let cursor = 
                match cursor with 
                | None -> ""
                | Some "" -> "\"cursor\":true,"
                | Some cursor -> "\"cursor\":\"" + cursor + "\","
            // add the query
            let url = serviceUrl + "/service/mqlread?query={"+cursor+"\"query\":"+query+"}"
            // api key not supported in old API
            url
        else
            // add the cursor
            let cursor = 
                match cursor with 
                | None -> ""
                | Some "" -> "&cursor"
                | Some cursor -> "&cursor=" + cursor 
            // add the query
            let url = serviceUrl + "/mqlread?query="+query+cursor 
            // add the apikey
            let url = if isStringNone apiKey then url else url + "&key=" + apiKey
            url

    /// Get a web response object for the given query.
    let queryRawText (url:string) =
        //printfn "request: %s" url
        sendingRequest.Trigger(System.Uri(url))
        let url = 
            match snapshotDate with 
            | None -> url
            | Some d -> url + "&as_of_time=" + d
        match getCache().TryRetrieve url with
        | Some resultText -> resultText
        | None ->
          let getResultText() = 
            if url.Length > 1500 && url.Contains "?"  then 
                let idx = url.IndexOf '?'
                let content = url.[idx + 1 .. ] 
                let shortUrl = url.[0.. idx - 1]
                //printfn "post, shortUrl = '%s'" shortUrl
                //printfn "post, content = '%s'" content
                Http.Request(shortUrl, meth = "POST", headers = [ "X-HTTP-Method-Override", "GET" ], body = content)
            else
                Http.Request(url)
          try
            let resultText = getResultText()
            getCache().Set(url, resultText)
            resultText
          with 
            | :? WebException as exn -> 
                if exn.Response = null then Http.reraisePreserveStackTrace exn
                let freebaseExn =
                    try
                        use responseStream = exn.Response.GetResponseStream()
                        use streamReader = new StreamReader(responseStream)
                        let response = streamReader.ReadToEnd() |> JsonValue.Parse 
                        let error = response.GetProperty("error").GetArrayValWithKey("errors").[0]
                        let domain = error.GetStringValWithKey("domain")
                        let reason = error.GetStringValWithKey("reason")
                        let message  = error.GetStringValWithKey("message")
                        let extendedHelp = error.GetOptionalStringValWithKey("extendedHelp")
                        Some <| FreebaseWebException(exn, domain, reason, message, extendedHelp)
                    with _ -> None
                match freebaseExn with
                | Some e -> raise e
                | None -> Http.reraisePreserveStackTrace exn

    let queryString(queryUrl, fromJson) : FreebaseResult<'T> = 
        let resultText = queryRawText queryUrl
        let fbr = JsonValue.Parse resultText
        let result = FreebaseResult<'T>.FromJson fromJson fbr
        if freebaseV0 && result.Code <> "/api/status/ok" then raise (InvalidOperationException(sprintf "failed query, error: '%s': \n----\n%s\n----" result.Message queryUrl))
        result
            

    member __.LocalCacheLocation = localCacheLocation
    member __.SendingRequest = sendingRequest.Publish
    member __.UseLocalCache with get() = useLocalCache and set v = useLocalCache <- v
    member __.ServiceUrl with get() = serviceUrl and set v = serviceUrl  <- v
    member __.SnapshotDate = snapshotDate
        
    member __.Query<'T>(query:string, fromJson) : 'T =
        let queryUrl = createQueryUrl(query,None)
        queryString(queryUrl, fromJson).Result

    member __.GetImageUrl(imageId) =
        if freebaseV0 then "http://freebaseread.com/api/trans/raw/"+imageId
        else 
            let url = "https://usercontent.googleapis.com/freebase/v1/image"+imageId
            let url = if isStringNone apiKey then url else url + "?key=" + apiKey
            url

    member __.QuerySequence<'T>(query:string,fromJson, explicitLimit) : 'T seq =
        seq { let cursor = ref (Some "")
              let complete = ref false
              let count = ref 0
              while not !complete && (match explicitLimit with None -> true | Some -1 -> true | Some lim -> !count < lim) do 
                  let queryUrl = createQueryUrl(query,!cursor)
                  let response = queryString(queryUrl, JsonValue.GetArrayVal fromJson)
                  count := !count + response.Result.Length
                  yield! response.Result
                  match response.Cursor with 
                  | "false" | "False" -> complete := true
                  | continuation -> cursor := Some continuation  }

    member fb.GetBlurbByArticleId (articleId:string) = 
        let queryUrl = 
            if freebaseV0 then serviceUrl + "/trans/blurb"+articleId+"?maxlength=1200"
            else serviceUrl + "/text"+articleId+"?maxlength=1200&format=plain"
        try 
            let resultText = queryRawText queryUrl
            let fbr = JsonValue.Parse resultText
            let result = fbr |> FreebaseResult<string>.FromJson (fun j -> j.AsString())
            Some result.Result
        with e -> None
            
