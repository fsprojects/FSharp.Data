module PortableLibrary

open FSharp.Data

type Simple = JsonProvider<""" { "name":"John", "age":94 } """>

let getJsonData() = seq {
    let simple = Simple.Parse(""" { "name":"Tomas", "age":4 } """)
    yield simple.Age.ToString()
    yield simple.Name
}

type Stocks = CsvProvider<"../docs/MSFT.csv">
 
let getCsvData forSilverlight = seq {

    let msft = 
        if forSilverlight then
            let data = """Date,Open,High,Low,Close,Volume,Adj Close
2012-01-27,29.45,29.53,29.17,29.23,44187700,29.23
2012-01-26,29.61,29.70,29.40,29.50,49102800,29.50"""
            Stocks.Parse(data)
        else
            Stocks.Load("http://ichart.finance.yahoo.com/table.csv?s=MSFT")
    for row in msft.Data |> Seq.truncate 10 do
        yield sprintf "HLOC: (%A, %A, %A, %A)" row.High row.Low row.Open row.Close
}

type Detailed = XmlProvider<"""<author><name full="true">Karl Popper</name></author>""">

let getXmlData() = seq {
    let info = Detailed.Parse("""<author><name full="false">Thomas Kuhn</name></author>""")
    yield sprintf "%s (full=%b)" info.Name.Value info.Name.Full
}

let getData forSilverlight = seq {

    yield! getJsonData()
    if not forSilverlight then        
        yield! getXmlData()
    yield! getCsvData forSilverlight
}

type WorldBank = JsonProvider<"../docs/WorldBank.json">

//let getJsonData (worldBankJsonStream:Stream) = seq {
//
//    let doc = WorldBank.Load(worldBankJsonStream)
//
//    let info = doc.Record
//    yield sprintf "Showing page %d of %d. Total records %d" info.Page info.Pages info.Total
//
//    // Print all data points
//    for record in doc.Array do
//      if record.Value <> null then
//        yield sprintf "%d: %f" (int record.Date) (float record.Value)
//}
//

type Authors = XmlProvider<"../docs/Writers.xml">

//let getXmlData (writerXmlStream:Stream) = seq {
//    let topic = Authors.Load(writerXmlStream)
//
//    yield topic.Topic
//    for author in topic.GetAuthors() do
//      yield sprintf " - %s" author.Name 
//      match author.Born with
//      | Some born -> yield sprintf " (%d)" born
//      | None -> ()
//}
