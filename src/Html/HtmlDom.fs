namespace FSharp.Data

open System
open System.Xml.Linq
open FSharp.Data
open System.Text
open System.Text.RegularExpressions
open System.ComponentModel
open System.Globalization
open System.Runtime.CompilerServices
open FSharp.Data.Runtime.StructuralInference
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes

// --------------------------------------------------------------------------------------
[<AutoOpen>]
module internal Utils =

    let inline toLower (s:string) = s.ToLowerInvariant()
    let inline getNameSet names = names |> Seq.map toLower |> Set.ofSeq

    let wsRegex = lazy Regex("\\s+", regexOptions)
    let invalidTypeNameRegex = lazy Regex("[^0-9a-zA-Z_]+", regexOptions)
    let headingRegex = lazy Regex("""h\d""", regexOptions)

/// Represents an HTML attribute. The name is always normalized to lowercase
type HtmlAttribute = 

    internal | HtmlAttribute of name:string * value:string    

    /// <summary>
    /// Creates an html attribute
    /// </summary>
    /// <param name="name">The name of the attribute</param>
    /// <param name="value">The value of the attribute</param>
    static member New(name:string, value:string) =
        HtmlAttribute(name.ToLowerInvariant(), value)

    /// Gets the name of the current attribute
    member attr.Name = 
        match attr with
        | HtmlAttribute(name = name) -> name

    /// Gets the value of the current attribute
    member attr.Value = 
        match attr with
        | HtmlAttribute(value = value) -> value

// --------------------------------------------------------------------------------------

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Module with operations on HTML attributes
module HtmlAttribute = 

    /// Gets the name of the given attribute
    let name (attr:HtmlAttribute) = attr.Name

    /// Gets the value of the given attribute
    let value (attr:HtmlAttribute) = attr.Value 

