#if INTERACTIVE
#r "../../bin/lib/net45/FSharp.Data.dll"
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/test/FsUnit/lib/net46/FsUnit.NUnit.dll"
#else
module FSharp.Data.Tests.HtmlParser
#endif

open System.Globalization
open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.HtmlDocument
open FSharp.Data.HtmlNode

let getTables includeLayoutTables = 
    let parameters : HtmlInference.Parameters = 
        { MissingValues = TextConversions.DefaultMissingValues
          CultureInfo = CultureInfo.InvariantCulture
          UnitsOfMeasureProvider = StructuralInference.defaultUnitsOfMeasureProvider
          PreferOptionals = false }
    HtmlRuntime.getTables (Some parameters) includeLayoutTables

[<Test>]
let ``Can handle unclosed tags correctly``() = 
    let simpleHtml = """<html>
                         <head>
                            <script language="JavaScript" src="/bwx_generic.js"></script>
                            <link rel="stylesheet" type="text/css" href="/bwx_style.css">
                            </head>
                        <body>
                            <img src="myimg.jpg">
                            <table title="table">
                                <tr><th>Column 1</th><th>Column 2</th></tr>
                                <tr><td>1</td><td>yes</td></tr>
                            </table>
                        </body>
                    </html>"""
    let result = HtmlDocument.Parse simpleHtml
    
    let expected = 
        HtmlDocument.New 
            [ HtmlNode.NewElement
                  ("html", 
                   [ HtmlNode.NewElement("head", 
                                         [ HtmlNode.NewElement("script", 
                                                               [ "language", "JavaScript"
                                                                 "src", "/bwx_generic.js" ])
                                           HtmlNode.NewElement("link", 
                                                               [ "rel", "stylesheet"
                                                                 "type", "text/css"
                                                                 "href", "/bwx_style.css" ]) ])
                     
                     HtmlNode.NewElement
                         ("body", 
                          [ HtmlNode.NewElement("img", [ "src", "myimg.jpg" ])
                            
                            HtmlNode.NewElement
                                ("table", [ "title", "table" ], 
                                 [ HtmlNode.NewElement("tr", 
                                                       [ HtmlNode.NewElement("th", [ HtmlNode.NewText "Column 1" ])
                                                         HtmlNode.NewElement("th", [ HtmlNode.NewText "Column 2" ]) ])
                                   HtmlNode.NewElement("tr", 
                                                       [ HtmlNode.NewElement("td", [ HtmlNode.NewText "1" ])
                                                         HtmlNode.NewElement("td", [ HtmlNode.NewText "yes" ]) ]) ]) ]) ]) ]
    result |> should equal expected

[<Test>]
let ``Can handle unclosed divs inside lis correctly``() = 
    let simpleHtml = "<ul><li><div></li><li></li></ul>"
    let result = HtmlDocument.Parse simpleHtml    
    let expected = 
        HtmlDocument.New 
            [ HtmlNode.NewElement
                  ("ul", 
                   [ HtmlNode.NewElement("li", [ HtmlNode.NewElement("div")])
                     HtmlNode.NewElement("li")]) ]
    result |> should equal expected

[<TestCase(@"<a href=http://test.com/index>Test</a>")>]
[<TestCase(@"<a href = http://test.com/index>Test</a>")>]
[<TestCase(@"<a href =http://test.com/index>Test</a>")>]
[<TestCase(@"<a href= http://test.com/index>Test</a>")>]
let ``Can handle slashes in unquoted attributes`` content =
    let result = HtmlDocument.Parse content
    let expected = 
        HtmlDocument.New
            [ HtmlNode.NewElement("a",
                [ "href", @"http://test.com/index" ],
                [ HtmlNode.NewText "Test" ]) ]
    result |> should equal expected

[<Test>]
let ``Can handle char refs in unquoted attributes``() =
    let result = HtmlDocument.Parse "<a alt=&lt;>Test</a>"
    let expected = 
        HtmlDocument.New
            [ HtmlNode.NewElement("a",
                [ "alt", "<" ],
                [ HtmlNode.NewText "Test" ]) ]
    result |> should equal expected

