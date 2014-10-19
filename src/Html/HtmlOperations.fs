namespace FSharp.Data

open System
open FSharp.Data
open System.Runtime.CompilerServices

[<AutoOpen>]
module private Utils =

    let inline toLower (s:string) = s.ToLowerInvariant()
    let inline getNameSet names = names |> Seq.map toLower |> Set.ofSeq

// --------------------------------------------------------------------------------------

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HtmlAttribute = 

    /// Gets the name of the given attribute
    let name attr = 
        match attr with
        | HtmlAttribute(name = name) -> name

    /// Gets the value of the given attribute
    let value attr = 
        match attr with
        | HtmlAttribute(value = value) -> value   

// --------------------------------------------------------------------------------------

[<Extension>]
type HtmlAttributeExtensions =

    /// Gets the name of the current attribute
    [<Extension>]
    static member Name(attr:HtmlAttribute) = 
        HtmlAttribute.name attr

    /// Gets the value of the current attribute
    [<Extension>]
    static member Value(attr:HtmlAttribute) = 
        HtmlAttribute.value attr

// --------------------------------------------------------------------------------------

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HtmlNode =

    /// Gets the given nodes name
    let name n =
        match n with
        | HtmlElement(name = name) -> name
        | _ -> String.Empty
        
    /// Gets all of the nodes immediately under this node
    let elements n =
        match n with
        | HtmlElement(elements = elements) -> elements
        | _ -> []

    /// <summary>
    /// Finds all of the elements nodes of this node that match the given set of names
    /// </summary>
    /// <param name="names">The set of names to match</param>
    /// <param name="n">The given node</param>
    let inline elementsNamed names n = 
        let nameSet = getNameSet names
        n |> elements |> List.filter (name >> nameSet.Contains)

    /// <summary>
    /// Gets all of the descendants of this node that statisfy the given predicate
    /// </summary>
    /// <param name="includeSelf">Include the current node</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    /// <param name="n">The given node</param>
    let descendants includeSelf recurseOnMatch predicate n = 
        let rec descendantsBy includeSelf n =
            [
                let proceed = ref true
                if includeSelf && predicate n then
                    yield n
                    if not recurseOnMatch then
                        proceed := false
                if !proceed then
                    for element in elements n do
                        yield! descendantsBy true element
            ]
        descendantsBy includeSelf n
    
    /// <summary>
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// </summary>
    /// <param name="includeSelf">Include the current node</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="n">The given node</param>
    let inline descendantsNamed includeSelf recurseOnMatch names n = 
        let nameSet = getNameSet names
        n |> descendants includeSelf recurseOnMatch (name >> nameSet.Contains)
        
    /// <summary>
    /// Gets all of the descendants of this node that statisfy the given predicate
    /// </summary>
    /// <param name="includeSelf">Include the current node</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    /// <param name="n">The given node</param>
    let descendantsWithPath includeSelf recurseOnMatch predicate n = 
        let rec descendantsBy includeSelf path n =
            [
                let proceed = ref true
                if includeSelf && predicate n then
                    yield n, path
                    if not recurseOnMatch then
                        proceed := false
                if !proceed then
                    for element in elements n do
                        yield! descendantsBy true (n::path) element
            ]
        descendantsBy includeSelf [] n
    
    /// <summary>
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// </summary>
    /// <param name="includeSelf">Include the current node</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="n">The given node</param>
    let inline descendantsNamedWithPath includeSelf recurseOnMatch names n = 
        let nameSet = getNameSet names
        n |> descendantsWithPath includeSelf recurseOnMatch (name >> nameSet.Contains)

    /// Gets all of the attributes of this node
    let attributes n =
        match n with
        | HtmlElement(attributes = attributes) -> attributes
        | _ -> []

    /// <summary>
    /// Tries to return an attribute that exists on the current node
    /// </summary>
    /// <param name="name">The name of the attribute to return.</param>
    let inline tryGetAttribute name n =
        n |> attributes |> List.tryFind (HtmlAttribute.name >> ((=) (toLower name)))
    
    /// <summary>
    /// Returns the attribute with the given name. If the
    /// attribute does not exist then this will throw an exception
    /// </summary>
    /// <param name="name">The name of the attribute to select</param>
    /// <param name="n">The given node</param>
    let inline attribute name n = 
        match tryGetAttribute name n with
        | Some v -> v
        | None -> failwithf "Unable to find attribute (%s)" name

    /// <summary>
    /// Return the value of the named attribute, or an empty string if not found.
    /// </summary>
    /// <param name="name">The name of the attribute to get the value from</param>
    /// <param name="n">The given node</param>
    let inline attributeValue name n = 
        defaultArg (n |> tryGetAttribute name |> Option.map HtmlAttribute.value) ""

    /// <summary>
    /// Returns true if the current node has an attribute that
    /// matches both the name and the value
    /// </summary>
    /// <param name="name">The name of the attribute</param>
    /// <param name="value">The value of the attribute</param>
    /// <param name="x">The given html node</param>
    let inline hasAttribute name value n = 
        match tryGetAttribute name n with
        | Some attr -> toLower (HtmlAttribute.value attr) = toLower value
        | None -> false

    /// Returns true if the current node has the specified id
    let inline hasId id n = 
        hasAttribute "id" id n

    /// Returns true if the current node has the specified class
    let inline hasClass cssClass n = 
        hasAttribute "class" cssClass n

    let innerTextExcluding exclusions n = 
        let exclusions = "style" :: "script" :: exclusions
        let rec innerText' = function
            | HtmlElement(name, _, content) when exclusions |> List.forall ((<>) name) ->
                seq { for e in content do
                        match e with
                        | HtmlText(text) -> yield text
                        | HtmlComment(_) -> yield String.Empty
                        | elem -> yield innerText' elem }
                |> String.Concat
            | HtmlText(text) -> text
            | _ -> String.Empty
        innerText' n

    /// <summary>
    /// Returns the inner text of the current node
    /// </summary>
    /// <param name="n">The given node</param>
    let inline innerText n = 
        innerTextExcluding [] n

// --------------------------------------------------------------------------------------

[<Extension>]
type HtmlNodeExtensions =
               
    /// Gets the given nodes name
    [<Extension>]
    static member Name(n:HtmlNode) = 
        HtmlNode.name n
        
    /// Gets all of the nodes immediately under this node
    [<Extension>]
    static member Elements(n:HtmlNode) = 
        HtmlNode.elements n

    /// <summary>
    /// Gets all of the elements of the current node, which match the given set of names
    /// </summary>
    /// <param name="names">The set of names by which to map the elements</param>
    [<Extension>]
    static member Elements(n:HtmlNode, names:seq<string>) = 
        HtmlNode.elementsNamed names n

    /// <summary>
    /// Gets all of the elements of the current node, which match the given name
    /// </summary>
    /// <param name="names">The name by which to map the elements</param>
    [<Extension>]
    static member Elements(n:HtmlNode, name:string) = 
        HtmlNode.elementsNamed [name] n
        
    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// </summary>
    /// <param name="predicate">The predicate for which descendants to return</param>
    /// <param name="includeSelf">Include the current node</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member Descendants(n:HtmlNode, predicate, includeSelf, recurseOnMatch) = 
        HtmlNode.descendants includeSelf recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Recurses on match
    /// </summary>
    /// <param name="predicate">The predicate for which descendants to return</param>
    /// <param name="includeSelf">Include the current node</param>
    [<Extension>]
    static member Descendants(n:HtmlNode, predicate, includeSelf) = 
        let recurseOnMatch = true
        HtmlNode.descendants includeSelf recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Recurses on match
    /// Doesn't include self
    /// </summary>
    /// <param name="predicate">The predicate for which descendants to return</param>
    [<Extension>]
    static member Descendants(n:HtmlNode, predicate) = 
        let includeSelf = false
        let recurseOnMatch = true
        HtmlNode.descendants includeSelf recurseOnMatch predicate n

    /// Gets all of the descendants of the current node
    /// Recurses on match
    /// Doesn't include self
    [<Extension>]
    static member Descendants(n:HtmlNode) = 
        let includeSelf = false
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlNode.descendants includeSelf recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// </summary>
    /// <param name="names">The set of names by which to map the descendants</param>
    /// <param name="includeSelf">Include the current node</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member Descendants(n:HtmlNode, names:seq<string>, includeSelf, recurseOnMatch) = 
        HtmlNode.descendantsNamed includeSelf recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// Recurses on match
    /// </summary>
    /// <param name="names">The set of names by which to map the descendants</param>
    /// <param name="includeSelf">Include the current node</param>
    [<Extension>]
    static member Descendants(n:HtmlNode, names:seq<string>, includeSelf) = 
        let recurseOnMatch = true
        HtmlNode.descendantsNamed includeSelf recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// Recurses on match
    /// Doesn't include self
    /// </summary>
    /// <param name="names">The set of names by which to map the descendants</param>
    [<Extension>]
    static member Descendants(n:HtmlNode, names:seq<string>) = 
        let includeSelf = false
        let recurseOnMatch = true
        HtmlNode.descendantsNamed includeSelf recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// </summary>
    /// <param name="name">The name by which to map the descendants</param>
    /// <param name="includeSelf">Include the current node</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member Descendants(n:HtmlNode, name:string, includeSelf, recurseOnMatch) = 
        HtmlNode.descendantsNamed includeSelf recurseOnMatch [name] n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// Recurses on match
    /// </summary>
    /// <param name="name">The name by which to map the descendants</param>
    /// <param name="includeSelf">Include the current node</param>
    [<Extension>]
    static member Descendants(n:HtmlNode, name:string, includeSelf) = 
        let recurseOnMatch = true
        HtmlNode.descendantsNamed includeSelf recurseOnMatch [name] n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// Recurses on match
    /// Doesn't include self
    /// </summary>
    /// <param name="name">The name by which to map the descendants</param>
    [<Extension>]
    static member Descendants(n:HtmlNode, name:string) = 
        let recurseOnMatch = true
        let includeSelf = false
        HtmlNode.descendantsNamed includeSelf recurseOnMatch [name] n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// </summary>
    /// <param name="predicate">The predicate for which descendants to return</param>
    /// <param name="includeSelf">Include the current node</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, predicate, includeSelf, recurseOnMatch) = 
        HtmlNode.descendantsWithPath includeSelf recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Recurses on match
    /// </summary>
    /// <param name="predicate">The predicate for which descendants to return</param>
    /// <param name="includeSelf">Include the current node</param>
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, predicate, includeSelf) = 
        let recurseOnMatch = true
        HtmlNode.descendantsWithPath includeSelf recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Recurses on match
    /// Doesn't include self
    /// </summary>
    /// <param name="predicate">The predicate for which descendants to return</param>
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, predicate) = 
        let recurseOnMatch = true
        let includeSelf = false
        HtmlNode.descendantsWithPath includeSelf recurseOnMatch predicate n

    /// Gets all of the descendants of the current node
    /// Recurses on match
    /// Doesn't include self
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode) = 
        let recurseOnMatch = true
        let includeSelf = false
        let predicate = fun _ -> true
        HtmlNode.descendantsWithPath includeSelf recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// </summary>
    /// <param name="names">The set of names by which to map the descendants</param>
    /// <param name="includeSelf">Include the current node</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, names:seq<string>, includeSelf, recurseOnMatch) = 
        HtmlNode.descendantsNamedWithPath includeSelf recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// Recurses on match
    /// </summary>
    /// <param name="names">The set of names by which to map the descendants</param>
    /// <param name="includeSelf">Include the current node</param>
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, names:seq<string>, includeSelf) = 
        let recurseOnMatch = true
        HtmlNode.descendantsNamedWithPath includeSelf recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// Recurses on match
    /// Doesn't include self
    /// </summary>
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, names:seq<string>) = 
        let recurseOnMatch = true
        let includeSelf = false
        HtmlNode.descendantsNamedWithPath includeSelf recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// </summary>
    /// <param name="name">The name by which to map the descendants</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="includeSelf">Include the current node</param>
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, name:string, includeSelf, recurseOnMatch) = 
        HtmlNode.descendantsNamedWithPath includeSelf recurseOnMatch [name] n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// Recurses on match
    /// </summary>
    /// <param name="name">The names by which to map the descendants</param>
    /// <param name="includeSelf">Include the current node</param>
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, name:string, includeSelf) = 
        let recurseOnMatch = true
        HtmlNode.descendantsNamedWithPath includeSelf recurseOnMatch [name] n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// Recurses on match
    /// Doesn't include self
    /// </summary>
    /// <param name="name">The names by which to map the descendants</param>
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, name:string) = 
        let recurseOnMatch = true
        let includeSelf = false
        HtmlNode.descendantsNamedWithPath includeSelf recurseOnMatch [name] n

    /// Gets all of the attributes of this node
    [<Extension>]
    static member Attributes(n:HtmlNode) = 
        HtmlNode.attributes n

    /// <summary>
    /// Tries to select an attribute with the given name from the current node.
    /// </summary>
    /// <param name="name">The name of the attribute to select</param>
    [<Extension>]
    static member TryGetAttribute(n:HtmlNode, name:string) = 
        HtmlNode.tryGetAttribute name n

    /// <summary>
    /// Returns the attribute with the given name. If the
    /// attribute does not exist then this will throw an exception
    /// </summary>
    /// <param name="name">The name of the attribute to select</param>
    [<Extension>]
    static member Attribute(n:HtmlNode, name) = 
        HtmlNode.attribute name n

    /// <summary>
    /// Return the value of the named attribute, or an empty string if not found.
    /// </summary>
    /// <param name="name">The name of the attribute to get the value from</param>
    [<Extension>]
    static member AttributeValue(n:HtmlNode, name) =
      HtmlNode.attributeValue name n

    /// <summary>
    /// Returns true if the current node has an attribute that
    /// matches both the name and the value
    /// </summary>
    /// <param name="name">The name of the attribute</param>
    /// <param name="value">The value of the attribute</param>
    [<Extension>]
    static member HasAttribute(n:HtmlNode, name, value) = 
        HtmlNode.hasAttribute name value n

    /// Returns true if the current node has the specified id
    [<Extension>]
    static member HasId(n:HtmlNode, id) = 
        HtmlNode.hasId id n

    /// Returns true if the current node has the specified class
    [<Extension>]
    static member HasClass(n:HtmlNode, cssClass) = 
        HtmlNode.hasClass cssClass n

    /// Returns the inner text of the current node
    [<Extension>]
    static member InnerText(n:HtmlNode) = 
        HtmlNode.innerText n

