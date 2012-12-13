(** 
# F# Data: WorldBank Type Provider

?
## Introducing the provider

?
*)

#r "../bin/FSharp.Data.dll"
open System.IO
open FSharp.Data

let data = WorldBank.GetDataContext()

data.Countries.``United Kingdom``.Indicators.``School enrollment, tertiary (% gross)``
|> Seq.maxBy fst

#load "lib/FSharpChart.fsx"
open System
open Samples.FSharp.Charting

type WorldBank = WorldBankProvider<"World Development Indicators", Asynchronous=true>
WorldBank.GetDataContext()

let wb = WorldBank.GetDataContext()

let countries = 
 [| wb.Countries.``Arab World`` 
    wb.Countries.``European Union``
    wb.Countries.Australia
    wb.Countries.Brazil
    wb.Countries.Canada
    wb.Countries.Chile
    wb.Countries.``Czech Republic``
    wb.Countries.Denmark
    wb.Countries.France
    wb.Countries.Greece
    wb.Countries.``Low income``
    wb.Countries.``High income``
    wb.Countries.``United Kingdom``
    wb.Countries.``United States`` |]

[ for c in countries ->
    c.Indicators.``School enrollment, tertiary (% gross)`` ]
|> Async.Parallel
|> Async.RunSynchronously
|> Array.map Chart.Line
|> Chart.Combine

(**
## Related articles

 * [F# Data: Type Providers](FSharpData.html) - gives mroe information about other
   type providers in the `FSharp.Data` package.

*)
