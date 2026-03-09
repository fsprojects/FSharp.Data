namespace FSharp.Data.Benchmarks

open System
open System.IO
open BenchmarkDotNet.Attributes
open FSharp.Data

[<MemoryDiagnoser>]
[<SimpleJob>]
type CsvBenchmarks() =

    let mutable airQualityCsvText = ""
    let mutable msftCsvText = ""
    let mutable titanicCsvText = ""
    let mutable banklistCsvText = ""

    [<GlobalSetup>]
    member this.Setup() =
        let dataPath = Path.Combine(__SOURCE_DIRECTORY__, "../FSharp.Data.Tests/Data")
        airQualityCsvText <- File.ReadAllText(Path.Combine(dataPath, "AirQuality.csv"))
        msftCsvText <- File.ReadAllText(Path.Combine(dataPath, "MSFT.csv"))
        titanicCsvText <- File.ReadAllText(Path.Combine(dataPath, "Titanic.csv"))
        banklistCsvText <- File.ReadAllText(Path.Combine(dataPath, "banklist.csv"))

    [<Benchmark>]
    member this.ParseAirQualityCsv() = CsvFile.Parse(airQualityCsvText)

    [<Benchmark>]
    member this.IterateAirQualityCsv() =
        let csv = CsvFile.Parse(airQualityCsvText)
        csv.Rows |> Seq.length

    [<Benchmark>]
    member this.ParseMSFTCsv() = CsvFile.Parse(msftCsvText)

    [<Benchmark>]
    member this.IterateMSFTCsv() =
        let csv = CsvFile.Parse(msftCsvText)
        csv.Rows |> Seq.length

    [<Benchmark>]
    member this.ParseTitanicCsv() = CsvFile.Parse(titanicCsvText)

    [<Benchmark>]
    member this.IterateTitanicCsv() =
        let csv = CsvFile.Parse(titanicCsvText)
        csv.Rows |> Seq.length

    [<Benchmark>]
    member this.ParseBanklistCsv() = CsvFile.Parse(banklistCsvText)

    [<Benchmark>]
    member this.IterateBanklistCsv() =
        let csv = CsvFile.Parse(banklistCsvText)
        csv.Rows |> Seq.length