[<Test>]
let ``Can handle multiple unquoted attributes``() =
    let result = HtmlDocument.Parse "<a src = target alt = logo>Test</a>"
    let expected = 
        HtmlDocument.New
            [ HtmlNode.NewElement("a",
                [ "src", "target"
                  "alt", "logo" ],
                [ HtmlNode.NewText "Test" ]) ]
    result |> should equal expected

[<Test>]
let ``Can handle multiple char refs in a text run``() = 
    let html = HtmlNode.Parse "<div>&quot;Foo&quot;</div>"
    let result = html.Head.InnerText()
    result |> should equal "\"Foo\""

[<Test>]
let ``Can handle attributes with no value``() = 
    let html = """<li itemscope itemtype="http://schema.org/Place"></li>"""
    let node = HtmlNode.Parse html |> List.head
    let expected = 
        [
            HtmlAttribute.New("itemscope", "")
            HtmlAttribute.New("itemtype", "http://schema.org/Place")
        ]
    node.Attributes() |> should equal expected

[<TestCase("var r = \"</script>\"")>]
[<TestCase("var r = '</script>'")>]
[<TestCase("var r = /</g")>]
[<TestCase("""var r = /\/</g""")>]
[<TestCase("""var r = /a\/</g""")>]
[<TestCase("""var r = /\\/g""")>]
[<TestCase("//</script>\n")>]
[<TestCase("/*</script>*/")>]
[<TestCase("/*</script>**/")>]
[<TestCase("""/*
</script>
Test comment
*/""")>]
let ``Can handle special characters in scripts`` content =
    let html = sprintf "<script>%s</script>" content
    let node = HtmlNode.Parse html |> List.head
    let expected = HtmlNode.NewElement("script", [ HtmlNode.NewText content ])
    node |> should equal expected

[<Test>]
let ``Can handle special characters in single line script comments`` () =
    let html = "<script>//</script><body></body>"
    let node = HtmlNode.Parse html |> List.head
    let expected = HtmlNode.NewElement("script")
    node |> should equal expected

[<Test>]
let ``Can parse tables from a simple html``() = 
    let html = """<html>
                    <body>
                        <table id="table">
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].HasHeaders |> should equal (Some true)
    tables.[0].Name |> should equal "table"
    tables.[0].Rows |> should equal [ [ "Column 1" ]
                                      [ "1" ] ]

[<Test>]
let ``Can parse tables from a simple html table but infer headers``() = 
    let html = """<html>
                    <body>
                        <table id="table">
                            <tr><td>Column 1</td></tr>
                            <tr><td>1</td></tr>
                            <tr><td>2</td></tr>
                        </table>
                    </body>
                </html>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].HasHeaders |> should equal (Some true)
    tables.[0].Name |> should equal "table"
    tables.[0].Rows |> should equal [ [ "Column 1" ]
                                      [ "1" ]
                                      [ "2" ] ]

[<Test>]
let ``Ignores empty tables``() = 
    let html = """<html>
                    <body>
                        <table id="table">
                        </table>
                    </body>
                </html>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 0

[<Test>]
let ``Can parse tables with no headers``() = 
    let html = """<html>
                    <body>
                        <table id="table">
                            <tr><td>2</td></tr>
                            <tr><td>1</td></tr>
                            <tr><td>3</td></tr>
                        </table>
                    </body>
                </html>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"
    tables.[0].HasHeaders |> should equal (Some false)
    tables.[0].Rows |> should equal [ [ "2" ]
                                      [ "1" ]
                                      [ "3" ] ]

[<Test>]
let ``Can parse tables with no headers and only 2 rows``() = 
    let html = """<html>
                    <body>
                        <table id="table">
                            <tr><td>1</td></tr>
                            <tr><td>3</td></tr>
                        </table>
                    </body>
                </html>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"
    tables.[0].HasHeaders |> should equal (Some false)
    tables.[0].Rows |> should equal [ [ "1" ]
                                      [ "3" ] ]

[<Test>]
let ``Extracts table when title attribute is set``() = 
    let html = """<html>
                    <body>
                        <table title="table">
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"

