#load "Net/UriUtils.fs"
#load "Net/Http.fs"
#load "CommonRuntime/IO.fs"
#load "CommonRuntime/TextConversions.fs"
#load "CommonRuntime/TextRuntime.fs"
#load "Html/HtmlCharRefs.fs"
#load "Html/HtmlParser.fs"

open System.IO
open FSharp.Data

let html = """
<ul>
    <li>1</il>
    <li>2</il>
</ul>"""

let doc = HtmlParser.parseFragment (new StringReader(html)) 
