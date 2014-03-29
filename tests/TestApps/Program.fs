open Test

[<EntryPoint>]
let main _ = 
    try
        getTestData() |> Async.Ignore |> Async.RunSynchronously
        0
    with e ->
        eprintfn "%s" e.Message
        -1