[<Test>]
let ``Extracts table when name attribute is set``() = 
    let html = """<html>
                    <body>
                        <table name="table">
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table"

[<Test>]
let ``When mutiple identifying attributes are set the id attribute is selected``() = 
    let html = """<html>
                    <body>
                        <table id="table_id" name="table_name" title="table_title">
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table_id"

[<Test>]
let ``When mutiple identifying attributes are set but not the id attribute is then name attribute selected``() = 
    let html = """<html>
                    <body>
                        <table title="table_title" name="table_name">
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "table_name"

[<Test>]
let ``Extracts tables without an id title or name attribute``() = 
    let html = """<html>
                    <body>
                        <table>
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1

[<Test>]
let ``Extracts data and headers with thead and tbody``() = 
    let html = """<table id="savings_table">
                    <thead>
                      <tr>
                        <th>Month</th><th>Savings</th>
                      </tr>
                    </thead>
                    <tfoot>
                      <tr>
                        <td>Sum</td><td>$180</td>
                      </tr>
                    </tfoot>
                    <tbody>
                      <tr>
                        <td>January</td><td>$100</td>
                      </tr>
                      <tr>
                        <td>February</td><td>$80</td>
                      </tr>
                    </tbody>
                  </table>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "savings_table"
    tables.[0].HasHeaders |> should equal (Some true)
    tables.[0].Rows |> should equal [ [ "Month"; "Savings" ]
                                      [ "Sum"; "$180" ]
                                      [ "January"; "$100" ]
                                      [ "February"; "$80" ] ]

[<Test>]
let ``Extracts data and headers with unclosed tr th and td``() = 
    let html = """<table id="savings_table">
                    <thead>
                      <tr>
                        <th>Month
                        <th>Savings
                    </thead>
                    <tfoot>
                      <tr>
                        <td>Sum
                        <td>$180
                    </tfoot>
                    <tbody>
                      <tr>
                        <td>January
                        <td>$100
                      <tr>
                        <td>February
                        <td>$80
                    </tbody>
                  </table>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "savings_table"
    tables.[0].HasHeaders |> should equal (Some true)
    tables.[0].Rows |> should equal [ [ "Month"; "Savings" ]
                                      [ "Sum"; "$180" ]
                                      [ "January"; "$100" ]
                                      [ "February"; "$80" ] ]

[<Test>]
let ``Extracts data and headers with unclosed tr``() = 
    let html = """<table id="savings_table">
                    <thead>
                      <tr>
                        <th>Month</th>
                        <th>Savings</th>
                    </thead>
                    <tfoot>
                      <tr>
                        <td>Sum</td>
                        <td>$180</td>
                    </tfoot>
                    <tbody>
                      <tr>
                        <td>January</td>
                        <td>$100</td>
                      <tr>
                        <td>February</td>
                        <td>$80</td>
                    </tbody>
                  </table>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "savings_table"
    tables.[0].HasHeaders |> should equal (Some true)
    tables.[0].Rows |> should equal [ [ "Month"; "Savings" ]
                                      [ "Sum"; "$180" ]
                                      [ "January"; "$100" ]
                                      [ "February"; "$80" ] ]

[<Test>]
let ``Extracts data and headers with unclosed tr th and td without tbody``() = 
    let html = """<table id="savings_table">
                      <tr>
                        <th>Month
                        <th>Savings
                      <tr>
                        <td>Sum
                        <td>$180
                      <tr>
                        <td>January
                        <td>$100
                      <tr>
                        <td>February
                        <td>$80
                  </table>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "savings_table"
    tables.[0].HasHeaders |> should equal (Some true)
    tables.[0].Rows |> should equal [ [ "Month"; "Savings" ]
                                      [ "Sum"; "$180" ]
                                      [ "January"; "$100" ]
                                      [ "February"; "$80" ] ]

