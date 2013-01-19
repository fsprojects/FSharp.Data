(**
# F# Data: JSON Parser and Reader

The F# [JSON Type Provider](JsonProvider.html) is built on top of an efficient JSON parser written
in F#. The parser is based on a JSON parser available in [F# 3.0 Sample Pack](http://fsharp3sample.codeplex.com), 
but F# Data adds simple API that can be used to access values dynamically.

When working with well-defined JSON documents, it is easier to use the 
[type provider](JsonProvider.html), but in a more dynamic scenario or when writing
quick and simple scripts, the parser might be simpler option.

## Loading JSON documents

To load a sample JSON document, we first need to reference the `FSharp.Data.dll` library
(when using F# Interactive) or to add reference to a project. 
*)

#r "../bin/FSharp.Data.dll"
open System.IO
open FSharp.Data.Json

(**
The `FSharp.Data.Json` namespace contains the `JsonValue` type that can be used
to parse strings formatted using JSON as follows:
*)

let info =
  JsonValue.Parse(""" 
    { "name": "Tomas", "born": 1985,
      "siblings": [ "Jan", "Alexander" ] } """)

(**
The parsed value can be processed using pattern matching - the `JsonValue` type
is a discriminated union with cases such as `Record`, `Collection` and other that
can be used to examine the structure.

## Using JSON reader extensions

We do not cover this technique in this introduction. Instead, we look at a number
of extensions that become available after opening the `FSharp.Data.Json.JsonReader` 
namespace. Once opened, we can write:

 * `value.AsBoolean` returns the value as Boolean if it is either `true` or `false`
 * `value.AsInteger` returns the value as integer if it is numeric and can be
   converted to an integer; `value.AsInteger64`, `value.AsDecimal` and
   `value.AsFloat` behave similarly.
 * `value.AsString` returns the value as a string
 * `value?child` used dynamic operator to obtain a record member named `child`
 * `[ for v in value -> v ]` treats `value` as a collection and iterates over it
 * `value.Properties` returns a list of all properties of a record node
 * `value.InnerText` concatenates all text or text in an array 
   (representing e.g. multi-line string)

The following example shows how to process the sample JSON value:
*)
open FSharp.Data.Json.JsonReader

// Print name and birth year
printfn "%s (%d)" info?name.AsString info?born.AsInteger

// Print names of all siblings
for sib in info?siblings do
  printfn "%s" sib.AsString

(**
Note that the `JsonValue` type does not actually implement the `IEnumerable<'T>` 
namespace (meaning that it cannot be pased to `Seq.xyz` functions). It only has
the `GetEnumerator` method, which makes it possible to use it in sequence expressions
and with the `for` loop.
*)

(*
## Parsing WorldBank response

To look at a more complex example, consider a sample document 
[`docs/WorldBank.json`](docs/WorldBank.json) which was obtained as a response to 
a WorldBank request (you can access the WorldBank data more conveniently using
[a type provider](WorldBank.html)). The document looks as follows:

    [ { "page": 1, "pages": 1, "total": 53 },
      [ { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":null,"decimal":"1","date":"2000"},
        { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":"16.6567773464055","decimal":"1","date":"2010"} ] ]

The document is formed by an array that contains record as the first element
and a collection of data points as the second element. The following code
reads the document and parses it:
*)

let file = File.ReadAllText(__SOURCE_DIRECTORY__ + "/docs/WorldBank.json")
let value = JsonValue.Parse(file)

(** 
To split the top-level array into the first record (with overall information) 
and the collection of data points, we use pattern matching and match the `value`
against the `JsonValue.Array` constructor:
*)
match value with
| JsonValue.Array [info; data] ->
    // Print overall information
    let page, pages, total = info?page, info?pages, info?total
    printfn 
      "Showing page %d of %d. Total records %d" 
      page.AsInteger pages.AsInteger total.AsInteger
    
    // Print every non-null data point
    for record in data do
      if record?value <> JsonValue.Null then
        printfn "%d: %f" (int record?date.AsString) 
                         (float record?value.AsString)
| _ -> printfn "failed"

(**
The `value` property of a data point is not always available - as demonstrated
above, the value may be `null`. In that case, we want to skipt the data point.
To check whether the property is `null` we simply compare it with `JsonValue.Null`.

Also note that the `date` and `value` properties are formatted as strings 
(e.g. `"1990"`) instead of numbers (e.g. `1990`) so we use standard F# 
functions `int` and `float` to convert the value obtained using `AsString`.


## Related articles

 * [F# Data: JSON Type Provider](JsonProvider.html) - discusses F# type provider
   that provides type-safe access to JSON data
 * [F# Data: WorldBank Provider](WorldBank.html) - the WorldBank type provider
   can be used to easily access data from the WorldBank
*)


