(** 
# F# Data: XML Type Provider

This article demonstrates how to use the XML type provider to access XML documents
in a statically typed way. We first look how the structure is infered and then 
demonstrate the provider by parsing RSS feed.

## Introducing the provider

As discussed in the [Type Providers introduction](FSharpData.html), the 
type provider can be loaded using the following commands
(note we also need a reference to `System.Xml.Linq`, because the provider uses the
`XDocument` type under the cover): *)

#r "../bin/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
open System.IO
open FSharp.Data

(**
### Inferring type from sample

As also discussed in the [introduction](FSharpData.html), the `XmlProvider` type
is parameterized by a `string` which is _either_ a sample XML string _or_ a file location
or URL. The following sample generates a type that can read simple XML documents with a root node
containing a two attributes:
*)

type Author = XmlProvider<"""<author name="Paul Feyerabend" born="1924" />""">
let sample = Author.Parse("""<author name="Karl Popper" born="1902" />""")

printfn "%s (%d)" sample.Name sample.Born

(**
The type provider generates a type `Author` that has properties corresponding to the
attributes of the root element of the XML document. The types of the properties are 
infered based on the values in the sample document. In this case, the `Name` property
has a type `string` and `Born` is `int`.

XML is quite flexible format, so we could represent the same document differently.
Instead of using attributes, we could use nested nodes (`<name>` and `<born>` nested
under `<author>`) that directly contain the values:*)

type AuthorAlt = XmlProvider<"<author><name>Karl Popper</name><born>1902</born></author>">
let doc = "<author><name>Paul Feyerabend</name><born>1924</born></author>"
let sampleAlt = AuthorAlt.Parse(doc)

printfn "%s (%d)" sampleAlt.Name sampleAlt.Born

(**
The generated type provides exactly the same API for reading documents following this
convention (Note that you cannot use `AuthorAlt` to parse samples that use the
first style - the implementation of the types differs, they just provide the same public API.) 

The provider turns a node into a simply typed property only when the node contains just
a primitive value and has no children or attributes. 

### Types for more complex structure

Let's now look at a number of exmaples that have more interesting structrue. First of 
all, what if a node contains some value, but also has some attributes?
*)

type Detailed = XmlProvider<"""<author><name full="true">Karl Popper</name></author>""">
let info = Detailed.Parse("""<author><name full="false">Thomas Kuhn</name></author>""")

printfn "%s (full=%b)" info.Name.Value info.Name.Full

(**
If the node cannot be represented as a simple type (like `string`) then the provider
builds a new type with multiple properties. Here, it generates a property `Full` 
(based on the name of the attribute) and infers its type to be boolean. Then it
adds property with a (special) name `Value` that returns the content of the element.

### Types for multiple simple elements

Another interesting case is when there are multiple nodes that contain just a 
primitive value. The following example shows what happens when the root node
contains multiple `<value>` nodes (we use the `Literal` attribtue so that we
do not need to repeat the input string):
*)

let [<Literal>] test = "<root><value>1</value><value>3</value></root>"
type Test = XmlProvider<test>

Test.Parse(test).GetValues()
|> Seq.iter (printfn "%d")

(**
The type provider generates a method `GetValue` that returns an array with the
values - as the `<value>` nodes do not contain any attributes or children, they
are turned into `int` values and so the `GetValues()` method returns just `int[]`!

## Processing philosophers

The following example demonstrates how the type provider works on a simple 
document that lists authors that write about a specific topic. The following snippet
defines the sampel input (as `Literal` so that we can pass it to the type provider):
*)

let [<Literal>] authors = """
  <authors topic="Philosophy of Science">
    <author name="Paul Feyerabend" born="1924" />
    <author name="Thomas Kuhn" />
  </authors> """

(**
Note that the `born` attribute is only available for some of the authors. Using a
type provider, we can print the file input as follows:
*)

type Authors = XmlProvider<authors>
let topic = Authors.Parse(authors)

printfn "%s" topic.Topic
for author in topic.GetAuthors() do
  printf " - %s" author.Name 
  author.Born |> Option.iter (printf " (%d)")
  printfn ""

(**
The value `topic` has a property `Topic` (of type `string`) which returns the value
of the attribute with the same name. It also has a method `GetAuthors()` that returns
a collection with all the authors. The `Born` property is missing for some authors,
so it becomes `option<int>` and we need to print it using `Option.iter`.

## Reading RSS feeds

To conclude this introduction with a more interesting example, let's look how to parse a
RSS feed. As discussed earlier, we can use relative paths or web addresses when calling
the type provider:
*)

type Rss = XmlProvider<"http://tomasp.net/blog/rss.aspx">

(**
This code builds a type `Rss` that represents RSS feed (with the features that are used
on `http://tomasp.net`). The type `Rss` provides static methods `Parse` and `Load`
to construct it - here, we need to use `Parse` again, because `Load` only works for 
files:
*)

let wc = new System.Net.WebClient()
let blog = Rss.Parse(wc.DownloadString("http://tomasp.net/blog/rss.aspx"))

(**
Printing the title of the RSS feed together with a list of recent posts is now quite
easy - you can simply type `blog` followed by `.` and see what the autocompletion
offers. The code looks like this:
*)

// Title is a property returning string 
printfn "%s" blog.Channel.Title

// Get all item nodes and print title with link
for item in blog.Channel.GetItems() do
  printfn " - %s (%s)" item.Title item.Link

(**

## Related articles

 * [F# Data: Type Providers](FSharpData.html) - gives mroe information about other
   type providers in the `FSharp.Data` package.

*)