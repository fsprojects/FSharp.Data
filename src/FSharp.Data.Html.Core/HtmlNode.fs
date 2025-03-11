#nowarn "10001"
namespace FSharp.Data

open System
open System.ComponentModel
open System.Text

// --------------------------------------------------------------------------------------

/// <summary>Represents an HTML attribute. The name is always normalized to lowercase</summary>
/// <namespacedoc>
///   <summary>Contains the primary types for the FSharp.Data package.</summary>
/// </namespacedoc>
///
type HtmlAttribute =

    internal
    | HtmlAttribute of name: string * value: string

    /// <summary>
    /// Creates an html attribute
    /// </summary>
    /// <param name="name">The name of the attribute</param>
    /// <param name="value">The value of the attribute</param>
    static member New(name: string, value: string) =
        HtmlAttribute(name.ToLowerInvariant(), value)

[<StructuredFormatDisplay("{_Print}")>]
[<RequireQualifiedAccess>]
/// Represents an HTML node. The names of elements are always normalized to lowercase
type HtmlNode =

    internal
    | HtmlElement of name: string * attributes: HtmlAttribute list * elements: HtmlNode list
    | HtmlText of content: string
    | HtmlComment of content: string
    | HtmlCData of content: string

    /// <summary>
    /// Creates an html element
    /// </summary>
    /// <param name="name">The name of the element</param>
    static member NewElement(name: string) =
        HtmlElement(name.ToLowerInvariant(), [], [])

    /// <summary>
    /// Creates an html element
    /// </summary>
    /// <param name="name">The name of the element</param>
    /// <param name="attrs">The HtmlAttribute(s) of the element</param>
    static member NewElement(name: string, attrs: seq<_>) =
        let attrs = attrs |> Seq.map HtmlAttribute.New |> Seq.toList
        HtmlElement(name.ToLowerInvariant(), attrs, [])

    /// <summary>
    /// Creates an html element
    /// </summary>
    /// <param name="name">The name of the element</param>
    /// <param name="children">The children elements of this element</param>
    static member NewElement(name: string, children: seq<_>) =
        HtmlElement(name.ToLowerInvariant(), [], List.ofSeq children)


    /// <summary>
    /// Creates an html element
    /// </summary>
    /// <param name="name">The name of the element</param>
    /// <param name="attrs">The HtmlAttribute(s) of the element</param>
    /// <param name="children">The children elements of this element</param>
    static member NewElement(name: string, attrs: seq<_>, children: seq<_>) =
        let attrs = attrs |> Seq.map HtmlAttribute.New |> Seq.toList
        HtmlElement(name.ToLowerInvariant(), attrs, List.ofSeq children)

    /// <summary>
    /// Creates a text content element
    /// </summary>
    /// <param name="content">The actual content</param>
    static member NewText content = HtmlText(content)

    /// <summary>
    /// Creates a comment element
    /// </summary>
    /// <param name="content">The actual content</param>
    static member NewComment content = HtmlComment(content)

    /// <summary>
    /// Creates a CData element
    /// </summary>
    /// <param name="content">The actual content</param>
    static member NewCData content = HtmlCData(content)

    override x.ToString() =
        let isVoidElement =
            let set =
                [| "area"
                   "base"
                   "br"
                   "col"
                   "command"
                   "embed"
                   "hr"
                   "img"
                   "input"
                   "keygen"
                   "link"
                   "meta"
                   "param"
                   "source"
                   "track"
                   "wbr" |]
                |> Set.ofArray

            fun name -> Set.contains name set

        let rec serialize (sb: StringBuilder) indentation canAddNewLine html =
            let append (str: string) = sb.Append str |> ignore

            let appendEndTag name =
                append "</"
                append name
                append ">"

            let newLine plus =
                sb.AppendLine() |> ignore
                String(' ', indentation + plus) |> append

            match html with
            | HtmlElement(name, attributes, elements) ->
                let onlyText =
                    elements
                    |> List.forall (function
                        | HtmlText _ -> true
                        | _ -> false)

                let isPreTag = name = "pre"

                if canAddNewLine && not (onlyText || isPreTag) then
                    newLine 0

                append "<"
                append name

                for HtmlAttribute(name, value) in attributes do
                    append " "
                    append name
                    append "=\""
                    append value
                    append "\""

                if isVoidElement name then
                    append " />"
                elif elements.IsEmpty then
                    append ">"
                    appendEndTag name
                else
                    append ">"

                    if not (onlyText || isPreTag) then
                        newLine 2

                    let mutable canAddNewLine = false

                    for element in elements do
                        serialize sb (indentation + 2) canAddNewLine element
                        canAddNewLine <- true

                    if not (onlyText || isPreTag) then
                        newLine 0

                    appendEndTag name
            | HtmlText str -> append str
            | HtmlComment str ->
                append "<!--"
                append str
                append "-->"
            | HtmlCData str ->
                append "<![CDATA["
                append str
                append "]]>"

        let sb = StringBuilder()
        serialize sb 0 false x |> ignore
        sb.ToString()

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    member x._Print =
        let str = x.ToString()

        if str.Length > 512 then
            str.Substring(0, 509) + "..."
        else
            str

[<StructuredFormatDisplay("{_Print}")>]
/// Represents an HTML document
type HtmlDocument =
    internal
    | HtmlDocument of docType: string * elements: HtmlNode list

    /// <summary>
    /// Creates an html document
    /// </summary>
    /// <param name="docType">The document type specifier string</param>
    /// <param name="children">The child elements of this document</param>
    static member New(docType, children: seq<_>) =
        HtmlDocument(docType, List.ofSeq children)

    /// <summary>
    /// Creates an html document
    /// </summary>
    /// <param name="children">The child elements of this document</param>
    static member New(children: seq<_>) = HtmlDocument("", List.ofSeq children)

    override x.ToString() =
        match x with
        | HtmlDocument(docType, elements) ->
            (if String.IsNullOrEmpty docType then
                 ""
             else
                 "<!DOCTYPE " + docType + ">" + Environment.NewLine)
            + (elements |> List.map (fun x -> x.ToString()) |> String.Concat)

    /// <exclude />
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.",
                               10001,
                               IsHidden = true,
                               IsError = false)>]
    member x._Print =
        let str = x.ToString()

        if str.Length > 512 then
            str.Substring(0, 509) + "..."
        else
            str
