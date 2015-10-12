// --------------------------------------------------------------------------------------
// Fixes the way slashs are encoded in System.Uri across Mono and .NET.
// Ported from https://github.com/glennblock/PUrify
// --------------------------------------------------------------------------------------
module internal FSharp.Data.Runtime.UriUtils

#if FX_NO_URI_WORKAROUND

let enableUriSlashes = id

#else

open System
open System.Reflection

type private UriInfo =
    { Path : string
      Query : string }

let private uriInfo (uri : Uri) (source : string) = 

    let fragPos = source.IndexOf("#")
    let queryPos = source.IndexOf("?")
    let start = source.IndexOf(uri.Host) + uri.Host.Length
    let pathEnd = 
        match queryPos, fragPos with
        | -1, -1 -> source.Length + 1 
        | -1, _ -> fragPos
        | _ -> queryPos

    let path =
        if queryPos > -1 || fragPos > -1 then source.Substring(start, pathEnd - start)
        else source.Substring(start)
    
    let query = 
        match queryPos, fragPos with
        | -1, _ -> ""
        | _, -1 -> source.Substring(queryPos)
        | _ -> source.Substring(queryPos, fragPos - queryPos)
    
    { Path = path
      Query = query }

let private privateInstanceFlags = BindingFlags.NonPublic ||| BindingFlags.Instance
let private publicInstanceFlags = BindingFlags.Public ||| BindingFlags.Instance

let private purifierDotNet = lazy(

    let uriType = typeof<Uri>
    let flagsField = uriType.GetField("m_Flags", privateInstanceFlags)
    let stringField = uriType.GetField("m_String", privateInstanceFlags)
    let infoField = uriType.GetField("m_Info", privateInstanceFlags)

    let infoFieldType = infoField.FieldType
    let infoStringField = infoFieldType.GetField("String", publicInstanceFlags)
    let moreInfoField = infoFieldType.GetField("MoreInfo", publicInstanceFlags)

    let moreInfoType = moreInfoField.FieldType
    let moreInfoAbsoluteUri = moreInfoType.GetField("AbsoluteUri", publicInstanceFlags)
    let moreInfoQuery = moreInfoType.GetField("Query", publicInstanceFlags)
    let moreInfoPath = moreInfoType.GetField("Path", publicInstanceFlags)

    // Code inspired by Rasmus Faber's solution in this post: http://stackoverflow.com/questions/781205/getting-a-url-with-an-url-encoded-slash
    fun (uri : Uri) ->

        uri.PathAndQuery |> ignore // need to access PathAndQuery
        uri.AbsoluteUri |> ignore // need to access this as well the MoreInfo prop is initialized.

        let flags = 
            flagsField.GetValue(uri) :?> uint64
            &&& (~~~ 0x30UL) // Flags.PathNotCanonical|Flags.QueryNotCanonical

        flagsField.SetValue(uri, flags)
        let info = infoField.GetValue(uri)
        let source = stringField.GetValue(uri) |> string
        infoStringField.SetValue(info, source)
        let moreInfo = moreInfoField.GetValue(info)
        moreInfoAbsoluteUri.SetValue(moreInfo, source)
        let uriInfo = uriInfo uri source
        moreInfoPath.SetValue(moreInfo, uriInfo.Path)
        moreInfoQuery.SetValue(moreInfo, uriInfo.Query)

        uri)

let private purifierMono = lazy(

    let uriType = typeof<Uri>

    let sourceField = uriType.GetField("source", privateInstanceFlags)
    let queryField = uriType.GetField("query", privateInstanceFlags)
    let pathField = uriType.GetField("path", privateInstanceFlags)
    let cachedToStringField = uriType.GetField("cachedToString", privateInstanceFlags)
    let cachedAbsoluteUriField = uriType.GetField("cachedAbsoluteUri",privateInstanceFlags)

    fun (uri : Uri) ->
    
        let source = string (sourceField.GetValue(uri))
        cachedToStringField.SetValue(uri, source)
        cachedAbsoluteUriField.SetValue(uri, source)
        let uriInfo = uriInfo uri source
        pathField.SetValue(uri, uriInfo.Path)
        queryField.SetValue(uri, uriInfo.Query)

        uri)

let private isMono = typeof<Uri>.GetField("m_Flags", privateInstanceFlags) = null

let private hasBrokenDotNetUri =
    if isMono then false
    else
        // ShouldUseLegacyV2Quirks was introduced in .net 4.5
        // Eventhough 4.5 is an inplace update of 4.0 this call will return 
        // a different value if an application specifically targets 4.0 or 4.5+
        let legacyV2Quirks = typeof<Uri>.GetProperty("ShouldUseLegacyV2Quirks", BindingFlags.Static ||| BindingFlags.NonPublic)
        match legacyV2Quirks with
        | null -> true //neither 4.0 or 4.5
        | _ ->
            let isBrokenUri = unbox (legacyV2Quirks.GetValue(null, null))
            if not isBrokenUri then false //application targets 4.5
            else
                // 4.0 uses legacyV2quirks on the UriParser but you can set
                // <uri>
                //   <schemeSettings>
                //     <add name="http" genericUriParserOptions="DontUnescapePathDotsAndSlashes" />
                //   </schemeSettings>
                // </uri>
                //
                //  this will fix AbsoluteUri but not ToString()
                //  i.e new Uri("http://google.com/%2F").AbsoluteUri
                //       will return the url untouched but:
                //  new Uri("http://google.com/%2F").ToString()
                //      will still return http://google.com//
                //
                //  so instead of using reflection perform a one off function test.

                let uri = new Uri("http://google.com/%2F")
                uri.ToString().EndsWith("%2F", StringComparison.InvariantCulture)

let enableUriSlashes =
    if isMono then id //purifierMono.Force()
    elif hasBrokenDotNetUri then purifierDotNet.Force()
    else id

#endif
