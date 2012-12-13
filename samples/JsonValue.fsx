(**
# F# Data: JSON Parser and Reader
*)

#r "../bin/FSharp.Data.dll"

open System.IO
open FSharp.Web
open FSharp.Web.JsonReader

(*
Assuming we have a document that looks as follows:

    [ { "page": 1, "pages": 1, "total": 53 },
      [ { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":null,"decimal":"1","date":"2000"},
        { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":"16.6567773464055","decimal":"1","date":"2010"} ] ]

*)

let file = File.ReadAllText(__SOURCE_DIRECTORY__ + "\\docs\\WorldBank.json")
let value = JsonValue.Parse(file)

(**
Given a value, you can write

 * `value.AsInteger` - ?
 * `value.AsBoolean` - ?
 * `value?child` - ?
 * `[ for v in value -> v ]` - ?
 * `value.Properties` -
 * `value.InnerText` - 
*)


(** 
Example that works
*)

match value with
| JsonValue.Array [info; data] ->
    let page, pages, total = info?page, info?pages, info?total
    printfn "Showing page %d of %d. Total records %d" page.AsInteger pages.AsInteger total.AsInteger
    for record in data do
      if record?value <> JsonValue.Null then
        printfn "%d: %f" (int record?date.AsString) (float record?value.AsString)
| _ -> printfn "failed"