// --------------------------------------------------------------------------------------

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
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
        doc |> elements |> List.collect (HtmlNode.descendants true recurseOnMatch predicate)

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
        doc |> elements |> List.collect (HtmlNode.descendantsWithPath true recurseOnMatch predicate)

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
    let inline body (x:HtmlDocument) = 
        match descendantsNamed false ["body"] x with
        | [] -> failwith "No element body found!"
        | body:: _ -> body

    /// <summary>
    /// Tries to find the body element of the given document.
    /// </summary>
    /// <param name="x">The given document</param>
    let inline tryGetBody (x:HtmlDocument) = 
        match descendantsNamed false ["body"] x with
        | [] -> None
        | body:: _ -> Some body

[<Extension>]
type HtmlDocumentExtensions =

    /// <summary>
    /// Returns all of the root elements of the current document
    /// that match the given predicate
    /// </summary>
    /// <param name="predicate">The predicate used to match elements</param>
    [<Extension>]
    static member Elements(doc:HtmlDocument) = 
        HtmlDocument.elements doc

    /// <summary>
    /// Returns all of the root elements in the current document that match the set of names
    /// </summary>
    /// <param name="names">The set of names to match</param>
    [<Extension>]
    static member Elements(doc:HtmlDocument, names:seq<string>) = 
        HtmlDocument.elementsNamed names doc

    /// <summary>
    /// Returns all of the root elements in the current document that match the name
    /// </summary>
    /// <param name="name">The name to match</param>
    [<Extension>]
    static member Elements(doc:HtmlDocument, name:string) = 
        HtmlDocument.elementsNamed [name] doc

    /// <summary>
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// </summary>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member Descendants(doc:HtmlDocument, predicate, recurseOnMatch) = 
        HtmlDocument.descendants recurseOnMatch predicate doc

    /// <summary>
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Recurses on match
    /// </summary>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    [<Extension>]
    static member Descendants(doc:HtmlDocument, predicate) = 
        let recurseOnMatch = true
        HtmlDocument.descendants recurseOnMatch predicate doc
        
    /// Gets all of the descendants of this document
    /// Recurses on match
    [<Extension>]
    static member Descendants(doc:HtmlDocument) = 
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlDocument.descendants recurseOnMatch predicate doc
        
    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// </summary>
    /// <param name="names">The set of names to match</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member Descendants(doc:HtmlDocument, names:seq<string>, recurseOnMatch) =
        HtmlDocument.descendantsNamed recurseOnMatch names doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Recurses on match
    /// </summary>
    /// <param name="names">The set of names to match</param>
    [<Extension>]
    static member Descendants(doc:HtmlDocument, names:seq<string>) =
        let recurseOnMatch = true
        HtmlDocument.descendantsNamed recurseOnMatch names doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given name
    /// </summary>
    /// <param name="name">The name to match</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member Descendants(doc:HtmlDocument, name:string, recurseOnMatch) =
        HtmlDocument.descendantsNamed recurseOnMatch [name] doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given name
    /// Recurses on match
    /// </summary>
    /// <param name="names">The name to match</param>
    [<Extension>]
    static member Descendants(doc:HtmlDocument, name:string) =
        let recurseOnMatch = true
        HtmlDocument.descendantsNamed recurseOnMatch [name] doc

    /// <summary>
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// </summary>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsWithPath(doc:HtmlDocument, predicate, recurseOnMatch) = 
        HtmlDocument.descendantsWithPath recurseOnMatch predicate doc

    /// <summary>
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Recurses on match
    /// </summary>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    [<Extension>]
    static member DescendantsWithPath(doc:HtmlDocument, predicate) = 
        let recurseOnMatch = true
        HtmlDocument.descendantsWithPath recurseOnMatch predicate doc

    /// Gets all of the descendants of this document
    /// Recurses on match
    [<Extension>]
    static member DescendantsWithPath(doc:HtmlDocument) = 
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlDocument.descendantsWithPath recurseOnMatch predicate doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// </summary>
    /// <param name="names">The set of names to match</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsWithPath(doc:HtmlDocument, names:seq<string>, recurseOnMatch) =
        HtmlDocument.descendantsNamedWithPath recurseOnMatch names doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Recurses on match
    /// </summary>
    /// <param name="names">The set of names to match</param>
    [<Extension>]
    static member DescendantsWithPath(doc:HtmlDocument, names:seq<string>) =
        let recurseOnMatch = true
        HtmlDocument.descendantsNamedWithPath recurseOnMatch names doc
        
    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given name
    /// </summary>
    /// <param name="name">The name to match</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsWithPath(doc:HtmlDocument, name:string, recurseOnMatch) =
        HtmlDocument.descendantsNamedWithPath recurseOnMatch [name] doc

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given name
    /// Recurses on match
    /// </summary>
    /// <param name="name">The name to match</param>
    [<Extension>]
    static member DescendantsWithPath(doc:HtmlDocument, name:string) =
        let recurseOnMatch = true
        HtmlDocument.descendantsNamedWithPath recurseOnMatch [name] doc
        
    /// Finds the body element of the given document,
    /// this throws an exception if no body element exists.
    [<Extension>]
    static member Body(doc:HtmlDocument) = 
        HtmlDocument.body doc

    /// Tries to find the body element of the given document.
    [<Extension>]
    static member TryGetBody(doc:HtmlDocument) = 
        HtmlDocument.tryGetBody doc

// --------------------------------------------------------------------------------------

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HtmlExtensions =

    let (?) (node : HtmlNode) name = 
        HtmlNode.attributeValue name node 
