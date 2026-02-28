module FSharp.Data.Tests.HtmlProvider

open NUnit.Framework
open FsUnit
open System
open FSharp.Data
open FSharp.Data.UnitSystems.SI.UnitNames

[<Literal>]
let simpleHtml = """<html>
                    <body>
                        <table title="table">
                            <tr><th>Date</th><th>Column 1</th><th>Column 2</th><th>Column 3</th><th>Column 4</th></tr>
                            <tr><td>01/01/2013 12:00</td><td>1</td><td>yes</td><td>2</td><td>2</td></tr>
                            <tr><td>01/02/2013 12:00</td><td>2</td><td>no</td><td>2.5</td><td>2</td></tr>
                            <tr><td>01/03/2013 12:00</td><td>3</td><td>true</td><td>3.456</td><td>2.3</td></tr>
                            <tr><td>01/04/2013 12:00</td><td>4</td><td>true</td><td>2.4</td><td>&nbsp;</td></tr>
                        </table>
                    </body>
                </html>"""

type SimpleHtml = HtmlProvider<simpleHtml, PreferOptionals=true>

[<Test>]
let ``SimpleHtml infers date type correctly ``() =
    let table = SimpleHtml().Tables.Table
    table.Rows.[0].Date |> should equal (DateTime(2013, 01, 01, 12, 00, 00))

[<Test>]
let ``SimpleHtml infers int type correctly ``() =
    let table = SimpleHtml().Tables.Table
    table.Rows.[0].``Column 1`` |> should equal 1

[<Test>]
let ``SimpleHtml infers bool type correctly ``() =
    let table = SimpleHtml().Tables.Table
    table.Rows.[0].``Column 2`` |> should equal true

[<Test>]
let ``SimpleHtml infers decimal type correctly ``() =
    let table = SimpleHtml().Tables.Table
    table.Rows.[0].``Column 3`` |> should equal 2M

[<Test>]
let ``SimpleHtml infers as optional fail through type correctly ``() =
    let table = SimpleHtml().Tables.Table
    table.Rows.[0].``Column 4`` |> should equal (Some 2M)

[<Test>]
let ``Can create type for simple table``() =
    let table = SimpleHtml().Tables.Table
    table.Rows.[0].``Column 1`` |> should equal 1

type MarketDepth = HtmlProvider<"Data/MarketDepth.htm", PreferDateOnly = true>

[<Test>]
let ``Can infer tables out of the market depth file``() =
    let table = MarketDepth().Tables.Table1
    table.Rows.[0].``Settlement Day`` |> should equal (DateOnly(2014, 1, 14))
    table.Rows.[0].Period |> should equal 1

[<Test>]
let ``NuGet table gets all rows``() =
    let table = HtmlProvider<"Data/NuGet.html">.GetSample().Tables.``Version History``
    table.Rows.Length |> should equal 35

[<Test>]
let ``Should find the table as a header``() =
    let table = HtmlProvider<"""<html>
                    <body>
                        <div>
                            <h2>Example Table</h2>
                        </div>
                        <table>
                            <tr><th>Date</th><th>Column 1</th><th>Column 2</th><th>Column 3</th><th>Column 4</th></tr>
                            <tr><td>01/01/2013 12:00</td><td>1</td><td>yes</td><td>2</td><td>2</td></tr>
                        </table>
                    </body>
                </html>""", PreferOptionals=true>.GetSample().Tables.``Example Table``
    table.Rows.[0].``Column 3`` |> should equal 2M

[<Test>]
let ``Should find the table as a header when nested deeper``() =
    let table = HtmlProvider<"""<html>
                    <body>
                        <div>
                            <h2>
                                <a href="/I/go/somewhere/">
                                Example Table
                                </a>
                            </h2>
                        </div>
                        <table>
                            <tr><th>Date</th><th>Column 1</th><th>Column 2</th><th>Column 3</th><th>Column 4</th></tr>
                            <tr><td>01/01/2013 12:00</td><td>1</td><td>yes</td><td>2</td><td>2</td></tr>
                        </table>
                    </body>
                </html>""", PreferOptionals=true>.GetSample().Tables.``Example Table``
    table.Rows.[0].``Column 3`` |> should equal 2M

[<Test>]
let ``Should parse units from table headers``() =
    let table = HtmlProvider<"""<html>
                    <body>
                        <table>
                            <tr><th>Date</th><th>Distance (m)</th><th>Time (s)</th><th>Column 3</th><th>Column 4</th></tr>
                            <tr><td>01/01/2013 12:00</td><td>1.5</td><td>30.5</td><td>2</td><td>2</td></tr>
                        </table>
                    </body>
                </html>""">.GetSample().Tables.Table1
    let distance = table.Rows.[0].Distance
    distance |> should equal 1.5<metre>
    let time = table.Rows.[0].Time
    time |> should equal 30.5<second>

