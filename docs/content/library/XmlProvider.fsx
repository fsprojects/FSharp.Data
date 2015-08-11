(** 
# F# Data: XML Type Provider

This article demonstrates how to use the XML Type Provider to access XML documents
in a statically typed way. We first look at how the structure is inferred and then 
demonstrate the provider by parsing a RSS feed.

The XML Type Provider provides statically typed access to XML documents.
It takes a sample document as an input (or document containing a root XML node with
multiple child nodes that are used as samples). The generated type can then be used 
to read files with the same structure. If the loaded file does not match the structure 
of the sample, a runtime error may occur (but only when accessing e.g. non-existing element).

## Introducing the provider

The type provider is located in the `FSharp.Data.dll` assembly. Assuming the assembly 
is located in the `../../bin` directory, we can load it in F# Interactive as follows:
(note we also need a reference to `System.Xml.Linq`, because the provider uses the
`XDocument` type internally): *)

#r "../../../bin/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
open FSharp.Data

(**
### Inferring type from sample

The `XmlProvider<...>` takes one static parameter of type `string`. The parameter can 
be _either_ a sample XML string _or_ a sample file (relative to the current folder or online 
accessible via `http` or `https`). It is not likely that this could lead to ambiguities. 

The following sample generates a type that can read simple XML documents with a root node
containing two attributes:
*)

type Author = XmlProvider<"""<author name="Paul Feyerabend" born="1924" />""">
let sample = Author.Parse("""<author name="Karl Popper" born="1902" />""")

printfn "%s (%d)" sample.Name sample.Born

(**
The type provider generates a type `Author` that has properties corresponding to the
attributes of the root element of the XML document. The types of the properties are 
inferred based on the values in the sample document. In this case, the `Name` property
has a type `string` and `Born` is `int`.

XML is a quite flexible format, so we could represent the same document differently.
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

Now let's look at a number of examples that have more interesting structure. First of 
all, what if a node contains some value, but also has some attributes?
*)

type Detailed = XmlProvider<"""<author><name full="true">Karl Popper</name></author>""">
let info = Detailed.Parse("""<author><name full="false">Thomas Kuhn</name></author>""")

printfn "%s (full=%b)" info.Name.Value info.Name.Full

(**
If the node cannot be represented as a simple type (like `string`) then the provider
builds a new type with multiple properties. Here, it generates a property `Full` 
(based on the name of the attribute) and infers its type to be boolean. Then it
adds a property with a (special) name `Value` that returns the content of the element.

### Types for multiple simple elements

Another interesting case is when there are multiple nodes that contain just a 
primitive value. The following example shows what happens when the root node
contains multiple `<value>` nodes (note that if we leave out the parameter to the 
`Parse` method, the same text used for the schema will be used as the runtime value).
*)

type Test = XmlProvider<"<root><value>1</value><value>3</value></root>">

Test.GetSample().Values
|> Seq.iter (printfn "%d")

(**
The type provider generates a property `Values` that returns an array with the
values - as the `<value>` nodes do not contain any attributes or children, they
are turned into `int` values and so the `Values` property returns just `int[]`!

## Processing philosophers

In this section we look at an example that demonstrates how the type provider works 
on a simple document that lists authors that write about a specific topic. The 
sample document [`data/Writers.xml`](../data/Writers.xml) looks as follows:

    [lang=xml]
    <authors topic="Philosophy of Science">
      <author name="Paul Feyerabend" born="1924" />
      <author name="Thomas Kuhn" />
    </authors> 

At runtime, we use the generated type provider to parse the following string
(which has the same structure as the sample document with the exception that 
one of the `author` nodes also contains a `died` attribute):
*)

let authors = """
  <authors topic="Philosophy of Mathematics">
    <author name="Bertrand Russell" />
    <author name="Ludwig Wittgenstein" born="1889" />
    <author name="Alfred North Whitehead" died="1947" />
  </authors> """

(**
When initializing the `XmlProvider`, we can pass it a file name or a web URL.
The `Load` and `AsyncLoad` methods allows reading the data from a file or from a web resource. The
`Parse` method takes the data as a string, so we can now print the information as follows:
*)

type Authors = XmlProvider<"../data/Writers.xml">
let topic = Authors.Parse(authors)

printfn "%s" topic.Topic
for author in topic.Authors do
  printf " - %s" author.Name 
  author.Born |> Option.iter (printf " (%d)")
  printfn ""

