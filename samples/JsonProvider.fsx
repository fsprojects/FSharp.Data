(** 
# F# Data: JSON Type Provider

The JSON type provider can be used to read JSON documents in a statically typed way.
The provider takes a sample JSON document (or a JSON document containing an array of
samples) and infers the structure of the file. The generated type can then be used
to read files with the same structure. If the loaded file does not match the 
structure of the sample, an exception may occur (but only when accessing e.g. non-existing
element).

## Introducing the provider

The type provider is located in `FSharp.Data.dll`. Assuming the assembly is located
in `../bin` directory, we can load it and open the `FSharp.Data` namespace as follows: *)

#r "../bin/FSharp.Data.dll"
open System.IO
open FSharp.Data

(**
### Inferring type from sample

The type provider is represented by a type `JsonProvider` that takes one required `string` 
parameter. The parameter can be _either_ a sample JSON string _or_ a sample file (relatively to
the current folder or online accessible via `http` or `https`). It is not likely that this 
could lead to ambiguities. The following sample passes small JSON string to the provider:
*)

type Simple = JsonProvider<""" { "name":"John", "age":94 } """>
let simple = Simple.Parse(""" { "name":"Tomas", "age":4 } """)
simple.Age
simple.Name

(**
You can see that the generated type has two properties - `Age` of type `int` and `Name` of
type `string`. The provider successfuly infers the types from the sample and exposes the
fields as properties (with PascalCase name to follow standard naming conventions).


### Inferring numeric types

In the previous case, the sample document simply contained an integer and so the provider
inferred the type `int`. Sometimes, the types in the sample document (or a list of samples) 
may not exactly match. For example, a list may mix integers and floats:
*)

type Numbers = JsonProvider<""" [1, 2, 3, 3.14] """>
let nums = Numbers.Parse(""" [1.2, 45.1, 98.2, 5] """)
let total = nums |> Seq.sum

(**
When the sample is a collection, the type provider finds a type that can be used to store
all values in the sample. In this case, the resulting type is `decimal`, because one
of the values is not an integer. In general, the provider supports (and prefers them 
in this order): `int`, `int64`, `decimal` and `float`.

Other primitive types cannot be combined into a single type. For example, if the list contains
numbers _and_ strings. In this case, the provider generates two methods that can be used
to get values that match one of the types:
*)

type Mixed = JsonProvider<""" [1, 2, "hello", "world"] """>
let mixed = Mixed.Parse(""" [4, 5, "hello", "world" ] """)

mixed.GetNumbers() |> Seq.sum
mixed.GetStrings() |> String.concat ", "