[<Test>]
let ``Should parse units from inferred table headers``() =
    let table = HtmlProvider<"""<html>
                    <body>
                        <table>
                            <tr><td>Date</td><td>Distance (m)</td><td>Time (s)</td><td>Column 3</td><td>Column 4</td></tr>
                            <tr><td>01/01/2013 12:00</td><td>1.5</td><td>30.5</td><td>2</td><td>2</td></tr>
                            <tr><td>01/01/2013 12:00</td><td>1.5</td><td>30.5</td><td>2</td><td>2</td></tr>
                        </table>
                    </body>
                </html>""">.GetSample().Tables.Table1
    let distance = table.Rows.[0].Distance
    distance |> should equal 1.5<metre>
    let time = table.Rows.[0].Time
    time |> should equal 30.5<second>

[<Test>]
let ``Can handle a table with a single column``() =
    let table = HtmlProvider<"""<html>
                    <body>
                        <table>
                            <tr><td>Value</td></tr>
                            <tr><td>2</td></tr>
                            <tr><td>2</td></tr>
                        </table>
                    </body>
                </html>""">.GetSample().Tables.Table1
    let percentage = table.Rows.[0]
    percentage |> should equal 2

[<Test>]
let ``Should infer a column with a currency prefix as the correct type``() =
    let table = HtmlProvider<"""<html>
                    <body>
                        <table>
                            <tr><td>Date</td><td>Cost</td></tr>
                            <tr><td>01/01/2013 12:00</td><td>£ 2</td></tr>
                            <tr><td>01/01/2013 12:00</td><td>£ 2</td></tr>
                        </table>
                    </body>
                </html>""">.GetSample().Tables.Table1
    let percentage = table.Rows.[0].Cost
    percentage |> should equal 2

[<Test>]
let ``Should infer a column with a percentage suffix as the correct type``() =
    let table = HtmlProvider<"""<html>
                    <body>
                        <table>
                            <tr><td>Date</td><td>Percentage</td></tr>
                            <tr><td>01/01/2013 12:00</td><td>2%</td></tr>
                            <tr><td>01/01/2013 12:00</td><td>2%</td></tr>
                        </table>
                    </body>
                </html>""">.GetSample().Tables.Table1
    let percentage = table.Rows.[0].Percentage
    percentage |> should equal 2

[<Test>]
let ``If finds name for table and element is empty reverts to default name``() =
    let table = HtmlProvider<"""<html>
                    <body>
                        <h2></h2>
                        <table>
                            <tr><td>Date</td><td>Percentage</td></tr>
                            <tr><td>01/01/2013 12:00</td><td>2%</td></tr>
                            <tr><td>01/01/2013 12:00</td><td>2%</td></tr>
                        </table>
                    </body>
                </html>""">.GetSample().Tables.Table1
    let percentage = table.Rows.[0].Percentage
    percentage |> should equal 2

[<Test>]
let ``Can handle a table with a thead declaring headers containing th``() =
    let table = HtmlProvider<"""<html>
                    <body>
                        <table>
                            <thead>
                                <tr>
                                    <th>Value</th>
                                    <th>Cost</th>
                                </tr>
                            </thead>
                            <tr><td>2</td><td>4</td></tr>
                            <tr><td>6</td><td>8</td></tr>
                        </table>
                    </body>
                </html>""">.GetSample().Tables.Table1
    let percentage = table.Rows.[0].Value
    percentage |> should equal 2

[<Test>]
let ``Can handle a table with a thead declaring headers containing td``() =
    let table = HtmlProvider<"""<html>
                    <body>
                        <table>
                            <thead>
                                <tr>
                                    <td>Value</td>
                                    <td>Cost</td>
                                </tr>
                            </thead>
                            <tr><td>2</td><td>4</td></tr>
                            <tr><td>6</td><td>8</td></tr>
                        </table>
                    </body>
                </html>""">.GetSample().Tables.Table1
    let percentage = table.Rows.[0].Value
    percentage |> should equal 2

[<Test>]
let ``Can handle a table with a thead, tfoot, tbody``() =
    let table = HtmlProvider<"""<html>
                    <body>
                        <table>
                         <thead>
                          <tr>
                             <th>Month</th>
                             <th>Savings</th>
                          </tr>
                         </thead>
                         <tfoot>
                          <tr>
                             <td>Sum</td>
                             <td>$180</td>
                          </tr>
                         </tfoot>
                         <tbody>
                          <tr>
                             <td>January</td>
                             <td>$100</td>
                          </tr>
                          <tr>
                             <td>February</td>
                             <td>$80</td>
                          </tr>
                         </tbody>
                        </table>
                    </body>
                </html>""">.GetSample().Tables.Table1
    let percentage = table.Rows.[0].Savings
    percentage |> should equal 180

