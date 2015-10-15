namespace FSharp.Data

open System
open FSharp.Data
open System.Runtime.CompilerServices

module HtmlCssSelectors =
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
                    acc, []
                    //failwith "Invalid css selector syntax"
        
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
                    []
                    //failwith (sprintf "Invalid css selector syntax (char '%c' at offset %d)" c offset)
                | _ ->
                    //failwith "Invalid css selector syntax"
                    []
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
                    let selectedNodes = filterByAttr acc name (fun v -> 
                        let chars = v.ToCharArray() |> Seq.skipWhile(fun c -> c = '\'') |> Seq.takeWhile Char.IsLetter |> Seq.toArray
                        let s = new String(chars)
                        s = value
                    )
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
                | _ -> []//failwith "Invalid token"

            selectElements' nodes tokens

        member public x.GetElements() =
            run()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CssSelectorExtensions =
    open HtmlCssSelectors

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


