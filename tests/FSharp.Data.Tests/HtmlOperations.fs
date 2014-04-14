#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../bin/FSharp.Data.Experimental.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#r "System.Xml.Linq.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Data.Tests.HtmlOperations
#endif

open NUnit.Framework
open FsUnit
open System
open FSharp.Data
open FSharp.Data.Html

[<Test>]
let ``Can get the name of a HtmlAttribute``() = 
    let attr = HtmlAttribute("id", "table_1")
    HtmlAttribute.name attr |> should equal "id"

[<Test>]
let ``Can get the value of a HtmlAttribute``() = 
    let attr = HtmlAttribute("id", "table_1")
    HtmlAttribute.value attr |> should equal "table_1"

[<Test>]
let ``Can parse the value of a HtmlAttribute to the correct type``() =
    let attr = HtmlAttribute("cost", "59.99")
    HtmlAttribute.parseValue Decimal.Parse attr |> should equal 59.99M

[<Test>]
let ``Can tryParse the value of a HtmlAttribute to the correct type``() =
    let attr = HtmlAttribute("cost", "59.99")
    HtmlAttribute.tryParseValue 0M Decimal.TryParse attr |> should equal 59.99M

[<Test>]
let ``If tryParse HtmlAttribute failes it should return the defaultValue``() =
    let attr = HtmlAttribute("cost", "59.99")
    HtmlAttribute.tryParseValue 0M (fun _ -> false, 100M) attr |> should equal 0M



