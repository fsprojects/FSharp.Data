module PortableLibrary

open FSharp.Data
open PortableLibrarySampleData

type SubItem = 
    { Title : string
      SubTitle : string
      Description : string
      Content : string }

type Item =
    { Title : string
      SubTitle : string
      Description : string
      SubItems : SubItem seq }

type SimpleJson = JsonProvider<""" { "name":"John", "age":94 } """>
type WorldBankJson = JsonProvider<"../docs/WorldBank.json">

let getJsonData() = seq {

    let simple = SimpleJson.Parse(""" { "name":"Tomas", "age":4 } """)
    yield { Title = "Simple Json"
            SubTitle = simple.Name
            Description = simple.Age.ToString()
            SubItems = [] }

    let doc = WorldBankJson.Parse(worldBankJson)
    let info = doc.Record
    yield { Title = "World Bank JSON"
            SubTitle = sprintf "Showing page %d of %d. Total records %d" info.Page info.Pages info.Total
            Description = ""
            SubItems = seq { 
                for record in doc.Array do
                    if record.Value <> null then 
                        yield { Title = record.Date
                                SubTitle = record.Value
                                Description = ""
                                Content = "" } } }
}

type SimpleXml = XmlProvider<"""<author><name full="true">Karl Popper</name></author>""">
type AuthorsXml = XmlProvider<"../docs/Writers.xml">

let getXmlData() = seq {

    let info = SimpleXml.Parse("""<author><name full="false">Thomas Kuhn</name></author>""")
    yield { Title = "Simple XML"
            SubTitle = info.Name.Value
            Description = sprintf "(full=%b)" info.Name.Full
            SubItems = [] }

    let topic = AuthorsXml.Parse(authorsXml)
    yield { Title = "Authors XML"
            SubTitle = topic.Topic
            Description = ""
            SubItems = seq { 
                for author in topic.GetAuthors() do
                    yield { Title = author.Name 
                            SubTitle = match author.Born with
                                        | Some born -> born.ToString()
                                        | None -> ""
                            Description = ""
                            Content = "" } } }
}

type StocksCsv = CsvProvider<"../docs/MSFT.csv">

let getCsvData forSilverlight = seq {

    let msft = 
        if forSilverlight then
            StocksCsv.Parse(msftCsv)
        else
            StocksCsv.Load("http://ichart.finance.yahoo.com/table.csv?s=MSFT")

    yield { Title = "MSFT Stock CSV"
            SubTitle = ""
            Description = ""
            SubItems = seq { 
                for row in msft.Data |> Seq.truncate 10 do
                    yield { Title = sprintf "HLOC: (%A, %A, %A, %A)" row.High row.Low row.Open row.Close
                            SubTitle = ""
                            Description = ""
                            Content = "" } } }
}

let getWorldBankData() = seq {

    let wb = WorldBankData.GetDataContext()

    yield { Title = "World Bank Countries"
            SubTitle = ""
            Description = ""
            SubItems = seq { 
                for c in wb.Countries |> Seq.truncate 10 do
                    yield { Title = c.Name
                            SubTitle = sprintf "GDP = %s" (c.Indicators.``GDP (current US$)`` |> Seq.map string |> String.concat ",")
                            Description = ""
                            Content = "" } } }
}

let getData forSilverlight = seq {

    yield! getJsonData()
    
    if not forSilverlight then
        yield! getXmlData()

    yield! getCsvData forSilverlight

    if not forSilverlight then
        yield! getWorldBankData()
}
