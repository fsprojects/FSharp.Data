namespace FSharp.Data

open System
open System.IO
open System.Xml
open FSharp.Data
open FSharp.Data.Runtime

module Html =

    [<AutoOpen>]
    module Dsl =
        
        /// <summary>
        /// Creates a HtmlElement
        /// </summary>
        /// <param name="name">The name of the element</param>
        /// <param name="attrs">The HtmlAttribute(s) of the element</param>
        /// <param name="children">The children elements of this element</param>
        let element name attrs children =
            (fun parent ->  
                let this = ref None
                let attrs = Seq.map HtmlAttribute attrs |> Seq.toList
                let e = HtmlElement(parent, name, attrs, children |> List.map (fun c -> c this))
                this := Some e
                e
            )
        
        /// <summary>
        /// Creates a HtmlContent element
        /// </summary>
        /// <param name="contentType">The content type</param>
        /// <param name="content">The actual content</param>
        let content contentType content = 
            (fun parent -> 
                HtmlContent(parent, contentType, content)
            )

        /// <summary>
        /// Creates a text content element
        /// </summary>
        /// <param name="content">The actual content</param>
        let text content = 
            (fun parent -> 
                HtmlContent(parent, HtmlContentType.Content, content)
            )

        /// <summary>
        /// Creates a comment element
        /// </summary>
        /// <param name="content">The actual content</param>
        let comment content = 
            (fun parent -> 
                HtmlContent(parent, HtmlContentType.Comment, content)
            )
        
        /// <summary>
        /// Creates a HtmlDocument
        /// </summary>
        /// <param name="docType">The document type specifier string</param>
        /// <param name="children">The child elements of this document</param>
        let doc docType children = 
            let this = ref None
            HtmlDocument(docType, children |> List.map (fun c -> c this))

    module HtmlAttribute = 
        ///<summary>
        ///Gets the name of the given attribute
        /// </summary>
        let name = function
            | HtmlAttribute(name = name) -> name

        ///<summary>
        ///Gets the values of the given attribute
        /// </summary>
        let value = function
            | HtmlAttribute(value = value) -> value   

        ///<summary>
        ///Parses the value of the attribute using the given function
        /// </summary>
        let parseValue parseF attr = 
            value attr |> parseF

        ///<summary>
        ///Attempts to parse the value of the attribute using the given function
        ///if the parse functions fails the defaultValue is returned
        /// </summary>
        let tryParseValue defaultValue parseF attr = 
            match value attr |> parseF with
            | true, v -> v
            | false, _ -> defaultValue
    
    type HtmlAttribute with
        /// <summary>
        /// Gets the name of the current attribute
        /// </summary>
        member x.Name with get() = HtmlAttribute.name x

        /// <summary>
        /// Gets the value of the current attribute
        /// </summary>
        member x.Value() = HtmlAttribute.value x

        /// <summary>
        /// Gets the value of the current attribute and parses the value
        /// using the function supplied by parseF
        /// </summary>
        /// <param name="parseF">The function to parse the attribute value</param>
        member x.Value<'a>(parseF : string -> 'a)= 
            HtmlAttribute.parseValue parseF x

        /// <summary>
        /// Attempts to parse the attribute value using the given function
        /// if the parse function returns false then the defaultValue is used
        /// </summary>
        /// <param name="defaultValue">Value to return if the parse function fails</param>
        /// <param name="parseF">Function to parse the attribute value</param>
        member x.Value<'a>(defaultValue, parseF : string -> (bool * 'a))= 
            HtmlAttribute.tryParseValue defaultValue parseF x
    
    module HtmlNode = 
        
        /// <summary>
        /// Gets the given nodes name
        /// </summary>
        let name = function
            | HtmlElement(_, name, _, _) -> name.ToLowerInvariant()
            | _ -> String.Empty
            
        /// <summary>
        /// Gets all of the nodes immediately under this node
        /// </summary>
        let children = function 
            | HtmlElement(_, _, _, children) -> children
            | _ -> []
    
        /// <summary>
        /// Gets the parent node of this node
        /// </summary>
        let parent = function
            | HtmlElement(parent = parent) -> !parent
            | HtmlContent(parent = parent) -> !parent
        
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
        let tryGetAttribute (name:string) = function
            | HtmlElement(_,_,attr,_) -> attr |> List.tryFind (fun a -> a.Name.ToLowerInvariant() = (name.ToLowerInvariant()))
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
        /// attribute does not exist then this will thorw an exception
        /// </summary>
        /// <param name="name">The name of the attribute to select</param>
        /// <param name="x">The current node</param>
        let attribute name x = 
            match tryGetAttribute name x with
            | Some(v) -> v
            | None -> failwithf "Unable to find attribute (%s)" name

        ///<summary>
        ///Returns true id the current node has an attribute that
        ///matches both the name and the value
        ///</summary>
        ///<param name="name">The name of the attribute</param>
        ///<param name="value">The value of the attribute</param>
        ///<param name="x">The given html node</param>
        let hasAttribute name (value:string) x = 
            tryGetAttribute name x
            |> function 
                | Some(attr) ->  attr.Value().ToLowerInvariant() = (value.ToLowerInvariant())
                | None -> false

        ///<summary>
        ///Returns the elements under the current node that mach the
        ///given predicate
        ///</summary>
        ///<param name="f">The predicate to match the element</param>
        ///<param name="x">The given html node</param>    
        let elements f x = 
            [
                for element in children x do
                    if f element 
                    then yield element
                    else ()
            ]

        ///<summary>
        ///Returns the elements under the current node that macht the
        ///given predicate, this also returns the node aswell if it
        ///matches the predicate
        ///</summary>
        ///<param name="f">The predicate to match the element</param>
        ///<param name="x">The given html node</param>    
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
    
        /// <summary>
        /// Returns the inner text of the current node
        /// </summary>
        /// <param name="x">The current node</param>
        let innerText x = 
            let rec innerText' = function
                | HtmlElement(_,name,_, content) when name <> "style" && name <> "script" ->
                    String.Join(" ", seq { for e in content do
                                                match e with
                                                | HtmlContent(_,HtmlContentType.Content,text) -> yield text
                                                | elem -> yield innerText' elem })
                | HtmlContent(_,HtmlContentType.Content,text) -> text
                | _ -> String.Empty
            innerText' x
        
        /// <summary>
        /// Returns the siblings of the current node, not including the given node
        /// </summary>
        /// <param name="x">The given node</param>
        let siblings x =
            match parent x with
            | Some(p) -> 
               elements ((<>) x) p
            | None -> []

        /// <summary>
        /// Trys to find a node that statifies the given function by walking backwards up the
        /// tree 
        /// </summary>
        /// <param name="f">The predicate to statisfy</param>
        /// <param name="x">The given HTML node</param>
        let rec tryFindPrevious f (x:HtmlNode) = 
            match parent x with
            | Some(p) ->
                let nearest = 
                    descendants true (fun _ -> true) p 
                    |> Seq.takeWhile ((<>) x) 
                    |> Seq.filter f
                    |> Seq.toList |> List.rev
                match nearest with
                | [] -> tryFindPrevious f p
                | h :: _ -> Some h 
            | None -> None
               
    type HtmlNode with
               
        /// <summary>
        /// Gets the given nodes name
        /// </summary>
        member x.Name with get() = HtmlNode.name x 
        
        /// <summary>
        /// Gets all of the nodes immediately under this node
        /// </summary>           
        member x.Children with get() = HtmlNode.children x

        /// <summary>
        /// Gets the parent node of this node
        /// </summary>
        member x.Parent with get() = HtmlNode.parent x

        /// <summary>
        /// Gets all of the descendants of the current node
        /// </summary>
        /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
        /// <param name="f">The predicate for which descendants to return</param>
        /// <param name="includeSelf">include the current node</param>
        member x.Descendants(?f, ?includeSelf, ?recurseOnMatch) = 
            let f = defaultArg f (fun _ -> true)
            let recurseOnMatch = defaultArg recurseOnMatch true
            if (defaultArg includeSelf false)
            then HtmlNode.descendantsAndSelf recurseOnMatch f x
            else HtmlNode.descendants recurseOnMatch f x

        /// <summary>
        /// Gets all of the descendants of the current node, which match the given set of names
        /// </summary>
        /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
        /// <param name="names">The set of names by which to map the descendants</param>
        /// <param name="includeSelf">include the current node</param>
        member x.Descendants(names:seq<string>, ?includeSelf, ?recurseOnMatch) = 
            let recurseOnMatch = defaultArg recurseOnMatch true
            if (defaultArg includeSelf false)
            then HtmlNode.descendantsAndSelfNamed recurseOnMatch names x
            else HtmlNode.descendantsNamed recurseOnMatch names x

        /// <summary>
        /// Trys to select an attribute with the given name from the current node.
        /// </summary>
        /// <param name="name">The name of the attribute to select</param>
        member x.TryGetAttribute (name : string) = HtmlNode.tryGetAttribute name x

        /// <summary>
        /// Trys to return a parsed value of the named attribute.
        /// </summary>
        /// <param name="defaultValue">The default value to return if the attribute does not exist or the parsing fails</param>
        /// <param name="parseF">The function to parse the value</param>
        /// <param name="name">The name of the attribute to get the value from</param>
        member x.GetAttributeValue (defaultValue,parseF,name) = HtmlNode.getAttributeValue defaultValue parseF name x

        /// <summary>
        /// Returns the attribute with the given name. If the
        /// attribute does not exist then this will thorw an exception
        /// </summary>
        /// <param name="name">The name of the attribute to select</param>
        member x.Attribute name = HtmlNode.attribute name x

        ///<summary>
        ///Returns true id the current node has an attribute that
        ///matches both the name and the value
        ///</summary>
        ///<param name="name">The name of the attribute</param>
        ///<param name="value">The value of the attribute</param>
        member x.HasAttribute (name,value:string) = HtmlNode.hasAttribute name value x

        /// <summary>
        /// Gets all of the element of the current node, which match the given predicate
        /// </summary>
        /// <param name="f">The predicate by which to match nodes</param>
        /// <param name="includeSelf">include the current node</param>
        member x.Elements(?f, ?includeSelf) = 
            let f = defaultArg f (fun _ -> true)
            if (defaultArg includeSelf false)
            then HtmlNode.elementsAndSelf f x
            else HtmlNode.elements f x 

        /// <summary>
        /// Gets all of the elements of the current node, which match the given set of names
        /// </summary>
        /// <param name="names">The set of names by which to map the elements</param>
        /// <param name="includeSelf">include the current node</param>
        member x.Elements(names:seq<string>, ?includeSelf) = 
            if (defaultArg includeSelf false)
            then HtmlNode.elementsAndSelfNamed names x
            else HtmlNode.elementsNamed names x 
        
        /// <summary>
        /// Returns true if the current node contains an element that matches any of the
        /// names in the given set
        /// </summary>
        /// <param name="names">The set of names to match</param>
        member x.HasElement (names:seq<string>) = HtmlNode.hasElements names x

        /// <summary>
        /// Returns the sibilings of the current node
        /// </summary>
        member x.Siblings() = HtmlNode.siblings x

        /// <summary>
        /// Returns the inner text of the current node
        /// </summary>
        member x.InnerText with get() = HtmlNode.innerText x

        /// <summary>
        /// Trys to find a node that statifies the given function by walking backwards up the
        /// tree 
        /// </summary>
        /// <param name="f">The predicate to statisfy</param>
        /// <param name="x">The given HTML node</param>
        member x.TryFindPrevious(f) = HtmlNode.tryFindPrevious f x

        /// <summary>
        /// Parses the specified HTML string to a list of HTML nodes
        /// </summary>
        static member Parse(text) = 
          use reader = new StringReader(text)
          HtmlParser.parseFragment reader

        /// <summary>
        /// Parses the specified HTML string to a list of HTML nodes
        /// </summary>
        static member ParseRooted(rootName, text) = 
          use reader = new StringReader(text)
          let parent = ref None
          let e = HtmlElement(ref None, rootName, [], HtmlParser.parseFragment reader parent)
          parent := Some e
          e
    
    module HtmlDocument = 
        
        /// <summary>
        /// Returns the doctype of the document
        /// </summary>
        let docType = function
            | HtmlDocument(docType = docType) -> docType 
        
        /// <summary>
        /// Returns all of the elements of the current document
        /// that match the given predicate
        /// </summary>
        /// <param name="f">The predicate used to match elements</param>
        let elements f = function
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
    
    type HtmlDocument with
        /// <summary>
        /// Finds the body element of the given document,
        /// this throws an exception if no body element exists.
        /// </summary>
        member x.Body with get() = HtmlDocument.body x

        /// <summary>
        /// Trys to find the body element of the given document.
        /// </summary>
        member x.TryBody() = HtmlDocument.tryBody x

        /// <summary>
        /// Gets all of the descendants of this document that statisfy the given predicate
        /// </summary>
        /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
        /// <param name="f">The predicate by which to match the nodes to return</param>
        member x.Descendants(?f, ?recurseOnMatch) = 
            let f = defaultArg f (fun _ -> true)
            let recurseOnMatch = defaultArg recurseOnMatch true
            HtmlDocument.descendants recurseOnMatch f x

        /// <summary>
        /// Finds all of the descendant nodes of this document that match the given set of names
        /// </summary>
        /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
        /// <param name="names">The set of names to match</param>
        member x.Descendants(names, ?recurseOnMatch) =
            let recurseOnMatch = defaultArg recurseOnMatch true
            HtmlDocument.descendantsNamed recurseOnMatch names x

        /// <summary>
        /// Returns all of the elements of the current document
        /// that match the given predicate
        /// </summary>
        /// <param name="f">The predicate used to match elements</param>
        member x.Elements(?f) = 
            let f = defaultArg f (fun _ -> true)
            HtmlDocument.elements f x

        /// <summary>
        /// Returns all of the elements in the current document that match the set of names
        /// </summary>
        /// <param name="names">The set of names to match</param>
        member x.Elements(names) = 
            HtmlDocument.elementsNamed names x
    
        /// <summary>
        /// Parses the specified HTML string
        /// </summary>
        static member Parse(text) = 
          use reader = new StringReader(text)
          HtmlParser.parseDocument reader
        
        /// <summary>
        /// Loads HTML from the specified stream
        /// </summary>
        static member Load(stream:Stream) = 
          use reader = new StreamReader(stream)
          HtmlParser.parseDocument reader
    
        /// <summary>
        /// Loads HTML from the specified reader
        /// </summary>
        static member Load(reader:TextReader) = 
          HtmlParser.parseDocument reader
        
        /// <summary>
        /// Loads HTML from the specified uri asynchronously
        /// </summary>
        static member AsyncLoad(uri:string) = async {
          let! reader = IO.asyncReadTextAtRuntime false "" "" "HTML" uri
          return HtmlParser.parseDocument reader
        }
    
        /// <summary>
        /// Loads HTML from the specified uri
        /// </summary>
        static member Load(uri:string) =
            HtmlDocument.AsyncLoad(uri)
            |> Async.RunSynchronously
    