[<Test>]
let ``Extracts data and headers with unclosed tr without tbody``() = 
    let html = """<table id="savings_table">
                      <tr>
                        <th>Month</th>
                        <th>Savings</th>
                      <tr>
                        <td>Sum</td>
                        <td>$180</td>
                      <tr>
                        <td>January</td>
                        <td>$100</td>
                      <tr>
                        <td>February</td>
                        <td>$80</td>
                  </table>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "savings_table"
    tables.[0].HasHeaders |> should equal (Some true)
    tables.[0].Rows |> should equal [ [ "Month"; "Savings" ]
                                      [ "Sum"; "$180" ]
                                      [ "January"; "$100" ]
                                      [ "February"; "$80" ] ]


[<Test>]
let ``Extracts tables in malformed html``() = 
    let html = """<html>
                    <body> >>
                    <br><br>All Tables
                        <table>
                            <tr><th>Column 1</th></tr>
                            <tr><td>1</td></tr>
                        </table>
                    </body>
                </html>"""
    
    let tables = 
        html
        |> HtmlDocument.Parse
        |> getTables true
    tables.Length |> should equal 1
    tables.[0].Name |> should equal "Table1"
    tables.[0].HasHeaders |> should equal (Some true)
    tables.[0].Rows |> should equal [ [ "Column 1" ]
                                      [ "1" ] ]

[<Test>]
let ``Can handle html with doctype and xml namespaces``() = 
    let html = 
        """<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd"><html lang="en" xml:lang="en" xmlns="http://www.w3.org/1999/xhtml"><body>content</body></html>"""
    let htmlDoc = HtmlDocument.Parse html
    
    let expected = 
        HtmlDocument.New
            ("html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\"", 
             [ HtmlNode.NewElement
                   ("html", 
                    [ "lang", "en"
                      "xml:lang", "en"
                      "xmlns", "http://www.w3.org/1999/xhtml" ], 
                    [ HtmlNode.NewElement("body", [ HtmlNode.NewText "content" ]) ]) ])
    expected |> should equal htmlDoc

[<Test>]
let ``Can find header when nested in a div``() = 
    let tables = 
        HtmlDocument.Load "Data/wimbledon_wikipedia.html"
        |> getTables false
        |> List.map (fun t -> t.Name, t)
        |> Map.ofList
    Map.containsKey "Ranking points" tables |> should equal true
    Map.containsKey "Records" tables |> should equal true
    Map.containsKey "Current champions" tables |> should equal true

[<Test>]
let ``Can parse tables imdb chart``() = 
    let imdb = HtmlDocument.Load "Data/imdb_chart.htm"
    let tables = imdb |> getTables false
    tables.Length |> should equal 2
    tables.[0].Name |> should equal "Top 250"
    tables.[0].HasHeaders |> should equal (Some true)
    tables.[0].Rows.Length |> should equal 251

[<Test>]
let ``Can parse tables ebay cars``() = 
    let ebay = HtmlDocument.Load "Data/ebay_cars.htm"
    true |> should equal true

[<Test>]
let ``Does not crash when parsing us presidents``() = 
    let table = HtmlDocument.Load "Data/us_presidents_wikipedia.html" |> getTables false
    true |> should equal true

[<Test>]
let ``Can parse non-self-closing tags of elements that can't have children when followed by comments``() = 
    let html = """<hr class="hr4"><!--comment1--><!--comment2--><hr class="hr5" />"""
    let expected = """<hr class="hr4" /><!--comment1--><!--comment2--><hr class="hr5" />"""
    let result = (HtmlDocument.Parse html).ToString()
    result |> should equal expected

[<Test>]
let ``Ignores spurious closing tags``() = 
    let html = 
        """<li class="even"><a class="clr" href="/pj/ldbdetails/kEW6eAApVxWdogIXhoHAew%3D%3D/?board=dep"><span class="time em">21:36<br /><small>On time</small></span></span><span class="station">Brighton (East Sussex)</span><span class="platform"><small>Platform</small><br />17</span></a></li>"""
    let expected = """<li class="even">
  <a class="clr" href="/pj/ldbdetails/kEW6eAApVxWdogIXhoHAew%3D%3D/?board=dep">
    <span class="time em">
      21:36
<small>On time</small>
    </span><span class="station">Brighton (East Sussex)</span>
    <span class="platform">
      <small>Platform</small>
17
    </span>
  </a>
</li>"""
    let result = (HtmlDocument.Parse html).ToString().Replace("\r", null)
    result |> should equal (expected.Replace("\r", null))