(**
The value `topic` has a property `Topic` (of type `string`) which returns the value
of the attribute with the same name. It also has a property `Authors` that returns
an array with all the authors. The `Born` property is missing for some authors,
so it becomes `option<int>` and we need to print it using `Option.iter`.

The `died` attribute was not present in the sample used for the inference, so we
cannot obtain it in a statically typed way (although it can still be obtained
dynamically using `author.XElement.Attribute(XName.Get("died"))`).

## Global inference mode

In the examples shown earlier, an element was never (recursively) contained in an
element of the same name (for example `<author>` never contained another `<author>`).
However, when we work with documents such as XHTML files, this can often be the case.
Consider for example, the following sample (a simplified version of 
[`data/HtmlBody.xml`](../data/HtmlBody.xml)):

    [lang=xml]
    <div id="root">
      <span>Main text</span>
      <div id="first">
        <div>Second text</div>
      </div>
    </div>

Here, a `<div>` element can contain other `<div>` elements and it is quite clear that
they should all have the same type - we want to be able to write a recursive function
that processes `<div>` elements. To make this possible, you need to set an optional
parameter `Global` to `true`:
*)

type Html = XmlProvider<"../data/HtmlBody.xml", Global=true>
let html = Html.GetSample()

(**
When the `Global` parameter is `true`, the type provider _unifies_ all elements of the
same name. This means that all `<div>` elements have the same type (with a union
of all attributes and all possible children nodes that appear in the sample document).

The type is located under a type `Html`, so we can write a `printDiv` function
that takes `Html.Div` and acts as follows:
*)

/// Prints the content of a <div> element
let rec printDiv (div:Html.Div) =
  div.Spans |> Seq.iter (printfn "%s")
  div.Divs |> Seq.iter printDiv
  if div.Spans.Length = 0 && div.Divs.Length = 0 then
      div.Value |> Option.iter (printfn "%s")

// Print the root <div> element with all children  
printDiv html

(**

The function first prints all text included as `<span>` (the element never has any
attributes in our sample, so it is inferred as `string`), then it recursively prints
the content of all `<div>` elements. If the element does not contain nested elements,
then we print the `Value` (inner text).

## Reading RSS feeds

To conclude this introduction with a more interesting example, let's look how to parse a
RSS feed. As discussed earlier, we can use relative paths or web addresses when calling
the type provider:
*)

type Rss = XmlProvider<"http://tomasp.net/blog/rss.aspx">

(**
This code builds a type `Rss` that represents RSS feeds (with the features that are used
on `http://tomasp.net`). The type `Rss` provides static methods `Parse`, `Load` and `AsyncLoad`
to construct it - here, we just want to reuse the same URI of the schema, so we
use the `GetSample` static method:
*)

let blog = Rss.GetSample()

(**
Printing the title of the RSS feed together with a list of recent posts is now quite
easy - you can simply type `blog` followed by `.` and see what the autocompletion
offers. The code looks like this:
*)

// Title is a property returning string 
printfn "%s" blog.Channel.Title

// Get all item nodes and print title with link
for item in blog.Channel.Items do
  printfn " - %s (%s)" item.Title item.Link

(**

## Transforming XML

In this example we will now also create XML in addition to consuming it.
Consider the problem of flattening a data set. Let's say you have xml data that looks like this:
*)

[<Literal>]
let customersXmlSample = """
  <Customers>
    <Customer name="ACME">
      <Order Number="A012345">
        <OrderLine Item="widget" Quantity="1"/>
      </Order>
      <Order Number="A012346">
        <OrderLine Item="trinket" Quantity="2"/>
      </Order>
    </Customer>
    <Customer name="Southwind">
      <Order Number="A012347">
        <OrderLine Item="skyhook" Quantity="3"/>
        <OrderLine Item="gizmo" Quantity="4"/>
      </Order>
    </Customer>
  </Customers>"""

(**
and you want to transform it into something like this:
*)

[<Literal>]
let orderLinesXmlSample = """
  <OrderLines>
    <OrderLine Customer="ACME" Order="A012345" Item="widget" Quantity="1"/>
    <OrderLine Customer="ACME" Order="A012346" Item="trinket" Quantity="2"/>
    <OrderLine Customer="Southwind" Order="A012347" Item="skyhook" Quantity="3"/>
    <OrderLine Customer="Southwind" Order="A012347" Item="gizmo" Quantity="4"/>
  </OrderLines>"""

(**
We'll create types from both the input and output samples and use the constructors on the types generated by the XmlProvider:
*)

type InputXml = XmlProvider<customersXmlSample>
type OutputXml = XmlProvider<orderLinesXmlSample>

let orderLines = 
  OutputXml.OrderLines [|
    for customer in InputXml.GetSample().Customers do
      for order in customer.Orders do
        for line in order.OrderLines do
          yield OutputXml.OrderLine(customer.Name,
                                    order.Number,
                                    line.Item,
                                    line.Quantity) |]

(**
## Related articles

 * [Using JSON provider in a library](JsonProvider.html#jsonlib) also applies to XML type provider
 * [API Reference: XmlProvider type provider](../reference/fsharp-data-xmlprovider.html)
 * [API Reference: XElementExtensions module](../reference/fsharp-data-xelementextensions.html)

*)