/// Represents an HTML node. The names of elements are always normalized to lowercase
[<StructuredFormatDisplay("{_Print}")>]
type HtmlNode =

    internal | HtmlElement of name:string * attributes:HtmlAttribute list * elements:HtmlNode list
             | HtmlText of content:string
             | HtmlComment of content:string
             | HtmlCData of content:string
    
    /// <summary>
    /// Creates an html element
    /// </summary>
    /// <param name="name">The name of the element</param>
    static member NewElement(name:string) =
        HtmlElement(name.ToLowerInvariant(), [], [])

    /// <summary>
    /// Creates an html element
    /// </summary>
    /// <param name="name">The name of the element</param>
    /// <param name="attrs">The HtmlAttribute(s) of the element</param>
    static member NewElement(name:string, attrs:seq<_>) =
        let attrs = attrs |> Seq.map HtmlAttribute.New |> Seq.toList
        HtmlElement(name.ToLowerInvariant(), attrs, [])

    /// <summary>
    /// Creates an html element
    /// </summary>
    /// <param name="name">The name of the element</param>
    /// <param name="children">The children elements of this element</param>
    static member NewElement(name:string, children:seq<_>) =
        HtmlElement(name.ToLowerInvariant(), [], List.ofSeq children)


    /// <summary>
    /// Creates an html element
    /// </summary>
    /// <param name="name">The name of the element</param>
    /// <param name="attrs">The HtmlAttribute(s) of the element</param>
    /// <param name="children">The children elements of this element</param>
    static member NewElement(name:string, attrs:seq<_>, children:seq<_>) =        
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

    override x.ToString() =
        let rec serialize (sb:StringBuilder) indentation canAddNewLine html =
            let append (str:string) = sb.Append str |> ignore
            let appendEndTag name =
                append "</"
                append name
                append ">"
            let shouldAppendEndTag name =
                name = "textarea"
            let newLine plus =
                sb.AppendLine() |> ignore
                String(' ', indentation + plus) |> append
            match html with
            | HtmlElement(name, attributes, elements) ->
                let onlyText = elements |> List.forall (function HtmlText _ -> true | _ -> false)
                if canAddNewLine && not onlyText then
                    newLine 0
                append "<"
                append name
                for HtmlAttribute(name, value) in attributes do
                    append " "
                    append name
                    append "=\""
                    append value
                    append "\""
                if elements.IsEmpty then
                    if shouldAppendEndTag name then
                        append ">"
                        appendEndTag name
                    else
                        append " />"
                else
                    append ">"
                    if not onlyText then
                        newLine 2
                    let mutable canAddNewLine = false
                    for element in elements do
                        serialize sb (indentation + 2) canAddNewLine element
                        canAddNewLine <- true
                    if not onlyText then
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

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    member x._Print = x.ToString()

    /// Gets the given nodes name
    member n.Name =
        match n with
        | HtmlElement(name = name) -> name |> toLower
        | _ -> ""

    member n.TryName = 
        match n with
        | HtmlElement(name = name) -> Some (name |> toLower)
        | _ -> None
        
    /// Gets all of the nodes immediately under this node
    member n.Elements() =
        match n with
        | HtmlElement(elements = contents) -> contents
        | _ -> []

    /// Gets all of the nodes immediately under this node
    member n.Elements(f) = 
        n.Elements() |> List.filter f

    /// Finds all of the elements nodes of this node that match the given set of names
    /// Parameters:
    ///  * names - The set of names to match
    member n.Elements(names) = 
        let nameSet = getNameSet names
        n.Elements(fun e -> e.Name |> nameSet.Contains)

    /// Finds all of the elements nodes of this node that match the given name
    /// Parameters:
    ///  * name - The set of name to match
    member n.Elements(name) = n.Elements([name]) 

    member private n.DescendantsBy(includeSelf,recurseOnMatch,predicate) = 
        let rec descendantsBy includeSelf (n : HtmlNode) = seq {
            let proceed = ref true
            if includeSelf && predicate n then
                yield n
                if not recurseOnMatch then
                    proceed := false
            if !proceed then
                for element in (n.Elements()) do
                    yield! descendantsBy true element
        }
        descendantsBy includeSelf n

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * n - The given node
    member n.Descendants(predicate, recurseOnMatch) =
        n.DescendantsBy(false,recurseOnMatch, predicate)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    member n.Descendants(names,recurseOnMatch:bool) = 
        let nameSet = getNameSet names
        n.Descendants((fun (e:HtmlNode) -> e.Name |> nameSet.Contains),recurseOnMatch)

    /// Finds all of the descendant nodes of this nodes that match the given name
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * name - name to match
    member n.Descendants(name,recurseOnMatch) = n.Descendants([name], recurseOnMatch)

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// Parameters:
    /// * predicate - The predicate by which to match the nodes to return
    /// * n - The given node
    member n.Descendants(predicate) =
        n.DescendantsBy(false,true, predicate)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// Parameters:
    /// * names - The set of names to match
    member n.Descendants(names:seq<string>) = n.Descendants(names, true)

    /// Finds all of the descendant nodes of this nodes that match the given name
    /// Parameters:
    /// * name - name to match
    member n.Descendants(name) = n.Descendants([name])

    /// Finds all of the descendant nodes of this nodes that match the given name
    /// Parameters:
    /// * name - name to match
    member n.Descendants() = n.Descendants((fun _ -> true), true)

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    member n.DescendantsAndSelf(predicate : HtmlNode -> bool, recurseOnMatch) =
        n.DescendantsBy(true,recurseOnMatch, predicate)
    
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    member n.DescendantsAndSelf(names,recurseOnMatch:bool) = 
        let nameSet = getNameSet names
        n.DescendantsAndSelf((fun (e:HtmlNode) -> e.Name |> nameSet.Contains), recurseOnMatch)

    /// Finds all of the descendant nodes of this nodes that match the given name
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The name to match
    member n.DescendantsAndSelf(name,recurseOnMatch) = n.DescendantsAndSelf([name], recurseOnMatch)

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * predicate - The predicate by which to match the nodes to return
    member n.DescendantsAndSelf(predicate : HtmlNode -> bool) =
        n.DescendantsBy(true,true, predicate)
    
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * names - The set of names to match
    member n.DescendantsAndSelf(names) = 
        let nameSet = getNameSet names
        n.DescendantsAndSelf((fun (e:HtmlNode) -> e.Name |> nameSet.Contains), true)

    /// Finds all of the descendant nodes of this nodes that match the given name
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * names - The name to match
    member n.DescendantsAndSelf(name) = n.DescendantsAndSelf([name])

    /// Finds all of the descendant nodes of this nodes that match the given name
    /// The current node is also considered in the comparison
    /// Parameters:
    member n.DescendantsAndSelf() = n.DescendantsAndSelf((fun _ -> true), true)
  
    member private n.DescendantsByWithPath(includeSelf, recurseOnMatch, predicate) = 
        let rec descendantsByWithPath includeSelf path (n:HtmlNode) = seq {
            let proceed = ref true
            if includeSelf && predicate n then
                yield n, path
                if not recurseOnMatch then
                    proceed := false
            if !proceed then
                for element in (n.Elements()) do
                    yield! descendantsByWithPath true (n::path) element
        }
        descendantsByWithPath includeSelf [] n

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    member n.DescendantsWithPath(predicate,recurseOnMatch) = 
        n.DescendantsByWithPath(false, recurseOnMatch, predicate)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    member n.DescendantsWithPath(names, recurseOnMatch:bool) = 
        let nameSet = getNameSet names
        n.DescendantsWithPath((fun (e:HtmlNode) -> e.Name |> nameSet.Contains), recurseOnMatch)

    /// Finds all of the descendant nodes of this nodes that match the given name
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The name to match
    member n.DescendantsWithPath(name, recurseOnMatch) = n.DescendantsWithPath([name], recurseOnMatch)

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// Parameters:
    /// * predicate - The predicate by which to match the nodes to return
    member n.DescendantsWithPath(predicate) = 
        n.DescendantsByWithPath(false, true, predicate)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// Parameters:
    /// * names - The set of names to match
    member n.DescendantsWithPath(names) = 
        let nameSet = getNameSet names
        n.DescendantsWithPath((fun (e:HtmlNode) -> e.Name |> nameSet.Contains), true)

    /// Finds all of the descendant nodes of this nodes that match the given name
    /// Parameters:
    /// * names - The name to match
    member n.DescendantsWithPath(name) = n.DescendantsWithPath([name])

    /// Finds all of the descendant nodes of this nodes that match the given name
    /// Parameters:
    member n.DescendantsWithPath() = n.DescendantsWithPath((fun _ -> true), true)

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    member n.DescendantsAndSelfWithPath(predicate,recurseOnMatch) = 
        n.DescendantsByWithPath(true,recurseOnMatch, predicate)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    member n.DescendantsAndSelfWithPath(names,recurseOnMatch:bool) = 
        let nameSet = getNameSet names
        n.DescendantsAndSelfWithPath((fun (e:HtmlNode) -> e.Name |> nameSet.Contains),recurseOnMatch)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    member n.DescendantsAndSelfWithPath(name,recurseOnMatch) = n.DescendantsAndSelfWithPath([name],recurseOnMatch)

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * predicate - The predicate by which to match the nodes to return
    member n.DescendantsAndSelfWithPath(predicate) = 
        n.DescendantsByWithPath(true,true, predicate)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * names - The set of names to match
    member n.DescendantsAndSelfWithPath(names) = 
        let nameSet = getNameSet names
        n.DescendantsAndSelfWithPath((fun (e:HtmlNode) -> e.Name |> nameSet.Contains),true)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * names - The set of names to match
    member n.DescendantsAndSelfWithPath(name) = n.DescendantsAndSelfWithPath([name])

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    member n.DescendantsAndSelfWithPath() = n.DescendantsAndSelfWithPath((fun _ -> true), true)

    /// Gets all of the attributes of this node
    member n.Attributes() =
        match n with
        | HtmlElement(attributes = attributes) -> attributes
        | _ -> []

    /// Tries to return an attribute that exists on the current node
    /// Parameters:
    /// * name - The name of the attribute to return.
    member n.TryGetAttribute(name) =
        n.Attributes() |> List.tryFind (fun attr -> attr.Name = (toLower name))
    
    /// Returns the attribute with the given name. If the
    /// attribute does not exist then this will throw an exception
    /// Parameters:
    /// * name - The name of the attribute to select
    /// * n - The given node
    member n.Attribute(name) = 
        match n.TryGetAttribute(name) with
        | Some v -> v
        | None -> failwithf "Unable to find attribute (%s)" name

    /// Return the value of the named attribute, or an empty string if not found.
    /// Parameters:
    /// * name - The name of the attribute to get the value from
    /// * n - The given node
    member n.AttributeValue(name) = 
        defaultArg (n.TryGetAttribute(name) |> Option.map (fun attr -> attr.Value)) ""

    /// Returns true if the current node has an attribute that
    /// matches both the name and the value
    /// Parameters:
    /// * name - The name of the attribute
    /// * value - The value of the attribute
    member n.HasAttribute(name,value) = 
        match n.TryGetAttribute(name) with
        | Some attr -> toLower attr.Value = toLower value
        | None -> false

    /// Returns true if the current node has the specified name
    member n.HasName(expectedName:string) = 
        n.Name = expectedName.ToLowerInvariant()

    /// Returns true if the current node has the specified id
    member n.HasId(id) = 
        n.HasAttribute("id",id)

    /// Returns true if the current node has the specified class
    member n.HasClass(cssClass) = 
        n.HasAttribute("class",cssClass)

    member n.InnerTextExcluding(exclusions) = 
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
    member n.InnerText() = 
        n.InnerTextExcluding([])

    /// Returns the concatenated inner text of the current nodes
    /// Parameters:
    /// * n - The given nodes
    static member InnerTextConcat(n:seq<HtmlNode>) = 
        (n |> Seq.map (fun n -> n.InnerTextExcluding ["table"; "ul"; "ol"; "dl"; "sup"; "sub"]) |> String.Concat).Replace(Environment.NewLine, "").Trim()
  
    
    ///Trys to get the inner text of the element
    static member TryGetInnerText(n:HtmlNode) = 
        let text = n.InnerText()
        if (String.IsNullOrEmpty text) then None else (Some text)
         
