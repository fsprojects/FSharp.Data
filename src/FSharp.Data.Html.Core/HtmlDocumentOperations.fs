namespace FSharp.Data

open FSharp.Data
open System.Runtime.CompilerServices

[<AutoOpen>]
module private DocumentUtils =

    let inline toLower (s: string) = s.ToLowerInvariant()
    let inline getNameSet names = names |> Seq.map toLower |> Set.ofSeq

// --------------------------------------------------------------------------------------

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Module with operations on HTML documents
module HtmlDocument =

    /// Returns the doctype of the document
    let docType doc =
        match doc with
        | HtmlDocument(docType = docType) -> docType

    //// Gets all of the root elements of the document
    let elements doc =
        match doc with
        | HtmlDocument(elements = elements) -> elements

    /// <summary>
    /// Returns all of the root elements of the document that match the set of names
    /// </summary>
    /// <param name="names">The set of names to match</param>
    /// <param name="doc">The given document</param>
    let inline elementsNamed names doc =
        let nameSet = getNameSet names

        doc |> elements |> List.filter (HtmlNode.name >> nameSet.Contains)

    /// <summary>
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    /// <param name="doc">The given document</param>
    let inline descendants recurseOnMatch predicate doc =
        doc
        |> elements
        |> Seq.collect (HtmlNode.descendantsAndSelf recurseOnMatch predicate)

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="doc">The given document</param>
    let inline descendantsNamed recurseOnMatch names doc =
        let nameSet = getNameSet names

        doc |> descendants recurseOnMatch (HtmlNode.name >> nameSet.Contains)

    /// <summary>
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    /// <param name="doc">The given document</param>
    let inline descendantsWithPath recurseOnMatch predicate doc =
        doc
        |> elements
        |> Seq.collect (HtmlNode.descendantsAndSelfWithPath recurseOnMatch predicate)

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="doc">The given document</param>
    let inline descendantsNamedWithPath recurseOnMatch names doc =
        let nameSet = getNameSet names

        doc |> descendantsWithPath recurseOnMatch (HtmlNode.name >> nameSet.Contains)

    /// <summary>
    /// Finds the body element of the given document,
    /// this throws an exception if no body element exists.
    /// </summary>
    /// <param name="x">The given document</param>
    let inline body (x: HtmlDocument) =
        match List.ofSeq <| descendantsNamed false [ "body" ] x with
        | [] -> failwith "No element body found!"
        | body :: _ -> body

    /// <summary>
    /// Tries to find the body element of the given document.
    /// </summary>
    /// <param name="x">The given document</param>
    let inline tryGetBody (x: HtmlDocument) =
        match List.ofSeq <| descendantsNamed false [ "body" ] x with
        | [] -> None
        | body :: _ -> Some body

    /// <summary>
    /// Finds the html element of the given document,
    /// this throws an exception if no html element exists.
    /// </summary>
    /// <param name="x">The given document</param>
    let inline html (x: HtmlDocument) =
        match List.ofSeq <| descendantsNamed false [ "html" ] x with
        | [] -> failwith "No element html found!"
        | html :: _ -> html

    /// <summary>
    /// Tries to find the html element of the given document.
    /// </summary>
    /// <param name="x">The given document</param>
    let inline tryGetHtml (x: HtmlDocument) =
        match List.ofSeq <| descendantsNamed false [ "html" ] x with
        | [] -> None
        | html :: _ -> Some html


