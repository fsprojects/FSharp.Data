namespace FSharp.Data

open System
open System.IO
open FSharp.Data
open FSharp.Data.Runtime
open System.Runtime.CompilerServices

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

    /// Parses the value of the attribute using the given function
    let parseValue parseF attr = 
        value attr |> parseF

    /// Attempts to parse the value of the attribute using the given function
    /// if the parse functions fails the defaultValue is returned
    let tryParseValue defaultValue parseF attr = 
        match value attr |> parseF with
        | true, v -> v
        | false, _ -> defaultValue
    
[<Extension>]
type HtmlAttributeExtensions =

    /// Gets the name of the current attribute
    [<Extension>]
    static member Name(attr:HtmlAttribute) = HtmlAttribute.name attr

    /// Gets the value of the current attribute
    [<Extension>]
    static member Value(attr:HtmlAttribute) = HtmlAttribute.value attr

    /// <summary>
    /// Gets the value of the current attribute and parses the value
    /// using the function supplied by parseF
    /// </summary>
    /// <param name="parseF">The function to parse the attribute value</param>
    [<Extension>]
    static member Value<'a>(attr:HtmlAttribute, parseF : string -> 'a) = 
        HtmlAttribute.parseValue parseF attr

    /// <summary>
    /// Attempts to parse the attribute value using the given function
    /// if the parse function returns false then the defaultValue is used
    /// </summary>
    /// <param name="defaultValue">Value to return if the parse function fails</param>
    /// <param name="parseF">Function to parse the attribute value</param>
    [<Extension>]
    static member Value<'a>(attr:HtmlAttribute, defaultValue, parseF : string -> (bool * 'a)) = 
        HtmlAttribute.tryParseValue defaultValue parseF attr
    
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HtmlNode = 
    
    /// <summary>
    /// Creates a HtmlElement
    /// All attribute names and tag names will be normalized to lowercase
    /// </summary>
    /// <param name="name">The name of the element</param>
    /// <param name="attrs">The HtmlAttribute(s) of the element</param>
    /// <param name="children">The children elements of this element</param>
    let createElement (name:string) attrs children =
        let attrs = Seq.map (fun (name:string, value) -> HtmlAttribute(name.ToLowerInvariant(), value)) attrs |> Seq.toList
        HtmlElement(name.ToLowerInvariant(), attrs, children)
    
    /// <summary>
    /// Creates a text content element
    /// </summary>
    /// <param name="content">The actual content</param>
    let createText content = HtmlText(content)

    /// <summary>
    /// Creates a comment element
    /// </summary>
    /// <param name="content">The actual content</param>
    let createComment content = HtmlComment(content)

    /// Gets the given nodes name
    let name x =
        match x with
        | HtmlElement(name = name) -> name
        | _ -> String.Empty
        
    /// Gets all of the nodes immediately under this node
    let children x =
        match x with
        | HtmlElement(elements = children) -> children
        | _ -> []

    /// <summary>
    /// Gets all of the descendants of this node that statisfy the given predicate
    /// the current node is also considered in the comparison
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="f">The predicate by which to match the nodes to return</param>
    /// <param name="x">The given node</param>
    let descendantsAndSelf recurseOnMatch f x = 
        let rec descendantsBy f (x:HtmlNode) =
                [   
                    if f x then yield x
                    for element in children x do
                        if f element 
                        then 
                            yield element
                            if recurseOnMatch then yield! descendantsBy f element
                        else yield! descendantsBy f element
                ]
        descendantsBy f x
    
    /// <summary>
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// the current node is also considered for comparison
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="x">The given node</param>
    let descendantsAndSelfNamed recurseOnMatch names x = 
        let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
        descendantsAndSelf recurseOnMatch (fun x -> name x |> nameSet.Contains) x
    
    /// <summary>
    /// Gets all of the descendants of this node that statisfy the given predicate
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="f">The predicate by which to match the nodes to return</param>
    /// <param name="x">The given node</param>
    let descendants recurseOnMatch f x = 
        let rec descendantsBy f (x:HtmlNode) =
                [   for element in children x do
                        if f element 
                        then 
                            yield element
                            if recurseOnMatch then yield! descendantsBy f element
                        else yield! descendantsBy f element
                ]
        descendantsBy f x

    /// <summary>
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="x">The given node</param>
    let descendantsNamed recurseOnMatch names x = 
        let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
        descendants recurseOnMatch (fun x -> name x |> nameSet.Contains) x
    
    /// <summary>
    /// Returns true if any of the descendants of the current node exist in the 
    /// given set of names
    /// </summary>
    /// <param name="names">The set of names to match against</param>
    /// <param name="x">The given node</param>
    let hasDescendants names x = 
        let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
        descendants true (fun x -> nameSet.Contains(name x)) x |> Seq.isEmpty |> not
    
    /// <summary>
    /// Trys to return an attribute that exists on the current node
    /// </summary>
    /// <param name="name">The name of the attribute to return.</param>
    let tryGetAttribute (name:string) x =
        let name = name.ToLowerInvariant()
        match x with
        | HtmlElement(attributes = attr) -> attr |> List.tryFind (fun a -> a.Name() = name)
        | _ -> None   
    
    /// <summary>
    /// Trys to return a parsed value of the named attribute.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the attribute does not exist or the parsing fails</param>
    /// <param name="parseF">The function to parse the value</param>
    /// <param name="name">The name of the attribute to get the value from</param>
    /// <param name="x">The given node</param>
    let getAttributeValue defaultValue parseF name x = 
        match tryGetAttribute name x with
        | Some(v) -> v.Value(defaultValue, parseF)
        | None -> defaultValue
    
    /// <summary>
    /// Returns the attribute with the given name. If the
    /// attribute does not exist then this will throw an exception
    /// </summary>
    /// <param name="name">The name of the attribute to select</param>
    /// <param name="x">The current node</param>
    let attribute name x = 
        match tryGetAttribute name x with
        | Some(v) -> v
        | None -> failwithf "Unable to find attribute (%s)" name

    /// <summary>
    /// Returns true id the current node has an attribute that
    /// matches both the name and the value
    /// </summary>
    /// <param name="name">The name of the attribute</param>
    /// <param name="value">The value of the attribute</param>
    /// <param name="x">The given html node</param>
    let hasAttribute name (value:string) x = 
        tryGetAttribute name x
        |> function 
            | Some(attr) -> attr.Value().ToLowerInvariant() = value.ToLowerInvariant()
            | None -> false

    /// <summary>
    /// Returns the elements under the current node that mach the
    /// given predicate
    /// </summary>
    /// <param name="f">The predicate to match the element</param>
    /// <param name="x">The given html node</param>    
    let elements f x = 
        [
            for element in children x do
                if f element 
                then yield element
                else ()
        ]

    /// <summary>
    /// Returns the elements under the current node that macht the
    /// given predicate, this also returns the node aswell if it
    /// matches the predicate
    /// </summary>
    /// <param name="f">The predicate to match the element</param>
    /// <param name="x">The given html node</param>    
    let elementsAndSelf f x = 
        [
            if f x then yield x
            for element in children x do
                if f element 
                then yield element
                else ()
        ]
    
    /// <summary>
    /// Returns the current element and any other elements that match the given
    /// set of names
    /// </summary>
    /// <param name="names">The set of names to match against</param>
    /// <param name="x">The given node</param>
    let elementsAndSelfNamed names x = 
        let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
        elementsAndSelf (fun x -> name x |> nameSet.Contains) x

    
    /// <summary>
    /// Returns true if any of the elements of the current node exist in the 
    /// given set of names, this also includes the current node
    /// </summary>
    /// <param name="names">The set of names to match against</param>
    /// <param name="x">The given node</param>
    let hasElementsAndSelf names x = 
        elementsAndSelfNamed names x |> List.isEmpty |> not

    /// <summary>
    /// Finds all of the elements nodes of this nodes that match the given set of names
    /// </summary>
    /// <param name="names">The set of names to match</param>
    /// <param name="x">The given node</param>
    let elementsNamed names x = 
        let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
        elements (fun x -> name x |> nameSet.Contains) x

    /// <summary>
    /// Returns true if any of the elements of the current node exist in the 
    /// given set of names
    /// </summary>
    /// <param name="names">The set of names to match against</param>
    /// <param name="x">The given node</param>
    let hasElements names x = 
        elementsNamed names x |> List.isEmpty |> not

    let internal innerTextExcluding exclusions x = 
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
        innerText' x    

    /// <summary>
    /// Returns the inner text of the current node
    /// </summary>
    /// <param name="x">The current node</param>
    let innerText x = innerTextExcluding [] x

