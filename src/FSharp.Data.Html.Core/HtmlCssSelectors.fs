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
                | ']' :: t -> tokenize' (CloseAttribute(getOffset t) :: acc) t
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

                    tokenize'
                        (AttributeValue(getOffset t + 1, s)
                         :: AttributeContainsPrefix(getOffset t)
                         :: acc)
                        t'
                | '*' :: '=' :: t ->
                    let s, t' = readString "" t

                    tokenize' (AttributeValue(getOffset t + 1, s) :: AttributeContains(getOffset t) :: acc) t'
                | '~' :: '=' :: t ->
                    let s, t' = readString "" t

                    tokenize' (AttributeValue(getOffset t + 1, s) :: AttributeContainsWord(getOffset t) :: acc) t'
                | '!' :: '=' :: t ->
                    let s, t' = readString "" t

                    tokenize' (AttributeValue(getOffset t + 1, s) :: AttributeNotEqual(getOffset t) :: acc) t'
                | StartsWith ":checkbox" t -> tokenize' (Checkbox(getOffset t + 1) :: acc) t
                | StartsWith ":selected" t -> tokenize' (Selected(getOffset t + 1) :: acc) t
                | StartsWith ":checked" t -> tokenize' (Checked(getOffset t + 1) :: acc) t
                | StartsWith ":button" t -> tokenize' (Button(getOffset t + 1) :: acc) t
                | StartsWith ":hidden" t -> tokenize' (Hidden(getOffset t + 1) :: acc) t
                | StartsWith ":radio" t -> tokenize' (Radio(getOffset t + 1) :: acc) t
                | StartsWith ":password" t -> tokenize' (Password(getOffset t + 1) :: acc) t
                | StartsWith ":empty" t -> tokenize' (EmptyNode(getOffset t + 1) :: acc) t
                | StartsWith ":image" t -> tokenize' (Image(getOffset t + 1) :: acc) t
                | StartsWith ":even" t -> tokenize' (Even(getOffset t + 1) :: acc) t
                | StartsWith ":odd" t -> tokenize' (Odd(getOffset t + 1) :: acc) t
                | TokenStr ":disabled" t ->
                    let _, t' = readString "" t
                    tokenize' (Disabled(getOffset t + 1) :: acc) t'
                | StartsWith ":enabled" t -> tokenize' (Enabled(getOffset t + 1) :: acc) t
                | StartsWith ":file" t -> tokenize' (File(getOffset t + 1) :: acc) t
                | StartsWith ":submit" t -> tokenize' (Submit(getOffset t + 1) :: acc) t
                | ':' :: t ->
                    let s, _ = readString "" t

                    raise (
                        NotSupportedException(
                            sprintf
                                "CSS pseudo-class or pseudo-element ':%s' is not supported. See https://fsprojects.github.io/FSharp.Data/library/HtmlCssSelectors.html for the list of supported selectors."
                                s
                        )
                    )

                | '>' :: t ->
                    let seqtoken = acc |> List.toSeq |> Seq.skip (1) |> Seq.toList

                    match acc.Head with
                    | AllChildren _ -> tokenize' (DirectChildren(getOffset t) :: seqtoken) t
                    | _ -> tokenize' (DirectChildren(getOffset t) :: acc) t
                | c :: t when Char.IsLetterOrDigit(c) ->
                    let str = c.ToString()
                    let s, t' = readString str t
                    tokenize' (TagName(getOffset t, s) :: acc) t'
                | [] -> List.rev acc
                | c :: t ->
                    let offset = getOffset (c :: t)
                    failwith (sprintf "Invalid css selector syntax (char '%c' at offset %d)" c offset)

            tokenize' [] source

        member public x.Tokenize(pCssSelector: string) =
            cssSelector <- pCssSelector
            source <- cssSelector.ToCharArray() |> Array.toList
            charCount <- source.Length
            tokenize ()

    type FilterLevel =
        | Root
        | Children
        | Descendants
