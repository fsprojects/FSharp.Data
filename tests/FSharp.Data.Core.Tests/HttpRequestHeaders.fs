module FSharp.Data.Tests.HttpRequestHeaders

open FsUnit
open NUnit.Framework
open System
open System.Globalization
open System.Text
open FSharp.Data
open FSharp.Data.HttpRequestHeaders

[<TestFixture>]
type HttpRequestHeadersTests() =

    [<Test>]
    member _.``Accept header should format correctly``() =
        Accept "application/json" |> should equal ("Accept", "application/json")
        Accept "text/html, application/xhtml+xml" |> should equal ("Accept", "text/html, application/xhtml+xml")

    [<Test>]
    member _.``AcceptCharset header should format correctly``() =
        AcceptCharset "utf-8" |> should equal ("Accept-Charset", "utf-8")
        AcceptCharset "utf-8, iso-8859-1;q=0.5" |> should equal ("Accept-Charset", "utf-8, iso-8859-1;q=0.5")

    [<Test>]
    member _.``AcceptDatetime header should format correctly with RFC1123``() =
        let dt = DateTime(2023, 11, 15, 14, 30, 0, DateTimeKind.Utc)
        AcceptDatetime dt |> should equal ("Accept-Datetime", dt.ToString("R", CultureInfo.InvariantCulture))

    [<Test>]
    member _.``AcceptEncoding header should format correctly``() =
        AcceptEncoding "gzip, deflate" |> should equal ("Accept-Encoding", "gzip, deflate")
        AcceptEncoding "br;q=1.0, gzip;q=0.8, *;q=0.1" |> should equal ("Accept-Encoding", "br;q=1.0, gzip;q=0.8, *;q=0.1")

    [<Test>]
    member _.``AcceptLanguage header should format correctly``() =
        AcceptLanguage "en-US" |> should equal ("Accept-Language", "en-US")
        AcceptLanguage "en-US,en;q=0.5" |> should equal ("Accept-Language", "en-US,en;q=0.5")

    [<Test>]
    member _.``Allow header should format correctly``() =
        Allow "GET, POST, PUT" |> should equal ("Allow", "GET, POST, PUT")
        Allow "GET, HEAD, OPTIONS" |> should equal ("Allow", "GET, HEAD, OPTIONS")

    [<Test>]
    member _.``Authorization header should format correctly``() =
        Authorization "Bearer token123" |> should equal ("Authorization", "Bearer token123")
        Authorization "Basic dXNlcjpwYXNz" |> should equal ("Authorization", "Basic dXNlcjpwYXNz")

    [<Test>]
    member _.``BasicAuth header should generate correct base64 encoding``() =
        let (headerName, headerValue) = BasicAuth "user" "password"
        headerName |> should equal "Authorization"
        // "user:password" in base64 is "dXNlcjpwYXNzd29yZA=="
        headerValue |> should equal "Basic dXNlcjpwYXNzd29yZA=="

    [<Test>]
    member _.``BasicAuth with special characters should encode correctly``() =
        let (headerName, headerValue) = BasicAuth "test@domain.com" "p@$$w0rd!"
        headerName |> should equal "Authorization"
        // Verify it starts with "Basic " and is valid base64
        headerValue |> should startWith "Basic "
        let base64Part = headerValue.Substring(6)
        let decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64Part))
        decoded |> should equal "test@domain.com:p@$$w0rd!"

    [<Test>]
    member _.``CacheControl header should format correctly``() =
        CacheControl "no-cache" |> should equal ("Cache-Control", "no-cache")
        CacheControl "max-age=3600, must-revalidate" |> should equal ("Cache-Control", "max-age=3600, must-revalidate")

    [<Test>]
    member _.``Connection header should format correctly``() =
        Connection "keep-alive" |> should equal ("Connection", "keep-alive")
        Connection "close" |> should equal ("Connection", "close")

    [<Test>]
    member _.``ContentDisposition header with attachment should format correctly``() =
        ContentDisposition ("attachment", None, Some "document.pdf") 
        |> should equal ("Content-Disposition", "attachment; filename=\"document.pdf\"")

    [<Test>]
    member _.``ContentDisposition header with form-data should format correctly``() =
        ContentDisposition ("form-data", Some "file", Some "upload.txt")
        |> should equal ("Content-Disposition", "form-data; name=\"file\"; filename=\"upload.txt\"")

    [<Test>]
    member _.``ContentDisposition header with inline should format correctly``() =
        ContentDisposition ("inline", None, None)
        |> should equal ("Content-Disposition", "inline")

    [<Test>]
    member _.``ContentEncoding header should format correctly``() =
        ContentEncoding "gzip" |> should equal ("Content-Encoding", "gzip")
        ContentEncoding "br" |> should equal ("Content-Encoding", "br")

    [<Test>]
    member _.``ContentLanguage header should format correctly``() =
        ContentLanguage "en-US" |> should equal ("Content-Language", "en-US")
        ContentLanguage "fr-FR" |> should equal ("Content-Language", "fr-FR")

    [<Test>]
    member _.``ContentLocation header should format correctly``() =
        ContentLocation "https://example.com/resource" |> should equal ("Content-Location", "https://example.com/resource")

    [<Test>]
    member _.``ContentMD5 header should format correctly``() =
        ContentMD5 "d41d8cd98f00b204e9800998ecf8427e" |> should equal ("Content-MD5", "d41d8cd98f00b204e9800998ecf8427e")

    [<Test>]
    member _.``ContentRange header should format correctly``() =
        ContentRange "bytes 200-1023/1024" |> should equal ("Content-Range", "bytes 200-1023/1024")

    [<Test>]
    member _.``ContentType header should format correctly``() =
        ContentType "application/json" |> should equal ("Content-Type", "application/json")
        ContentType "text/html" |> should equal ("Content-Type", "text/html")

    [<Test>]
    member _.``ContentTypeWithEncoding header should format correctly``() =
        ContentTypeWithEncoding ("text/html", Encoding.UTF8) |> should equal ("Content-Type", "text/html; charset=utf-8")
        ContentTypeWithEncoding ("application/xml", Encoding.Unicode) |> should equal ("Content-Type", "application/xml; charset=utf-16")

    [<Test>]
    member _.``Date header should format with RFC1123``() =
        let dt = DateTime(2023, 11, 15, 14, 30, 0, DateTimeKind.Utc)
        Date dt |> should equal ("Date", dt.ToString("R", CultureInfo.InvariantCulture))

    [<Test>]
    member _.``Expect header should format correctly``() =
        Expect "100-continue" |> should equal ("Expect", "100-continue")

    [<Test>]
    member _.``Expires header should format with RFC1123``() =
        let dt = DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        Expires dt |> should equal ("Expires", dt.ToString("R", CultureInfo.InvariantCulture))

    [<Test>]
    member _.``From header should format correctly``() =
        From "test@example.com" |> should equal ("From", "test@example.com")

    [<Test>]
    member _.``Host header should format correctly``() =
        Host "www.example.com" |> should equal ("Host", "www.example.com")
        Host "example.com:8080" |> should equal ("Host", "example.com:8080")

    [<Test>]
    member _.``IfMatch header should format correctly``() =
        IfMatch "\"737060cd8c284d8af7ad3082f209582d\"" |> should equal ("If-Match", "\"737060cd8c284d8af7ad3082f209582d\"")

    [<Test>]
    member _.``IfModifiedSince header should format with RFC1123``() =
        let dt = DateTime(2023, 10, 1, 12, 0, 0, DateTimeKind.Utc)
        IfModifiedSince dt |> should equal ("If-Modified-Since", dt.ToString("R", CultureInfo.InvariantCulture))

    [<Test>]
    member _.``IfNoneMatch header should format correctly``() =
        IfNoneMatch "\"737060cd8c284d8af7ad3082f209582d\"" |> should equal ("If-None-Match", "\"737060cd8c284d8af7ad3082f209582d\"")
        IfNoneMatch "*" |> should equal ("If-None-Match", "*")

    [<Test>]
    member _.``IfRange header should format correctly``() =
        IfRange "\"737060cd8c284d8af7ad3082f209582d\"" |> should equal ("If-Range", "\"737060cd8c284d8af7ad3082f209582d\"")

    [<Test>]
    member _.``IfUnmodifiedSince header should format with RFC1123``() =
        let dt = DateTime(2023, 9, 15, 10, 0, 0, DateTimeKind.Utc)
        IfUnmodifiedSince dt |> should equal ("If-Unmodified-Since", dt.ToString("R", CultureInfo.InvariantCulture))

    [<Test>]
    member _.``KeepAlive header should format correctly``() =
        KeepAlive "timeout=5, max=1000" |> should equal ("Keep-Alive", "timeout=5, max=1000")

    [<Test>]
    member _.``LastModified header should format with RFC1123``() =
        let dt = DateTime(2023, 8, 20, 16, 45, 0, DateTimeKind.Utc)
        LastModified dt |> should equal ("Last-Modified", dt.ToString("R", CultureInfo.InvariantCulture))

    [<Test>]
    member _.``MaxForwards header should format correctly``() =
        MaxForwards 10 |> should equal ("Max-Forwards", "10")
        MaxForwards 0 |> should equal ("Max-Forwards", "0")

    [<Test>]
    member _.``Origin header should format correctly``() =
        Origin "https://example.com" |> should equal ("Origin", "https://example.com")
        Origin "https://sub.example.com:8080" |> should equal ("Origin", "https://sub.example.com:8080")

    [<Test>]
    member _.``Pragma header should format correctly``() =
        Pragma "no-cache" |> should equal ("Pragma", "no-cache")

    [<Test>]
    member _.``Prefer header should format correctly``() =
        Prefer "return=representation" |> should equal ("Prefer", "return=representation")
        Prefer "respond-async" |> should equal ("Prefer", "respond-async")

    [<Test>]
    member _.``ProxyAuthorization header should format correctly``() =
        ProxyAuthorization "Basic dXNlcjpwYXNz" |> should equal ("Proxy-Authorization", "Basic dXNlcjpwYXNz")

    [<Test>]
    member _.``Range header should format correctly``() =
        Range (0L, 499L) |> should equal ("Range", "bytes=0-499")
        Range (500L, 999L) |> should equal ("Range", "bytes=500-999")

    [<Test>]
    member _.``Referer header should format correctly``() =
        Referer "https://www.google.com/" |> should equal ("Referer", "https://www.google.com/")

    [<Test>]
    member _.``TE header should format correctly``() =
        TE "trailers, deflate;q=0.5" |> should equal ("TE", "trailers, deflate;q=0.5")

    [<Test>]
    member _.``Trailer header should format correctly``() =
        Trailer "Max-Forwards" |> should equal ("Trailer", "Max-Forwards")

    [<Test>]
    member _.``TransferEncoding header should format correctly``() =
        TransferEncoding "chunked" |> should equal ("Transfer-Encoding", "chunked")
        TransferEncoding "gzip" |> should equal ("Transfer-Encoding", "gzip")

    [<Test>]
    member _.``Translate header should format correctly``() =
        Translate "f" |> should equal ("Translate", "f")

    [<Test>]
    member _.``Upgrade header should format correctly``() =
        Upgrade "HTTP/2.0, HTTPS/1.3, IRC/6.9, RTA/x11" |> should equal ("Upgrade", "HTTP/2.0, HTTPS/1.3, IRC/6.9, RTA/x11")

    [<Test>]
    member _.``UserAgent header should format correctly``() =
        UserAgent "Mozilla/5.0 (compatible; FSharp.Data)" |> should equal ("User-Agent", "Mozilla/5.0 (compatible; FSharp.Data)")

    [<Test>]
    member _.``Via header should format correctly``() =
        Via "1.0 fred, 1.1 example.com (Apache/1.1)" |> should equal ("Via", "1.0 fred, 1.1 example.com (Apache/1.1)")

    [<Test>]
    member _.``Warning header should format correctly``() =
        Warning "199 Miscellaneous warning" |> should equal ("Warning", "199 Miscellaneous warning")

    [<Test>]
    member _.``XHTTPMethodOverride header should format correctly``() =
        XHTTPMethodOverride "PUT" |> should equal ("X-HTTP-Method-Override", "PUT")
        XHTTPMethodOverride "DELETE" |> should equal ("X-HTTP-Method-Override", "DELETE")