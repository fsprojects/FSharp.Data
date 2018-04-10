namespace FSharp.Data

open System
open FSharp.Data
open System.Runtime.CompilerServices

module internal HtmlCssSelectors =

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
        | Button of int
        | EmptyNode of int
        | File of int
        | Hidden of int
        | Radio of int
        | Password of int
        | Image of int
        | Textbox of int
        | Submit of int
        | Even of int
        | Odd of int

    type CssSelectorTokenizer() =
        let mutable charCount:int = 0
        let mutable source = List<char>.Empty
        let mutable cssSelector = ""
        let mutable inQuotes:bool = false

        let getOffset (t:List<char>) = 
            charCount - 1 - t.Length

        let isCharacterEscapable (c:char) =
            (* CSS 2.1: Any character (except a hexadecimal digit, linefeed,
               carriage return, or form feed) can be escaped with a backslash to
               remove its special meaning *)
            let isHexadecimalDigit = Char.IsDigit(c) || (Char.ToLower(c) >= 'a' && Char.ToLower(c) <= 'f')
            (isHexadecimalDigit || c = '\n' || c = '\f' || c = '\r')
            |> not

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
            | '\\' :: c :: t when isCharacterEscapable c ->
                readString (acc + (c.ToString())) t
            | c :: t when inQuotes ->
                readString (acc + (c.ToString())) t
            | c :: t -> acc, c :: t
            | [] -> 
                acc, []
            | c ->
                failwithf "Invalid css selector syntax at: %s" (new String(Array.ofList c))
            
        let (|StartsWith|_|) (s:string) (items:char list) = 
            let candidates = s.ToCharArray()
            if items.Length < candidates.Length then
                None
            else
                let start = items |> Seq.take(candidates.Length) |> Seq.toList
                if (Seq.compareWith (fun a b -> (int a) - (int b)) start candidates) = 0 then 
                    Some (items |> Seq.skip s.Length |> Seq.toList)
                else
                    None

        let (|TokenStr|_|) (s:string) x  =
            let chars = s.ToCharArray() |> Array.toList

            let rec equal x s =
                match x, s with
                | x, [] -> Some(x)
                | xh :: xt, sh :: st when xh = sh -> equal xt st
                | _ -> None

            equal x chars

        let tokenize() = 
            let rec tokenize' acc sourceChars = 
                match sourceChars with
                | w :: t when Char.IsWhiteSpace(w) -> 
                    let seqtoken = acc |> List.tail
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
                | StartsWith ":checkbox" t  ->
                    tokenize' (Checkbox(getOffset t + 1) :: acc) t
                | StartsWith ":selected" t  ->
                    tokenize' (Selected(getOffset t + 1) :: acc) t
                | StartsWith ":checked" t ->
                    tokenize' (Checked(getOffset t + 1) :: acc) t
                | StartsWith ":button" t -> 
                    tokenize' (Button(getOffset t + 1) :: acc) t
                | StartsWith ":hidden" t -> 
                    tokenize' (Hidden(getOffset t + 1) :: acc) t
                | StartsWith ":radio" t -> 
                    tokenize' (Radio(getOffset t + 1) :: acc) t
                | StartsWith ":password" t -> 
                    tokenize' (Password(getOffset t + 1) :: acc) t
                | StartsWith ":empty" t ->
                    tokenize' (EmptyNode(getOffset t + 1) :: acc) t
                | StartsWith ":image" t ->
                    tokenize' (Image(getOffset t + 1) :: acc) t
                | StartsWith ":even" t ->
                    tokenize' (Even(getOffset t + 1) :: acc) t
                | StartsWith ":odd" t ->
                    tokenize' (Odd(getOffset t + 1) :: acc) t
                | TokenStr ":disabled" t ->
                    let _, t' = readString "" t
                    tokenize' (Disabled(getOffset t + 1) :: acc) t'
                | StartsWith ":enabled" t ->
                    tokenize' (Enabled(getOffset t + 1) :: acc) t
                | StartsWith ":file" t ->
                    tokenize' (File(getOffset t + 1) :: acc) t
                | StartsWith ":submit" t ->
                    tokenize' (Submit(getOffset t + 1) :: acc) t
                
                | '>' :: t ->
                    let seqtoken = acc |> List.toSeq |> Seq.skip(1) |> Seq.toList
                    match acc.Head with
                        | AllChildren _ -> tokenize' (DirectChildren(getOffset t) :: seqtoken) t
                        | _ -> tokenize' (DirectChildren(getOffset t) :: acc) t
                | c :: t when Char.IsLetterOrDigit(c) -> 
                    let str = c.ToString()
                    let s, t' = readString str t
                    tokenize' (TagName(getOffset t, s) :: acc) t'
                | [] -> List.rev acc 
                | c::t ->
                    let offset = getOffset (c::t)
                    failwith (sprintf "Invalid css selector syntax (char '%c' at offset %d)" c offset)
            tokenize' [] source

        member public x.Tokenize(pCssSelector:string) =
            cssSelector <- pCssSelector
            source <- cssSelector.ToCharArray() |> Array.toList
            charCount <- source.Length
            tokenize()

    type FilterLevel = 
        | Root
        | Children
        | Descendants

    let getTargets level (matched:HtmlNode list) = 
        match level with
        | FilterLevel.Children      -> matched |> Seq.collect(fun m -> m.Elements())
        | FilterLevel.Descendants   -> matched |> Seq.collect(fun m -> m.Descendants())
        | _                         -> matched |> Seq.ofList

    let searchTag level (matched:HtmlNode list) (tag:string) =
        match level with
        | Children -> matched |> List.collect(fun m -> m.Elements tag)
        | _ -> matched |> Seq.collect(fun m -> m.DescendantsAndSelf tag) |> Seq.toList

    let filterByAttr level (matched:HtmlNode list) (attr:string) (f:string -> bool) = 
        matched 
        |> getTargets level
        |> Seq.filter (fun x -> x.AttributeValue attr |> f)
        |> Seq.toList

    let attrExists level (matched:HtmlNode list) (attr:string) =
        matched
        |> getTargets level
        |> Seq.filter (fun x -> x.Attributes() |> Seq.exists(fun a -> a.Name() = attr))
        |> Seq.toList

    let selectCssElements (tokens:SelectorToken list) (nodes:HtmlNode list) = 
        let whiteSpaces = [|' '; '\t'; '\r'; '\n'|]            
        let rec selectElements' level (acc:HtmlNode list) source =

            // if we already have an empty list, terminate early
            if acc.Length = 0 then [] else 

            let selectDescendantOfType ty t = 
                let selectedNodes = filterByAttr level acc "type" (fun v -> v = ty)
                selectElements' FilterLevel.Root selectedNodes t

            let selectEvenOdd (isEven:bool) =
                acc 
                |> List.mapi(fun i n -> (i,n))
                |> List.filter(
                    fun (i,_) -> 
                        match isEven with
                        | true -> i%2 = 0
                        | false -> i%2 <> 0
                )
                |> List.map (fun (_,n) -> n)

            let containsIgnoreCase (value:string) (word:string) = word.IndexOf(value, StringComparison.OrdinalIgnoreCase) <> -1
            let equalsIgnoreCase (value:string) (word:string) = word.Equals(value, StringComparison.OrdinalIgnoreCase)

            match source with
            | TagName(_, name) :: t -> 
                let selectedNodes = searchTag level acc name
                selectElements' FilterLevel.Root selectedNodes t
            | ClassPrefix _ :: CssClass(_, className) :: t -> 
                let selectedNodes = filterByAttr level acc "class" (fun v -> v.Split(whiteSpaces) |> Array.exists ((=) className))
                selectElements' FilterLevel.Root selectedNodes t

            | IdPrefix _ :: CssId(_, id) :: t ->
                let selectedNodes = filterByAttr level acc "id" (fun v -> v = id)
                selectElements' FilterLevel.Root selectedNodes t

            | OpenAttribute _ :: AttributeName(_, name) :: Assign _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                let selectedNodes = filterByAttr level acc name (fun v -> v = value)
                selectElements' FilterLevel.Root selectedNodes t

            | OpenAttribute _ :: AttributeName(_, name) :: EndWith _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                let selectedNodes = filterByAttr level acc name (fun v -> v.EndsWith value)
                selectElements' FilterLevel.Root selectedNodes t

            | OpenAttribute _ :: AttributeName(_, name) :: StartWith _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                let selectedNodes = filterByAttr level acc name (fun v -> v.StartsWith value)
                selectElements' FilterLevel.Root selectedNodes t

            | OpenAttribute _ :: AttributeName(_, name) :: AttributeContainsPrefix _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                let selectedNodes = filterByAttr level acc name (fun v -> 
                    let chars = v.ToCharArray() |> Seq.skipWhile(fun c -> c = '\'') |> Seq.takeWhile Char.IsLetter |> Seq.toArray
                    let s = new String(chars)
                    s = value )
                selectElements' FilterLevel.Root selectedNodes t

            | OpenAttribute _ :: AttributeName(_, name) :: AttributeContains _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                let selectedNodes = filterByAttr level acc name (containsIgnoreCase value)
                selectElements' FilterLevel.Root selectedNodes t
                
            | OpenAttribute _ :: AttributeName(_, name) :: AttributeContainsWord _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                let selectedNodes = filterByAttr level acc name (fun v -> v.Split(whiteSpaces) |> Array.exists (equalsIgnoreCase value))
                selectElements' FilterLevel.Root selectedNodes t

            | OpenAttribute _ :: AttributeName(_, name) :: AttributeNotEqual _ :: AttributeValue(_, value) :: CloseAttribute _ :: t ->
                let selectedNodes = filterByAttr level acc name (fun v -> v <> value)
                selectElements' FilterLevel.Root selectedNodes t

            | OpenAttribute _ ::  AttributeName(_, name) :: CloseAttribute _ :: t ->
                let selectedNodes = 
                    acc |> List.filter(fun n -> 
                            n.Attributes()
                            |> List.exists(fun a -> a.Name() = name) )
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
                    filterByAttr level acc "type" (fun v -> v = "button")
                    |> Seq.append (acc |> Seq.collect (fun n -> n.DescendantsAndSelf "button"))
                    |> Seq.toList
                selectElements' FilterLevel.Root selectedNodes t

            | Checked _ :: t ->
                let selectedNodes = attrExists level acc "checked"
                selectElements' FilterLevel.Root selectedNodes t

            | EmptyNode _ :: t ->
                let selectedNodes = 
                    acc 
                    |> Seq.collect(fun n -> 
                        n.DescendantsAndSelf() 
                        |> Seq.filter(fun d ->
                            String.IsNullOrWhiteSpace (d.DirectInnerText()) && d.Descendants() |> Seq.isEmpty))
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
                    |> Seq.filter (fun x -> x.Attributes() |> Seq.exists(fun a -> a.Name() = "disabled") |> not)
                    |> Seq.toList
                selectElements' FilterLevel.Root selectedNodes t

            | AllChildren _ :: t -> 
                selectElements' FilterLevel.Descendants acc t

            | DirectChildren _ :: t -> 
                selectElements' FilterLevel.Children acc t

            | [] -> acc
            | tok -> failwithf "Invalid token: %A" tok

        selectElements' FilterLevel.Descendants nodes tokens

[<AutoOpen>]
module CssSelectorExtensions =

    open HtmlCssSelectors

    [<Extension>]
    type CssSelectorExtensions =

        static member private Select nodes selector =
            let tokenizer = CssSelectorTokenizer()
            match tokenizer.Tokenize selector with
            | [] -> []
            | tokens -> 
                List.ofSeq nodes |> selectCssElements tokens

        /// Gets descendants matched by Css selector
        [<Extension>]
        static member CssSelect(doc:HtmlDocument, selector) = 
            CssSelectorExtensions.Select [doc.Html()] selector
        
        /// Gets descendants matched by Css selector
        [<Extension>]
        static member CssSelect(nodes:HtmlNode seq, selector) = 
            CssSelectorExtensions.Select (List.ofSeq nodes) selector

        /// Gets descendants matched by Css selector
        [<Extension>]
        static member CssSelect(node:HtmlNode, selector) = 
            CssSelectorExtensions.Select [node] selector


