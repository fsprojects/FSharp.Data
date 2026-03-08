namespace FSharp.Data

open System
open FSharp.Data
open System.Runtime.CompilerServices

[<AutoOpen>]
module private Utils =

    let inline toLower (s: string) = s.ToLowerInvariant()
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
    static member Name(attr: HtmlAttribute) = HtmlAttribute.name attr

    /// Gets the value of the current attribute
    [<Extension>]
    static member Value(attr: HtmlAttribute) = HtmlAttribute.value attr

// --------------------------------------------------------------------------------------

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Module with operations on HTML nodes
module HtmlNode =

    /// Gets the given nodes name
    let name n =
        match n with
        | HtmlNode.HtmlElement(name = name) -> name
        | _ -> ""

    /// Gets all of the nodes immediately under this node
    let elements n =
        match n with
        | HtmlNode.HtmlElement(elements = elements) -> elements
        | _ -> []

    /// <summary>
    /// Finds all of the elements nodes of this node that match the given set of names
    /// </summary>
    /// <param name="names">The set of names to match</param>
    /// <param name="n">The given node</param>
    let inline elementsNamed names n =
        let nameSet = getNameSet names

        n |> elements |> List.filter (name >> nameSet.Contains)

    let private descendantsBy includeSelf recurseOnMatch predicate n =
        let rec descendantsBy includeSelf n =
            seq {
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

    /// <summary>
    /// Gets all of the descendants of this node that statisfy the given predicate
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    /// <param name="n">The given node</param>
    let descendants recurseOnMatch predicate n =
        descendantsBy false recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of this node that statisfy the given predicate
    /// The current node is also considered in the comparison
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    /// <param name="n">The given node</param>
    let descendantsAndSelf recurseOnMatch predicate n =
        descendantsBy true recurseOnMatch predicate n

    /// <summary>
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="n">The given node</param>
    let inline descendantsNamed recurseOnMatch names n =
        let nameSet = getNameSet names

        n |> descendants recurseOnMatch (name >> nameSet.Contains)

    /// <summary>
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="n">The given node</param>
    let inline descendantsAndSelfNamed recurseOnMatch names n =
        let nameSet = getNameSet names

        n |> descendantsAndSelf recurseOnMatch (name >> nameSet.Contains)

    let private descendantsByWithPath includeSelf recurseOnMatch predicate n =
        let rec descendantsByWithPath includeSelf path n =
            seq {
                let proceed = ref true

                if includeSelf && predicate n then
                    yield n, path

                    if not recurseOnMatch then
                        proceed := false

                if !proceed then
                    for element in elements n do
                        yield! descendantsByWithPath true (n :: path) element
            }

        descendantsByWithPath includeSelf [] n

    /// <summary>
    /// Gets all of the descendants of this node that statisfy the given predicate
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    /// <param name="n">The given node</param>
    let descendantsWithPath recurseOnMatch predicate n =
        descendantsByWithPath false recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of this node that statisfy the given predicate
    /// The current node is also considered in the comparison
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="predicate">The predicate by which to match the nodes to return</param>
    /// <param name="n">The given node</param>
    let descendantsAndSelfWithPath recurseOnMatch predicate n =
        descendantsByWithPath true recurseOnMatch predicate n

    /// <summary>
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="n">The given node</param>
    let inline descendantsNamedWithPath recurseOnMatch names n =
        let nameSet = getNameSet names

        n |> descendantsWithPath recurseOnMatch (name >> nameSet.Contains)

    /// <summary>
    /// Finds all of the descendant nodes of this nodes that match the given set of names
    /// The current node is also considered in the comparison
    /// </summary>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    /// <param name="names">The set of names to match</param>
    /// <param name="n">The given node</param>
    let inline descendantsAndSelfNamedWithPath recurseOnMatch names n =
        let nameSet = getNameSet names

        n |> descendantsAndSelfWithPath recurseOnMatch (name >> nameSet.Contains)

    /// Gets all of the attributes of this node
    let attributes n =
        match n with
        | HtmlNode.HtmlElement(attributes = attributes) -> attributes
        | _ -> []

    /// <summary>
    /// Tries to return an attribute that exists on the current node
    /// </summary>
    /// <param name="name">The name of the attribute to return.</param>
    /// <param name="n">The given node</param>
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
    /// <param name="n">The given html node</param>
    let inline hasAttribute name value n =
        match tryGetAttribute name n with
        | Some attr -> toLower (HtmlAttribute.value attr) = toLower value
        | None -> false

    /// Returns true if the current node has the specified name
    let inline hasName (expectedName: string) n =
        name n = expectedName.ToLowerInvariant()

    /// Returns true if the current node has the specified id
    let inline hasId id n = hasAttribute "id" id n

    /// Returns true if the current node has the specified class
    let inline hasClass (cssClass: string) n =
        let presentClasses = (attributeValue "class" n).Split [| ' ' |]
        let classesToLookFor = cssClass.Split [| ' ' |]

        classesToLookFor
        |> Array.forall (fun cssClass -> presentClasses |> Array.exists ((=) cssClass))

    let private innerTextExcluding' recurse exclusions n =
        let rec innerText' n =
            match n with
            | HtmlNode.HtmlElement(name, _, content) when List.forall ((<>) name) exclusions ->
                seq {
                    for e in content do
                        match e with
                        | HtmlNode.HtmlText(text) -> yield text
                        | HtmlNode.HtmlComment(_) -> yield ""
                        | elem -> if recurse then yield innerText' elem else yield ""
                }
                |> String.Concat
            | HtmlNode.HtmlText(text) -> text
            | _ -> ""

        innerText' n

    let innerTextExcluding exclusions n = innerTextExcluding' true exclusions n

    /// <summary>
    /// Returns the inner text of the current node
    /// </summary>
    /// <param name="n">The given node</param>
    let inline innerText n = innerTextExcluding [] n

    /// <summary>
    /// Returns the direct inner text of the current node
    /// </summary>
    /// <param name="n">The given node</param>
    let directInnerText n = innerTextExcluding' false [] n

    open HtmlCssSelectors

    let private getTargets level matched =
        match level with
        | FilterLevel.Children -> matched |> Seq.collect elements
        | FilterLevel.Descendants -> matched |> Seq.collect (descendants true (fun _ -> true))
        | _ -> matched |> Seq.ofList

    let private searchTag level matched tag =
        match level with
        | Children -> matched |> List.collect (elementsNamed [ tag ])
        | _ -> matched |> Seq.collect (descendantsAndSelfNamed true [ tag ]) |> Seq.toList

    let private filterByAttr level matched attr f =
        matched
        |> getTargets level
        |> Seq.filter (attributeValue attr >> f)
        |> Seq.toList

    let private attrExists level matched attr =
        matched
        |> getTargets level
        |> Seq.filter (attributes >> Seq.exists (HtmlAttribute.name >> (=) attr))
        |> Seq.toList

    let private selectCssElements tokens nodes =
        let whiteSpaces = [| ' '; '\t'; '\r'; '\n' |]

        let rec selectElements' level acc source =

            // if we already have an empty list, terminate early
            if List.isEmpty acc then
                []
            else

                let selectDescendantOfType ty t =
                    let selectedNodes = filterByAttr level acc "type" (fun v -> v = ty)
                    selectElements' FilterLevel.Root selectedNodes t

                let selectEvenOdd (isEven: bool) =
                    acc
                    |> List.mapi (fun i n -> (i, n))
                    |> List.filter (fun (i, _) ->
                        match isEven with
                        | true -> i % 2 = 0
                        | false -> i % 2 <> 0)
                    |> List.map (fun (_, n) -> n)

                let containsIgnoreCase (value: string) (word: string) =
                    word.IndexOf(value, StringComparison.OrdinalIgnoreCase) <> -1

                let equalsIgnoreCase (value: string) (word: string) =
                    word.Equals(value, StringComparison.OrdinalIgnoreCase)

                match source with
                | TagName(_, name) :: t ->
                    let selectedNodes = searchTag level acc name
                    selectElements' FilterLevel.Root selectedNodes t
                | ClassPrefix _ :: CssClass(_, className) :: t ->
                    let selectedNodes =
                        filterByAttr level acc "class" (fun v -> v.Split(whiteSpaces) |> Array.exists ((=) className))

                    selectElements' FilterLevel.Root selectedNodes t

                | IdPrefix _ :: CssId(_, id) :: t ->
                    let selectedNodes = filterByAttr level acc "id" (fun v -> v = id)
                    selectElements' FilterLevel.Root selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: Assign _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes = filterByAttr level acc name (fun v -> v = value)
                    selectElements' FilterLevel.Root selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: EndWith _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes =
                        filterByAttr level acc name (fun v -> v.EndsWith(value, StringComparison.Ordinal))

                    selectElements' FilterLevel.Root selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: StartWith _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes =
                        filterByAttr level acc name (fun v -> v.StartsWith(value, StringComparison.Ordinal))

                    selectElements' FilterLevel.Root selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: AttributeContainsPrefix _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes =
                        filterByAttr level acc name (fun v ->
                            let chars =
                                v.ToCharArray()
                                |> Seq.skipWhile (fun c -> c = '\'')
                                |> Seq.takeWhile Char.IsLetter
                                |> Seq.toArray

                            let s = new String(chars)
                            s = value)

                    selectElements' FilterLevel.Root selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: AttributeContains _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes = filterByAttr level acc name (containsIgnoreCase value)
                    selectElements' FilterLevel.Root selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: AttributeContainsWord _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes =
                        filterByAttr level acc name (fun v ->
                            v.Split(whiteSpaces) |> Array.exists (equalsIgnoreCase value))

                    selectElements' FilterLevel.Root selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: AttributeNotEqual _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                    let selectedNodes = filterByAttr level acc name ((<>) value)
                    selectElements' FilterLevel.Root selectedNodes t

                | OpenAttribute _ :: AttributeName(_, name) :: CloseAttribute _ :: t ->
                    let selectedNodes =
                        acc |> List.filter (attributes >> List.exists (HtmlAttribute.name >> (=) name))

                    selectElements' FilterLevel.Root selectedNodes t

                | Checkbox _ :: t -> selectDescendantOfType "checkbox" t
                | File _ :: t -> selectDescendantOfType "file" t
                | Hidden _ :: t -> selectDescendantOfType "hidden" t
                | Radio _ :: t -> selectDescendantOfType "radio" t
                | Password _ :: t -> selectDescendantOfType "password" t
                | Image _ :: t -> selectDescendantOfType "image" t
                | Textbox _ :: t -> selectDescendantOfType "text" t
                | Submit _ :: t -> selectDescendantOfType "submit" t

                | Even _ :: t ->
                    let selectedNodes = selectEvenOdd true
                    selectElements' FilterLevel.Root selectedNodes t

                | Odd _ :: t ->
                    let selectedNodes = selectEvenOdd false
                    selectElements' FilterLevel.Root selectedNodes t

                | Button _ :: t ->
                    let selectedNodes =
                        filterByAttr level acc "type" ((=) "button")
                        |> Seq.append (acc |> Seq.collect (descendantsAndSelfNamed true [ "button" ]))
                        |> Seq.toList

                    selectElements' FilterLevel.Root selectedNodes t

                | Checked _ :: t ->
                    let selectedNodes = attrExists level acc "checked"
                    selectElements' FilterLevel.Root selectedNodes t

                | EmptyNode _ :: t ->
                    let selectedNodes =
                        acc
                        |> Seq.collect (
                            descendantsAndSelf true (fun _ -> true)
                            >> Seq.filter (fun d ->
                                String.IsNullOrWhiteSpace(d |> directInnerText)
                                && (d |> descendants true (fun _ -> true)) |> Seq.isEmpty)
                        )
                        |> Seq.toList

                    selectElements' FilterLevel.Root selectedNodes t

                | Selected _ :: t ->
                    let selectedNodes = attrExists level acc "selected"
                    selectElements' FilterLevel.Root selectedNodes t

                | Disabled _ :: t ->
                    let selectedNodes = attrExists level acc "disabled"
                    selectElements' FilterLevel.Root selectedNodes t

                | Enabled _ :: t ->
                    let selectedNodes =
                        acc
                        |> getTargets level
                        |> Seq.filter (attributes >> Seq.exists (HtmlAttribute.name >> (=) "disabled") >> not)
                        |> Seq.toList

                    selectElements' FilterLevel.Root selectedNodes t

                | AllChildren _ :: t -> selectElements' FilterLevel.Descendants acc t

                | DirectChildren _ :: t -> selectElements' FilterLevel.Children acc t

                | [] -> acc
                | tok -> failwithf "Invalid token: %A" tok

        selectElements' FilterLevel.Descendants nodes tokens

    let internal Select nodes selector =
        let tokenizer = CssSelectorTokenizer()

        match tokenizer.Tokenize selector with
        | [] -> []
        | tokens -> List.ofSeq nodes |> selectCssElements tokens

    /// Gets descendants matched by Css selector
    let cssSelect node selector = Select [ node ] selector

// --------------------------------------------------------------------------------------

[<Extension>]
/// Extension methods with operations on HTML nodes
type HtmlNodeExtensions =

    /// Gets the given nodes name
    [<Extension>]
    static member Name(n: HtmlNode) = HtmlNode.name n

    /// Gets all of the nodes immediately under this node
    [<Extension>]
    static member Elements(n: HtmlNode) = HtmlNode.elements n

    /// <summary>
    /// Gets all of the elements of the current node, which match the given set of names
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="names">The set of names by which to map the elements</param>
    [<Extension>]
    static member Elements(n: HtmlNode, names: seq<string>) = HtmlNode.elementsNamed names n

    /// <summary>
    /// Gets all of the elements of the current node, which match the given name
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The name by which to map the elements</param>
    [<Extension>]
    static member Elements(n: HtmlNode, name: string) = HtmlNode.elementsNamed [ name ] n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="predicate">The predicate for which descendants to return</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member Descendants(n: HtmlNode, predicate, recurseOnMatch) =
        HtmlNode.descendants recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// The current node is also considered in the comparison
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="predicate">The predicate for which descendants to return</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsAndSelf(n: HtmlNode, predicate, recurseOnMatch) =
        HtmlNode.descendantsAndSelf recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Recurses on match
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="predicate">The predicate for which descendants to return</param>
    [<Extension>]
    static member Descendants(n: HtmlNode, predicate) =
        let recurseOnMatch = true
        HtmlNode.descendants recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// The current node is also considered in the comparison
    /// Recurses on match
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="predicate">The predicate for which descendants to return</param>
    [<Extension>]
    static member DescendantsAndSelf(n: HtmlNode, predicate) =
        let recurseOnMatch = true
        HtmlNode.descendantsAndSelf recurseOnMatch predicate n

    /// Gets all of the descendants of the current node
    /// Recurses on match
    [<Extension>]
    static member Descendants(n: HtmlNode) =
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlNode.descendants recurseOnMatch predicate n

    /// Gets all of the descendants of the current node
    /// The current node is also considered in the comparison
    /// Recurses on match
    [<Extension>]
    static member DescendantsAndSelf(n: HtmlNode) =
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlNode.descendantsAndSelf recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="names">The set of names by which to map the descendants</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member Descendants(n: HtmlNode, names: seq<string>, recurseOnMatch) =
        HtmlNode.descendantsNamed recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// The current node is also considered in the comparison
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="names">The set of names by which to map the descendants</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsAndSelf(n: HtmlNode, names: seq<string>, recurseOnMatch) =
        HtmlNode.descendantsAndSelfNamed recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// Recurses on match
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="names">The set of names by which to map the descendants</param>
    [<Extension>]
    static member Descendants(n: HtmlNode, names: seq<string>) =
        let recurseOnMatch = true
        HtmlNode.descendantsNamed recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// The current node is also considered in the comparison
    /// Recurses on match
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="names">The set of names by which to map the descendants</param>
    [<Extension>]
    static member DescendantsAndSelf(n: HtmlNode, names: seq<string>) =
        let recurseOnMatch = true
        HtmlNode.descendantsAndSelfNamed recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The name by which to map the descendants</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member Descendants(n: HtmlNode, name: string, recurseOnMatch) =
        HtmlNode.descendantsNamed recurseOnMatch [ name ] n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// The current node is also considered in the comparison
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The name by which to map the descendants</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsAndSelf(n: HtmlNode, name: string, recurseOnMatch) =
        HtmlNode.descendantsAndSelfNamed recurseOnMatch [ name ] n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// Recurses on match
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The name by which to map the descendants</param>
    [<Extension>]
    static member Descendants(n: HtmlNode, name: string) =
        let recurseOnMatch = true
        HtmlNode.descendantsNamed recurseOnMatch [ name ] n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// The current node is also considered in the comparison
    /// Recurses on match
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The name by which to map the descendants</param>
    [<Extension>]
    static member DescendantsAndSelf(n: HtmlNode, name: string) =
        let recurseOnMatch = true
        HtmlNode.descendantsAndSelfNamed recurseOnMatch [ name ] n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="predicate">The predicate for which descendants to return</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsWithPath(n: HtmlNode, predicate, recurseOnMatch) =
        HtmlNode.descendantsWithPath recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// The current node is also considered in the comparison
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="predicate">The predicate for which descendants to return</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsAndSelfWithPath(n: HtmlNode, predicate, recurseOnMatch) =
        HtmlNode.descendantsAndSelfWithPath recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// Recurses on match
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="predicate">The predicate for which descendants to return</param>
    [<Extension>]
    static member DescendantsWithPath(n: HtmlNode, predicate) =
        let recurseOnMatch = true
        HtmlNode.descendantsWithPath recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node that satisfy the predicate
    /// The current node is also considered in the comparison
    /// Recurses on match
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="predicate">The predicate for which descendants to return</param>
    [<Extension>]
    static member DescendantsAndSelfWithPath(n: HtmlNode, predicate) =
        let recurseOnMatch = true
        HtmlNode.descendantsAndSelfWithPath recurseOnMatch predicate n

    /// Gets all of the descendants of the current node
    /// Recurses on match
    [<Extension>]
    static member DescendantsWithPath(n: HtmlNode) =
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlNode.descendantsWithPath recurseOnMatch predicate n

    /// Gets all of the descendants of the current node
    /// The current node is also considered in the comparison
    /// Recurses on match
    [<Extension>]
    static member DescendantsAndSelfWithPath(n: HtmlNode) =
        let recurseOnMatch = true
        let predicate = fun _ -> true
        HtmlNode.descendantsAndSelfWithPath recurseOnMatch predicate n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="names">The set of names by which to map the descendants</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsWithPath(n: HtmlNode, names: seq<string>, recurseOnMatch) =
        HtmlNode.descendantsNamedWithPath recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// The current node is also considered in the comparison
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="names">The set of names by which to map the descendants</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsAndSelfWithPath(n: HtmlNode, names: seq<string>, recurseOnMatch) =
        HtmlNode.descendantsAndSelfNamedWithPath recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// Recurses on match
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="names">The set of names by which to map the descendants</param>
    [<Extension>]
    static member DescendantsWithPath(n: HtmlNode, names: seq<string>) =
        let recurseOnMatch = true
        HtmlNode.descendantsNamedWithPath recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given set of names
    /// The current node is also considered in the comparison
    /// Recurses on match
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="names">The set of names by which to map the descendants</param>
    [<Extension>]
    static member DescendantsAndSelfWithPath(n: HtmlNode, names: seq<string>) =
        let recurseOnMatch = true
        HtmlNode.descendantsAndSelfNamedWithPath recurseOnMatch names n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The name by which to map the descendants</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsWithPath(n: HtmlNode, name: string, recurseOnMatch) =
        HtmlNode.descendantsNamedWithPath recurseOnMatch [ name ] n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// The current node is also considered in the comparison
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The name by which to map the descendants</param>
    /// <param name="recurseOnMatch">If a match is found continues down the tree matching child elements</param>
    [<Extension>]
    static member DescendantsAndSelfWithPath(n: HtmlNode, name: string, recurseOnMatch) =
        HtmlNode.descendantsAndSelfNamedWithPath recurseOnMatch [ name ] n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// Recurses on match
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The names by which to map the descendants</param>
    [<Extension>]
    static member DescendantsWithPath(n: HtmlNode, name: string) =
        let recurseOnMatch = true
        HtmlNode.descendantsNamedWithPath recurseOnMatch [ name ] n

    /// <summary>
    /// Gets all of the descendants of the current node, which match the given name
    /// The current node is also considered in the comparison
    /// Recurses on match
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The names by which to map the descendants</param>
    [<Extension>]
    static member DescendantsAndSelfWithPath(n: HtmlNode, name: string) =
        let recurseOnMatch = true
        HtmlNode.descendantsAndSelfNamedWithPath recurseOnMatch [ name ] n

    /// Gets all of the attributes of this node
    [<Extension>]
    static member Attributes(n: HtmlNode) = HtmlNode.attributes n

    /// <summary>
    /// Tries to select an attribute with the given name from the current node.
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The name of the attribute to select</param>
    [<Extension>]
    static member TryGetAttribute(n: HtmlNode, name: string) = HtmlNode.tryGetAttribute name n

    /// <summary>
    /// Returns the attribute with the given name. If the
    /// attribute does not exist then this will throw an exception
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The name of the attribute to select</param>
    [<Extension>]
    static member Attribute(n: HtmlNode, name) = HtmlNode.attribute name n

    /// <summary>
    /// Return the value of the named attribute, or an empty string if not found.
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The name of the attribute to get the value from</param>
    [<Extension>]
    static member AttributeValue(n: HtmlNode, name) = HtmlNode.attributeValue name n

    /// <summary>
    /// Returns true if the current node has an attribute that
    /// matches both the name and the value
    /// </summary>
    /// <param name="n">The given node</param>
    /// <param name="name">The name of the attribute</param>
    /// <param name="value">The value of the attribute</param>
    [<Extension>]
    static member HasAttribute(n: HtmlNode, name, value) = HtmlNode.hasAttribute name value n

    /// Returns true if the current node has the specified name
    [<Extension>]
    static member HasName(n: HtmlNode, name) = HtmlNode.hasName name n

    /// Returns true if the current node has the specified id
    [<Extension>]
    static member HasId(n: HtmlNode, id) = HtmlNode.hasId id n

    /// Returns true if the current node has the specified class
    [<Extension>]
    static member HasClass(n: HtmlNode, cssClass) = HtmlNode.hasClass cssClass n

    /// Returns the inner text of the current node
    [<Extension>]
    static member InnerText(n: HtmlNode) = HtmlNode.innerText n

    /// Returns the direct inner text of the current node
    [<Extension>]
    static member DirectInnerText(n: HtmlNode) = HtmlNode.directInnerText n
