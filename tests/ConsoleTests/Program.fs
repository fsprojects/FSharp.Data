open Test

[<EntryPoint>]
let main _ = 
    try
        getStocks() |> ignore
        getRss() |> ignore
        getIssues() |> ignore
        0
    with e ->
        eprintfn "%s" e.Message
        -1
