#r "bin/FSharp.Data.dll"
#load "packages/FSharp.Charting/FSharp.Charting.fsx"

open FSharp.Data
open FSharp.Charting

type Times = CsvProvider<"log.csv",Separators="|",IgnoreErrors=true,Schema="category,instance,time",HasHeaders=false>

let byCategory = 
    Times.GetSample().Rows
    |> Seq.groupBy (fun x -> x.category)
    |> Seq.map (fun (cat, values) -> cat, values |> Seq.sumBy (fun x -> x.time))
    |> Seq.sortBy (snd >> (~-))

Chart.Column byCategory

let byInstance = 
    Times.GetSample().Rows
    |> Seq.groupBy (fun x -> x.instance)
    |> Seq.map (fun (cat, values) -> cat, values |> Seq.sumBy (fun x -> x.time))
    |> Seq.sortBy (snd >> (~-))
    |> Seq.truncate 10

Chart.Column byInstance
