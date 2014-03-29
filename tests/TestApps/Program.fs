open Test

[<EntryPoint>]
let main _ = 
    try
        if System.Reflection.Assembly.GetExecutingAssembly().GetName().Name = "ConsoleApp_4310_PCL7_3310" then
            printfn "Test disabled until #521 is fixed"
        else
            getTestData() |> Async.Ignore |> Async.RunSynchronously
        0
    with e ->
        eprintfn "%s" e.Message
        -1
