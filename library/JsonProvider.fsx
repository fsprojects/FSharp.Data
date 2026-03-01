(**

*)
#r "nuget: FSharp.Data,8.1.0-beta"
#endif
(**
[![Binder](../img/badge-binder.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Data/gh-pages?filepath=library/JsonProvider.ipynb)&emsp;
[![Script](../img/badge-script.svg)](https://fsprojects.github.io/FSharp.Data//library/JsonProvider.fsx)&emsp;
[![Notebook](../img/badge-notebook.svg)](https://fsprojects.github.io/FSharp.Data//library/JsonProvider.ipynb)

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
The type provider is located in the `FSharp.Data.dll` assembly and namespace:

*)
open FSharp.Data
(**
### Inferring types from the sample

The `JsonProvider<...>` takes one static parameter of type `string`. The parameter can
be **either** a sample string **or** a sample file (relative to the current folder or online
accessible via `http` or `https`). It is not likely that this could lead to ambiguities.

The following sample passes a small JSON string to the provider:

*)
type Simple = JsonProvider<""" { "name":"John", "age":94 } """>
let simple = Simple.Parse(""" { "name":"Tomas", "age":4 } """)
simple.Age
simple.Name(* output: 
type Simple = JsonProvider<...>
val simple: JsonProvider<...>.Root = {
  "name": "Tomas",
  "age": 4
}
val it: string = "Tomas"*)
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
let total = nums |> Seq.sum(* output: 
type Numbers = JsonProvider<...>
val nums: decimal array = [|1.2M; 45.1M; 98.2M; 5M|]
val total: decimal = 149.5M*)
(**
When the sample is a collection, the type provider generates a type that can be used to store
all values in the sample. In this case, the resulting type is `decimal`, because one
of the values is not an integer. In general, the provider supports (and prefers them
in this order): `int`, `int64`, `decimal` and `float`.

Other primitive types cannot be combined into a single type. For example, if the list contains
numbers **and** strings. In this case, the provider generates two methods that can be used
to get values that match one of the types:

*)
type Mixed = JsonProvider<""" [1, 2, "hello", "world"] """>
let mixed = Mixed.Parse(""" [4, 5, "hello", "world" ] """)

mixed.Numbers |> Seq.sum
mixed.Strings |> String.concat ", "(* output: 
type Mixed = JsonProvider<...>
val mixed: JsonProvider<...>.Root = [
  4,
  5,
  "hello",
  "world"
]
val it: string = "4, 5, hello, world"*)
(**
As you can see, the `Mixed` type has properties `Numbers` and `Strings` that
return only `int` and `string` values from the collection. This means that we get
type-safe access to the values, but not in the original order (if order matters, then
you can use the `mixed.JsonValue` property to get the underlying `JsonValue` and
process it dynamically as described in [the documentation for `JsonValue`](JsonValue.html).

### Inferring date types

String values in JSON that look like dates are inferred as `DateTime` or `DateTimeOffset`.
On .NET 6 and later, when you set `PreferDateOnly = true`, strings that represent a date without a time component (e.g. `"2023-01-15"`)
are inferred as `DateOnly`, and time-only strings are inferred as `TimeOnly`. By default (`PreferDateOnly = false`),
all dates are inferred as `DateTime` for backward compatibility.

Set `PreferDateTimeOffset = true` to infer all date-time values (that would otherwise be `DateTime`) as `DateTimeOffset`.
Values that already contain an explicit timezone offset (e.g. `"2023-06-15T10:30:00+05:30"`) are always inferred as
`DateTimeOffset` regardless of this flag.

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
    printfn ""(* output: 
John (94)
Tomas 
type People = JsonProvider<...>
val it: unit = ()*)
(**
The inferred type for `items` is a collection of (anonymous) JSON entities - each entity
has properties `Name` and `Age`. As `Age` is unavailable for all records in the sample
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
    | _ -> printfn "Some other value!"(* output: 
Numeric: 94
Text: Tomas
type Values = JsonProvider<...>
val it: unit = ()*)
(**
Here, the `Value` property is either a number or a string, The type provider generates
a type that has an optional property for each possible option, so we can use
simple pattern matching on `option<int>` and `option<string>` values to distinguish
between the two options. This is similar to the handling of heterogeneous arrays.

Note that we have a `GetSamples` method because the sample is a JSON list. If it was a JSON
object, we would have a `GetSample` method instead.

#### More complex object type on the root level

If you want the root type to be an object type, not an array, but
you need more samples at the root level, you can use the `SampleIsList` parameter.
Applied to the previous example, this would be:

*)
type People2 =
    JsonProvider<
        """
  [ { "name":"John", "age":94 },
    { "name":"Tomas" } ] """,
        SampleIsList=true
     >

let person = People2.Parse("""{ "name":"Gustavo" }""")(* output: 
type People2 = JsonProvider<...>
val person: JsonProvider<...>.Root = {
  "name": "Gustavo"
}*)
(**
Note that starting with version 4.2.9 of this package, JSON comments are supported
(Comments are either single-line and start with `//` or multi-line when wrapped in `/*` and `*/`).
This is not a standard feature of JSON, but it can be really convenient,
e.g. to annotate each sample when using multiple ones.

## Type inference hints / inline schemas

Starting with version 4.2.10 of this package, it's possible to enable basic type annotations
directly in the sample used by the provider, to complete or to override type inference.
(Only basic types are supported. See the reference documentation of the provider for the full list)

This feature is disabled by default and has to be explicitly enabled with the `InferenceMode`
static parameter.

Let's consider an example where this can be useful:

*)
type AmbiguousEntity =
    JsonProvider<
        Sample="""
        { "code":"000", "length":"0" }
        { "code":"123", "length":"42" }
        { "code":"4E5", "length":"1.83" }
        """,
        SampleIsList=true
     >

let code = (AmbiguousEntity.GetSamples()[1]).Code
let length = (AmbiguousEntity.GetSamples()[1]).Length(* output: 
type AmbiguousEntity = JsonProvider<...>
val code: float = 123.0
val length: decimal = 42M*)
(**
In the previous example, `Code` is inferred as a `float`,
even though it looks more like it should be a `string`.
(`4E5` is interpreted as an exponential float notation instead of a string)

Now, let's enable inline schemas:

*)
open FSharp.Data.Runtime.StructuralInference

type AmbiguousEntity2 =
    JsonProvider<
        Sample="""
        { "code":"typeof<string>", "length":"typeof< float<metre> >" }
        { "code":"123", "length":"42" }
        { "code":"4E5", "length":"1.83" }
        """,
        SampleIsList=true,
        InferenceMode=InferenceMode.ValuesAndInlineSchemasOverrides
     >

let code2 = (AmbiguousEntity2.GetSamples().[1]).Code
let length2 = (AmbiguousEntity2.GetSamples().[1]).Length(* output: 
type AmbiguousEntity2 = JsonProvider<...>
val code2: string = "123"
val length2: JsonProvider<...>.DecimalOrString = "42"*)
(**
With the `ValuesAndInlineSchemasOverrides` inference mode, the `typeof<string>` inline schema
takes priority over the type inferred from other values.
`Code` is now a `string`, as we wanted it to be!

Note that an alternative to obtain the same result would have been to replace all the `Code` values
in the samples with unambiguous string values. (But this can be very cumbersome, especially with big samples)

If we had used the `ValuesAndInlineSchemasHints` inference mode instead, our inline schema
would have had the same precedence as the types inferred from other values, and `Code`
would have been inferred as a choice between either a number or a string,
exactly as if we had added another sample with an unambiguous string value for `Code`.

You can use either angle brackets `<>` or curly brackets `{}` when defining inline schemas.

### Units of measure

Inline schemas also enable support for units of measure.

In the previous example, the `Length` property is now inferred as a `float`
with the `metre` unit of measure (from the default SI units).

Warning: units of measures are discarded when merged with types without a unit or with a different unit.
As mentioned previously, with the `ValuesAndInlineSchemasHints` inference mode,
inline schemas types are merged with other inferred types with the same precedence.
Since values-inferred types never have units, inline-schemas-inferred types will lose their
unit if the sample contains other values...

## Loading WorldBank data

Now, let's use the type provider to process some real data. We use a data set returned by
[the WorldBank](https://data.worldbank.org), which has (roughly) the following structure:

    [lang=js]
    [ { "page": 1, "pages": 1, "total": 53 },
      [ { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":null,"decimal":"1","date":"2000"},
        { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":"16.6567773464055","decimal":"1","date":"2010"} ] ]

The response to a request contains an array of two items. The first item is a record
with general information about the response (page, total pages, etc.) and the second item
is another array which contains the actual data points. For every data point, we get
some information and the actual `value`. Note that the `value` is passed as a string
(for some unknown reason). It is wrapped in quotes, so the provider infers its type as
`string` (and we need to convert it manually).

The following sample generates type based on the [`data/WorldBank.json`](../data/WorldBank.json)
file and loads it:

*)
[<Literal>]
let ResolutionFolder = __SOURCE_DIRECTORY__

type WorldBank = JsonProvider<"../data/WorldBank.json", ResolutionFolder=ResolutionFolder>
let doc = WorldBank.GetSample()
(**
Note that we can also load the data directly from the web both in the `Load` method and in
the type provider sample parameter, and there's an asynchronous `AsyncLoad` method available too:

*)
let wbReq =
    "https://api.worldbank.org/country/cz/indicator/"
    + "GC.DOD.TOTL.GD.ZS?format=json"

let docAsync = WorldBank.AsyncLoad(wbReq)(* output: 
val wbReq: string =
  "https://api.worldbank.org/country/cz/indicator/GC.DOD.TOTL.GD"+[15 chars]
val docAsync: Async<JsonProvider<...>.Root>*)
(**
The `doc` is an array of heterogeneous types, so the provider generates a type
that can be used to get the record and the array, respectively. Note that the
provider infers that there is only one record and one array. We can print the data set as follows:

*)
// Print general information
let info = doc.Record
printfn "Showing page %d of %d. Total records %d" info.Page info.Pages info.Total

// Print all data points
for record in doc.Array do
    record.Value |> Option.iter (fun value -> printfn "%d: %f" record.Date value)(* output: 
Showing page 1 of 1. Total records 53
2010: 35.142297
2009: 31.034880
2008: 25.475164
2007: 24.193320
2006: 23.708055
2005: 22.033462
2004: 20.108379
2003: 18.267725
2002: 15.425565
2001: 14.874434
2000: 13.218869
1999: 11.356696
1998: 10.178780
1997: 10.153566
1996: 10.520301
1995: 12.707834
1994: 14.781808
1993: 16.656777
val info: JsonProvider<...>.Record2 =
  {
  "page": 1,
  "pages": 1,
  "per_page": "1000",
  "total": 53
}
val it: unit = ()*)
(**
When printing the data points, some of the values might be missing (in the input, the value
is `null` instead of a valid number). This is another example of a heterogeneous type -
the type is either `Number` or some other type (representing `null` value). This means
that `record.Value` has a `Number` property (when the value is a number) and we can use
it to print the result only when the data point is available.

## Parsing Twitter stream

We now look at how to parse tweets returned by the [Twitter API](http://dev.twitter.com/).
Tweets are quite heterogeneous, so we infer the structure from a **list** of inputs rather than from
just a single input. To do that, we use the file [`data/TwitterStream.json`](../data/TwitterStream.json)
(containing a list of tweets) and pass an optional parameter `SampleIsList=true` which tells the
provider that the sample is actually a **list of samples**:

*)
type Tweet = JsonProvider<"../data/TwitterStream.json", SampleIsList=true, ResolutionFolder=ResolutionFolder>

let text = (*[omit:(omitted)]*)

let tweet = Tweet.Parse(text)

printfn "%s (retweeted %d times)\n:%s" tweet.User.Value.Name tweet.RetweetCount.Value tweet.Text.Value(* output: 
*)
(**
After creating the `Tweet` type, we parse a single sample tweet and print some details about the
tweet. As you can see, the `tweet.User` property has been inferred as optional, and so are
`RetweetCount` and `Text`. The reason is that `TwitterStream.json` contains not only tweet objects
but also other event types (such as `delete` events) with a completely different schema. When the
type provider merges multiple heterogeneous sample objects, any field that does not appear in all
samples is inferred as optional. We unsafely get the values using the `Value` property since we
know our input is a tweet.

## Getting and creating GitHub issues

In this example we will now also create JSON in addition to consuming it.
Let's start by listing the 5 most recently updated open issues in the FSharp.Data repository.

*)
// GitHub.json downloaded from
// https://api.github.com/repos/fsharp/FSharp.Data/issues
// to prevent rate limit when generating these docs
type GitHub = JsonProvider<"../data/GitHub.json", ResolutionFolder=ResolutionFolder>

let topRecentlyUpdatedIssues =
    GitHub.GetSamples()
    |> Seq.filter (fun issue -> issue.State = "open")
    |> Seq.sortBy (fun issue -> System.DateTimeOffset.Now - issue.UpdatedAt)
    |> Seq.truncate 5

for issue in topRecentlyUpdatedIssues do
    printfn "#%d %s" issue.Number issue.Title(* output: 
#879 Bug when call request from Http module
#867 XmlProvider in 2.2.5 on F# 4 project causes multiple FSharp.Core assembly references
#877 Header being considered as data row in HTMLProvider
#878 Replace GitHub JsonProvider example with something else in ConsoleTests because of rate limit
#873 Fix HtmlInference inferListType when passing an empty seq
type GitHub = JsonProvider<...>
val topRecentlyUpdatedIssues: JsonProvider<...>.Root seq
val it: unit = ()*)
(**
And now let's create a new issue. We look into the documentation at [http://developer.github.com/v3/issues/#create-an-issue](http://developer.github.com/v3/issues/#create-an-issue) and we see that
we need to post a JSON value similar to this:

*)
[<Literal>]
let issueSample =
    """
{
  "title": "Found a bug",
  "body": "I'm having a problem with this.",
  "assignee": "octocat",
  "milestone": 1,
  "labels": [
    "Label1",
    "Label2"
  ]
}
"""
(**
This JSON is different from what we got for each issue in the previous API call, so we'll define a new type based on this sample,
create an instance, and send a POST request:

*)
type GitHubIssue = JsonProvider<issueSample, RootName="issue">

let newIssue =
    GitHubIssue.Issue(
        "Test issue",
        "This is a test issue created in FSharp.Data documentation",
        assignee = "",
        labels = [||],
        milestone = 0
    )

newIssue.JsonValue.Request "https://api.github.com/repos/fsharp/FSharp.Data/issues"
(**
<a name="jsonlib"></a>
## Using JSON provider in a library

You can use the types created by JSON type provider in a public API of a library that you are building,
but there is one important thing to keep in mind - when the user references your library, the type
provider will be loaded, and the types will be generated at that time (the JSON provider is not
currently a **generative** type provider). This means that the type provider will need to be able to
access the sample JSON. This works fine when the sample is specified inline, but it won't work when
the sample is specified as a local file (unless you distribute the samples with your library).

For this reason, the JSON provider lets you specify samples as embedded resources using the
static parameter `EmbeddedResource`. When this parameter is set, the type provider at design time
reads the sample from the local path, but at runtime (when the library is loaded by a consumer)
it reads the sample from the embedded resource in the compiled assembly.

### Step-by-step guide

**Step 1**: Mark your sample file as an embedded resource in the `.fsproj` file:

```xml
<ItemGroup>
  <EmbeddedResource Include="data/worldbank.json" />
</ItemGroup>
```

**Step 2**: Use the `EmbeddedResource` static parameter. The value must be
`"AssemblyName, AssemblyName.dotted.path.to.file.json"` where:
- `AssemblyName` is the name of your library assembly (without `.dll`)
- The file path uses **dots** (not slashes) as separators, prefixed with `AssemblyName`

So for a file `data/worldbank.json` in a library `MyLib`, the value is:

*)
type WB =
    JsonProvider<
        "../data/WorldBank.json",
        EmbeddedResource="MyLib, MyLib.data.worldbank.json",
        ResolutionFolder=ResolutionFolder
     >
(**
You still need to specify the local path, but this is only used when compiling `MyLib.dll`.
When a user of your library references `MyLib.dll` later, the JSON Type Provider will be able
to load `MyLib.dll` and locate the sample `worldbank.json` as a resource of the library. When
this succeeds, it does not attempt to find the local file and so your library can be used
without providing a local copy of the sample JSON files.

> **Common pitfall**: If you get a cryptic error where the type provider interprets the file path
as the CSV/JSON content itself (resulting in a single-column type named after the path), you
have likely forgotten to add the `EmbeddedResource` parameter, or the assembly name or resource
path in the parameter value is incorrect.
> 

> To verify the embedded resource name, you can inspect the compiled `.dll` using a tool such as
`ildasm`, `dotnet-ildasm`, or ILSpy and look at the `.mresource` entries.
> 

## Related articles

* [JSON Parser](JsonValue.html) - provides more information about
working with JSON values dynamically.

* API Reference: [JsonProvider](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-jsonprovider.html)

* API Reference: [JsonValue](https://fsprojects.github.io/FSharp.Data/reference/fsharp-data-jsonvalue.html)

*)

