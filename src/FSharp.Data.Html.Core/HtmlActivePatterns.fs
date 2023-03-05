namespace FSharp.Data

[<AutoOpen>]
module HtmlActivePatterns =
    let (|HtmlElement|HtmlText|HtmlComment|HtmlCData|) (node: HtmlNode) =
        match node with
        | HtmlNode.HtmlText content -> HtmlText(content)
        | HtmlNode.HtmlComment content -> HtmlComment(content)
        | HtmlNode.HtmlCData content -> HtmlCData(content)
        | HtmlNode.HtmlElement (name, attributes, elements) -> HtmlElement(name, attributes, elements)

    let (|HtmlAttribute|) (attribute: HtmlAttribute) =
        match attribute with
        | HtmlAttribute.HtmlAttribute (name, value) -> HtmlAttribute(name, value)
