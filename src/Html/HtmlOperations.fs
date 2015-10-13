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
/// Module with operations on HTML attributes
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
/// Extension methods with operations on HTML attributes
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
/// Module with operations on HTML nodes
module HtmlNode =

    /// Gets the given nodes name
    let name n =
        match n with
        | HtmlElement(name = name) -> name
        | _ -> ""
        
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

    let private descendantsBy includeSelf recurseOnMatch predicate n = 
        let rec descendantsBy includeSelf n = seq {
            let proceed = ref true
            if includeSelf && predicate n then
                yield n
                if not recurseOnMatch then
                    proceed := false
            if !proceed then
                for element in elements n do
                    yield! descendantsBy true element
        }
        descendantsBy includeSelf n

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * n - The given node
    let descendants recurseOnMatch predicate n =
        descendantsBy false recurseOnMatch predicate n

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * n - The given node
    let descendantsAndSelf recurseOnMatch predicate n =
        descendantsBy true recurseOnMatch predicate n
    
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * n - The given node
    let inline descendantsNamed recurseOnMatch names n = 
        let nameSet = getNameSet names
        n |> descendants recurseOnMatch (name >> nameSet.Contains)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * n - The given node
    let inline descendantsAndSelfNamed recurseOnMatch names n = 
        let nameSet = getNameSet names
        n |> descendantsAndSelf recurseOnMatch (name >> nameSet.Contains)
                
    let private descendantsByWithPath includeSelf recurseOnMatch predicate n = 
        let rec descendantsByWithPath includeSelf path n = seq {
            let proceed = ref true
            if includeSelf && predicate n then
                yield n, path
                if not recurseOnMatch then
                    proceed := false
            if !proceed then
                for element in elements n do
                    yield! descendantsByWithPath true (n::path) element
        }
        descendantsByWithPath includeSelf [] n
    
    /// Gets all of the descendants of this node that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * n - The given node
    let descendantsWithPath recurseOnMatch predicate n = 
        descendantsByWithPath false recurseOnMatch predicate n

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * n - The given node
    let descendantsAndSelfWithPath recurseOnMatch predicate n = 
        descendantsByWithPath true recurseOnMatch predicate n

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * n - The given node
    let inline descendantsNamedWithPath recurseOnMatch names n = 
        let nameSet = getNameSet names
        n |> descendantsWithPath recurseOnMatch (name >> nameSet.Contains)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * n - The given node
    let inline descendantsAndSelfNamedWithPath recurseOnMatch names n = 
        let nameSet = getNameSet names
        n |> descendantsAndSelfWithPath recurseOnMatch (name >> nameSet.Contains)

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

    /// Returns true if the current node has the specified name
    let inline hasName (expectedName:string) n = 
        name n = expectedName.ToLowerInvariant()

    /// Returns true if the current node has the specified id
    let inline hasId id n = 
        hasAttribute "id" id n

    /// Returns true if the current node has the specified class
    let inline hasClass (cssClass:string) n = 
        let presentClasses = (attributeValue "class" n).Split [|' '|] 
        let classesToLookFor = cssClass.Split [|' '|]
        classesToLookFor |> Array.forall (fun cssClass -> presentClasses |> Array.exists ((=) cssClass))

    let innerTextExcluding exclusions n = 
        let exclusions = "style" :: "script" :: exclusions
        let isAriaHidden (n:HtmlNode) = 
            match tryGetAttribute "aria-hidden" n with
            | Some a -> 
                match bool.TryParse(a.Value()) with
                | true, v -> v
                | false, _ -> false 
            | None -> false
        let rec innerText' inRoot n =
            let exclusions = if inRoot then ["style"; "script"] else exclusions
            match n with
            | HtmlElement(name, _, content) when List.forall ((<>) name) exclusions && not (isAriaHidden n) ->
                seq { for e in content do
                        match e with
                        | HtmlText(text) -> yield text
                        | HtmlComment(_) -> yield ""
                        | elem -> yield innerText' false elem }
                |> String.Concat
            | HtmlText(text) -> text
            | _ -> ""
        innerText' true n

    /// Returns the inner text of the current node
    /// Parameters:
    /// * n - The given node
    let inline innerText n = 
        innerTextExcluding [] n

// --------------------------------------------------------------------------------------

[<Extension>]
/// Extension methods with operations on HTML nodes
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
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member Descendants(n:HtmlNode, predicate, recurseOnMatch) = 
        HtmlNode.descendants recurseOnMatch predicate n

    /// Gets all of the descendants of the current node that satisfy the predicate
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsAndSelf(n:HtmlNode, predicate, recurseOnMatch) = 
        HtmlNode.descendantsAndSelf recurseOnMatch predicate n

    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Recurses on match
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
    [<Extension>]
    static member Descendants(n:HtmlNode, predicate) = 
        let recurseOnMatch = true
        HtmlNode.descendants recurseOnMatch predicate n

    /// Gets all of the descendants of the current node that satisfy the predicate
    /// The current node is also considered in the comparison
    /// Recurses on match
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
    [<Extension>]
    static member DescendantsAndSelf(n:HtmlNode, predicate) = 
        let recurseOnMatch = true
        HtmlNode.descendantsAndSelf recurseOnMatch predicate n
    
    /// Gets all of the descendants of the current node
    /// Recurses on match
    [<Extension>]
    static member Descendants(n:HtmlNode) = 
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlNode.descendants recurseOnMatch predicate n

    /// Gets all of the descendants of the current node
    /// The current node is also considered in the comparison
    /// Recurses on match
    [<Extension>]
    static member DescendantsAndSelf(n:HtmlNode) = 
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlNode.descendantsAndSelf recurseOnMatch predicate n

    /// Gets all of the descendants of the current node, which match the given set of names
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member Descendants(n:HtmlNode, names:seq<string>, recurseOnMatch) = 
        HtmlNode.descendantsNamed recurseOnMatch names n

    /// Gets all of the descendants of the current node, which match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsAndSelf(n:HtmlNode, names:seq<string>, recurseOnMatch) = 
        HtmlNode.descendantsAndSelfNamed recurseOnMatch names n

    /// Gets all of the descendants of the current node, which match the given set of names
    /// Recurses on match
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    [<Extension>]
    static member Descendants(n:HtmlNode, names:seq<string>) = 
        let recurseOnMatch = true
        HtmlNode.descendantsNamed recurseOnMatch names n

    /// Gets all of the descendants of the current node, which match the given set of names
    /// The current node is also considered in the comparison
    /// Recurses on match
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    [<Extension>]
    static member DescendantsAndSelf(n:HtmlNode, names:seq<string>) = 
        let recurseOnMatch = true
        HtmlNode.descendantsAndSelfNamed recurseOnMatch names n

    /// Gets all of the descendants of the current node, which match the given name
    /// Parameters:
    /// * name - The name by which to map the descendants
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member Descendants(n:HtmlNode, name:string, recurseOnMatch) = 
        HtmlNode.descendantsNamed recurseOnMatch [name] n

    /// Gets all of the descendants of the current node, which match the given name
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * name - The name by which to map the descendants
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsAndSelf(n:HtmlNode, name:string, recurseOnMatch) = 
        HtmlNode.descendantsAndSelfNamed recurseOnMatch [name] n

    /// Gets all of the descendants of the current node, which match the given name
    /// Recurses on match
    /// Parameters:
    /// * name - The name by which to map the descendants
    [<Extension>]
    static member Descendants(n:HtmlNode, name:string) = 
        let recurseOnMatch = true
        HtmlNode.descendantsNamed recurseOnMatch [name] n

    /// Gets all of the descendants of the current node, which match the given name
    /// The current node is also considered in the comparison
    /// Recurses on match
    /// Parameters:
    /// * name - The name by which to map the descendants
    [<Extension>]
    static member DescendantsAndSelf(n:HtmlNode, name:string) = 
        let recurseOnMatch = true
        HtmlNode.descendantsAndSelfNamed recurseOnMatch [name] n   

    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, predicate, recurseOnMatch) = 
        HtmlNode.descendantsWithPath recurseOnMatch predicate n

    /// Gets all of the descendants of the current node that satisfy the predicate
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsAndSelfWithPath(n:HtmlNode, predicate, recurseOnMatch) = 
        HtmlNode.descendantsAndSelfWithPath recurseOnMatch predicate n

    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Recurses on match
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, predicate) = 
        let recurseOnMatch = true
        HtmlNode.descendantsWithPath recurseOnMatch predicate n

    /// Gets all of the descendants of the current node that satisfy the predicate
    /// The current node is also considered in the comparison
    /// Recurses on match
    /// Parameters:
    /// * predicate - The predicate for which descendants to return
    [<Extension>]
    static member DescendantsAndSelfWithPath(n:HtmlNode, predicate) = 
        let recurseOnMatch = true
        HtmlNode.descendantsAndSelfWithPath recurseOnMatch predicate n

    /// Gets all of the descendants of the current node
    /// Recurses on match
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode) = 
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlNode.descendantsWithPath recurseOnMatch predicate n

    /// Gets all of the descendants of the current node
    /// The current node is also considered in the comparison
    /// Recurses on match
    [<Extension>]
    static member DescendantsAndSelfWithPath(n:HtmlNode) = 
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlNode.descendantsAndSelfWithPath recurseOnMatch predicate n

    /// Gets all of the descendants of the current node, which match the given set of names
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, names:seq<string>, recurseOnMatch) = 
        HtmlNode.descendantsNamedWithPath recurseOnMatch names n

    /// Gets all of the descendants of the current node, which match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsAndSelfWithPath(n:HtmlNode, names:seq<string>, recurseOnMatch) = 
        HtmlNode.descendantsAndSelfNamedWithPath recurseOnMatch names n

    /// Gets all of the descendants of the current node, which match the given set of names
    /// Recurses on match
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, names:seq<string>) = 
        let recurseOnMatch = true
        HtmlNode.descendantsNamedWithPath recurseOnMatch names n

    /// Gets all of the descendants of the current node, which match the given set of names
    /// The current node is also considered in the comparison
    /// Recurses on match
    /// Parameters:
    /// * names - The set of names by which to map the descendants
    [<Extension>]
    static member DescendantsAndSelfWithPath(n:HtmlNode, names:seq<string>) = 
        let recurseOnMatch = true
        HtmlNode.descendantsAndSelfNamedWithPath recurseOnMatch names n

    /// Gets all of the descendants of the current node, which match the given name
    /// Parameters:
    /// * name - The name by which to map the descendants
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, name:string, recurseOnMatch) = 
        HtmlNode.descendantsNamedWithPath recurseOnMatch [name] n

    /// Gets all of the descendants of the current node, which match the given name
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * name - The name by which to map the descendants
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    [<Extension>]
    static member DescendantsAndSelfWithPath(n:HtmlNode, name:string, recurseOnMatch) = 
        HtmlNode.descendantsAndSelfNamedWithPath recurseOnMatch [name] n

    /// Gets all of the descendants of the current node, which match the given name
    /// Recurses on match
    /// Parameters:
    /// * name - The names by which to map the descendants
    [<Extension>]
    static member DescendantsWithPath(n:HtmlNode, name:string) = 
        let recurseOnMatch = true
        HtmlNode.descendantsNamedWithPath recurseOnMatch [name] n

    /// Gets all of the descendants of the current node, which match the given name
    /// The current node is also considered in the comparison
    /// Recurses on match
    /// Parameters:
    /// * name - The names by which to map the descendants
    [<Extension>]
    static member DescendantsAndSelfWithPath(n:HtmlNode, name:string) = 
        let recurseOnMatch = true
        HtmlNode.descendantsAndSelfNamedWithPath recurseOnMatch [name] n

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

    /// Returns true if the current node has the specified name
    [<Extension>]
    static member HasName(n:HtmlNode, name) = 
        HtmlNode.hasName name n

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
        doc |> elements |> Seq.collect (HtmlNode.descendantsAndSelf recurseOnMatch predicate)

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
        doc |> elements |> Seq.collect (HtmlNode.descendantsAndSelfWithPath recurseOnMatch predicate)

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
        match List.ofSeq <| descendantsNamed false ["body"] x with
        | [] -> failwith "No element body found!"
        | body:: _ -> body

    /// Tries to find the body element of the given document.
    /// Parameters:
    /// * x - The given document
    let inline tryGetBody (x:HtmlDocument) = 
        match List.ofSeq <| descendantsNamed false ["body"] x with
        | [] -> None
        | body:: _ -> Some body

