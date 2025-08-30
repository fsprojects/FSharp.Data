#I "src/FSharp.Data.Csv.Core/bin/Release/netstandard2.0"
#I "src/FSharp.Data.Runtime.Utilities/bin/Release/netstandard2.0"
#r "FSharp.Data.Runtime.Utilities.dll"
#r "FSharp.Data.Csv.Core.dll"

open System
open System.IO
open System.Diagnostics
open FSharp.Data

// Create test data
let simpleData = "Name,Age,City\nJohn,30,NYC\nJane,25,LA\nBob,35,Chicago"

let mediumData = 
    let header = "Name,Age,City,Salary,Department"
    let rows = 
        [1..1000]
        |> List.map (fun i -> sprintf "Employee%d,%d,City%d,%.2f,Dept%d" 
                                i (20 + i % 50) (i % 10) (30000.0 + float i * 10.0) (i % 5))
    String.concat "\n" (header :: rows)

let largeData =
    let header = "ID,Name,Age,City,Salary,Department"
    let rows =
        [1..10000]
        |> List.map (fun i -> 
            sprintf "%d,Employee%d,%d,City%d,%.2f,Department%d"
                i i (20 + i % 50) (i % 100) (30000.0 + float i * 10.0) (i % 10))
    String.concat "\n" (header :: rows)

// Read Titanic data
let titanic = File.ReadAllText("docs/data/Titanic.csv")

// Performance test function
let timeTest name iterations testFunc =
    // Warmup
    testFunc() |> ignore
    testFunc() |> ignore
    
    let sw = Stopwatch.StartNew()
    for _ in 1..iterations do
        testFunc() |> ignore
    sw.Stop()
    
    let avgMs = sw.ElapsedMilliseconds / int64 iterations
    printfn "%s: %d iterations, avg %.2f ms per iteration" name iterations (float avgMs)
    avgMs

// Test functions
let testSimple () = 
    use reader = new StringReader(simpleData)
    let csv = CsvFile.Load(reader)
    csv.Rows |> Seq.length

let testMedium () = 
    use reader = new StringReader(mediumData)
    let csv = CsvFile.Load(reader)
    csv.Rows |> Seq.length

let testLarge () = 
    use reader = new StringReader(largeData)
    let csv = CsvFile.Load(reader)
    csv.Rows |> Seq.length

let testTitanic () = 
    use reader = new StringReader(titanic)
    let csv = CsvFile.Load(reader)
    csv.Rows |> Seq.length

let testDataAccess () = 
    use reader = new StringReader(mediumData)
    let csv = CsvFile.Load(reader)
    csv.Rows 
    |> Seq.sumBy (fun row -> 
        let age = row.["Age"] |> int
        let salary = row.["Salary"] |> float
        age + int salary)

printfn "CSV Performance Baseline Test"
printfn "============================="

timeTest "Simple CSV (4 rows)" 10000 testSimple |> ignore
timeTest "Medium CSV (1000 rows)" 100 testMedium |> ignore  
timeTest "Large CSV (10000 rows)" 10 testLarge |> ignore
timeTest "Titanic CSV" 100 testTitanic |> ignore
timeTest "Data Access (Medium)" 100 testDataAccess |> ignore

printfn ""
printfn "Test completed!"