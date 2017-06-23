module Test

open FSharp.Data
open FSharp.Data.HttpRequestHeaders

type Stocks = CsvProvider<"http://www.google.com/finance/historical?q=MSFT&output=csv">

type RSS = XmlProvider<"http://tomasp.net/blog/rss.aspx">

type GitHub = JsonProvider<"../../../docs/content/data/GitHub.json">

let getTestData() = async {
    do! Http.AsyncRequest("https://accounts.coursera.org/api/v1/login",
                          headers = [ Origin "https://accounts.coursera.org"
                                      "X-CSRFToken", "something"
                                      Referer "https://accounts.coursera.org/signin" ],
                          body = FormValues [ "email", "a"; "password", "b" ],
                          cookies = [ "csrftoken", "something" ],
                          silentHttpErrors = true)
        |> Async.Ignore
    do! Http.AsyncRequest("http://m.nationalrail.co.uk/pj/ldbboard/dep/vic",
                          headers = [ UserAgent "Mozilla/5.0 (compatible; MSIE 10.0; Windows Phone 8.0; Trident/6.0; IEMobile/10.0; ARM; Touch; NOKIA; Lumia 920)" ])
        |> Async.Ignore
    let! stocks = Stocks.AsyncGetSample()
    let! rss = RSS.AsyncGetSample()
#if GITHUB
    let! issues = async {
        try
            // doesn't work on Win8 (#548)
            return! GitHub.AsyncGetSamples()
        with _ ->
            return [| |]
    }
#endif
    let! indicator = WorldBankDataProvider<Asynchronous=true>.GetDataContext().Countries.``United Kingdom``.Indicators.``Gross enrolment ratio, tertiary, both sexes (%)``
    let result =
        [
          [ for row in Seq.truncate 5 stocks.Rows -> sprintf "HLOC: (%A, %A, %A, %A)" row.High row.Low row.Open row.Close ]
          [ for item in Seq.truncate 5 rss.Channel.Items -> item.Title ]
#if GITHUB
          [ for issue in Seq.truncate 5 issues -> sprintf "#%d %s" issue.Number issue.Title ]
#endif
          [ for year, value in Seq.truncate 5 indicator -> sprintf "%d %f" year value ]
        ]
        |> List.collect id
        |> String.concat "\n"
    return result
}

let getTestDataAsTask() =
    getTestData() |> Async.StartAsTask


// A small test for single-column CSV text with only one line
type OneLineOneColumnCsvHeader = CsvProvider<"COLUMN", HasHeaders = true>
let sample1 = OneLineOneColumnCsvHeader.GetSample()
let getLine1() : string = (sample1.Rows |> Seq.head).COLUMN

// A small test for single-column CSV text with two lines
type TwoLineOneColumnCsvHeader = CsvProvider<"COLUMN\n10", HasHeaders = true>
let sample2 = TwoLineOneColumnCsvHeader.GetSample()
let getLine2() : int = (sample2.Rows |> Seq.head).COLUMN

