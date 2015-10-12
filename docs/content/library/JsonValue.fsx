(**
# F# Data: JSON Parser

The F# [JSON Type Provider](JsonProvider.html) is built on top of an efficient JSON parser written
in F#. This parser is based on a JSON parser available in [F# 3.0 Sample Pack](http://fsharp3sample.codeplex.com), 
but F# Data adds a simple API that can be used to access values dynamically.

When working with well-defined JSON documents, it is easier to use the 
[type provider](JsonProvider.html), but in a more dynamic scenario or when writing
quick and simple scripts, the parser might be a simpler option.

## Loading JSON documents

To load a sample JSON document, we first need to reference the `FSharp.Data.dll` library
(when using F# Interactive) or to add reference to a project. 
*)

#r "../../../bin/FSharp.Data.dll"
open FSharp.Data

(**
The `FSharp.Data` namespace contains the `JsonValue` type that can be used
to parse strings formatted using JSON as follows:
*)

let info =
  JsonValue.Parse(""" 
    { "name": "Tomas", "born": 1985,
      "siblings": [ "Jan", "Alexander" ] } """)

(**
The parsed value can be processed using pattern matching - the `JsonValue` type
is a discriminated union with cases such as `Record`, `Collection` and others that
can be used to examine the structure.

## Using JSON extensions

We do not cover this technique in this introduction. Instead, we look at a number
of extensions that become available after opening the `FSharp.Data.JsonExtensions` 
namespace. Once opened, we can write:

 * `value.AsBoolean()` returns the value as boolean if it is either `true` or `false`.
 * `value.AsInteger()` returns the value as integer if it is numeric and can be
   converted to an integer; `value.AsInteger64()`, `value.AsDecimal()` and
   `value.AsFloat()` behave similarly.
 * `value.AsString()` returns the value as a string.
 * `value.AsDateTime()` parses the string as a `DateTime` value using either the
    [ISO 8601](http://en.wikipedia.org/wiki/ISO_8601) format, or using the 
    `\/Date(...)\/` JSON format containing number of milliseconds since 1/1/1970.
 * `value.AsGuid()` parses the string as a `Guid` value.
 * `value?child` uses the dynamic operator to obtain a record member named `child`;
    alternatively, you can also use `value.GetProperty(child)` or an indexer
    `value.[child]`.
 * `value.TryGetProperty(child)` can be used to safely obtain a record member 
    (if the member is missing or the value is not a record then, `TryGetProperty` 
    returns `None`).
 * `[ for v in value -> v ]` treats `value` as a collection and iterates over it;
   alternatively, it is possible to obtain all elements as an array using 
   `value.AsArray()`.
 * `value.Properties()` returns a list of all properties of a record node.
 * `value.InnerText()` concatenates all text or text in an array 
   (representing e.g. multi-line string).

Methods that may need to parse a numeric value or date (such as `AsFloat` and
`AsDateTime`) receive an optional culture parameter.

The following example shows how to process the sample JSON value:
*)

open FSharp.Data.JsonExtensions

// Print name and birth year
let n = info?name
printfn "%s (%d)" (info?name.AsString()) (info?born.AsInteger())

// Print names of all siblings
for sib in info?siblings do
  printfn "%s" (sib.AsString())

(**
Note that the `JsonValue` type does not actually implement the `IEnumerable<'T>` 
interface (meaning that it cannot be passed to `Seq.xyz` functions). It only has
the `GetEnumerator` method, which makes it possible to use it in sequence expressions
and with the `for` loop.
*)

(**
## Parsing WorldBank response

To look at a more complex example, consider a sample document 
[`data/WorldBank.json`](../data/WorldBank.json) which was obtained as a response to 
a WorldBank request (you can access the WorldBank data more conveniently using
[a type provider](WorldBank.html)). The document looks as follows:

    [lang=js]
    [ { "page": 1, "pages": 1, "total": 53 },
      [ { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":null,"decimal":"1","date":"2000"},
        { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":"16.6567773464055","decimal":"1","date":"2010"} ] ]

The document is formed by an array that contains a record as the first element
and a collection of data points as the second element. The following code
reads the document and parses it:
*)

let value = JsonValue.Load(__SOURCE_DIRECTORY__ + "../../data/WorldBank.json")

(** Note that we can also load the data directly from the web, and there's an
asynchronous version available too: *)

let valueAsync = JsonValue.AsyncLoad("http://api.worldbank.org/country/cz/indicator/GC.DOD.TOTL.GD.ZS?format=json")

(** To split the top-level array into the first record (with overall information) 
and the collection of data points, we use pattern matching and match the `value`
against the `JsonValue.Array` constructor:
*)

match value with
| JsonValue.Array [| info; data |] ->
    // Print overall information
    let page, pages, total = info?page, info?pages, info?total
    printfn 
      "Showing page %d of %d. Total records %d" 
      (page.AsInteger()) (pages.AsInteger()) (total.AsInteger())
    
    // Print every non-null data point
    for record in data do
      if record?value <> JsonValue.Null then
        printfn "%d: %f" (record?date.AsInteger()) 
                         (record?value.AsFloat())
| _ -> printfn "failed"

(**
The `value` property of a data point is not always available - as demonstrated
above, the value may be `null`. In that case, we want to skip the data point.
To check whether the property is `null` we simply compare it with `JsonValue.Null`.

Also note that the `date` and `value` properties are formatted as strings 
in the source file (e.g. `"1990"`) instead of numbers (e.g. `1990`). When you try
accessing the value as an integer or float, the `JsonValue` automatically parses
the string into the desired format. In general, the API attempts to be as tolerant
as possible when parsing the file.

## Related articles

 * [F# Data: JSON Type Provider](JsonProvider.html) - discusses a F# type provider
   that provides type-safe access to JSON data
 * [F# Data: WorldBank Provider](WorldBank.html) - the WorldBank type provider
   can be used to easily access data from the WorldBank
 * [API Reference: JsonValue discriminated union](../reference/fsharp-data-jsonvalue.html)
 * [API Reference: JsonExtensions module](../reference/fsharp-data-jsonextensions.html)

*)
