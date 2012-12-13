(** 
# F# Data: CSV Type Provider

?
## Introducing the provider

?
*)

#r "../bin/FSharp.Data.dll"
open System.IO
open System.Net
open FSharp.Data

type Stocks = CsvProvider<"docs/MSFT.csv">

let wc = new WebClient()
let msft = Stocks.Parse(wc.DownloadString("http://ichart.finance.yahoo.com/table.csv?s=MSFT"))
for row in msft.Data do
  printfn "HLOC: (%A, %A, %A, %A)" row.High row.Low row.Open row.Close

#load "lib/FSharpChart.fsx"
open System
open Samples.FSharp.Charting

[ for row in msft.Data -> DateTime.Parse(row.Date), row.Open ]
|> Chart.FastLine

let recent = 
  [ for row in msft.Data do
      let dt = DateTime.Parse(row.Date)
      if dt > DateTime.Now.AddDays(-30.0) then
        yield dt, row.High, row.Low, row.Open, row.Close ]
Chart.Candlestick(recent).AndYAxis(Max = 30.0, Min = 25.0)

open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames

type Test = CsvProvider<"docs/SmallTest.csv">
let small = Test.Load(Path.Combine(__SOURCE_DIRECTORY__, "docs/SmallTest.csv"))
for row in small.Data do
  let speed = row.Distance / row.Time
  if speed > 15.0M<meter/second> then 
    printfn "%s (%A m/s)" row.Name speed

(**
## Related articles

 * [F# Data: Type Providers](FSharpData.html) - gives mroe information about other
   type providers in the `FSharp.Data` package.

*)
