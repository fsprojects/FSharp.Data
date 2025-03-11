// --------------------------------------------------------------------------------------
// XML type provider - methods & types used by the generated erased code
// --------------------------------------------------------------------------------------
namespace FSharp.Data

open System.Xml.Linq
open System.Runtime.InteropServices

// XElementExtensions is not a static class with C#-style extension methods because that would
// force to reference System.Xml.Linq.dll everytime you reference FSharp.Data, even when not using
// any of the XML parts
[<AutoOpen>]
/// Extension methods for XElement
module XElementExtensions =

    type XElement with

        /// Sends the XML to the specified uri. Defaults to a POST request.
        member x.Request(uri: string, [<Optional>] ?httpMethod, [<Optional>] ?headers: seq<_>) =
            let httpMethod = defaultArg httpMethod HttpMethod.Post
            let headers = defaultArg (Option.map List.ofSeq headers) []

            let headers =
                if headers |> List.exists (fst >> (=) (fst (HttpRequestHeaders.UserAgent ""))) then
                    headers
                else
                    HttpRequestHeaders.UserAgent "FSharp.Data XML Type Provider" :: headers

            let headers = HttpRequestHeaders.ContentType HttpContentTypes.Xml :: headers

            Http.Request(
                uri,
                body = TextRequest(x.ToString(SaveOptions.DisableFormatting)),
                headers = headers,
                httpMethod = httpMethod
            )

        /// Sends the XML to the specified uri. Defaults to a POST request.
        member x.RequestAsync(uri: string, [<Optional>] ?httpMethod, [<Optional>] ?headers: seq<_>) =
            let httpMethod = defaultArg httpMethod HttpMethod.Post
            let headers = defaultArg (Option.map List.ofSeq headers) []

            let headers =
                if headers |> List.exists (fst >> (=) (fst (HttpRequestHeaders.UserAgent ""))) then
                    headers
                else
                    HttpRequestHeaders.UserAgent "FSharp.Data XML Type Provider" :: headers

            let headers = HttpRequestHeaders.ContentType HttpContentTypes.Xml :: headers

            Http.AsyncRequest(
                uri,
                body = TextRequest(x.ToString(SaveOptions.DisableFormatting)),
                headers = headers,
                httpMethod = httpMethod
            )