[<Extension>]
/// Extension methods with operations on HTML documents
type HtmlDocumentExtensions =

    /// <summary>
    /// Returns all of the root elements of the current document
    /// </summary>
    /// <param name="doc">The given document</param>
    [<Extension>]
    static member Elements(doc: HtmlDocument) = HtmlDocument.elements doc

    /// <summary>
    /// Returns all of the root elements in the current document that match the set of names
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="names">The set of names to match</param>
    [<Extension>]
    static member Elements(doc: HtmlDocument, names: seq<string>) = HtmlDocument.elementsNamed names doc

    /// <summary>
    /// Returns all of the root elements in the current document that match the name
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="name">The name to match</param>
    [<Extension>]
    static member Elements(doc: HtmlDocument, name: string) = HtmlDocument.elementsNamed [ name ] doc

    /// <summary>
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member Descendants(doc: HtmlDocument, predicate, recurseOnMatch) =
        HtmlDocument.descendants recurseOnMatch predicate doc

    /// <summary>
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Recurses on match
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    [<Extension>]
    static member Descendants(doc: HtmlDocument, predicate) =
        let recurseOnMatch = true
        HtmlDocument.descendants recurseOnMatch predicate doc

    /// Gets all of the descendants of this document
    /// Recurses on match
    [<Extension>]
    static member Descendants(doc: HtmlDocument) =
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlDocument.descendants recurseOnMatch predicate doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member Descendants(doc: HtmlDocument, names: seq<string>, recurseOnMatch) =
        HtmlDocument.descendantsNamed recurseOnMatch names doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Recurses on match
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="names">The set of names to match</param>
    [<Extension>]
    static member Descendants(doc: HtmlDocument, names: seq<string>) =
        let recurseOnMatch = true
        HtmlDocument.descendantsNamed recurseOnMatch names doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given name
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="name">The name to match</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member Descendants(doc: HtmlDocument, name: string, recurseOnMatch) =
        HtmlDocument.descendantsNamed recurseOnMatch [ name ] doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given name
    /// Recurses on match
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="name">The name to match</param>
    [<Extension>]
    static member Descendants(doc: HtmlDocument, name: string) =
        let recurseOnMatch = true
        HtmlDocument.descendantsNamed recurseOnMatch [ name ] doc

    /// <summary>
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsWithPath(doc: HtmlDocument, predicate, recurseOnMatch) =
        HtmlDocument.descendantsWithPath recurseOnMatch predicate doc

    /// <summary>
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Recurses on match
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    [<Extension>]
    static member DescendantsWithPath(doc: HtmlDocument, predicate) =
        let recurseOnMatch = true
        HtmlDocument.descendantsWithPath recurseOnMatch predicate doc

    /// Gets all of the descendants of this document
    /// Recurses on match
    [<Extension>]
    static member DescendantsWithPath(doc: HtmlDocument) =
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlDocument.descendantsWithPath recurseOnMatch predicate doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsWithPath(doc: HtmlDocument, names: seq<string>, recurseOnMatch) =
        HtmlDocument.descendantsNamedWithPath recurseOnMatch names doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Recurses on match
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="names">The set of names to match</param>
    [<Extension>]
    static member DescendantsWithPath(doc: HtmlDocument, names: seq<string>) =
        let recurseOnMatch = true
        HtmlDocument.descendantsNamedWithPath recurseOnMatch names doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given name
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="name">The name to match</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsWithPath(doc: HtmlDocument, name: string, recurseOnMatch) =
        HtmlDocument.descendantsNamedWithPath recurseOnMatch [ name ] doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given name
    /// Recurses on match
    /// </summary>
    /// <param name="doc">The given document</param>
    /// <param name="name">The name to match</param>
    [<Extension>]
    static member DescendantsWithPath(doc: HtmlDocument, name: string) =
        let recurseOnMatch = true
        HtmlDocument.descendantsNamedWithPath recurseOnMatch [ name ] doc

    /// Finds the body element of the given document,
    /// this throws an exception if no body element exists.
    [<Extension>]
    static member Body(doc: HtmlDocument) = HtmlDocument.body doc

    /// Tries to find the body element of the given document.
    [<Extension>]
    static member TryGetBody(doc: HtmlDocument) = HtmlDocument.tryGetBody doc

    /// Finds the html element of the given document,
    /// this throws an exception if no html element exists.
    [<Extension>]
    static member Html(doc: HtmlDocument) = HtmlDocument.html doc

    /// Tries to find the html element of the given document.
    [<Extension>]
    static member TryGetHtml(doc: HtmlDocument) = HtmlDocument.tryGetHtml doc

// --------------------------------------------------------------------------------------

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Provides the dynamic operator for getting attribute values from HTML elements
module HtmlExtensions =

    /// Gets the value of an attribute from an HTML element
    let (?) (node: HtmlNode) name = HtmlNode.attributeValue name node