[<Test>]
let ``Can handle a table with headers directly inside thead``() =
    let table = HtmlProvider<"""<html>
                    <body>
                        <table>
                            <thead>
                                <th>Month</th>
                                <th>Savings</th>
                            </thead>
                            <tbody>
                                <tr>
                                    <td>January</td>
                                    <td>$100</td>
                                </tr>
                                <tr>
                                    <td>February</td>
                                    <td>$80</td>
                                </tr>
                            </tbody>
                        </table>
                    </body>
                </html>""">.GetSample().Tables.Table1
    match table.Headers with
    | None -> failwith "No headers found"
    | Some headers -> headers |> should equal [| "Month"; "Savings" |]

[<Test>]
let ``Handles closing tag with number in script (Bug 800)``() =
    let html = HtmlProvider<"""
            <html>
                <head>
                    <title>Title</title>
                </head>
                <body>
                    <a href="www.google.com">0</a>
                    <script type="text/javascript">
                        var a = '</1>'
                    </script>
                    <a href="www.google.com">1</a>
                    <a href="www.google.com">2</a>
                    <a href="www.google.com">3</a>
                </body>
            </html>""">.GetSample()
    let data = html.Html.Descendants ["a"] |> Seq.toList
    data.Length |> should equal 4

type DoctorWho = FSharp.Data.HtmlProvider<"Data/doctor_who2.html">

[<Test>]
let ``List and Table with same nome don't clash``() =
    DoctorWho().Lists.``Reference websites``.Values.[0] |> should equal "Doctor Who on TARDIS Data Core, an external wiki"
    DoctorWho().Tables.``Reference websites``.Rows.[0].Awards |> should equal "Preceded by The Bill"

[<Test>]
let ``Count columns correctly in the presence of colspans (Bug 989)``() =
    let html = HtmlProvider<"""
            <html>
                <head>
                    <title>Title</title>
                </head>
                <body>
                   <table>
                            <tbody>
                                <tr>
                                    <th scope="col" colspan="2">Double</th>
                                    <th scope="col" colspan="1">Single</th>
                                </tr>
                                <tr>
                                    <td>Single</th>
                                    <td colspan="2">Double</td>
                                </tr>
                            </tbody>
                        </table>
                    </body>
                </body>
            </html>""">.GetSample()
    let table = html.Tables.Table1
    match table.Headers with
    | None -> failwith "No headers found"
    | Some headers -> headers |> should equal [| "Double"; "Double"; "Single" |]

[<Test>]
let ``Can infer DateTime and DateTimeOffset types correctly`` () =
    let html = HtmlProvider<"""
            <html>
                <head>
                    <title>Title</title>
                </head>
                <body>
                    <table>
                        <thead>
                        <tr>
                            <th>DateOnly</th>
                            <th>MixedDate</th>
                            <th>DateWithOffset</th>
                        </tr>
                        </thead>
                        <tbody>
                        <tr>
                            <td>2018-04-21</td>
                            <td>2018-04-22</td>
                            <td>2018-04-23+10:00</td>
                        </tr>
                        <tr>
                            <td>2018-04-24</td>
                            <td>2018-04-25+00:00</td>
                            <td>2018-04-26+06:00</td>
                        </tr>
                        </tbody>
                    </table>
            </html>""", PreferDateOnly = true>.GetSample()
    let table = html.Tables.Table1
    table.Rows.[0].DateOnly.GetType() |> should equal typeof<DateOnly>
    table.Rows.[0].MixedDate.GetType() |> should equal typeof<DateTime>
    table.Rows.[0].DateWithOffset.GetType() |> should equal typeof<DateTimeOffset>

// ============================================
// Schema.org Microdata via HtmlProvider
// ============================================

[<Literal>]
let schemaMicrodata = """<html><body>
  <div itemscope itemtype="http://schema.org/Person">
    <span itemprop="name">Jane Smith</span>
    <span itemprop="jobTitle">Software Engineer</span>
    <a itemprop="url" href="https://example.com">Homepage</a>
  </div>
  <div itemscope itemtype="http://schema.org/Person">
    <span itemprop="name">Bob Jones</span>
    <span itemprop="jobTitle">Designer</span>
  </div>
</body></html>"""

type SchemaMicrodataHtml = HtmlProvider<schemaMicrodata>

[<Test>]
let ``HtmlProvider exposes Schemas container for microdata`` () =
    let doc = SchemaMicrodataHtml.GetSample()
    let people = doc.Schemas.Person
    people |> should not' (be null)

[<Test>]
let ``HtmlProvider Schemas.Person returns all items`` () =
    let doc = SchemaMicrodataHtml.GetSample()
    let people = doc.Schemas.Person
    people |> should haveLength 2

[<Test>]
let ``HtmlProvider Schemas.Person items have typed properties`` () =
    let doc = SchemaMicrodataHtml.GetSample()
    let people = doc.Schemas.Person
    people.[0].Name |> should equal "Jane Smith"
    people.[0].JobTitle |> should equal "Software Engineer"
    people.[0].Url |> should equal "https://example.com"
    people.[1].Name |> should equal "Bob Jones"