(**
As you can see, the `Mixed` type has methods `GetNumbers()` and `GetStrings()` that 
return only `int` and `string` values from the collection. This means that we get a nice
type-safe access to the values, but not in the original order (if order matters, then
you can use the `mixed.JsonValue` property to get the underlying `JsonValue` and 
process it dynamically as described in [the documentation for `JsonValue`](JsonValue.html).

### Inferring record types

Now, let's look at a sample JSON document that contains a list of records. The
following example uses two records - one with `name` and `age` and the second with just
`name`. If a property is missing, then the provider infers it as optional.

To simplify the sample, we use the `[<Literal>]` attribtue and use the same string
as a schema and as runtime value:
*)

let [<Literal>] people = """ [{ "name":"John", "age":94 }, { "name":"Tomas" }] """
type People = JsonProvider<people>

let items = People.Parse(people)
for item in items do 
  printf "%s " item.Name 
  item.Age |> Option.iter (printf "(%d)")
  printfn ""

(**
The inferred type for `items` is a collection of (anonymous) JSON entities - each entity
has properties `Name` and `Age`. As `Age` is not available for all records in the sample
data set, it is inferred as `option<int>`. The above sample uses `Option.iter` to print
the value only when it is available.

In the previous case, the values of individual properties had common type - `string` 
for the `Name` proprety and numeric type for `Age`. However, what if the property of
a record can have multiple different types? In that case, the type provider behaves
as follows: 
*)

let [<Literal>] values = """ [{"value":94 }, {"value":"Tomas" }] """
type Values = JsonProvider<values>

let items = Values.Parse(values)
for item in items do 
  match item.Value.Number, item.Value.String with
  | Some num, _ -> printfn "Numeric: %d" num
  | _, Some str -> printfn "Text: %s" str
  | _ -> printfn "Some other value!"

(**
Here, the `Value` property is either a number or a string, The type provider generates
a type that has an optional property for each possible option, so we can use 
simple pattern matching on `option<int>` and `option<string>` values to distinguish
between the two options. This is similar to the handling of heterogeneous arrays.

## Loading WorldBank data

Let's now use the type provider to process some real data. We use a data set returned by 
[the WorldBank](http://data.worldbank.org), which has (roughly) the following structure:

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

The following sample generates type based on the [`docs/WorldBank.json`](docs/WorldBank.json) 
file and loads it:
*)

type WorldBank = JsonProvider<"docs/WorldBank.json">
let doc = WorldBank.Load(__SOURCE_DIRECTORY__ + "\\docs\\WorldBank.json")

(**
The `doc` is an array of heterogeneous types, so the provider generates a type
that can be used to get the record and the array, respectively. Note that the 
provider infers that there is only one record and one array (unlike in the previous 
case when we had multiple numbers and multiple strings in an array). In that
case it does not generate methods (like `GetArray()` and `GetRecord()`) but it
generates simple properties `Record` and `Array`. We can print the data set as follows:
*)

// Print general information
let info = doc.Record
printfn "Showing page %d of %d. Total records %d" 
  info.Page info.Pages info.Total

// Print all data points
for record in doc.Array do
  if record.Value <> null then
    printfn "%d: %f" (int record.Date) (float record.Value)

(**
When printing the data points, some of them might be missing. Previously, this was handled
using the `option` type, but in this case, the type is `string` and F# strings can have 
`null` as a value, so the provider just returns a `string` which may be `null`. We can easily
test that and print only available data.

## Parsing Twitter stream

In our last example, we look how to parse tweets returned by the [Twitter API](http://dev.twitter.com/).
Tweets are quite heterogeneous, so we infer the structure from a _list_ of inputs rather than from 
just a single input. To do that, we use the file [`docs/TwitterStream.json`](docs/TwitterStream.json) 
(containing a list of tweets) and pass an optional parameter `SampleList=true` which tells the 
provider that the sample is actually a _list of samples_:

*)

type Tweet = JsonProvider<"docs/TwitterStream.json", SampleList=true>
let text = (*[omit:(omitted)]*)""" {"in_reply_to_status_id_str":null,"text":"\u5927\u91d1\u6255\u3063\u3066\u904a\u3070\u3057\u3066\u3082\u3089\u3046\u3002\u3082\u3046\u3053\u306e\u4e0a\u306a\u3044\u8d05\u6ca2\u3002\u3067\u3082\uff0c\u5b9f\u969b\u306b\u306f\u305d\u306e\u8d05\u6ca2\u306e\u672c\u8cea\u3092\u6e80\u55ab\u3067\u304d\u308b\u4eba\u306f\u9650\u3089\u308c\u3066\u308b\u3002\u305d\u3053\u306b\u76ee\u306b\u898b\u3048\u306a\u3044\u968e\u5c64\u304c\u3042\u308b\u3068\u304a\u3082\u3046\u3002","in_reply_to_user_id_str":null,"retweet_count":0,"geo":null,"source":"web","retweeted":false,"truncated":false,"id_str":"263290764686155776","entities":{"user_mentions":[],"hashtags":[],"urls":[]},"in_reply_to_user_id":null,"in_reply_to_status_id":null,"place":null,"coordinates":null,"in_reply_to_screen_name":null,"created_at":"Tue Oct 30 14:46:24 +0000 2012","user":{"notifications":null,"contributors_enabled":false,"time_zone":"Tokyo","profile_background_color":"FFFFFF","location":"Kodaira Tokyo Japan","profile_background_tile":false,"profile_image_url_https":"https:\/\/si0.twimg.com\/profile_images\/1172376796\/70768_100000537851636_3599485_q_normal.jpg","default_profile_image":false,"follow_request_sent":null,"profile_sidebar_fill_color":"17451B","description":"KS(Green62)\/WasedaUniv.(Schl Adv Sci\/Eng)\/SynBio\/ChronoBio\/iGEM2010-2012\/Travel\/Airplane\/ \u5bfa\u30fb\u5ead\u3081\u3050\u308a","favourites_count":17,"screen_name":"Merlin_wand","profile_sidebar_border_color":"000000","id_str":"94788486","verified":false,"lang":"ja","statuses_count":8641,"profile_use_background_image":true,"protected":false,"profile_image_url":"http:\/\/a0.twimg.com\/profile_images\/1172376796\/70768_100000537851636_3599485_q_normal.jpg","listed_count":31,"geo_enabled":true,"created_at":"Sat Dec 05 13:07:32 +0000 2009","profile_text_color":"000000","name":"Marin","profile_background_image_url":"http:\/\/a0.twimg.com\/profile_background_images\/612807391\/twitter_free1.br.jpg","friends_count":629,"url":null,"id":94788486,"is_translator":false,"default_profile":false,"following":null,"profile_background_image_url_https":"https:\/\/si0.twimg.com\/profile_background_images\/612807391\/twitter_free1.br.jpg","utc_offset":32400,"profile_link_color":"ADADAD","followers_count":426},"id":263290764686155776,"contributors":null,"favorited":false} """(*[/omit]*)
let tweet = Tweet.Parse(text)

printfn "%s (retweeted %d times)\n:%s"
  tweet.User.Value.Name tweet.RetweetCount.Value tweet.Text

(**
After creating the `Tweet` type, we parse a single sample tweet and print some details about the
tweet. As you can see, the `tweet.User` property has been inferred as optional (meaning that a 
tweet might not have an author?) so we unsafely get the value using the `Value` property.
The `RetweetCount` property may also be missing and `Text` is a `string` so if it was not
available, we would get `null`.

## Related articles

 * [F# Data: Type Providers](TypeProviders.html) - gives mroe information about other
   type providers in the `FSharp.Data` package.
 * [F# Data: JSON Parser and Reader](JsonValue.html) - provides more information about 
   working with JSON values dynamically.

*)
