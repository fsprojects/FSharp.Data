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

(**
In the previous example, `Code` is inferred as a `float`,
even though it looks more like it should be a `string`.
(`4E5` is interpreted as an exponential float notation instead of a string)

Now let's enable inline schemas:
*)

open FSharp.Data.Runtime.StructuralInference

type AmbiguousEntity2 =
    JsonProvider<Sample="""
        { "code":"typeof<string>", "length":"typeof<float<metre>>" }
        { "code":"123", "length":"42" }
        { "code":"4E5", "length":"1.83" }
        """, SampleIsList=true, InferenceMode=InferenceMode.ValuesAndInlineSchemasOverrides>

let code2 = (AmbiguousEntity2.GetSamples()[1]).Code
let length2 = (AmbiguousEntity2.GetSamples()[1]).Length

(*** include-fsi-merged-output ***)

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

*)

(**

## Loading WorldBank data

Now let's use the type provider to process some real data. We use a data set returned by
[the WorldBank](http://data.worldbank.org), which has (roughly) the following structure:

    [lang=js]
    [ { "page": 1, "pages": 1, "total": 53 },
      [ { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":null,"decimal":"1","date":"2000"},
        { "indicator": {"value": "Central government debt, total (% of GDP)"},
          "country": {"id":"CZ","value":"Czech Republic"},
          "value":"16.6567773464055","decimal":"1","date":"2010"} ] ]

The response to a request contains an array with two items. The first item is a record
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

(** Note that we can also load the data directly from the web both in the `Load` method and in
the type provider sample parameter, and there's an asynchronous `AsyncLoad` method available too: *)
let wbReq =
    "http://api.worldbank.org/country/cz/indicator/"
    + "GC.DOD.TOTL.GD.ZS?format=json"

let docAsync = WorldBank.AsyncLoad(wbReq)

(*** include-fsi-merged-output ***)

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
    record.Value
    |> Option.iter (fun value -> printfn "%d: %f" record.Date value)

(*** include-fsi-merged-output ***)

(**
When printing the data points, some of the values might be missing (in the input, the value
is `null` instead of a valid number). This is another example of a heterogeneous type -
the type is either `Number` or some other type (representing `null` value). This means
that `record.Value` has a `Number` property (when the value is a number) and we can use
it to print the result only when the data point is available.

## Parsing Twitter stream

We now look on how to parse tweets returned by the [Twitter API](http://dev.twitter.com/).
Tweets are quite heterogeneous, so we infer the structure from a _list_ of inputs rather than from
just a single input. To do that, we use the file [`data/TwitterStream.json`](../data/TwitterStream.json)
(containing a list of tweets) and pass an optional parameter `SampleIsList=true` which tells the
provider that the sample is actually a _list of samples_:

*)

type Tweet = JsonProvider<"../data/TwitterStream.json", SampleIsList=true, ResolutionFolder=ResolutionFolder>

let text = (*[omit:(omitted)]*)
    """ {"in_reply_to_status_id_str":null,"text":"\u5927\u91d1\u6255\u3063\u3066\u904a\u3070\u3057\u3066\u3082\u3089\u3046\u3002\u3082\u3046\u3053\u306e\u4e0a\u306a\u3044\u8d05\u6ca2\u3002\u3067\u3082\uff0c\u5b9f\u969b\u306b\u306f\u305d\u306e\u8d05\u6ca2\u306e\u672c\u8cea\u3092\u6e80\u55ab\u3067\u304d\u308b\u4eba\u306f\u9650\u3089\u308c\u3066\u308b\u3002\u305d\u3053\u306b\u76ee\u306b\u898b\u3048\u306a\u3044\u968e\u5c64\u304c\u3042\u308b\u3068\u304a\u3082\u3046\u3002","in_reply_to_user_id_str":null,"retweet_count":0,"geo":null,"source":"web","retweeted":false,"truncated":false,"id_str":"263290764686155776","entities":{"user_mentions":[],"hashtags":[],"urls":[]},"in_reply_to_user_id":null,"in_reply_to_status_id":null,"place":null,"coordinates":null,"in_reply_to_screen_name":null,"created_at":"Tue Oct 30 14:46:24 +0000 2012","user":{"notifications":null,"contributors_enabled":false,"time_zone":"Tokyo","profile_background_color":"FFFFFF","location":"Kodaira Tokyo Japan","profile_background_tile":false,"profile_image_url_https":"https:\/\/si0.twimg.com\/profile_images\/1172376796\/70768_100000537851636_3599485_q_normal.jpg","default_profile_image":false,"follow_request_sent":null,"profile_sidebar_fill_color":"17451B","description":"KS(Green62)\/WasedaUniv.(Schl Adv Sci\/Eng)\/SynBio\/ChronoBio\/iGEM2010-2012\/Travel\/Airplane\/ \u5bfa\u30fb\u5ead\u3081\u3050\u308a","favourites_count":17,"screen_name":"Merlin_wand","profile_sidebar_border_color":"000000","id_str":"94788486","verified":false,"lang":"ja","statuses_count":8641,"profile_use_background_image":true,"protected":false,"profile_image_url":"http:\/\/a0.twimg.com\/profile_images\/1172376796\/70768_100000537851636_3599485_q_normal.jpg","listed_count":31,"geo_enabled":true,"created_at":"Sat Dec 05 13:07:32 +0000 2009","profile_text_color":"000000","name":"Marin","profile_background_image_url":"http:\/\/a0.twimg.com\/profile_background_images\/612807391\/twitter_free1.br.jpg","friends_count":629,"url":null,"id":94788486,"is_translator":false,"default_profile":false,"following":null,"profile_background_image_url_https":"https:\/\/si0.twimg.com\/profile_background_images\/612807391\/twitter_free1.br.jpg","utc_offset":32400,"profile_link_color":"ADADAD","followers_count":426},"id":263290764686155776,"contributors":null,"favorited":false} """ (*[/omit]*)

let tweet = Tweet.Parse(text)

printfn "%s (retweeted %d times)\n:%s" tweet.User.Value.Name tweet.RetweetCount.Value tweet.Text.Value

(*** include-fsi-merged-output ***)

(**
After creating the `Tweet` type, we parse a single sample tweet and print some details about the
tweet. As you can see, the `tweet.User` property has been inferred as optional (meaning that a
tweet might not have an author?) so we unsafely get the value using the `Value` property.
The `RetweetCount` and `Text` properties may be also missing, so we also access them unsafely.

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
    printfn "#%d %s" issue.Number issue.Title

(*** include-fsi-merged-output ***)

(**

And now let's create a new issue. We look into the documentation at http://developer.github.com/v3/issues/#create-an-issue and we see that
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

(*** do-not-eval ***)

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
provider will be loaded and the types will be generated at that time (the JSON provider is not
currently a _generative_ type provider). This means that the type provider will need to be able to
access the sample JSON. This works fine when the sample is specified inline, but it won't work when
the sample is specified as a local file (unless you distribute the samples with your library).

For this reason, the JSON provider lets you specify samples as embedded resources using the
static parameter `EmbeddedResource` (don't forget then to [include the file](https://docs.microsoft.com/en-us/visualstudio/ide/build-actions) as EmbeddedResource in the
project file). If you are building a library `MyLib.dll`, you can write:

*)
type WB =
    JsonProvider<"../data/WorldBank.json", EmbeddedResource="MyLib, MyLib.data.worldbank.json", ResolutionFolder=ResolutionFolder>

(**
You still need to specify the local path, but this is only used when compiling `MyLib.dll`.
When a user of your library references `MyLib.dll` later, the JSON Type Provider will be able
to load `MyLib.dll` and locate the sample `worldbank.json` as a resource of the library. When
this succeeds, it does not attempt to find the local file and so your library can be used
without providing a local copy of the sample JSON files.

## Related articles

 * [JSON Parser](JsonValue.html) - provides more information about
   working with JSON values dynamically.
 * API Reference: `cref:T:FSharp.Data.JsonProvider`
 * API Reference: `cref:T:FSharp.Data.JsonValue`

*)