[<Extension>]
type HtmlNodeExtensions =
               
    /// Gets the given nodes name
    [<Extension>]
    static member Name(n:HtmlNode) = HtmlNode.name n
        
    /// Gets all of the nodes immediately under this node
    [<Extension>]
    static member Children(n:HtmlNode) = HtmlNode.children n

    /// <summary>
    /// Gets all of the descendants of the current node
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="f">The predicate for which descendants to return</param>
    /// <param name="includeSelf">include the current node</param>
    [<Extension>]
    static member Descendants(n:HtmlNode, ?f, ?includeSelf, ?recurseOnMatch) = 
        let f = defaultArg f (fun _ -> true)
        let recurseOnMatch = defaultArg recurseOnMatch true
        if (defaultArg includeSelf false)
        then HtmlNode.descendantsAndSelf recurseOnMatch f n
        else HtmlNode.descendants recurseOnMatch f n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names by which to map the descendants</param>
    /// <param name="includeSelf">include the current node</param>
    [<Extension>]
    static member Descendants(n:HtmlNode, names:seq<string>, ?includeSelf, ?recurseOnMatch) = 
        let recurseOnMatch = defaultArg recurseOnMatch true
        if (defaultArg includeSelf false)
        then HtmlNode.descendantsAndSelfNamed recurseOnMatch names n
        else HtmlNode.descendantsNamed recurseOnMatch names n

    /// <summary>
    /// Trys to select an attribute with the given name from the current node.
    /// </summary>
    /// <param name="name">The name of the attribute to select</param>
    [<Extension>]
    static member TryGetAttribute(n:HtmlNode, name : string) = HtmlNode.tryGetAttribute name n

    /// <summary>
    /// Trys to return a parsed value of the named attribute.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the attribute does not exist or the parsing fails</param>
    /// <param name="parseF">The function to parse the value</param>
    /// <param name="name">The name of the attribute to get the value from</param>
    [<Extension>]
    static member GetAttributeValue(n:HtmlNode, defaultValue, parseF, name) =
      HtmlNode.getAttributeValue defaultValue parseF name n

    /// <summary>
    /// Returns the attribute with the given name. If the
    /// attribute does not exist then this will throw an exception
    /// </summary>
    /// <param name="name">The name of the attribute to select</param>
    [<Extension>]
    static member Attribute(n:HtmlNode, name) = HtmlNode.attribute name n

    /// <summary>
    /// Returns true id the current node has an attribute that
    /// matches both the name and the value
    /// </summary>
    /// <param name="name">The name of the attribute</param>
    /// <param name="value">The value of the attribute</param>
    [<Extension>]
    static member HasAttribute(n:HtmlNode, name, value:string) = HtmlNode.hasAttribute name value n

    /// <summary>
    /// Gets all of the element of the current node, which match the given predicate
    /// </summary>
    /// <param name="f">The predicate by which to match nodes</param>
    /// <param name="includeSelf">include the current node</param>
    [<Extension>]
    static member Elements(n:HtmlNode, ?f, ?includeSelf) = 
        let f = defaultArg f (fun _ -> true)
        if (defaultArg includeSelf false)
        then HtmlNode.elementsAndSelf f n
        else HtmlNode.elements f n

    /// <summary>
    /// Gets all of the elements of the current node, which match the given set of names
    /// </summary>
    /// <param name="names">The set of names by which to map the elements</param>
    /// <param name="includeSelf">include the current node</param>
    [<Extension>]
    static member Elements(n:HtmlNode, names:seq<string>, ?includeSelf) = 
        if (defaultArg includeSelf false)
        then HtmlNode.elementsAndSelfNamed names n
        else HtmlNode.elementsNamed names n
        
    /// <summary>
    /// Returns true if the current node contains an element that matches any of the
    /// names in the given set
    /// </summary>
    /// <param name="names">The set of names to match</param>
    [<Extension>]
    static member HasElement(n:HtmlNode, names:seq<string>) = HtmlNode.hasElements names n

    /// Returns the inner text of the current node
    [<Extension>]
    static member InnerText(n:HtmlNode) = HtmlNode.innerText n

