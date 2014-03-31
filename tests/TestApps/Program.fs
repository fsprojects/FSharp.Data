open Test

[<EntryPoint>]
let main _ = 
    if System.Reflection.Assembly.GetExecutingAssembly().GetName().Name = "ConsoleApp_4310_PCL7_3310" then
        printfn "Test disabled until #521 is fixed"
    else
        getTestData() |> Async.Ignore |> Async.RunSynchronously
    0
