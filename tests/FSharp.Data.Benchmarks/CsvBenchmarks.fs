namespace FSharp.Data.Benchmarks

open BenchmarkDotNet.Attributes
open System.IO
open FSharp.Data

[<MemoryDiagnoser>]
[<SimpleJob>]
type CsvParsingBenchmarks() =

    let simpleData = "Name,Age,City\nJohn,30,NYC\nJane,25,LA\nBob,35,Chicago"
    
    let mediumData = 
        let header = "Name,Age,City,Salary,Department,Manager,Phone,Email"
        let rows = 
            [1..1000]
            |> List.map (fun i -> sprintf "Employee%d,%d,City%d,%.2f,Dept%d,Manager%d,555-000-%04d,emp%d@company.com" 
                                    i (20 + i % 50) (i % 10) (30000.0 + float i * 10.0) (i % 5) (i % 20) i i)
        String.concat "\n" (header :: rows)

    let largeData =
        let header = "ID,Name,Age,City,Salary,Department,Manager,Phone,Email,Address,ZipCode,State"
        let rows =
            [1..10000]
            |> List.map (fun i -> 
                sprintf "%d,Employee%d,%d,City%d,%.2f,Department%d,Manager%d,555-000-%04d,emp%d@company.com,123 Main St Apt %d,%05d,State%d"
                    i i (20 + i % 50) (i % 100) (30000.0 + float i * 10.0) (i % 10) (i % 50) i i i (10000 + i % 90000) (i % 50))
        String.concat "\n" (header :: rows)

    let quotedData =
        let header = "Name,Description,Notes"
        let rows =
            [1..1000]
            |> List.map (fun i -> 
                sprintf "\"Product %d\",\"This is a \"\"quoted\"\" description with, commas and\nnewlines\",\"Notes for item %d with \"\"quotes\"\" and commas,\"" i i)
        String.concat "\n" (header :: rows)

    let titanic =
        let dataPath = Path.Combine(__SOURCE_DIRECTORY__, "../FSharp.Data.Tests/Data")
        File.ReadAllText(Path.Combine(dataPath, "Titanic.csv"))
        
    [<Benchmark>]
    member _.ParseSimpleCsv() =
        use reader = new StringReader(simpleData)
        let csv = CsvFile.Load(reader)
        csv.Rows |> Seq.length

    [<Benchmark>]
    member _.ParseMediumCsv() =
        use reader = new StringReader(mediumData)  
        let csv = CsvFile.Load(reader)
        csv.Rows |> Seq.length

    [<Benchmark>]
    member _.ParseLargeCsv() =
        use reader = new StringReader(largeData)
        let csv = CsvFile.Load(reader)
        csv.Rows |> Seq.length

    [<Benchmark>]
    member _.ParseQuotedCsv() =
        use reader = new StringReader(quotedData)
        let csv = CsvFile.Load(reader)
        csv.Rows |> Seq.length

    [<Benchmark>]
    member _.ParseTitanicCsv() =
        use reader = new StringReader(titanic)
        let csv = CsvFile.Load(reader)
        csv.Rows |> Seq.length

    [<Benchmark>]
    member _.ParseAndAccessData() =
        use reader = new StringReader(mediumData)
        let csv = CsvFile.Load(reader)
        csv.Rows 
        |> Seq.sumBy (fun row -> 
            let age = row.["Age"] |> int
            let salary = row.["Salary"] |> float
            age + int salary)

[<MemoryDiagnoser>]
[<SimpleJob>]
type CsvStreamingBenchmarks() =

    let mutable tempFile = ""

    let generateLargeFile() =
        let path = Path.GetTempFileName()
        use writer = new StreamWriter(path)
        writer.WriteLine("ID,Name,Value,Description")
        for i in 1..50000 do
            writer.WriteLine(sprintf "%d,Item%d,%.2f,Description for item %d with some longer text" i i (float i * 1.5) i)
        path

    [<GlobalSetup>]
    member this.Setup() = 
        tempFile <- generateLargeFile()

    [<GlobalCleanup>]  
    member this.Cleanup() = 
        if File.Exists(tempFile) then File.Delete(tempFile)

    [<Benchmark>]
    member this.StreamLargeFile() =
        use reader = new StreamReader(tempFile)
        let csv = CsvFile.Load(reader)
        csv.Rows |> Seq.length

    [<Benchmark>]
    member this.StreamAndProcessLargeFile() =
        use reader = new StreamReader(tempFile)
        let csv = CsvFile.Load(reader)
        csv.Rows 
        |> Seq.sumBy (fun row ->
            let id = row.["ID"] |> int
            let value = row.["Value"] |> float
            id + int value)