[<Extension>]
/// Extension methods with operations on HTML documents
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
/// Provides the dynamic operator for getting attribute values from HTML elements
module HtmlExtensions =

    /// Gets the value of an attribute from an HTML element
    let (?) (node : HtmlNode) name = 
        HtmlNode.attributeValue name node 

module CssExtensions =
    type SelectorToken =
        | ClassPrefix of int
        | IdPrefix of int
        | TagName of int * string
        | CssClass of int * string
        | CssId of int * string
        | AllChildren of int
        | OpenAttribute of int 
        | CloseAttribute of int
        | AttributeName of int * string
        | AttributeValue of int * string
        | Assign of int
        | EndWith of int
        | StartWith of int
        | DirectChildren of int 
        | AttributeContainsPrefix of int
        | AttributeContains of int
        | AttributeContainsWord of int
        | AttributeNotEqual of int
        | Checkbox of int
        | Checked of int
        | Disabled of int
        | Enabled of int
        | Selected of int

    type CssSelectorTokenizer() =
        let mutable charCount:int = 0
        let mutable source = List<char>.Empty
        let mutable cssSelector = ""
        let mutable inQuotes:bool = false

        let getOffset (t:List<char>) = 
                charCount - 1 - t.Length

        member public x.Tokenize(pCssSelector:string) =
            cssSelector <- pCssSelector
            source <- Array.toList(cssSelector.ToCharArray())
            charCount <- source.Length
            x.tokenize()
            
        member private x.tokenize() = 
            let rec readString acc = function
                | c :: t when Char.IsLetterOrDigit(c) || c.Equals('-') || c.Equals('_') 
                    || c.Equals('+') || c.Equals('/')
                     -> readString (acc + (c.ToString())) t
                | '\'' :: t -> 
                    if inQuotes then
                        inQuotes <- false
                        acc, t
                    else
                        inQuotes <- true
                        readString acc t
            
                | '\\' :: '\'' :: t when inQuotes ->
                    readString (acc + ('\''.ToString())) t

                | c :: t when inQuotes ->
                    readString (acc + (c.ToString())) t
                | c :: t -> acc, c :: t
                | [] -> 
                    acc, []
                | _ ->
                    failwith "Invalid css selector syntax"
        
            let (|TokenStr|_|) (s:string) x  =
                let chars = s.ToCharArray() |> Seq.toList

                let rec equal x s =
                    match x, s with
                    | x, [] -> Some(x)
                    | xh :: xt, sh :: st when xh = sh -> equal xt st
                    | _ -> None

                equal x chars

            let rec tokenize' acc sourceChars = 
                match sourceChars with
                | w :: t when Char.IsWhiteSpace(w) -> 
                    let seqtoken = acc |> List.toSeq |> Seq.skip 1 |> Seq.toList
                    match acc.Head with
                        | AllChildren _ -> tokenize' (AllChildren(getOffset t) :: seqtoken) t
                        | DirectChildren _ -> tokenize' (DirectChildren(getOffset t) :: seqtoken) t
                        | _ -> tokenize' (AllChildren(getOffset t) :: acc) t
                | '.' :: t -> 
                    let s, t' = readString "" t
                    tokenize' (CssClass(getOffset t + 1, s) :: ClassPrefix(getOffset t) :: acc) t'
                | '#' :: t -> 
                    let s, t' = readString "" t
                    tokenize' (CssId(getOffset t + 1, s) :: IdPrefix(getOffset t) :: acc) t'
                | '[' :: t ->
                    let s, t' = readString "" t
                    tokenize' (AttributeName(getOffset t + 1, s) :: OpenAttribute(getOffset t) :: acc) t'
                | ']' :: t ->
                    tokenize' (CloseAttribute(getOffset t) :: acc) t
                | '=' :: t ->
                    let s, t' = readString "" t
                    tokenize' (AttributeValue(getOffset t + 1, s) :: Assign(getOffset t) :: acc) t'
                | '$' :: '=' :: t ->
                    let s, t' = readString "" t
                    tokenize' (AttributeValue(getOffset t + 1, s) :: EndWith(getOffset t) :: acc) t'
                | '^' :: '=' :: t ->
                    let s, t' = readString "" t
                    tokenize' (AttributeValue(getOffset t + 1, s) :: StartWith(getOffset t) :: acc) t'
                | '|' :: '=' :: t ->
                    let s, t' = readString "" t
                    tokenize' (AttributeValue(getOffset t + 1, s) :: AttributeContainsPrefix(getOffset t) :: acc) t'
                | '*' :: '=' :: t ->
                    let s, t' = readString "" t
                    tokenize' (AttributeValue(getOffset t + 1, s) :: AttributeContains(getOffset t) :: acc) t'
                | '~' :: '=' :: t ->
                    let s, t' = readString "" t
                    tokenize' (AttributeValue(getOffset t + 1, s) :: AttributeContainsWord(getOffset t) :: acc) t'
                | '!' :: '=' :: t ->
                    let s, t' = readString "" t
                    tokenize' (AttributeValue(getOffset t + 1, s) :: AttributeNotEqual(getOffset t) :: acc) t'
                |  TokenStr ":checkbox" t  ->
                    let _, t' = readString "" t
                    tokenize' (Checkbox(getOffset t + 1) :: acc) t'
                |  TokenStr ":selected" t  ->
                    let _, t' = readString "" t
                    tokenize' (Selected(getOffset t + 1) :: acc) t'
                | TokenStr ":checked" t ->
                    let _, t' = readString "" t
                    tokenize' (Checked(getOffset t + 1) :: acc) t'
                | TokenStr ":disabled" t ->
                    let _, t' = readString "" t
                    tokenize' (Disabled(getOffset t + 1) :: acc) t'
                | TokenStr ":enabled" t ->
                    let _, t' = readString "" t
                    tokenize' (Enabled(getOffset t + 1) :: acc) t'
                | '>' :: t ->
                    let seqtoken = acc |> List.toSeq |> Seq.skip(1) |> Seq.toList
                    match acc.Head with
                        | AllChildren _ -> tokenize' (DirectChildren(getOffset t) :: seqtoken) t
                        | _ -> tokenize' (DirectChildren(getOffset t) :: acc) t
                | c :: t when Char.IsLetterOrDigit(c) -> 
                    let str = c.ToString()
                    let s, t' = readString str t
                    tokenize' (TagName(getOffset t, s) :: acc) t'
                | [] -> List.rev acc // TODO: refactor code to remove this
                | c :: t when Char.IsLetterOrDigit c |> not ->
                    let offset = getOffset t
                    failwith (sprintf "Invalid css selector syntax (char '%c' at offset %d)" c offset)
                | _ ->
                    failwith "Invalid css selector syntax"
            tokenize' [] source

    type FilterLevel = 
        | Root
        | Children
        | Descendants

    type CssSelectorExecutor(nodes:HtmlNode list, tokens:SelectorToken list) = 
        let mutable level = FilterLevel.Descendants

        let getTargets (matched:HtmlNode list) = 
            match level with
            | FilterLevel.Children      -> matched |> Seq.collect(fun m -> m.Elements())
            | FilterLevel.Descendants   -> matched |> Seq.collect(fun m -> m.Descendants())
            | _                         -> matched |> Seq.ofList

        let searchTag (matched:HtmlNode list) (tag:string) =
            match level with
            | Children -> matched |> List.collect(fun m -> m.Elements tag)
            | _ -> matched |> Seq.collect(fun m -> m.DescendantsAndSelf tag) |> Seq.toList

        let filterByAttr (matched:HtmlNode list) (attr:string) (f:string -> bool) = 
            matched 
            |> getTargets 
            |> Seq.filter (fun x -> x.AttributeValue attr |> f)
            |> Seq.toList

        let attrExists (matched:HtmlNode list) (attr:string) =
            matched
            |> getTargets 
            |> Seq.filter (fun x -> x.Attributes() |> Seq.exists(fun a -> a.Name() = attr))
            |> Seq.toList

        let run() = 
            
            let whiteSpaces = [|' '; '\t'; '\r'; '\n'|]
            
            let rec selectElements' (acc:HtmlNode list) source =
                match source with
                | TagName(_, name) :: t -> 
                    let selectedNodes = searchTag acc name
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t
                | ClassPrefix _ :: CssClass(_, className) :: t -> 
                    let selectedNodes = filterByAttr acc "class" (fun v -> v.Split(whiteSpaces) |> Seq.exists (fun c -> c = className))
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t

                | IdPrefix _ :: CssId(_, id) :: t ->
                    let selectedNodes = filterByAttr acc "id" (fun v -> v = id)
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: Assign _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes = filterByAttr acc name (fun v -> v = value)
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: EndWith _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes = filterByAttr acc name (fun v -> v.EndsWith value)
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: StartWith _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes = filterByAttr acc name (fun v -> v.StartsWith value)
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: AttributeContainsPrefix _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes = filterByAttr acc name (fun v -> v.StartsWith value)
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: AttributeContains _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes = filterByAttr acc name (fun v -> v.ToLowerInvariant().Contains(value.ToLowerInvariant()))
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t
                
                | OpenAttribute _ :: AttributeName(_, name) :: AttributeContainsWord _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes = filterByAttr acc name (fun v -> v.Split(whiteSpaces) |> Seq.exists(fun word -> word.ToLowerInvariant() = value.ToLowerInvariant()))
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: AttributeNotEqual _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes = filterByAttr acc name (fun v -> v <> value)
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t
                
                | Checkbox _ :: t ->
                    let selectedNodes = filterByAttr acc "type" (fun v -> v = "checkbox")
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t

                | Checked _ :: t ->
                    let selectedNodes = attrExists acc "checked"
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t

                | Selected _ :: t ->
                    let selectedNodes = attrExists acc "selected"
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t

                | Disabled _ :: t ->
                    let selectedNodes = attrExists acc "disabled"
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t

                | Enabled _ :: t ->
                    let selectedNodes = acc
                                        |> getTargets 
                                        |> Seq.filter (fun x -> x.Attributes() |> Seq.exists(fun a -> a.Name() = "disabled") |> not)
                                        |> Seq.toList
                    level <- FilterLevel.Root
                    selectElements' selectedNodes t

                | AllChildren _ :: t -> 
                    level <- FilterLevel.Descendants
                    selectElements' acc t

                | DirectChildren _ :: t -> 
                    level <- FilterLevel.Children
                    selectElements' acc t

                | [] -> acc
                | _ -> failwith "Invalid token"

            selectElements' nodes tokens

        member public x.GetElements() =
            run()


    [<Extension>]
    type CssSelectorExtensions =

        static member private Select (nodes:HtmlNode list) selector =
            let tokenizer = CssSelectorTokenizer()
            let tokens = tokenizer.Tokenize selector
            let executor = CssSelectorExecutor(nodes, tokens)
            executor.GetElements()

        [<Extension>]
        static member CssSelect(doc:HtmlDocument, selector) = 
            CssSelectorExtensions.Select [doc.Body()] selector
        
        [<Extension>]
        static member CssSelect(nodes, selector) = 
            CssSelectorExtensions.Select nodes selector

        [<Extension>]
        static member CssSelect(node:HtmlNode, selector) = 
            CssSelectorExtensions.Select [node] selector


