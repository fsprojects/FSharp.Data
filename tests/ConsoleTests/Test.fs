module Test

open FSharp.Data

type Stocks = CsvProvider<"http://ichart.finance.yahoo.com/table.csv?s=MSFT">

type RSS = XmlProvider<"http://tomasp.net/blog/rss.aspx">

type GitHub = JsonProvider<"https://api.github.com/repos/fsharp/FSharp.Data/issues">

let getStocks() =
   [ for row in Stocks.GetSample().Rows -> sprintf "HLOC: (%A, %A, %A, %A)" row.High row.Low row.Open row.Close ] |> String.concat "\n"

let getRss() = 
   [ for item in RSS.GetSample().Channel.Items -> item.Title ] |> String.concat "\n"

let getIssues() =
    GitHub.GetSamples()
    |> Seq.filter (fun issue -> issue.State = "open")
    |> Seq.sortBy (fun issue -> System.DateTime.Now - issue.UpdatedAt)
    |> Seq.truncate 5
    |> Seq.map (fun issue -> issue.Title)
    |> String.concat "\n"
