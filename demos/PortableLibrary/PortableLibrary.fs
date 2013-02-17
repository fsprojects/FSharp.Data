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

type StocksCsv = CsvProvider<"../../samples/docs/MSFT.csv">

let getCsvData() = seq {

    let msft = StocksCsv.Load("http://ichart.finance.yahoo.com/table.csv?s=MSFT")

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

type SimpleJson = JsonProvider<""" { "name":"John", "age":94 } """>
type WorldBankJson = JsonProvider<"../../samples/docs/WorldBank.json">

let getJsonData() = seq {
    new System.Xml.Linq.XElement(System.Xml.Linq.XName.Get "aaa") |> ignore
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
                    match record.Value.Number with
                    | Some value -> yield { Title = record.Date.ToString()
                                            SubTitle = value.ToString()
                                            Description = ""
                                            Content = "" }
                    | None -> () } }
}

type SimpleXml = XmlProvider<"""<author><name full="true">Karl Popper</name></author>""">
type AuthorsXml = XmlProvider<"../../samples/docs/Writers.xml">

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

let getFreebaseData() = seq {

    let fb = FreebaseData.GetDataContext()
    yield { Title = "Asteroids"
            SubTitle = ""
            Description = ""
            SubItems = seq { 
                for a in fb.``Science and Technology``.Astronomy.Asteroids |> Seq.truncate 10 do
                    yield { Title = a.Name
                            SubTitle = ""
                            Description = a.Description |> String.concat "\n"
                            Content = a.Blurb |> String.concat "\n" } } }
}

let getData() = seq {

    yield! getCsvData()
    yield! getJsonData()
#if SILVERLIGHT
#else
    yield! getXmlData()
#endif
    yield! getWorldBankData()
    yield! getFreebaseData()
}

let populateDataAsync (add:System.Action<_>) = 
    let synchronizationContext = System.Threading.SynchronizationContext.Current
    async { 
        for item in getData() do
            synchronizationContext.Post((fun _ -> add.Invoke item), null) |> ignore
    }
    |> Async.Start