[<AutoOpen>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HtmlNodeExtensions =

    type HtmlNode with
      /// Parses the specified HTML string to a list of HTML nodes
      static member Parse(text) = 
        use reader = new StringReader(text)
        HtmlParser.parseFragment reader

      /// Parses the specified HTML string to a list of HTML nodes
      static member ParseRooted(rootName, text) = 
        use reader = new StringReader(text)
        HtmlElement(rootName, [], HtmlParser.parseFragment reader)
    
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HtmlDocument = 
    
    /// <summary>
    /// Creates a HtmlDocument
    /// </summary>
    /// <param name="docType">The document type specifier string</param>
    /// <param name="children">The child elements of this document</param>
    let createDoc docType children = 
        HtmlDocument(docType, children)

    /// Returns the doctype of the document
    let docType x =
        match x with
        | HtmlDocument(docType = docType) -> docType 
    
    /// <summary>
    /// Returns all of the elements of the current document
    /// that match the given predicate
    /// </summary>
    /// <param name="f">The predicate used to match elements</param>
    let elements f x =
        match x with
        | HtmlDocument(elements = elements) ->
            [
                for e in elements do
                    if f e then yield e
            ]
                
    /// <summary>
    /// Returns all of the elements in the current document that match the set of names
    /// </summary>
    /// <param name="names">The set of names to match</param>
    /// <param name="x">The current node</param>
    let elementsNamed names x = 
        let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
        elements (fun x -> HtmlNode.name x |> nameSet.Contains) x

    /// <summary>
    /// Returns true if any of the elements of the current document exist in the 
    /// given set of names
    /// </summary>
    /// <param name="names">The set of names to match against</param>
    /// <param name="x">The given document</param>
    let hasElements names x = 
        elementsNamed names x |> List.isEmpty |> not

    /// <summary>
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="f">The predicate by which to match the nodes to return</param>
    /// <param name="x">The given node</param>
    let descendants recurseOnMatch f x =
        [
           for e in elements (fun _ -> true) x do
               if f e 
               then 
                    yield e
                    if recurseOnMatch then yield! HtmlNode.descendants recurseOnMatch f e
               else yield! HtmlNode.descendants recurseOnMatch f e
        ] 

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="x">The given document</param>
    let descendantsNamed recurseOnMatch names x = 
        let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
        descendants recurseOnMatch (fun x -> HtmlNode.name x |> nameSet.Contains) x

    /// <summary>
    /// Returns true if any of the descendants of the current document exist in the 
    /// given set of names
    /// </summary>
    /// <param name="names">The set of names to match against</param>
    /// <param name="x">The given document</param>
    let hasDescendants names x = 
        descendantsNamed true names x |> List.isEmpty |> not

    /// <summary>
    /// Finds the body element of the given document,
    /// this throws an exception if no body element exists.
    /// </summary>
    /// <param name="x">The given document</param>
    let body (x:HtmlDocument) = 
        match descendantsNamed true ["body"] x with
        | [] -> failwith "No element body found!"
        | h:: _ -> h

    /// <summary>
    /// Trys to find the body element of the given document.
    /// </summary>
    /// <param name="x">The given document</param>
    let tryBody (x:HtmlDocument) = 
        match descendantsNamed true ["body"] x with
        | [] -> None
        | h:: _ -> Some(h)

[<Extension>]
type HtmlDocumentExtensions =

    /// Finds the body element of the given document,
    /// this throws an exception if no body element exists.
    [<Extension>]
    static member Body(d:HtmlDocument) = HtmlDocument.body d

    /// Trys to find the body element of the given document.
    [<Extension>]
    static member TryBody(d:HtmlDocument) = HtmlDocument.tryBody d

    /// <summary>
    /// Gets all of the descendants of this document that statisfy the given predicate
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="f">The predicate by which to match the nodes to return</param>
    [<Extension>]
    static member Descendants(d:HtmlDocument, ?f, ?recurseOnMatch) = 
        let f = defaultArg f (fun _ -> true)
        let recurseOnMatch = defaultArg recurseOnMatch true
        HtmlDocument.descendants recurseOnMatch f d

    /// <summary>
    /// Finds all of the descendant nodes of this document that match the given set of names
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names to match</param>
    [<Extension>]
    static member Descendants(d:HtmlDocument, names, ?recurseOnMatch) =
        let recurseOnMatch = defaultArg recurseOnMatch true
        HtmlDocument.descendantsNamed recurseOnMatch names d

    /// <summary>
    /// Returns all of the elements of the current document
    /// that match the given predicate
    /// </summary>
    /// <param name="f">The predicate used to match elements</param>
    [<Extension>]
    static member Elements(d:HtmlDocument, ?f) = 
        let f = defaultArg f (fun _ -> true)
        HtmlDocument.elements f d

    /// <summary>
    /// Returns all of the elements in the current document that match the set of names
    /// </summary>
    /// <param name="names">The set of names to match</param>
    [<Extension>]
    static member Elements(d:HtmlDocument, names) = 
        HtmlDocument.elementsNamed names d

[<AutoOpen>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HtmlDocumentExtensions =
    
    type HtmlDocument with
      /// Parses the specified HTML string
      static member Parse(text) = 
        use reader = new StringReader(text)
        HtmlParser.parseDocument reader
        
      /// Loads HTML from the specified stream
      static member Load(stream:Stream) = 
        use reader = new StreamReader(stream)
        HtmlParser.parseDocument reader
    
      /// Loads HTML from the specified reader
      static member Load(reader:TextReader) = 
        HtmlParser.parseDocument reader
        
      /// Loads HTML from the specified uri asynchronously
      static member AsyncLoad(uri:string) = async {
        let! reader = IO.asyncReadTextAtRuntime false "" "" "HTML" "" uri
        return HtmlParser.parseDocument reader
      }
    
      /// Loads HTML from the specified uri
      static member Load(uri:string) =
          HtmlDocument.AsyncLoad(uri)
          |> Async.RunSynchronously