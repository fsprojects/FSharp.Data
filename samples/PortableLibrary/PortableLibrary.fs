module PortableLibrary

open System.IO
open System.Net
open Microsoft.FSharp.Control
open FSharp.Data

type Stocks = CsvProvider<"../docs/MSFT.csv">
type Simple = JsonProvider<""" { "name":"John", "age":94 } """>
type WorldBank = JsonProvider<"../docs/WorldBank.json">
type Authors = XmlProvider<"../docs/Writers.xml">
type Detailed = XmlProvider<"""<author><name full="true">Karl Popper</name></author>""">

let getData() = seq {

    //=== json ===
    
    let simple = Simple.Parse(""" { "name":"Tomas", "age":4 } """)
    yield simple.Age.ToString()
    yield simple.Name

    //=== xml ===

    let msft = Stocks.Load("http://ichart.finance.yahoo.com/table.csv?s=MSFT")
    for row in msft.Data do
      yield sprintf "HLOC: (%A, %A, %A, %A)" row.High row.Low row.Open row.Close

    //=== xml ===
    let info = Detailed.Parse("""<author><name full="false">Thomas Kuhn</name></author>""")
    yield sprintf "%s (full=%b)" info.Name.Value info.Name.Full
}

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