// A whole bunch of uses of type providers to use for stress testing compilation performance and editing reactivity
module Stress =
    let [<Literal>] simpleCsv = """
      Column1,Column2,Column3
      TRUE,no,3
      "yes", "false", 1.92 """

    type SimpleCsv = CsvProvider<simpleCsv>
    type CsvWithSampleWhichIsAValidFilename = CsvProvider<Sample="1;2;3", HasHeaders=false, Separators=";">

    let [<Literal>] percentageCsv = """
        Column1,Column2,Column3
        TRUE,no,3
        "yes", "false", 1.92%"""

    let [<Literal>] simpleWithStrCsv = """
        Column1,ColumnB,Column3
        TRUE,abc,3
        "yes","Freddy", 1.92 """

    let [<Literal>] currency = """
        Column1,Column2,Column3
        £1, $2, £3
        £4, $5, £6"""

    module CsvFiles1 =
        module CSV1 =
            type UTF8 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
            type CP932 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">
            type PercentageCsv = CsvProvider<percentageCsv>
            let percentageCsvSample = PercentageCsv.GetSample()
            type Currency = CsvProvider<currency>
            let currencySample = Currency.GetSample()
            type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>
            type CsvUom = CsvProvider<"../../FSharp.Data.Tests/Data/SmallTest.csv">

        module CSV2 =
            type UTF8 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
            type CP932 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">
            type PercentageCsv = CsvProvider<percentageCsv>
            let percentageCsvSample = PercentageCsv.GetSample()
            type Currency = CsvProvider<currency>
            let currencySample = Currency.GetSample()
            type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>
            type CsvUom = CsvProvider<"../../FSharp.Data.Tests/Data/SmallTest.csv">

        module CSV3 =
            type UTF8 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
            type CP932 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">
            type PercentageCsv = CsvProvider<percentageCsv>
            let percentageCsvSample = PercentageCsv.GetSample()
            type Currency = CsvProvider<currency>
            let currencySample = Currency.GetSample()
            type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>
            type CsvUom = CsvProvider<"../../FSharp.Data.Tests/Data/SmallTest.csv">

    module CsvFiles2 =
        module CSV1 =
            type UTF8 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
            type CP932 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">
            type PercentageCsv = CsvProvider<percentageCsv>
            let percentageCsvSample = PercentageCsv.GetSample()
            type Currency = CsvProvider<currency>
            let currencySample = Currency.GetSample()
            type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>
            type CsvUom = CsvProvider<"../../FSharp.Data.Tests/Data/SmallTest.csv">

        module CSV2 =
            type UTF8 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
            type CP932 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">
            type PercentageCsv = CsvProvider<percentageCsv>
            let percentageCsvSample = PercentageCsv.GetSample()
            type Currency = CsvProvider<currency>
            let currencySample = Currency.GetSample()
            type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>
            type CsvUom = CsvProvider<"../../FSharp.Data.Tests/Data/SmallTest.csv">

        module CSV3 =
            type UTF8 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
            type CP932 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">
            type PercentageCsv = CsvProvider<percentageCsv>
            let percentageCsvSample = PercentageCsv.GetSample()
            type Currency = CsvProvider<currency>
            let currencySample = Currency.GetSample()
            type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>
            type CsvUom = CsvProvider<"../../FSharp.Data.Tests/Data/SmallTest.csv">

    module CsvFiles3 =
        module CSV1 =
            type UTF8 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
            type CP932 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">
            type PercentageCsv = CsvProvider<percentageCsv>
            let percentageCsvSample = PercentageCsv.GetSample()
            type Currency = CsvProvider<currency>
            let currencySample = Currency.GetSample()
            type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>
            type CsvUom = CsvProvider<"../../FSharp.Data.Tests/Data/SmallTest.csv">

        module CSV2 =
            type UTF8 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
            type CP932 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">
            type PercentageCsv = CsvProvider<percentageCsv>
            let percentageCsvSample = PercentageCsv.GetSample()
            type Currency = CsvProvider<currency>
            let currencySample = Currency.GetSample()
            type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>
            type CsvUom = CsvProvider<"../../FSharp.Data.Tests/Data/SmallTest.csv">

        module CSV3 =
            type UTF8 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
            type CP932 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">
            type PercentageCsv = CsvProvider<percentageCsv>
            let percentageCsvSample = PercentageCsv.GetSample()
            type Currency = CsvProvider<currency>
            let currencySample = Currency.GetSample()
            type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>
            type CsvUom = CsvProvider<"../../FSharp.Data.Tests/Data/SmallTest.csv">

    module CsvFiles4 =
        module CSV1 =
            type UTF8 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
            type CP932 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">
            type PercentageCsv = CsvProvider<percentageCsv>
            let percentageCsvSample = PercentageCsv.GetSample()
            type Currency = CsvProvider<currency>
            let currencySample = Currency.GetSample()
            type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>
            type CsvUom = CsvProvider<"../../FSharp.Data.Tests/Data/SmallTest.csv">

        module CSV2 =
            type UTF8 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
            type CP932 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">
            type PercentageCsv = CsvProvider<percentageCsv>
            let percentageCsvSample = PercentageCsv.GetSample()
            type Currency = CsvProvider<currency>
            let currencySample = Currency.GetSample()
            type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>
            type CsvUom = CsvProvider<"../../FSharp.Data.Tests/Data/SmallTest.csv">

        module CSV3 =
            type UTF8 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", HasHeaders = true, MissingValues = "NaN (非数値)">
            type CP932 = CsvProvider<"../../FSharp.Data.Tests/Data/cp932.csv", Culture = "ja-JP", Encoding = "932", HasHeaders = true, MissingValues = "NaN (非数値)">
            type PercentageCsv = CsvProvider<percentageCsv>
            let percentageCsvSample = PercentageCsv.GetSample()
            type Currency = CsvProvider<currency>
            let currencySample = Currency.GetSample()
            type SimpleWithStrCsv = CsvProvider<simpleWithStrCsv>
            type CsvUom = CsvProvider<"../../FSharp.Data.Tests/Data/SmallTest.csv">

    module XmlText1 =
        module Person1 =
            type PersonXml = XmlProvider<"""<authors><author name="Ludwig" surname="Wittgenstein" age="29" /></authors>""">
            let newXml = """<authors><author name="Jane" surname="Doe" age="23" /></authors>"""
            let newXml2 = """<authors><author name="Jim" surname="Smith" age="24" /></authors>"""

            let firstPerson = PersonXml.Parse(newXml).Author
            let nextPerson = PersonXml.Parse(newXml2).Author

        module Person2 =
            type PersonXml = XmlProvider<"""<authors><author name="Ludwig" surname="Wittgenstein" age="30" /></authors>""">
            let newXml = """<authors><author name="Jane" surname="Doe" age="23" /></authors>"""
            let newXml2 = """<authors><author name="Jim" surname="Smith" age="24" /></authors>"""

            let firstPerson = PersonXml.Parse(newXml).Author
            let nextPerson = PersonXml.Parse(newXml2).Author

        module Person3 =
            type PersonXml = XmlProvider<"""<authors><author name="Ludwig" surname="Wittgenstein" age="31" /></authors>""">
            let newXml = """<authors><author name="Jane" surname="Doe" age="23" /></authors>"""
            let newXml2 = """<authors><author name="Jim" surname="Smith" age="24" /></authors>"""

            let firstPerson = PersonXml.Parse(newXml).Author
            let nextPerson = PersonXml.Parse(newXml2).Author

        module Person4 =
            type PersonXml = XmlProvider<"""<authors><author name="Ludwig" surname="Wittgenstein" age="32" /></authors>""">
            let newXml = """<authors><author name="Jane" surname="Doe" age="23" /></authors>"""
            let newXml2 = """<authors><author name="Jim" surname="Smith" age="24" /></authors>"""

            let firstPerson = PersonXml.Parse(newXml).Author
            let nextPerson = PersonXml.Parse(newXml2).Author

        module Person5 =
            type PersonXml = XmlProvider<"""<authors><author name="Ludwig" surname="Wittgenstein" age="33" /></authors>""">
            let newXml = """<authors><author name="Jane" surname="Doe" age="23" /></authors>"""
            let newXml2 = """<authors><author name="Jim" surname="Smith" age="24" /></authors>"""

            let firstPerson = PersonXml.Parse(newXml).Author
            let nextPerson = PersonXml.Parse(newXml2).Author

        module Person6 =
            type PersonXml = XmlProvider<"""<authors><author name="Ludwig" surname="Wittgenstein" age="34" /></authors>""">
            let newXml = """<authors><author name="Jane" surname="Doe" age="23" /></authors>"""
            let newXml2 = """<authors><author name="Jim" surname="Smith" age="24" /></authors>"""

            let firstPerson = PersonXml.Parse(newXml).Author
            let nextPerson = PersonXml.Parse(newXml2).Author

        module Person7 =
            type PersonXml = XmlProvider<"""<authors><author name="Ludwig" surname="Wittgenstein" age="35" /></authors>""">
            let newXml = """<authors><author name="Jane" surname="Doe" age="23" /></authors>"""
            let newXml2 = """<authors><author name="Jim" surname="Smith" age="24" /></authors>"""

            let firstPerson = PersonXml.Parse(newXml).Author
            let nextPerson = PersonXml.Parse(newXml2).Author

        module Person8 =
            type PersonXml = XmlProvider<"""<authors><author name="Ludwig" surname="Wittgenstein" age="36" /></authors>""">
            let newXml = """<authors><author name="Jane" surname="Doe" age="23" /></authors>"""
            let newXml2 = """<authors><author name="Jim" surname="Smith" age="24" /></authors>"""

            let firstPerson = PersonXml.Parse(newXml).Author
            let nextPerson = PersonXml.Parse(newXml2).Author

    module XmlGroup1 =
        module XmlFiles1 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles2 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles3 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles4 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles5 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles6 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles7 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles8 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles9 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles10 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">


    module XmlGroup2 =
        module XmlFiles1 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles2 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles3 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles4 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles5 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles6 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles7 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles8 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles9 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles10 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

    module XmlGroup3 =
        module XmlFiles1 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles2 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles3 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles4 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles5 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles6 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles7 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles8 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles9 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles10 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

    module XmlGroup4 =
        module XmlFiles1 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles2 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles3 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles4 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles5 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles6 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles7 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles8 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles9 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">

        module XmlFiles10 =
            type AtomSearch = XmlProvider<"../../FSharp.Data.Tests/Data/Search.Atom.xml", SampleIsList=true>
            type philosophyType = XmlProvider<"../../FSharp.Data.Tests/Data/Philosophy.xml">


