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

    /// Finds all of the elements nodes of this node that match the given set of names
    /// Parameters:
    ///  * names - The set of names to match
    ///  * n - The given node
    let inline elementsNamed names n = 
        let nameSet = getNameSet names
        n |> elements |> List.filter (name >> nameSet.Contains)

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// Parameters:
    /// * includeSelf - Include the current node
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * n - The given node
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
    
    
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// Parameters:
    /// * includeSelf - Include the current node
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * n - The given node
    let inline descendantsNamed includeSelf recurseOnMatch names n = 
        let nameSet = getNameSet names
        n |> descendants includeSelf recurseOnMatch (name >> nameSet.Contains)
        
    
    /// Gets all of the descendants of this node that statisfy the given predicate
    /// Parameters:
    /// * includeSelf - Include the current node
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * n - The given node
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
    
    
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// Parameters:
    /// * includeSelf - Include the current node
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * n - The given node
    let inline descendantsNamedWithPath includeSelf recurseOnMatch names n = 
        let nameSet = getNameSet names
        n |> descendantsWithPath includeSelf recurseOnMatch (name >> nameSet.Contains)

    /// Gets all of the attributes of this node
    let attributes n =
        match n with
        | HtmlElement(attributes = attributes) -> attributes
        | _ -> []

    
    /// Tries to return an attribute that exists on the current node
    /// Parameters:
    /// * name - The name of the attribute to return.
    let inline tryGetAttribute name n =
        n |> attributes |> List.tryFind (HtmlAttribute.name >> ((=) (toLower name)))
    
    
    /// Returns the attribute with the given name. If the
    /// attribute does not exist then this will throw an exception
    /// Parameters:
    /// * name - The name of the attribute to select
    /// * n - The given node
    let inline attribute name n = 
        match tryGetAttribute name n with
        | Some v -> v
        | None -> failwithf "Unable to find attribute (%s)" name

    
    /// Return the value of the named attribute, or an empty string if not found.
    /// Parameters:
    /// * name - The name of the attribute to get the value from
    /// * n - The given node
    let inline attributeValue name n = 
        defaultArg (n |> tryGetAttribute name |> Option.map HtmlAttribute.value) ""

    
    /// Returns true if the current node has an attribute that
    /// matches both the name and the value
    /// Parameters:
    /// * name - The name of the attribute
    /// * value - The value of the attribute
    /// * x - The given html node
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

    
    /// Returns the inner text of the current node
    /// Parameters:
    /// * n - The given node
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

    
    /// Gets all of the elements of the current node, which match the given set of names
    /// Parameters:
    /// * names - The set of names by which to map the elements
    [<Extension>]
    static member Elements(n:HtmlNode, names:seq<string>) = 
        HtmlNode.elementsNamed names n

    
    /// Gets all of the elements of the current node, which match the given name
    /// Parameters:
    /// * names - The name by which to map the elements
    [<Extension>]
    static member Elements(n:HtmlNode, name:string) = 
        HtmlNode.elementsNamed [name] n
        
    
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
    /// * includeSelf - Include the current node
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member Descendants(n:HtmlNode, predicate, includeSelf, recurseOnMatch) = 
        HtmlNode.descendants includeSelf recurseOnMatch predicate n

    
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Recurses on match
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
    /// * includeSelf - Include the current node
    [<Extension>]
    static member Descendants(n:HtmlNode, predicate, includeSelf) = 
        let recurseOnMatch = true
        HtmlNode.descendants includeSelf recurseOnMatch predicate n

    
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Recurses on match
    /// Doesn't include self
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
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

    
    /// Gets all of the descendants of the current node, which match the given set of names
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    /// * includeSelf - Include the current node
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member Descendants(n:HtmlNode, names:seq<string>, includeSelf, recurseOnMatch) = 
        HtmlNode.descendantsNamed includeSelf recurseOnMatch names n

    
    /// Gets all of the descendants of the current node, which match the given set of names
    /// Recurses on match
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    /// * includeSelf - Include the current node
    [<Extension>]
    static member Descendants(n:HtmlNode, names:seq<string>, includeSelf) = 
        let recurseOnMatch = true
        HtmlNode.descendantsNamed includeSelf recurseOnMatch names n

    
    /// Gets all of the descendants of the current node, which match the given set of names
    /// Recurses on match
    /// Doesn't include self
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    [<Extension>]
    static member Descendants(n:HtmlNode, names:seq<string>) = 
        let includeSelf = false
        let recurseOnMatch = true
        HtmlNode.descendantsNamed includeSelf recurseOnMatch names n

    
    /// Gets all of the descendants of the current node, which match the given name
    /// Parameters:
    /// * name - The name by which to map the descendants
    /// * includeSelf - Include the current node
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member Descendants(n:HtmlNode, name:string, includeSelf, recurseOnMatch) = 
        HtmlNode.descendantsNamed includeSelf recurseOnMatch [name] n

    
    /// Gets all of the descendants of the current node, which match the given name
    /// Recurses on match
    /// Parameters:
    /// * name - The name by which to map the descendants
    /// * includeSelf - Include the current node
    [<Extension>]
    static member Descendants(n:HtmlNode, name:string, includeSelf) = 
        let recurseOnMatch = true
        HtmlNode.descendantsNamed includeSelf recurseOnMatch [name] n

    
    /// Gets all of the descendants of the current node, which match the given name
    /// Recurses on match
    /// Doesn't include self
    /// Parameters:
    /// * name - The name by which to map the descendants
    [<Extension>]
    static member Descendants(n:HtmlNode, name:string) = 
        let recurseOnMatch = true
        let includeSelf = false
        HtmlNode.descendantsNamed includeSelf recurseOnMatch [name] n

    
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
    /// * includeSelf - Include the current node
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, predicate, includeSelf, recurseOnMatch) = 
        HtmlNode.descendantsWithPath includeSelf recurseOnMatch predicate n

    
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Recurses on match
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
    /// * includeSelf - Include the current node
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, predicate, includeSelf) = 
        let recurseOnMatch = true
        HtmlNode.descendantsWithPath includeSelf recurseOnMatch predicate n

    
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Recurses on match
    /// Doesn't include self
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
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

    
    /// Gets all of the descendants of the current node, which match the given set of names
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    /// * includeSelf - Include the current node
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, names:seq<string>, includeSelf, recurseOnMatch) = 
        HtmlNode.descendantsNamedWithPath includeSelf recurseOnMatch names n

    
    /// Gets all of the descendants of the current node, which match the given set of names
    /// Recurses on match
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    /// * includeSelf - Include the current node
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, names:seq<string>, includeSelf) = 
        let recurseOnMatch = true
        HtmlNode.descendantsNamedWithPath includeSelf recurseOnMatch names n

    
    /// Gets all of the descendants of the current node, which match the given set of names
    /// Recurses on match
    /// Doesn't include self
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, names:seq<string>) = 
        let recurseOnMatch = true
        let includeSelf = false
        HtmlNode.descendantsNamedWithPath includeSelf recurseOnMatch names n

    
    /// Gets all of the descendants of the current node, which match the given name
    /// Parameters:
    /// * name - The name by which to map the descendants
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * includeSelf - Include the current node
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, name:string, includeSelf, recurseOnMatch) = 
        HtmlNode.descendantsNamedWithPath includeSelf recurseOnMatch [name] n

    
    /// Gets all of the descendants of the current node, which match the given name
    /// Recurses on match
    /// Parameters:
    /// * name - The names by which to map the descendants
    /// * includeSelf - Include the current node
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, name:string, includeSelf) = 
        let recurseOnMatch = true
        HtmlNode.descendantsNamedWithPath includeSelf recurseOnMatch [name] n

    
    /// Gets all of the descendants of the current node, which match the given name
    /// Recurses on match
    /// Doesn't include self
    /// Parameters:
    /// * name - The names by which to map the descendants
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, name:string) = 
        let recurseOnMatch = true
        let includeSelf = false
        HtmlNode.descendantsNamedWithPath includeSelf recurseOnMatch [name] n

    /// Gets all of the attributes of this node
    [<Extension>]
    static member Attributes(n:HtmlNode) = 
        HtmlNode.attributes n

    
    /// Tries to select an attribute with the given name from the current node.
    /// Parameters:
    /// * name - The name of the attribute to select
    [<Extension>]
    static member TryGetAttribute(n:HtmlNode, name:string) = 
        HtmlNode.tryGetAttribute name n

    
    /// Returns the attribute with the given name. If the
    /// attribute does not exist then this will throw an exception
    /// Parameters:
    /// * name - The name of the attribute to select
    [<Extension>]
    static member Attribute(n:HtmlNode, name) = 
        HtmlNode.attribute name n

    
    /// Return the value of the named attribute, or an empty string if not found.
    /// Parameters:
    /// * name - The name of the attribute to get the value from
    [<Extension>]
    static member AttributeValue(n:HtmlNode, name) =
      HtmlNode.attributeValue name n

    
    /// Returns true if the current node has an attribute that
    /// matches both the name and the value
    /// Parameters:
    /// * name - The name of the attribute
    /// * value - The value of the attribute
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
                
    
    /// Returns all of the root elements of the document that match the set of names
    /// Parameters:
    /// * names - The set of names to match
    /// * doc - The given document
    let inline elementsNamed names doc = 
        let nameSet = getNameSet names
        doc |> elements |> List.filter (HtmlNode.name >> nameSet.Contains)

    
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * doc - The given document
    let inline descendants recurseOnMatch predicate doc =
        doc |> elements |> List.collect (HtmlNode.descendants true recurseOnMatch predicate)

    
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * doc - The given document
    let inline descendantsNamed recurseOnMatch names doc = 
        let nameSet = getNameSet names
        doc |> descendants recurseOnMatch (HtmlNode.name >> nameSet.Contains)

    
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * doc - The given document
    let inline descendantsWithPath recurseOnMatch predicate doc =
        doc |> elements |> List.collect (HtmlNode.descendantsWithPath true recurseOnMatch predicate)

    
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * doc - The given document
    let inline descendantsNamedWithPath recurseOnMatch names doc = 
        let nameSet = getNameSet names
        doc |> descendantsWithPath recurseOnMatch (HtmlNode.name >> nameSet.Contains)

    
    /// Finds the body element of the given document,
    /// this throws an exception if no body element exists.
    /// Parameters:
    /// * x - The given document
    let inline body (x:HtmlDocument) = 
        match descendantsNamed false ["body"] x with
        | [] -> failwith "No element body found!"
        | body:: _ -> body

    
    /// Tries to find the body element of the given document.
    /// Parameters:
    /// * x - The given document
    let inline tryGetBody (x:HtmlDocument) = 
        match descendantsNamed false ["body"] x with
        | [] -> None
        | body:: _ -> Some body