[<Test>]
let ``Renders textarea closing tag``() =
    let html = """<textarea cols="40" rows="2"></textarea>"""
    let result = (HtmlDocument.Parse html).ToString()

    result |> should equal """<textarea cols="40" rows="2"></textarea>"""

[<Test>]
let ``Can handle CDATA blocks``() = 
    let cData = """
      Trying to provoke the CDATA parser with almost complete CDATA end tags
      ]
      >
      ]]
      ]>
      All done!
"""

    let html = """
    <!DOCTYPE html>
    <html xmlns="http://www.w3.org/1999/xhtml" xml:lang="en" lang="en" xmlns:fb="http://www.facebook.com/2008/fbml" xmlns:og="http://opengraphprotocol.org/schema/">
     <head>
        <script type="text/javascript">
            var google_tag_params = { PROP_intent: "RENT", PROP_use: "RES", PROP_loc: "London", PROP_minprice: "1500", PROP_maxprice: "1750", PROP_beds: "1" };
        </script>

        <p>
         <![CDATA[""" + cData + """]]>
        </p>
     </head>
     <body>
         <ul>
             <li>1</li>
             <li>2</li>
         <ul>
     </body>
    </html>
    """
    
    let doc = HtmlDocument.Parse html
    let result =
        doc
        |> HtmlDocument.descendantsNamed false [ "li" ]
        |> Seq.map (HtmlNode.innerText)
        |> Seq.toList
    result |> should equal [ "1"; "2"]

    let cDataResult =
        doc
        |> HtmlDocument.descendantsNamed false [ "p" ]
        |> Seq.collect HtmlNode.elements
        |> Seq.filter (function HtmlCData _ -> true | _ -> false)
        |> Seq.map (function HtmlCData s -> s | _ -> "")
        |> Seq.toList
    cDataResult |> should equal [ cData ]

[<Test>]
let ``Can handle large CDATA blocks``() =
    let bigString : string = new System.String ('a', 100000)
    let html = """
    <!DOCTYPE html>
    <html xmlns="http://www.w3.org/1999/xhtml" xml:lang="en" lang="en" xmlns:fb="http://www.facebook.com/2008/fbml" xmlns:og="http://opengraphprotocol.org/schema/">
     <head>
        <p>
         <![CDATA[""" + bigString + """]]>
        </p>
     </head>
    </html>
    """

    let sw = System.Diagnostics.Stopwatch ()
    sw.Start ()
    let doc = HtmlDocument.Parse html
    sw.Stop ()
    let elapsed = sw.ElapsedMilliseconds

    let result =
        doc
        |> HtmlDocument.descendantsNamed false [ "p" ]
        |> Seq.collect HtmlNode.elements
        |> Seq.filter (function HtmlCData _ -> true | _ -> false)
        |> Seq.map (function HtmlCData s -> s | _ -> "")
        |> Seq.toList
    result |> should equal [ bigString ]

    // Timing tests are difficult in unit tests but parsing 100,000 CDATA characters
    //  should take a lot less time than 1 second.
    //  The old implementation took a lot more than 1 second
    elapsed |> should lessThan 1000L

[<Test>]
let ``Can parse nested lists correctly when stops on recurse``() = 
    let html = """
        <ul>
            <li>
                <ul>
                    <li>1</li>
                    <li>2</li>
                </ul>
            </li>
            <li>3</li>
            <li>4</li>
       </ul>
    """
    
    let result = 
        (HtmlDocument.Parse html)
        |> HtmlDocument.descendantsNamed false [ "li" ]
        |> Seq.map (HtmlNode.innerText)
        |> Seq.toList
    result |> should equal [ "12"; "3"; "4" ]

[<Test>]
let ``Can parse nested lists correctly when continues on recurse``() = 
    let html = """
        <ul>
            <li>
                <ul>
                    <li>1</li>
                    <li>2</li>
                </ul>
            </li>
            <li>3</li>
            <li>4</li>
       </ul>
    """
    
    let result = 
        (HtmlDocument.Parse html)
        |> HtmlDocument.descendantsNamed true [ "li" ]
        |> Seq.map (HtmlNode.innerText)
        |> Seq.toList
    result |> should equal [ "12"; "1"; "2"; "3"; "4" ]

