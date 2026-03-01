namespace FSharp.Data
#if HIDE_REPRESENTATION
[<AutoOpen>]
#endif
/// Active patterns for decomposing HtmlNode and HtmlAttribute values
module HtmlActivePatterns =

    /// <summary>
    /// Active pattern that decomposes an <see cref="HtmlNode"/> into one of four cases:
    /// HtmlElement (name, attributes, child elements), HtmlText (text content),
    /// HtmlComment (comment content), or HtmlCData (CDATA content).
    /// </summary>
    let (|HtmlElement|HtmlText|HtmlComment|HtmlCData|) (node: HtmlNode) =
        match node with
        | HtmlNode.HtmlText content -> HtmlText(content)
        | HtmlNode.HtmlComment content -> HtmlComment(content)
        | HtmlNode.HtmlCData content -> HtmlCData(content)
        | HtmlNode.HtmlElement(name, attributes, elements) -> HtmlElement(name, attributes, elements)

    /// <summary>
    /// Active pattern that decomposes an <see cref="HtmlAttribute"/> into its name and value.
    /// </summary>
    let (|HtmlAttribute|) (attribute: HtmlAttribute) =
        match attribute with
        | HtmlAttribute.HtmlAttribute(name, value) -> HtmlAttribute(name, value)
