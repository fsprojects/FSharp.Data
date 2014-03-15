#if INTERACTIVE
#load "Net/UriUtils.fs"
#load "Net/Http.fs"
#load "CommonRuntime/IO.fs"
#load "CommonRuntime/TextConversions.fs"
#load "CommonRuntime/TextRuntime.fs"
#load "Html/HtmlCharRefs.fs"
#load "Html/HtmlParser.fs"
#load "Html/HtmlOperations.fs"
#load "Html/HtmlRuntime.fs"
#else
module internal Test.HtmlRuntime
#endif

open System
open FSharp.Data
open FSharp.Data.Runtime

let doc = HtmlDocument.Load (__SOURCE_DIRECTORY__ + """\..\tests\FSharp.Data.Tests\Data\NuGet.html""")
let tables = HtmlRuntime.getTables doc
()