[<Test>]
let ``Can parse nested lists correctly when continues closing tags are missing``() = 
    let html = """
        <ul>
            <li>
                <ul><li>1<li>2</ul>
            <li>3
            <li>4
       </ul>
    """
    
    let result = 
        (HtmlDocument.Parse html)
        |> HtmlDocument.descendantsNamed true [ "li" ]
        |> Seq.map (HtmlNode.innerText)
        |> Seq.toList
    result |> should equal [ "12"; "1"; "2"; "3 "; "4 " ]


[<Test>]
let ``Can parse pre blocks``() = 
    let html = "<pre>\r\n        This code should be indented and\r\n        have line feeds in it</pre>"
    
    let result = 
        (HtmlDocument.Parse html)
        |> HtmlDocument.descendantsNamed true [ "pre" ]
        |> Seq.map (HtmlNode.innerText)
        |> Seq.toList
    result |> should equal [ "\r\n        This code should be indented and\r\n        have line feeds in it" ]

[<Test>]
let ``Can parse code blocks``() = 
    let html = "<code>\r\n        let f a b = a * b\r\n        f 5 6 |> should equal 30</code>"
    
    let result = 
        (HtmlDocument.Parse html)
        |> HtmlDocument.descendantsNamed true [ "code" ]
        |> Seq.map (HtmlNode.innerText)
        |> Seq.toList
    result |> should equal [ "\r\n        let f a b = a * b\r\n        f 5 6 |> should equal 30" ]

[<Test>]
let ``Can parse national rail mobile site correctly``() = 
    HtmlDocument.Load "Data/UKDepartures.html"
    |> HtmlDocument.descendantsNamed false [ "li" ]
    |> Seq.length
    |> should equal 68
    HtmlDocument.Load "Data/UKLiveProgress.html"
    |> HtmlDocument.descendantsNamed false [ "li" ]
    |> Seq.length
    |> should equal 15
    HtmlDocument.Load "Data/UKDepartures.html"
    |> HtmlDocument.descendantsNamed false [ "li"; "hr" ]
    |> Seq.length
    |> should equal 69
    HtmlDocument.Load "Data/UKLiveProgress.html"
    |> HtmlDocument.descendantsNamed false [ "li"; "hr" ]
    |> Seq.length
    |> should equal 17

[<Test>]
let ``Can parse old zoopla site correctly``() = 
    HtmlDocument.Load "Data/zoopla.html"
    |> HtmlDocument.descendants false (fun x -> HtmlNode.hasName "li" x && HtmlNode.hasAttribute "itemtype" "http://schema.org/Place" x)
    |> Seq.length 
    |> should equal 100

[<Test>]
let ``Can parse new zoopla site correctly``() = 
    HtmlDocument.Load "Data/zoopla2.html"
    |> HtmlDocument.descendants false (fun x -> HtmlNode.hasName "li" x && HtmlNode.hasAttribute "itemtype" "http://schema.org/Residence" x)
    |> Seq.length 
    |> should equal 10

[<Test>]
let ``Doesn't insert whitespace on attribute name when there are two whitespace characters before an attribute``() = 
    HtmlNode.Parse 
        "<a data-lecture-id=\"27\"\r\ndata-modal-iframe=\"https://class.coursera.org/mathematicalmethods-001/lecture/view?lecture_id=27\"></a>"
    |> List.head
    |> HtmlNode.attributeValue "data-modal-iframe"
    |> should equal "https://class.coursera.org/mathematicalmethods-001/lecture/view?lecture_id=27"

[<Test>]
let ``Includes DOCTYPE when transforming HtmlDocument to string``() = 
    let html = """<!DOCTYPE html><html lang="en"><head><title>Test</title></head><body>I Just Love F#</body></html>"""
    let doc = HtmlDocument.Parse html
    let typ = doc |> HtmlDocument.docType
    let newDoc = HtmlDocument.New(typ, doc.Elements())
    newDoc 
    |> string
    |> should startWith "<!DOCTYPE html>"

[<Test>]
let ``Can create new CData element``() = 
    HtmlNode.NewCData("some element content")
    |> string
    |> should equal "<![CDATA[some element content]]>"