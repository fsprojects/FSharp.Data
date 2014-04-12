open Test

[<EntryPoint>]
let main _ = 
    getTestData() |> Async.Ignore |> Async.RunSynchronously
    0