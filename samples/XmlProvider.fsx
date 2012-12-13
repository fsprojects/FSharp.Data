(** 
# F# Data: XML Type Provider

*)

#r "../bin/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
open System.IO
open FSharp.Data

type Author = XmlProvider<"""<author name="Paul Feyerabend" born="1924" />""">
let sample = Author.Parse("""<author name="Karl Popper" born="1902" />""")
sample.Name
sample.Born

XmlProvider<"<a>123</a>">.Parse("<a>123</a>")

type AuthorAlt = XmlProvider<"<author><name>Karl Popper</name><born>1902</born></author>">
let res = AuthorAlt.Parse("<author><name>Paul Feyerabend</name><born>1924</born></author>")
res.Name
res.Born

let [<Literal>] test = "<a><foo>1</foo><foo>3</foo></a>"
type Test = XmlProvider<test>
for foo in Test.Parse(test).GetFoos() do
  printfn "%A" foo


let [<Literal>] authors = """
  <authors topic="Philosophy of Science">
    <author name="Paul Feyerabend" born="1924" />
    <author name="Thomas Kuhn" />
  </authors> """

type Authors = XmlProvider<authors>
let list = Authors.Parse(authors)

printfn "%s" list.Topic
for author in list.GetAuthors() do
  printf " - %s" author.Name 
  author.Born |> Option.iter (printf " (%d)")
  printfn ""

type Rss = XmlProvider<"http://tomasp.net/blog/rss.aspx">
let wc = new System.Net.WebClient()
let blog = Rss.Parse(wc.DownloadString("http://tomasp.net/blog/rss.aspx"))

printfn "%s" blog.Channel.Title

for item in blog.Channel.GetItems() do
  printfn " - %s (%s)\n %s\n" item.Title item.Link item.Description

(**

## Related articles

 * [F# Data: Type Providers](TypeProviders.html) - gives mroe information about other
   type providers in the `FSharp.Data` package.
 * [F# Data: JSON Parser and Reader](JsonValue.html) - provides more information about 
   working with JSON values dynamically.

*)