[<Extension>]
type HtmlDocumentExtensions =

    
    /// Returns all of the root elements of the current document
    /// that match the given predicate
    /// Parameters:
    /// * predicate - The predicate used to match elements
    [<Extension>]
    static member Elements(doc:HtmlDocument) = 
        HtmlDocument.elements doc

    
    /// Returns all of the root elements in the current document that match the set of names
    /// Parameters:
    /// * names - The set of names to match
    [<Extension>]
    static member Elements(doc:HtmlDocument, names:seq<string>) = 
        HtmlDocument.elementsNamed names doc

    
    /// Returns all of the root elements in the current document that match the name
    /// Parameters:
    /// * name - The name to match
    [<Extension>]
    static member Elements(doc:HtmlDocument, name:string) = 
        HtmlDocument.elementsNamed [name] doc

    
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Parameters:
    /// * predicate - The predicate by which to match the nodes to return
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member Descendants(doc:HtmlDocument, predicate, recurseOnMatch) = 
        HtmlDocument.descendants recurseOnMatch predicate doc

    
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Recurses on match
    /// Parameters:
    /// * predicate - The predicate by which to match the nodes to return
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
        
    
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Parameters:
    /// * names - The set of names to match
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member Descendants(doc:HtmlDocument, names:seq<string>, recurseOnMatch) =
        HtmlDocument.descendantsNamed recurseOnMatch names doc

    
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Recurses on match
    /// Parameters:
    /// * names - The set of names to match
    [<Extension>]
    static member Descendants(doc:HtmlDocument, names:seq<string>) =
        let recurseOnMatch = true
        HtmlDocument.descendantsNamed recurseOnMatch names doc

    
    /// Finds all of the descendant nodes of this document that match the given name
    /// Parameters:
    /// * name - The name to match
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member Descendants(doc:HtmlDocument, name:string, recurseOnMatch) =
        HtmlDocument.descendantsNamed recurseOnMatch [name] doc

    
    /// Finds all of the descendant nodes of this document that match the given name
    /// Recurses on match
    /// Parameters:
    /// * names - The name to match
    [<Extension>]
    static member Descendants(doc:HtmlDocument, name:string) =
        let recurseOnMatch = true
        HtmlDocument.descendantsNamed recurseOnMatch [name] doc

    
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Parameters:
    /// * predicate - The predicate by which to match the nodes to return
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsWithPath(doc:HtmlDocument, predicate, recurseOnMatch) = 
        HtmlDocument.descendantsWithPath recurseOnMatch predicate doc

    
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Recurses on match
    /// Parameters:
    /// * predicate - The predicate by which to match the nodes to return
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

    
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Parameters:
    /// * names - The set of names to match
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsWithPath(doc:HtmlDocument, names:seq<string>, recurseOnMatch) =
        HtmlDocument.descendantsNamedWithPath recurseOnMatch names doc

    
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Recurses on match
    /// Parameters:
    /// * names - The set of names to match
    [<Extension>]
    static member DescendantsWithPath(doc:HtmlDocument, names:seq<string>) =
        let recurseOnMatch = true
        HtmlDocument.descendantsNamedWithPath recurseOnMatch names doc
        
    
    /// Finds all of the descendant nodes of this document that match the given name
    /// Parameters:
    /// * name - The name to match
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsWithPath(doc:HtmlDocument, name:string, recurseOnMatch) =
        HtmlDocument.descendantsNamedWithPath recurseOnMatch [name] doc

    
    /// Finds all of the descendant nodes of this document that match the given name
    /// Recurses on match
    /// Parameters:
    /// * name - The name to match
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
