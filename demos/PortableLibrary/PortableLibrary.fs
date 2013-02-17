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

    let msft = StocksCsv.Load "http://ichart.finance.yahoo.com/table.csv?s=MSFT"

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

type GitHubJson = JsonProvider<"../../tests/FSharp.Data.Tests/data/GitHub.json">

let getJsonData() = seq {

    let issues = GitHubJson.Parse gitHubJson
    
    yield { Title = "GitHub Issues for FSharp.Data"
            SubTitle = ""
            Description = ""
            SubItems = seq { 
                for issue in issues |> Seq.truncate 10 do
                    yield { Title = issue.Title
                            SubTitle = issue.Number.ToString()
                            Description = issue.User.Login
                            Content = issue.Body } } }
}

type AuthorsXml = XmlProvider<"../../samples/docs/Writers.xml">

let getXmlData() = seq {

    let topic = AuthorsXml.Parse authorsXml

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

let getApiaryData() = seq {

    let db = new ApiaryProvider<"themoviedb">("http://api.themoviedb.org")
    db.AddQueryParam("api_key", "6ce0ef5b176501f8c07c634dfa933cff")

    let movies = db.Search.Movie(query=["query","batman"]).Results

    yield { Title = "Batman Movies"
            SubTitle = ""
            Description = ""
            SubItems = seq { 
                for movie in movies |> Seq.truncate 10 do
                    yield { Title = movie.Title
                            SubTitle = ""
                            Description = ""
                            Content = movie.OriginalTitle } } }
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
    yield! getApiaryData()
}

let populateDataAsync (add:System.Action<_>) = 
    let synchronizationContext = System.Threading.SynchronizationContext.Current
    async { 
        for item in getData() do
            synchronizationContext.Post((fun _ -> add.Invoke item), null) |> ignore
    }
    |> Async.Start