// --------------------------------------------------------------------------------------
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Module with operations on HTML nodes
module HtmlNode =

    /// Gets the given nodes name
    let name (n:HtmlNode) = n.Name

    let tryName (n:HtmlNode) = n.TryName
        
    /// Gets all of the nodes immediately under this node
    let elements (n:HtmlNode) = n.Elements()

    /// Finds all of the elements nodes of this node that match the given set of names
    /// Parameters:
    ///  * names - The set of names to match
    ///  * n - The given node
    let inline elementsNamed (names:seq<string>) (n:HtmlNode) = n.Elements(names)

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * n - The given node
    let descendants recurseOnMatch (predicate : HtmlNode -> bool) (n:HtmlNode) = 
        n.Descendants(predicate, recurseOnMatch)

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * n - The given node
    let descendantsAndSelf recurseOnMatch (predicate:HtmlNode -> bool) (n:HtmlNode) = 
        n.DescendantsAndSelf(predicate, recurseOnMatch)
    
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * n - The given node
    let inline descendantsNamed recurseOnMatch (names:seq<string>) (n:HtmlNode) = 
        n.Descendants(names, recurseOnMatch)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * n - The given node
    let inline descendantsAndSelfNamed recurseOnMatch (names:seq<string>) (n:HtmlNode) = 
        n.DescendantsAndSelf(names, recurseOnMatch)
                    
    /// Gets all of the descendants of this node that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * n - The given node
    let descendantsWithPath recurseOnMatch (predicate:HtmlNode -> bool) (n:HtmlNode) = 
        n.DescendantsWithPath(predicate, recurseOnMatch)

    /// Gets all of the descendants of this node that statisfy the given predicate
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * n - The given node
    let descendantsAndSelfWithPath recurseOnMatch (predicate:HtmlNode -> bool) (n:HtmlNode) = 
        n.DescendantsAndSelfWithPath(predicate, recurseOnMatch)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * n - The given node
    let inline descendantsNamedWithPath recurseOnMatch (names:seq<string>) (n:HtmlNode) = 
        n.DescendantsWithPath(names, recurseOnMatch)

    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * n - The given node
    let inline descendantsAndSelfNamedWithPath recurseOnMatch (names:seq<string>) (n:HtmlNode) = 
        n.DescendantsAndSelf(names, recurseOnMatch)

    /// Gets all of the attributes of this node
    let attributes (n:HtmlNode) = n.Attributes()

    /// Tries to return an attribute that exists on the current node
    /// Parameters:
    /// * name - The name of the attribute to return.
    let inline tryGetAttribute name (n:HtmlNode) = n.TryGetAttribute(name) 
    
    /// Returns the attribute with the given name. If the
    /// attribute does not exist then this will throw an exception
    /// Parameters:
    /// * name - The name of the attribute to select
    /// * n - The given node
    let inline attribute name (n:HtmlNode) = n.Attribute(name)

    /// Return the value of the named attribute, or an empty string if not found.
    /// Parameters:
    /// * name - The name of the attribute to get the value from
    /// * n - The given node
    let inline attributeValue name (n:HtmlNode) = n.AttributeValue(name)

    /// Returns true if the current node has an attribute that
    /// matches both the name and the value
    /// Parameters:
    /// * name - The name of the attribute
    /// * value - The value of the attribute
    /// * x - The given html node
    let inline hasAttribute name value (n:HtmlNode) = n.HasAttribute(name, value)

    /// Returns true if the current node has the specified name
    let inline hasName (expectedName:string) (n:HtmlNode) = n.HasName(expectedName)

    /// Returns true if the current node has the specified id
    let inline hasId id (n:HtmlNode) = n.HasId(id)

    /// Returns true if the current node has the specified class
    let inline hasClass cssClass (n:HtmlNode) = n.HasClass(cssClass)

    let innerTextExcluding exclusions (n:HtmlNode) = n.InnerTextExcluding(exclusions)

    /// Returns the inner text of the current node
    /// Parameters:
    /// * n - The given node
    let inline innerText (n:HtmlNode) = n.InnerText()

    /// Returns the concatenated inner text of the current nodes
    /// Parameters:
    /// * n - The given nodes
    let inline innerTextConcat n = HtmlNode.InnerTextConcat(n)

    /// Lifts inforamtion out of signifcant tags such as a, link, img, micro-schema data ect.
    /// and returns them as much simpler elements. Trimming any extra cruft.
    

