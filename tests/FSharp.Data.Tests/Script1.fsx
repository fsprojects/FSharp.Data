#r "../../bin/FSharp.Data.dll"
#r "../../bin/FSharp.Data.Experimental.dll"

open FSharp.Data.Experimental

[<Literal>]
let simpleHtml = """<html>
                    <body>
                        <table title="table">
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""

type SimpleHtml = HtmlProvider<simpleHtml>

let table = SimpleHtml.Tables.table.Load(simpleHtml)

table.Data