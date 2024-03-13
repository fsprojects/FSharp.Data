(**
---
category: Type Providers
categoryindex: 1
index: 3
---
*)
(*** condition: prepare ***)
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Http.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Runtime.Utilities.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.Json.Core.dll"
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.dll"
(*** condition: fsx ***)
#if FSX
#r "nuget: FSharp.Data,{{fsdocs-package-version}}"
#endif
(*** condition: ipynb ***)
#if IPYNB
#r "nuget: FSharp.Data,{{fsdocs-package-version}}"

Formatter.SetPreferredMimeTypesFor(typeof<obj>, "text/plain")
Formatter.Register(fun (x: obj) (writer: TextWriter) -> fprintfn writer "%120A" x)
#endif
(**
[![Binder](../img/badge-binder.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Data/gh-pages?filepath={{fsdocs-source-basename}}.ipynb)&emsp;
[![Script](../img/badge-script.svg)]({{root}}/{{fsdocs-source-basename}}.fsx)&emsp;
[![Notebook](../img/badge-notebook.svg)]({{root}}/{{fsdocs-source-basename}}.ipynb)

# JSON Type Provider

This article demonstrates how to use the JSON Type Provider to access JSON files
in a statically typed way. We first look at how the structure is inferred and then
demonstrate the provider by parsing data from WorldBank and Twitter.

The JSON Type Provider provides statically typed access to JSON documents.
It takes a sample document as an input (or a document containing a JSON array of samples).
The generated type can then be used to read files with the same structure.

If the loaded file does not match the structure of the sample, a runtime error may occur
(but only when explicitly accessing an element incompatible with the original sample — e.g. if it is no longer present).

## Introducing the provider


<div class="container-fluid" style="margin:15px 0px 15px 0px;">
    <div class="row-fluid">
        <div class="span1"></div>
        <div class="span10" id="anim-holder">
            <a id="lnk" href="../images/json.gif"><img id="anim" src="../images/json.gif" /></a>
        </div>
        <div class="span1"></div>
    </div>
</div>

The type provider is located in the `FSharp.Data.dll` assembly and namespace: *)

open FSharp.Data

(**
### Inferring types from the sample

The `JsonProvider<...>` takes one static parameter of type `string`. The parameter can
be _either_ a sample string _or_ a sample file (relative to the current folder or online
accessible via `http` or `https`). It is not likely that this could lead to ambiguities.

The following sample passes a small JSON string to the provider:
*)

type Simple = JsonProvider<""" { "name":"John", "age":94 } """>
let simple = Simple.Parse(""" { "name":"Tomas", "age":4 } """)
simple.Age
simple.Name

(*** include-fsi-merged-output ***)

(**
You can see that the generated type has two properties - `Age` of type `int` and `Name` of
type `string`. The provider successfully infers the types from the sample and exposes the
fields as properties (with PascalCase name to follow standard naming conventions).


### Inferring numeric types

In the previous case, the sample document simply contained an integer and so the provider
inferred the type `int`. Sometimes, the types in the sample document (or a list of samples)
may not match exactly. For example, a list may mix integers and floats:
*)

type Numbers = JsonProvider<""" [1, 2, 3, 3.14] """>
let nums = Numbers.Parse(""" [1.2, 45.1, 98.2, 5] """)
let total = nums |> Seq.sum

(*** include-fsi-merged-output ***)

(**
When the sample is a collection, the type provider generates a type that can be used to store
all values in the sample. In this case, the resulting type is `decimal`, because one
of the values is not an integer. In general, the provider supports (and prefers them
in this order): `int`, `int64`, `decimal` and `float`.

Other primitive types cannot be combined into a single type. For example, if the list contains
numbers _and_ strings. In this case, the provider generates two methods that can be used
to get values that match one of the types:
*)

type Mixed = JsonProvider<""" [1, 2, "hello", "world"] """>
let mixed = Mixed.Parse(""" [4, 5, "hello", "world" ] """)

mixed.Numbers |> Seq.sum
mixed.Strings |> String.concat ", "

(*** include-fsi-merged-output ***)

(**
As you can see, the `Mixed` type has properties `Numbers` and `Strings` that
return only `int` and `string` values from the collection. This means that we get
type-safe access to the values, but not in the original order (if order matters, then
you can use the `mixed.JsonValue` property to get the underlying `JsonValue` and
process it dynamically as described in [the documentation for `JsonValue`](JsonValue.html).

### Inferring record types

Now let's look at a sample JSON document that contains a list of records. The
following example uses two records - one with `name` and `age` and the second with just
`name`. If a property is missing, then the provider infers it as optional.

If we want to just use the same text used for the schema at runtime, we can use the `GetSamples` method:
*)

type People =
    JsonProvider<"""
  [ { "name":"John", "age":94 },
    { "name":"Tomas" } ] """>

for item in People.GetSamples() do
    printf "%s " item.Name
    item.Age |> Option.iter (printf "(%d)")
    printfn ""

(*** include-fsi-merged-output ***)

(**
The inferred type for `items` is a collection of (anonymous) JSON entities - each entity
has properties `Name` and `Age`. As `Age` is not available for all records in the sample
data set, it is inferred as `option<int>`. The above sample uses `Option.iter` to print
the value only when it is available.

In the previous case, the values of individual properties had common types - `string`
for the `Name` property and numeric type for `Age`. However, what if the property of
a record can have multiple different types? In that case, the type provider behaves
as follows:
*)

type Values = JsonProvider<""" [{"value":94 }, {"value":"Tomas" }] """>

for item in Values.GetSamples() do
    match item.Value.Number, item.Value.String with
    | Some num, _ -> printfn "Numeric: %d" num
    | _, Some str -> printfn "Text: %s" str
    | _ -> printfn "Some other value!"

(*** include-fsi-merged-output ***)

(**
Here, the `Value` property is either a number or a string, The type provider generates
a type that has an optional property for each possible option, so we can use
simple pattern matching on `option<int>` and `option<string>` values to distinguish
between the two options. This is similar to the handling of heterogeneous arrays.

Note that we have a `GetSamples` method because the sample is a JSON list. If it was a JSON
object, we would have a `GetSample` method instead.

#### More complex object type on root level

If you want the root type to be an object type, not an array, but
you need more samples at root level, you can use the `SampleIsList` parameter.
Applied to the previous example this would be:

*)

type People2 =
    JsonProvider<"""
  [ { "name":"John", "age":94 },
    { "name":"Tomas" } ] """, SampleIsList=true>

let person = People2.Parse("""{ "name":"Gustavo" }""")

(*** include-fsi-merged-output ***)

(**
Note that starting with version 4.2.9 of this package, JSON comments are supported
(Comments are either single-line and start with `//` or multi-line when wrapped in `/*` and `*/`).
This is not a standard feature of JSON, but it can be really convenient,
e.g. to annotate each sample when using multiple ones.
*)

(**
## Type inference hints / inline schemas

Starting with version 4.2.10 of this package, it's possible to enable basic type annotations
directly in the sample used by the provider, to complete or to override type inference.
(Only basic types are supported. See the reference documentation of the provider for the full list)

This feature is disabled by default and has to be explicitly enabled with the `InferenceMode`
static parameter.

Let's consider an example where this can be useful:

*)

type AmbiguousEntity =
    JsonProvider<Sample="""
        { "code":"000", "length":"0" }
        { "code":"123", "length":"42" }
        { "code":"4E5", "length":"1.83" }
        """, SampleIsList=true>

let code = (AmbiguousEntity.GetSamples()[1]).Code
let length = (AmbiguousEntity.GetSamples()[1]).Length

(*** include-fsi-merged-output ***)