module HtmlDom = 

    let getName defaultName (parents:HtmlNode list) (element:HtmlNode) = 

        let parents = parents |> Seq.truncate 2 |> Seq.toList

        let tryGetName choices =
            choices
            |> List.tryPick (fun attrName -> 
                element 
                |> HtmlNode.tryGetAttribute attrName
                |> Option.map HtmlAttribute.value
            )

        let rec tryFindPrevious f (x:HtmlNode) (parents:HtmlNode list) = 
            match parents with
            | p::rest ->
                let nearest = 
                    p
                    |> HtmlNode.descendants true (fun _ -> true)
                    |> Seq.takeWhile ((<>) x) 
                    |> Seq.filter f
                    |> Seq.toList
                    |> List.rev
                match nearest with
                | [] -> tryFindPrevious f p rest
                | h :: _ -> Some h 
            | [] -> None

        let deriveFromSibling element parents = 
            let isHeading s = s |> HtmlNode.name |> Utils.headingRegex.Value.IsMatch
            tryFindPrevious isHeading element parents

        let cleanup (str:String) =
            Utils.wsRegex.Value.Replace(str.Replace('–', '-'), " ").Replace("[edit]", null).Trim()

        match deriveFromSibling element parents with
        | Some e -> 
            let innerText = e.InnerText()
            if String.IsNullOrWhiteSpace(innerText)
            then defaultName
            else cleanup(innerText)
        | _ ->
                match List.ofSeq <| element.Descendants("caption", false) with
                | [] ->
                     match tryGetName ["id"; "name"; "title"; "summary"] with
                     | Some name -> cleanup name
                     | _ -> defaultName
                | h :: _ -> h.InnerText()

    let rec normalise (n:HtmlNode list) : HtmlNode list =
       
        let rec getValue' (n:HtmlNode) =
            let nodeName = (HtmlNode.name n)
            match nodeName with
            | "a" | "link" ->
                HtmlElement(nodeName, [],
                            [
                                HtmlElement("href", [], [HtmlText(HtmlNode.attributeValue "href" n)])
                            ])
            | "img" ->
                HtmlElement(nodeName, [],
                            [
                                HtmlElement("href", [], [HtmlText(HtmlNode.attributeValue "href" n)])
                            ])
            | "meta" -> 
                let valueAttrs = ["content"; "value"; "src"]
                let value = 
                    match valueAttrs |> List.tryPick (fun x -> HtmlNode.tryGetAttribute x n) with
                    | Some attr -> attr.Value
                    | None -> HtmlNode.innerText n
                HtmlText(value)
            | _ -> 
                match tryParseMicroSchema n with
                | Some h -> h 
                | None -> HtmlText(HtmlNode.innerText n)

        and tryParseMicroSchema (n:HtmlNode) =
            let (|Attr|_|) (name:string) (n:HtmlNode) = 
                let attr = (HtmlNode.tryGetAttribute name n)
                attr |> Option.map (fun x -> x.Value)

            let getPath str = 
                (match Uri.TryCreate(str, UriKind.Absolute) with 
                 | true, uri -> NameUtils.nicePascalName uri.LocalPath 
                 | false, _ -> "").Trim('/')

            let rec walk state (n:HtmlNode) = 
                match n with
                | Attr "itemscope" _ & Attr "itemtype" scope & Attr "itemprop" prop ->  
                      (HtmlElement(NameUtils.nicePascalName prop, [], [HtmlElement(getPath scope, [], HtmlNode.elements n |> List.fold walk [])])) :: state
                | Attr "itemtype" scope -> 
                      (HtmlElement(getPath scope, [], HtmlNode.elements n |> List.fold walk [])) :: state
                | Attr "itemprop" prop ->
                      (HtmlElement(NameUtils.nicePascalName prop, [], normalise (HtmlNode.elements n))) :: state
                | _ ->  HtmlNode.elements n |> List.fold walk state
            
            match walk [] n with
            | [] -> None
            | h :: _ -> Some h

        n |> List.map (fun n -> getValue' n)
    
    type HtmlTable = 
         { Name : string
           HeaderNamesAndUnits : (string * Type option)[] // always set
           HasHeaders: bool // always set at designtime, never at runtime
           Data :  HtmlNode option [][]
           Html : HtmlNode }
    
         member x.ToHtmlElement(hasHeaders:bool, headers:string[]) = 
             let headerMap = headers |> Array.mapi (fun i c -> i,c) |> Map.ofArray
             let rows =
                 if hasHeaders then x.Data.[1..] else x.Data
                 |> Array.mapi (fun _ cols ->
                     let data =
                         cols |> Array.mapi (fun colI n -> 
                             match n with 
                             | Some (HtmlElement(_,_,contents)) -> 
                                 HtmlElement(headerMap.[colI], [], normalise contents)
                             | Some (HtmlText(t)) -> HtmlElement(headerMap.[colI], [], [HtmlText(t)])
                             | Some _ | None -> HtmlElement(headerMap.[colI], [], [HtmlText("")])
                          ) |> List.ofArray
                     HtmlElement("row",[], data))
                 |> List.ofArray
             HtmlElement(x.Name, [], rows)
         
         override x.ToString() = x.Data.ToString()

    type HtmlList = 
        { Name : string
          Values : HtmlNode[]
          Html : HtmlNode }

        member x.ToHtmlElement() = 
            let rows =
                x.Values
                |> Array.choose (function
                        | HtmlElement(_,_,contents) -> 
                            Some(HtmlElement("Value", [], normalise contents))
                        | HtmlText(_) as t -> Some(HtmlElement("Value", [], [t]))
                        | _ -> None)
                |> List.ofArray
            HtmlElement(x.Name, [], rows)
    
    type HtmlDefinitionList = 
        { Name : string
          Definitions : HtmlList list
          Html : HtmlNode }

        member x.ToHtmlElement() =
            HtmlElement(x.Name, [], x.Definitions |> List.map (fun x -> x.ToHtmlElement()))

    
    type HtmlObject = 
        | Table of HtmlTable
        | List of HtmlList
        | DefinitionList of HtmlDefinitionList
        member x.Name = 
            match x with
            | Table t -> t.Name
            | List l -> l.Name
            | DefinitionList l -> l.Name
        member x.ToHtmlElement(hasHeaders, headers) = 
            match x with
            | Table table -> table.ToHtmlElement(hasHeaders, headers)
            | List l -> l.ToHtmlElement()
            | DefinitionList l -> l.ToHtmlElement()

    type TableInferenceParameters = {
        MissingValues: string[]
        CultureInfo: CultureInfo
        UnitsOfMeasureProvider: IUnitsOfMeasureProvider
        PreferOptionals: bool }

    let tryParseTable (ip:TableInferenceParameters option) includeLayoutTables index (table:HtmlNode, parents:HtmlNode list) = 
        
        let getTableHeaders (numberCols:int) (ip:TableInferenceParameters) (rows:(HtmlNode option)[][]) =
            let nodeText = function
                | Some n -> HtmlNode.innerText n
                | None -> ""
            if rows.Length <= 2 then
                if rows.[0] |> Array.forall (function | Some(HtmlElement("th",_,_)) -> true | _ -> false)
                then Some (rows.[0] |> Array.map nodeText)
                else None
            else

                let headerRow, firstDataRow = rows.[0] |> Array.map nodeText, rows.[1..] |> Array.map (Array.map nodeText)
                let headerNamesAndUnits = headerRow |> Array.map (fun x -> x,None)
                let schema = Array.init numberCols (fun _ -> None)
                
                let headerRowType = 
                    CsvInference.inferColumnTypes headerNamesAndUnits schema [headerRow] 0 ip.MissingValues ip.CultureInfo false ip.PreferOptionals

                let rowType = 
                    CsvInference.inferColumnTypes headerNamesAndUnits schema firstDataRow 0 ip.MissingValues ip.CultureInfo false ip.PreferOptionals

                if headerRowType = rowType
                then None
                else Some headerRow
        
        let rows =
            let header =
                match table.Descendants("thead", false) |> Seq.toList with
                | [ head ] ->
                    // if we have a tr in here, do nothing - we get all trs next anyway
                    match head.Descendants("tr" ,false) |> Seq.toList with
                    | [] -> [ head ]
                    | _ -> []
                | _ -> []
            header @ (table.Descendants("tr", false) |> List.ofSeq)
            |> List.mapi (fun i r -> i,r)
        
        if rows.Length <= 1 then None 
        else
        
        let cells = rows |> List.map (fun (_,r) -> r.Elements ["td"; "th"] |> List.mapi (fun i e -> i, e))
        let rowLengths = cells |> List.map (fun x -> x.Length)
        let numberOfColumns = List.max rowLengths
        if not includeLayoutTables && (numberOfColumns < 1) then None else
        
        let tableData = Array.init rows.Length (fun _ -> Array.init numberOfColumns (fun _ -> None))

        for rowindex, _ in rows do
            for colindex, cell in cells.[rowindex] do
                let rowSpan = max 1 (defaultArg (TextConversions.AsInteger CultureInfo.InvariantCulture (HtmlNode.attributeValue "rowspan" cell)) 0) - 1
                let colSpan = max 1 (defaultArg (TextConversions.AsInteger CultureInfo.InvariantCulture (HtmlNode.attributeValue "colspan" cell)) 0) - 1

                let col_i = ref colindex
                while !col_i < tableData.[rowindex].Length && tableData.[rowindex].[!col_i] <> None do incr(col_i)
                for j in [!col_i..(!col_i + colSpan)] do
                    for i in [rowindex..(rowindex + rowSpan)] do
                        if i < rows.Length && j < numberOfColumns
                        then tableData.[i].[j] <- (Some cell)

        let tableName = (getName (sprintf "Table%d" (index + 1)) parents table)
        let (hasHeaders, headers) = 
            match ip with
            | Some ip ->  
                let headers = getTableHeaders numberOfColumns ip tableData
                let (headerWithMeasure,_) = CsvInference.parseHeaders headers numberOfColumns "" ip.UnitsOfMeasureProvider
                let headerWithMeasure = 
                    headerWithMeasure 
                    |> Array.map (fun (name, unit) -> 
                                        match unit with
                                        | Some _ -> name.Split('\n').[1].Replace(" ","_"), unit
                                        | None -> name.Split('\n').[0].Replace(" ","_"), unit
                                 )
                match headers with
                | Some _ -> true, headerWithMeasure
                | None -> false, headerWithMeasure
            | None -> false, Array.init numberOfColumns (fun i -> "Column_" + (string i), None)
                

        { Name = tableName
          HeaderNamesAndUnits = headers
          HasHeaders = hasHeaders
          Data = tableData
          Html = table } |> Some

    let tryParseList index (list:HtmlNode, parents:HtmlNode list) =

        let rows = 
            list.Descendants("li", false)
            |> Array.ofSeq
    
        if rows.Length <= 1 then None else

        let name = getName (sprintf "List%d" (index + 1)) parents list

        { Name = name
          Values = rows
          Html = list } |> Some

    let tryParseDefinitionList index (definitionList:HtmlNode, parents:HtmlNode list) =
        
        let rec createDefinitionGroups (nodes:HtmlNode list) =
            let rec loop state ((groupName, _, elements) as currentGroup) (nodes:HtmlNode list) =
                match nodes with
                | [] -> (currentGroup :: state) |> List.rev
                | h::t when HtmlNode.name h = "dt" ->
                    loop (currentGroup :: state) (NameUtils.nicePascalName (HtmlNode.innerText h), h, []) t
                | h::t ->
                    loop state (groupName, h, (h :: elements)) t
            match nodes with
            | [] -> []
            | h :: t when HtmlNode.name h = "dt" -> loop [] (NameUtils.nicePascalName (HtmlNode.innerText h), h, []) t
            | h :: t -> loop [] ("Undefined", h, []) t        
        
        let data =
            definitionList
            |> HtmlNode.descendantsNamed false ["dt"; "dd"]
            |> List.ofSeq
            |> createDefinitionGroups
            |> List.map (fun (group, node, values) -> { Name = group
                                                        Values = values |> List.rev |> List.toArray
                                                        Html = node })

        if data.Length <= 1 then None else

        let name = (getName (sprintf "DefinitionList%d" (index + 1)) parents definitionList)
        
        { Name = name
          Definitions = data
          Html = definitionList } |> Some

[<StructuredFormatDisplay("{_Print}")>]
/// Represents an HTML document
type HtmlDocument = 
    private | HtmlDocument of docType:string * elements:HtmlNode list
  
    /// <summary>
    /// Creates an html document
    /// </summary>
    /// <param name="docType">The document type specifier string</param>
    /// <param name="children">The child elements of this document</param>
    static member New(docType, children:seq<_>) = 
        HtmlDocument(docType, List.ofSeq children)

    /// <summary>
    /// Creates an html document
    /// </summary>
    /// <param name="children">The child elements of this document</param>
    static member New(children:seq<_>) = 
        HtmlDocument("", List.ofSeq children)

    override x.ToString() =
        match x with
        | HtmlDocument(docType, elements) ->
            (if String.IsNullOrEmpty docType then "" else "<!" + docType + ">" + Environment.NewLine)
            +
            (elements |> List.map (fun x -> x.ToString()) |> String.Concat)

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    member x._Print = x.ToString()

    /// Returns the doctype of the document
    member doc.DocType =
        match doc with
        | HtmlDocument(docType = docType) -> docType 
    
    //// Gets all of the root elements of the document
    member doc.Elements() =
        match doc with
        | HtmlDocument(elements = elements) -> elements
                
    /// Returns all of the root elements of the document that match the set of names
    /// Parameters:
    /// * names - The set of names to match
    /// * doc - The given document
    member doc.Elements(names) = 
        let nameSet = getNameSet names
        doc.Elements() |> List.filter (HtmlNode.name >> nameSet.Contains)

    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    member doc.Descendants(predicate:HtmlNode -> bool, recurseOnMatch) =
        doc.Elements() |> Seq.collect (HtmlNode.descendants recurseOnMatch predicate)

    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    member doc.Descendants(names, recurseOnMatch:bool) = 
        let nameSet = getNameSet names
        doc.Descendants((HtmlNode.name >> nameSet.Contains), recurseOnMatch)

    /// Finds all of the descendant nodes of this document that match the given name
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    member doc.Descendants(name:string, recurseOnMatch) =
         doc.Descendants([name], recurseOnMatch)

    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Parameters:
    /// * predicate - The predicate by which to match the nodes to return
    member doc.Descendants(predicate:HtmlNode -> bool) =
        doc.Elements() |> Seq.collect (HtmlNode.descendants true predicate)

    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Parameters:
    /// * names - The set of names to match
    member doc.Descendants(names) = 
        let nameSet = getNameSet names
        doc.Descendants((HtmlNode.name >> nameSet.Contains), true)

    /// Finds all of the descendant nodes of this document that match the given name
    /// Parameters:
    /// * names - The set of names to match
    member doc.Descendants(name:string) =
         doc.Descendants([name], true)

    /// Finds all of the descendant nodes of this document that match the given name
    /// Parameters:
    member doc.Descendants() =
         doc.Descendants((fun _ -> true), true)

    /// Gets all of the descendants and self of this document that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    member doc.DescendantsAndSelf(predicate:HtmlNode -> bool, recurseOnMatch) =
        doc.Elements() |> Seq.collect (HtmlNode.descendantsAndSelf recurseOnMatch predicate)

    /// Finds all of the descendant and self nodes of this document that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    member doc.DescendantsAndSelf(names, recurseOnMatch:bool) = 
        let nameSet = getNameSet names
        doc.Descendants((HtmlNode.name >> nameSet.Contains), recurseOnMatch)

    /// Finds all of the descendant and self nodes of this document that match the given name
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    member doc.DescendantsAndSelf(name:string, recurseOnMatch) =
         doc.DescendantsAndSelf([name], recurseOnMatch)

    /// Gets all of the descendants and self of this document that statisfy the given predicate
    /// Parameters:
    /// * predicate - The predicate by which to match the nodes to return
    member doc.DescendantsAndSelf(predicate:HtmlNode -> bool) =
        doc.Elements() |> Seq.collect (HtmlNode.descendants true predicate)

    /// Finds all of the descendant and self nodes of this document that match the given set of names
    /// Parameters:
    /// * names - The set of names to match
    member doc.DescendantsAndSelf(names) = 
        let nameSet = getNameSet names
        doc.DescendantsAndSelf((HtmlNode.name >> nameSet.Contains), true)

    /// Finds all of the descendant and self nodes of this document that match the given name
    /// Parameters:
    /// * names - The set of names to match
    member doc.DescendantsAndSelf(name:string) =
         doc.DescendantsAndSelf([name], true)

    /// Finds all of the descendant and self nodes of this document that match the given name
    /// Parameters:
    member doc.DescendantsAndSelf() =
         doc.DescendantsAndSelf((fun _ -> true), true)


    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    member doc.DescendantsWithPath(predicate, recurseOnMatch) =
        doc.Elements() |> Seq.collect (HtmlNode.descendantsWithPath recurseOnMatch predicate)

    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    member doc.DescendantsWithPath(names, recurseOnMatch:bool) = 
        let nameSet = getNameSet names
        doc.DescendantsWithPath((HtmlNode.name >> nameSet.Contains), recurseOnMatch)

    /// Finds all of the descendant nodes of this document that match the given name
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The name to match
    member doc.DescendantsWithPath(name, recurseOnMatch) = doc.DescendantsWithPath([name], recurseOnMatch)

    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Parameters:
    /// * predicate - The predicate by which to match the nodes to return
    member doc.DescendantsWithPath(predicate) =
        doc.Elements() |> Seq.collect (HtmlNode.descendantsWithPath true predicate)

    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Parameters:
    /// * names - The set of names to match
    member doc.DescendantsWithPath(names) = 
        let nameSet = getNameSet names
        doc.DescendantsWithPath((HtmlNode.name >> nameSet.Contains), true)

    /// Finds all of the descendant nodes of this document that match the given name
    /// Parameters:
    /// * names - The name to match
    member doc.DescendantsWithPath(name) = doc.DescendantsWithPath([name])

    /// Finds all of the descendant nodes of this document that match the given name
    /// Parameters:
    member doc.DescendantsWithPath() = 
           doc.DescendantsWithPath((fun _ -> true), true)

    /// Gets all of the descendants and self of this document that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    member doc.DescendantsAndSelfWithPath(predicate, recurseOnMatch) =
        doc.Elements() |> Seq.collect (HtmlNode.descendantsAndSelfWithPath recurseOnMatch predicate)

    /// Finds all of the descendant and self nodes of this document that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    member doc.DescendantsAndSelfWithPath(names, recurseOnMatch:bool) = 
        let nameSet = getNameSet names
        doc.DescendantsAndSelfWithPath((HtmlNode.name >> nameSet.Contains), recurseOnMatch)

    /// Finds all of the descendant and self nodes of this document that match the given name
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The name to match
    member doc.DescendantsAndSelfWithPath(name, recurseOnMatch) = doc.DescendantsAndSelfWithPath([name], recurseOnMatch)

    /// Gets all of the descendants and self of this document that statisfy the given predicate
    /// Parameters:
    /// * predicate - The predicate by which to match the nodes to return
    member doc.DescendantsAndSelfWithPath(predicate) =
        doc.Elements() |> Seq.collect (HtmlNode.descendantsAndSelfWithPath true predicate)

    /// Finds all of the descendant and self nodes of this document that match the given set of names
    /// Parameters:
    /// * names - The set of names to match
    member doc.DescendantsAndSelfWithPath(names) = 
        let nameSet = getNameSet names
        doc.DescendantsAndSelfWithPath((HtmlNode.name >> nameSet.Contains), true)

    /// Finds all of the descendant and self nodes of this document that match the given name
    /// Parameters:
    /// * names - The name to match
    member doc.DescendantsAndSelfWithPath(name) = doc.DescendantsAndSelfWithPath([name])

    /// Finds all of the descendant and self nodes of this document that match the given name
    /// Parameters:
    member doc.DescendantsAndSelfWithPath() = 
           doc.DescendantsAndSelfWithPath((fun _ -> true), true)

    /// Finds the body element of the given document,
    /// this throws an exception if no body element exists.
    /// Parameters:
    member doc.Body = 
        match List.ofSeq <| doc.Descendants(["body"],false) with
        | [] -> failwith "No element body found!"
        | body:: _ -> body

    /// Tries to find the body element of the given document.
    /// Parameters:
    member doc.TryGetBody() = 
        match List.ofSeq <| doc.Descendants(["body"],false) with
        | [] -> None
        | body:: _ -> Some body

    member doc.Tables(inferenceParameters, includeLayoutTables) =
        let tableElements = doc.DescendantsAndSelfWithPath "table" |> List.ofSeq
        let tableElements = 
            if includeLayoutTables
            then tableElements
            else tableElements |> List.filter (fun (e, _) -> not (e.HasAttribute("cellspacing", "0") && e.HasAttribute("cellpadding", "0")))
        tableElements
        |> List.mapi (HtmlDom.tryParseTable inferenceParameters includeLayoutTables)
        |> List.choose id

    member doc.Lists() =        
        doc.DescendantsAndSelfWithPath(["ol"; "ul"])
        |> List.ofSeq
        |> List.mapi HtmlDom.tryParseList
        |> List.choose id

    member doc.DefinitionLists() =                
        doc.DescendantsAndSelfWithPath(["dl"], false)
        |> List.ofSeq
        |> List.mapi HtmlDom.tryParseDefinitionList
        |> List.choose id

    member doc.GetObjects(inferenceParameters, includeLayoutTables) = 
        (doc.Tables(inferenceParameters,includeLayoutTables) |> List.map HtmlDom.Table) 
        @ (doc.Lists() |> List.map HtmlDom.List)
        @ (doc.DefinitionLists() |> List.map HtmlDom.DefinitionList)
        

// --------------------------------------------------------------------------------------

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Module with operations on HTML documents
module HtmlDocument = 
    
    /// Returns the doctype of the document
    let docType (doc:HtmlDocument) = doc.DocType
    
    //// Gets all of the root elements of the document
    let elements (doc:HtmlDocument)  = doc.Elements()
                
    /// Returns all of the root elements of the document that match the set of names
    /// Parameters:
    /// * names - The set of names to match
    /// * doc - The given document
    let inline elementsNamed names (doc:HtmlDocument) = doc.Elements(names)

    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * doc - The given document
    let inline descendants recurseOnMatch (predicate : HtmlNode-> bool) (doc:HtmlDocument) = 
        doc.Descendants(predicate, recurseOnMatch)

    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * doc - The given document
    let inline descendantsNamed recurseOnMatch (names:seq<string>) (doc:HtmlDocument)  = 
        doc.Descendants(names, recurseOnMatch)

    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * doc - The given document
    let inline descendantsAndSelf recurseOnMatch (predicate : HtmlNode-> bool) (doc:HtmlDocument) = 
        doc.DescendantsAndSelf(predicate, recurseOnMatch)

    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * doc - The given document
    let inline descendantsAndSelfNamed recurseOnMatch (names:seq<string>) (doc:HtmlDocument)  = 
        doc.DescendantsAndSelf(names, recurseOnMatch)

    /// Gets all of the descendants of this document that statisfy the given predicate
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * predicate - The predicate by which to match the nodes to return
    /// * doc - The given document
    let inline descendantsWithPath recurseOnMatch  (predicate : HtmlNode-> bool) (doc:HtmlDocument) =
        doc.DescendantsWithPath(predicate, recurseOnMatch)

    /// Finds all of the descendant nodes of this document that match the given set of names
    /// Parameters:
    /// * recurseOnMatch - If a match is found continues down the tree matching child elements
    /// * names - The set of names to match
    /// * doc - The given document
    let inline descendantsNamedWithPath recurseOnMatch (names:seq<string>) (doc:HtmlDocument) = 
        doc.DescendantsWithPath(names, recurseOnMatch)

    /// Finds the body element of the given document,
    /// this throws an exception if no body element exists.
    /// Parameters:
    /// * x - The given document
    let inline body (x:HtmlDocument) = x.Body

    /// Tries to find the body element of the given document.
    /// Parameters:
    /// * x - The given document
    let inline tryGetBody (x:HtmlDocument) = x.TryGetBody()

    let getTable inferenceParameters includeLayoutTables (doc:HtmlDocument) = 
        doc.Tables(inferenceParameters, includeLayoutTables)

    let getLists (doc:HtmlDocument) = 
        doc.Lists()

    let getDefinitionLists (doc:HtmlDocument) =
        doc.DefinitionLists()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Provides the dynamic operator for getting attribute values from HTML elements
module HtmlExtensions =

    /// Gets the value of an attribute from an HTML element
    let (?) (node : HtmlNode) name = 
        HtmlNode.attributeValue name node 
