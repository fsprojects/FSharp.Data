module PortableLibrary

open FSharp.Data

type SimpleJson = JsonProvider<""" { "name":"John", "age":94 } """>
type StocksCsv = CsvProvider<"../docs/MSFT.csv">
type DetailedXml = XmlProvider<"""<author><name full="true">Karl Popper</name></author>""">
type WorldBankJson = JsonProvider<"../docs/WorldBank.json">
type AuthorsXml = XmlProvider<"../docs/Writers.xml">


let getJsonData() = seq {

    let simple = SimpleJson.Parse(""" { "name":"Tomas", "age":4 } """)
    yield simple.Age.ToString()
    yield simple.Name

//    let doc = WorldBankJson.Load(worldBankJsonStream)
//
//    let info = doc.Record
//    yield sprintf "Showing page %d of %d. Total records %d" info.Page info.Pages info.Total
//
//     Print all data points
//    for record in doc.Array do
//      if record.Value <> null then
//        yield sprintf "%d: %f" (int record.Date) (float record.Value)
}

let getXmlData() = seq {

    let info = DetailedXml.Parse("""<author><name full="false">Thomas Kuhn</name></author>""")
    yield sprintf "%s (full=%b)" info.Name.Value info.Name.Full

//    let topic = AuthorsXml.Load(writerXmlStream)
//
//    yield topic.Topic
//    for author in topic.GetAuthors() do
//      yield sprintf " - %s" author.Name 
//      match author.Born with
//      | Some born -> yield sprintf " (%d)" born
//      | None -> ()
}

let getCsvData forSilverlight = seq {

    let msft = 
        if forSilverlight then
            let data = """Date,Open,High,Low,Close,Volume,Adj Close
2012-01-27,29.45,29.53,29.17,29.23,44187700,29.23
2012-01-26,29.61,29.70,29.40,29.50,49102800,29.50"""
            StocksCsv.Parse(data)
        else
            StocksCsv.Load("http://ichart.finance.yahoo.com/table.csv?s=MSFT")
    for row in msft.Data |> Seq.truncate 10 do
        yield sprintf "HLOC: (%A, %A, %A, %A)" row.High row.Low row.Open row.Close
}

let getWorldBankData() = seq {
    let wb = WorldBankData.GetDataContext()
    for c in wb.Countries |> Seq.truncate 10 do
        yield c.Name
        yield sprintf "GDP = %s" (c.Indicators.``GDP (current US$)`` |> Seq.map string |> String.concat ",")
}

let getData forSilverlight = seq {

    yield! getJsonData()
    
    if not forSilverlight then
        yield! getXmlData()

    yield! getCsvData forSilverlight

    //if not forSilverlight then
    yield! getWorldBankData()
}
