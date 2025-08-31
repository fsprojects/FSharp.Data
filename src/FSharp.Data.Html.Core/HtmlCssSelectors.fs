namespace FSharp.Data

open System

#nowarn "26"

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
        let mutable charCount: int = 0
        let mutable source = List<char>.Empty
        let mutable cssSelector = ""
        let mutable inQuotes: bool = false

        let getOffset (t: List<char>) = charCount - 1 - t.Length

        let isCharacterEscapable (c: char) =
            (* CSS 2.1: Any character (except a hexadecimal digit, linefeed,
               carriage return, or form feed) can be escaped with a backslash to
               remove its special meaning *)
            let isHexadecimalDigit =
                Char.IsDigit(c) || (Char.ToLower(c) >= 'a' && Char.ToLower(c) <= 'f')

            (isHexadecimalDigit || c = '\n' || c = '\f' || c = '\r') |> not

        let rec readString acc =
            function
            | c :: t when
                Char.IsLetterOrDigit(c)
                || c.Equals('-')
                || c.Equals('_')
                || c.Equals('+')
                || c.Equals('/')
                ->
                readString (acc + (c.ToString())) t
            | '\'' :: t ->
                if inQuotes then
                    inQuotes <- false
                    acc, t
                else
                    inQuotes <- true
                    readString acc t
            | '\\' :: c :: t when isCharacterEscapable c -> readString (acc + (c.ToString())) t
            | c :: t when inQuotes -> readString (acc + (c.ToString())) t
            | c :: t -> acc, c :: t
            | [] -> acc, []
            | c -> failwithf "Invalid css selector syntax at: %s" (new String(Array.ofList c))

        let (|StartsWith|_|) (s: string) (items: char list) =
            let candidates = s.ToCharArray()

            if items.Length < candidates.Length then
                None
            else
                let start = items |> Seq.take (candidates.Length) |> Seq.toList

                if (Seq.compareWith (fun a b -> (int a) - (int b)) start candidates) = 0 then
                    Some(items |> Seq.skip s.Length |> Seq.toList)
                else
                    None

        let (|TokenStr|_|) (s: string) x =
            let chars = s.ToCharArray() |> Array.toList

            let rec equal x s =
                match x, s with
                | x, [] -> Some(x)
                | xh :: xt, sh :: st when xh = sh -> equal xt st
                | _ -> None

            equal x chars

        let tokenize () =
            let rec tokenize' (acc: ResizeArray<SelectorToken>) sourceChars =
                match sourceChars with
                | w :: t when Char.IsWhiteSpace(w) ->
                    if acc.Count > 0 then
                        let lastToken = acc.[acc.Count - 1]

                        match lastToken with
                        | AllChildren _ ->
                            acc.[acc.Count - 1] <- AllChildren(getOffset t)
                            tokenize' acc t
                        | DirectChildren _ ->
                            acc.[acc.Count - 1] <- DirectChildren(getOffset t)
                            tokenize' acc t
                        | _ ->
                            acc.Add(AllChildren(getOffset t))
                            tokenize' acc t
                    else
                        acc.Add(AllChildren(getOffset t))
                        tokenize' acc t
                | '.' :: t ->
                    let s, t' = readString "" t
                    acc.Add(ClassPrefix(getOffset t))
                    acc.Add(CssClass(getOffset t + 1, s))
                    tokenize' acc t'
                | '#' :: t ->
                    let s, t' = readString "" t
                    acc.Add(IdPrefix(getOffset t))
                    acc.Add(CssId(getOffset t + 1, s))
                    tokenize' acc t'
                | '[' :: t ->
                    let s, t' = readString "" t
                    acc.Add(OpenAttribute(getOffset t))
                    acc.Add(AttributeName(getOffset t + 1, s))
                    tokenize' acc t'
                | ']' :: t ->
                    acc.Add(CloseAttribute(getOffset t))
                    tokenize' acc t
                | '=' :: t ->
                    let s, t' = readString "" t
                    acc.Add(Assign(getOffset t))
                    acc.Add(AttributeValue(getOffset t + 1, s))
                    tokenize' acc t'
                | '$' :: '=' :: t ->
                    let s, t' = readString "" t
                    acc.Add(EndWith(getOffset t))
                    acc.Add(AttributeValue(getOffset t + 1, s))
                    tokenize' acc t'
                | '^' :: '=' :: t ->
                    let s, t' = readString "" t
                    acc.Add(StartWith(getOffset t))
                    acc.Add(AttributeValue(getOffset t + 1, s))
                    tokenize' acc t'
                | '|' :: '=' :: t ->
                    let s, t' = readString "" t
                    acc.Add(AttributeContainsPrefix(getOffset t))
                    acc.Add(AttributeValue(getOffset t + 1, s))
                    tokenize' acc t'
                | '*' :: '=' :: t ->
                    let s, t' = readString "" t
                    acc.Add(AttributeContains(getOffset t))
                    acc.Add(AttributeValue(getOffset t + 1, s))
                    tokenize' acc t'
                | '~' :: '=' :: t ->
                    let s, t' = readString "" t
                    acc.Add(AttributeContainsWord(getOffset t))
                    acc.Add(AttributeValue(getOffset t + 1, s))
                    tokenize' acc t'
                | '!' :: '=' :: t ->
                    let s, t' = readString "" t
                    acc.Add(AttributeNotEqual(getOffset t))
                    acc.Add(AttributeValue(getOffset t + 1, s))
                    tokenize' acc t'
                | StartsWith ":checkbox" t ->
                    acc.Add(Checkbox(getOffset t + 1))
                    tokenize' acc t
                | StartsWith ":selected" t ->
                    acc.Add(Selected(getOffset t + 1))
                    tokenize' acc t
                | StartsWith ":checked" t ->
                    acc.Add(Checked(getOffset t + 1))
                    tokenize' acc t
                | StartsWith ":button" t ->
                    acc.Add(Button(getOffset t + 1))
                    tokenize' acc t
                | StartsWith ":hidden" t ->
                    acc.Add(Hidden(getOffset t + 1))
                    tokenize' acc t
                | StartsWith ":radio" t ->
                    acc.Add(Radio(getOffset t + 1))
                    tokenize' acc t
                | StartsWith ":password" t ->
                    acc.Add(Password(getOffset t + 1))
                    tokenize' acc t
                | StartsWith ":empty" t ->
                    acc.Add(EmptyNode(getOffset t + 1))
                    tokenize' acc t
                | StartsWith ":image" t ->
                    acc.Add(Image(getOffset t + 1))
                    tokenize' acc t
                | StartsWith ":even" t ->
                    acc.Add(Even(getOffset t + 1))
                    tokenize' acc t
                | StartsWith ":odd" t ->
                    acc.Add(Odd(getOffset t + 1))
                    tokenize' acc t
                | TokenStr ":disabled" t ->
                    let _, t' = readString "" t
                    acc.Add(Disabled(getOffset t + 1))
                    tokenize' acc t'
                | StartsWith ":enabled" t ->
                    acc.Add(Enabled(getOffset t + 1))
                    tokenize' acc t
                | StartsWith ":file" t ->
                    acc.Add(File(getOffset t + 1))
                    tokenize' acc t
                | StartsWith ":submit" t ->
                    acc.Add(Submit(getOffset t + 1))
                    tokenize' acc t

                | '>' :: t ->
                    if acc.Count > 0 then
                        let lastToken = acc.[acc.Count - 1]

                        match lastToken with
                        | AllChildren _ ->
                            acc.[acc.Count - 1] <- DirectChildren(getOffset t)
                            tokenize' acc t
                        | _ ->
                            acc.Add(DirectChildren(getOffset t))
                            tokenize' acc t
                    else
                        acc.Add(DirectChildren(getOffset t))
                        tokenize' acc t
                | c :: t when Char.IsLetterOrDigit(c) ->
                    let str = c.ToString()
                    let s, t' = readString str t
                    acc.Add(TagName(getOffset t, s))
                    tokenize' acc t'
                | [] -> List.ofSeq acc
                | c :: t ->
                    let offset = getOffset (c :: t)
                    failwith (sprintf "Invalid css selector syntax (char '%c' at offset %d)" c offset)

            tokenize' (ResizeArray<SelectorToken>()) source

        member public x.Tokenize(pCssSelector: string) =
            cssSelector <- pCssSelector
            source <- cssSelector.ToCharArray() |> Array.toList
            charCount <- source.Length
            tokenize ()

    type FilterLevel =
        | Root
        | Children
        | Descendants
