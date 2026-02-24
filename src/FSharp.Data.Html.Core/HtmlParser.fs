#nowarn "10001"
namespace FSharp.Data

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open FSharp.Data
open FSharp.Data.Runtime
open System.Runtime.InteropServices
open System.Collections.Generic

// --------------------------------------------------------------------------------------

module private TextParser =

    let toPattern f c = if f c then ValueSome c else ValueNone

    [<return: Struct>]
    let (|EndOfFile|_|) (c: char) =
        let value = c |> int

        if (value = -1 || value = 65535) then
            ValueSome c
        else
            ValueNone

    [<return: Struct>]
    let (|Whitespace|_|) = toPattern Char.IsWhiteSpace

    [<return: Struct>]
    let (|LetterDigit|_|) = toPattern Char.IsLetterOrDigit

    [<return: Struct>]
    let (|Letter|_|) = toPattern Char.IsLetter

// --------------------------------------------------------------------------------------

module internal HtmlParser =

    let wsRegex = lazy Regex("\\s+", RegexOptions.Compiled)
    let invalidTypeNameRegex = lazy Regex("[^0-9a-zA-Z_]+", RegexOptions.Compiled)
    let headingRegex = lazy Regex("""h\d""", RegexOptions.Compiled)

    type HtmlToken =
        | DocType of string
        | Tag of isSelfClosing: bool * name: string * attrs: HtmlAttribute list
        | TagEnd of string
        | Text of string
        | Comment of string
        | CData of string
        | InlineWhitespace // normalised whitespace-only DefaultMode text; kept only between inline siblings
        | EOF

        override x.ToString() =
            match x with
            | DocType dt -> sprintf "doctype %s" dt
            | Tag(selfClose, name, _) -> sprintf "tag %b %s" selfClose name
            | TagEnd name -> sprintf "tagEnd %s" name
            | Text _ -> "text"
            | Comment _ -> "comment"
            | EOF -> "eof"
            | CData _ -> "cdata"
            | InlineWhitespace -> "inlineWhitespace"

        member x.IsEndTag name =
            match x with
            | TagEnd(endName) when name = endName -> true
            | _ -> false

    type TextReader with

        member x.PeekChar() = x.Peek() |> char
        member x.ReadChar() = x.Read() |> char

        member x.ReadNChar(n) =
            let buffer = Array.zeroCreate n
            x.ReadBlock(buffer, 0, n) |> ignore
            String(buffer)

    type CharList =
        { mutable Contents: StringBuilder }

        static member Empty = { Contents = StringBuilder() }

        override x.ToString() = x.Contents.ToString()

        member x.Cons(c: char) = x.Contents.Append(c) |> ignore
        member x.Length = x.Contents.Length
        member x.Clear() = x.Contents.Clear() |> ignore

    type InsertionMode =
        | DefaultMode
        | ScriptMode
        | CharRefMode
        | CommentMode
        | DocTypeMode
        | CDATAMode

        override x.ToString() =
            match x with
            | DefaultMode -> "default"
            | ScriptMode -> "script"
            | CharRefMode -> "charref"
            | CommentMode -> "comment"
            | DocTypeMode -> "doctype"
            | CDATAMode -> "cdata"

    type HtmlState =
        { mutable Attributes: (CharList * CharList) list
          mutable CurrentTag: CharList
          mutable Content: CharList
          mutable HasFormattedParent: bool
          mutable InsertionMode: InsertionMode
          mutable Tokens: HtmlToken list
          Reader: TextReader }

        static member Create(reader: TextReader) =
            { Attributes = []
              CurrentTag = { Contents = StringBuilder() }
              Content = { Contents = StringBuilder() }
              HasFormattedParent = false
              InsertionMode = DefaultMode
              Tokens = []
              Reader = reader }

        member x.Pop() = x.Reader.Read() |> ignore
        member x.Peek() = x.Reader.PeekChar()

        member x.Pop(count) =
            [| 0 .. (count - 1) |] |> Array.map (fun _ -> x.Reader.ReadChar())

        member x.Contents = x.Content.ToString()
        member x.ContentLength = x.Content.Length

        member x.NewAttribute() =
            x.Attributes <- ({ Contents = StringBuilder() }, { Contents = StringBuilder() }) :: x.Attributes

        member x.ConsAttrName() =
            match x.Attributes with
            | [] ->
                x.NewAttribute()
                x.ConsAttrName()
            | (h, _) :: _ -> h.Cons(Char.ToLowerInvariant(x.Reader.ReadChar()))

        member x.CurrentTagName() = x.CurrentTag.ToString().Trim()

        member x.CurrentAttrName() =
            match x.Attributes with
            | [] -> String.Empty
            | (h, _) :: _ -> h.ToString()

        member x.ConsAttrValue(c) =
            match x.Attributes with
            | [] ->
                x.NewAttribute()
                x.ConsAttrValue(c)
            | (_, h) :: _ -> h.Cons(c)

        member x.ConsAttrValue() = x.ConsAttrValue(x.Reader.ReadChar())

        member x.GetAttributes() =
            x.Attributes
            |> List.choose (fun (key, value) ->
                if key.Length > 0 then
                    Some <| HtmlAttribute(key.ToString(), value.ToString())
                else
                    None)
            |> List.rev

        member x.EmitSelfClosingTag() =
            let name = x.CurrentTag.ToString().Trim()
            let result = Tag(true, name, x.GetAttributes())
            x.CurrentTag <- { Contents = StringBuilder() }
            x.InsertionMode <- DefaultMode
            x.Attributes <- []
            x.Tokens <- result :: x.Tokens

        member x.IsFormattedTag =
            match x.CurrentTagName().ToLower() with
            | "pre" -> true
            | _ -> false

        member x.IsScriptTag =
            match x.CurrentTagName().ToLower() with
            | "script"
            | "style" -> true
            | _ -> false

        member x.EmitTag(isEnd) =
            let name = x.CurrentTag.ToString().Trim()

            let result =
                if isEnd then
                    if x.ContentLength > 0 then
                        x.Emit()
                        TagEnd(name)
                    else
                        TagEnd(name)
                else
                    Tag(false, name, x.GetAttributes())

            // pre is the only default formatted tag, nested pres are not
            // allowed in the spec.
            if x.IsFormattedTag then
                x.HasFormattedParent <- not isEnd
            else
                x.HasFormattedParent <- x.HasFormattedParent || x.IsFormattedTag

            x.InsertionMode <-
                if x.IsScriptTag && (not isEnd) then
                    ScriptMode
                else
                    DefaultMode

            x.CurrentTag <- { Contents = StringBuilder() }
            x.Attributes <- []
            x.Tokens <- result :: x.Tokens

        member x.EmitToAttributeValue() =
            assert (x.InsertionMode = InsertionMode.CharRefMode)
            let content = x.Content.ToString() |> HtmlCharRefs.substitute

            for c in content.ToCharArray() do
                x.ConsAttrValue c

            x.Content <- { Contents = StringBuilder() }
            x.InsertionMode <- DefaultMode

        member x.Emit() : unit =
            let result =
                let content = x.Content.ToString()

                match x.InsertionMode with
                | DefaultMode ->
                    if x.HasFormattedParent then
                        Text content
                    else
                        let normalizedContent = wsRegex.Value.Replace(content, " ")

                        if normalizedContent = " " then
                            InlineWhitespace // inter-element whitespace; kept only between inline siblings
                        else
                            Text normalizedContent
                | ScriptMode -> content |> Text
                | CharRefMode -> content.Trim() |> HtmlCharRefs.substitute |> Text
                | CommentMode -> Comment content
                | DocTypeMode -> DocType content
                | CDATAMode -> CData(content.Replace("<![CDATA[", "").Replace("]]>", ""))

            x.Content <- { Contents = StringBuilder() }
            x.InsertionMode <- DefaultMode

            match result with
            | Text t when String.IsNullOrEmpty(t) -> ()
            | _ -> x.Tokens <- result :: x.Tokens

        member x.Cons() = x.Content.Cons(x.Reader.ReadChar())
        member x.Cons(char: char) = x.Content.Cons(char)
        member x.Cons(chars: char array) = Array.iter (x.Content.Cons) chars
        member x.Cons(chars: string) = x.Cons(chars.ToCharArray())

        member x.ConsTag() =
            match x.Reader.ReadChar() with
            | TextParser.Whitespace _ -> ()
            | a -> x.CurrentTag.Cons(Char.ToLowerInvariant a)

        member x.ClearContent() = x.Content.Clear()

    // Tokenises a stream into a sequence of HTML tokens.
    let private tokenise reader =
        let state = HtmlState.Create reader

        let rec data (state: HtmlState) =
            match state.Peek() with
            | '<' ->
                if state.ContentLength > 0 then
                    state.Emit()
                else
                    state.Pop()
                    tagOpen state
            | TextParser.EndOfFile _ -> state.Tokens <- EOF :: state.Tokens
            | '&' ->
                if state.ContentLength > 0 then
                    state.Emit()
                else
                    state.InsertionMode <- CharRefMode
                    charRef state
            | _ ->
                match state.InsertionMode with
                | DefaultMode ->
                    state.Cons()
                    data state
                | ScriptMode -> script state
                | CharRefMode -> charRef state
                | DocTypeMode -> docType state
                | CommentMode -> comment state
                | CDATAMode -> data state

        and script state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | ''' ->
                state.Cons()
                scriptSingleQuoteString state
            | '"' ->
                state.Cons()
                scriptDoubleQuoteString state
            | '/' ->
                state.Cons()
                scriptSlash state
            | '<' ->
                state.Pop()
                scriptLessThanSign state
            | _ ->
                state.Cons()
                script state

        and scriptSingleQuoteString state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | ''' ->
                state.Cons()
                script state
            | '\\' ->
                state.Cons()
                scriptSingleQuoteStringBackslash state
            | _ ->
                state.Cons()
                scriptSingleQuoteString state

        and scriptDoubleQuoteString state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '"' ->
                state.Cons()
                script state
            | '\\' ->
                state.Cons()
                scriptDoubleQuoteStringBackslash state
            | _ ->
                state.Cons()
                scriptDoubleQuoteString state

        and scriptSingleQuoteStringBackslash state =
            match state.Peek() with
            | _ ->
                state.Cons()
                scriptSingleQuoteString state

        and scriptDoubleQuoteStringBackslash state =
            match state.Peek() with
            | _ ->
                state.Cons()
                scriptDoubleQuoteString state

        and scriptSlash state =
            match state.Peek() with
            | '/' ->
                state.Cons()
                scriptSingleLineComment state
            | '*' ->
                state.Cons()
                scriptMultiLineComment state
            | _ -> script state

        and scriptMultiLineComment state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '*' ->
                state.Cons()
                scriptMultiLineCommentStar state
            | _ ->
                state.Cons()
                scriptMultiLineComment state

        and scriptMultiLineCommentStar state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '/' ->
                state.Cons()
                script state
            | _ -> scriptMultiLineComment state

        and scriptSingleLineComment state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '\n' ->
                state.Cons()
                script state
            | _ ->
                state.Cons()
                scriptSingleLineComment state

        and scriptLessThanSign state =
            match state.Peek() with
            | '/' ->
                state.Pop()
                scriptEndTagOpen state
            | '!' ->
                state.Cons('<')
                state.Cons()
                scriptDataEscapeStart state
            | _ ->
                state.Cons('<')
                state.Cons()
                script state

        and scriptDataEscapeStart state =
            match state.Peek() with
            | '-' ->
                state.Cons()
                scriptDataEscapeStartDash state
            | _ -> script state

        and scriptDataEscapeStartDash state =
            match state.Peek() with
            | '-' ->
                state.Cons()
                scriptDataEscapedDashDash state
            | _ -> script state

        and scriptDataEscapedDashDash state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '-' ->
                state.Cons()
                scriptDataEscapedDashDash state
            | '<' ->
                state.Pop()
                scriptDataEscapedLessThanSign state
            | '>' ->
                state.Cons()
                script state
            | _ ->
                state.Cons()
                scriptDataEscaped state

        and scriptDataEscapedLessThanSign state =
            match state.Peek() with
            | '/' ->
                state.Pop()
                scriptDataEscapedEndTagOpen state
            | TextParser.Letter _ ->
                state.Cons('<')
                state.Cons()
                scriptDataDoubleEscapeStart state
            | _ ->
                state.Cons('<')
                state.Cons()
                scriptDataEscaped state

        and scriptDataDoubleEscapeStart state =
            match state.Peek() with
            | TextParser.Whitespace _
            | '/'
            | '>' when state.IsScriptTag ->
                state.Cons()
                scriptDataDoubleEscaped state
            | TextParser.Letter _ ->
                state.Cons()
                scriptDataDoubleEscapeStart state
            | _ ->
                state.Cons()
                scriptDataEscaped state

        and scriptDataDoubleEscaped state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '-' ->
                state.Cons()
                scriptDataDoubleEscapedDash state
            | '<' ->
                state.Cons()
                scriptDataDoubleEscapedLessThanSign state
            | _ ->
                state.Cons()
                scriptDataDoubleEscaped state

        and scriptDataDoubleEscapedDash state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '-' ->
                state.Cons()
                scriptDataDoubleEscapedDashDash state
            | '<' ->
                state.Cons()
                scriptDataDoubleEscapedLessThanSign state
            | _ ->
                state.Cons()
                scriptDataDoubleEscaped state

        and scriptDataDoubleEscapedLessThanSign state =
            match state.Peek() with
            | '/' ->
                state.Cons()
                scriptDataDoubleEscapeEnd state
            | _ ->
                state.Cons()
                scriptDataDoubleEscaped state

        and scriptDataDoubleEscapeEnd state =
            match state.Peek() with
            | TextParser.Whitespace _
            | '/'
            | '>' when state.IsScriptTag ->
                state.Cons()
                scriptDataDoubleEscaped state
            | TextParser.Letter _ ->
                state.Cons()
                scriptDataDoubleEscapeEnd state
            | _ ->
                state.Cons()
                scriptDataDoubleEscaped state

        and scriptDataDoubleEscapedDashDash state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '-' ->
                state.Cons()
                scriptDataDoubleEscapedDashDash state
            | '<' ->
                state.Cons()
                scriptDataDoubleEscapedLessThanSign state
            | '>' ->
                state.Cons()
                script state
            | _ ->
                state.Cons()
                scriptDataDoubleEscaped state

        and scriptDataEscapedEndTagOpen state =
            match state.Peek() with
            | TextParser.Letter _ -> scriptDataEscapedEndTagName state
            | _ ->
                state.Cons([| '<'; '/' |])
                state.Cons()
                scriptDataEscaped state

        and scriptDataEscapedEndTagName state =
            match state.Peek() with
            | TextParser.Whitespace _ when state.IsScriptTag ->
                state.Pop()
                beforeAttributeName state
            | '/' when state.IsScriptTag ->
                state.Pop()
                selfClosingStartTag state
            | '>' when state.IsScriptTag ->
                state.Pop()
                state.EmitTag(true)
            | '>' ->
                state.Cons([| '<'; '/' |])
                state.Cons(state.CurrentTagName())
                state.CurrentTag.Clear()
                script state
            | TextParser.Letter _ ->
                state.ConsTag()
                scriptDataEscapedEndTagName state
            | _ ->
                state.Cons([| '<'; '/' |])
                state.Cons()
                scriptDataEscaped state

        and scriptDataEscaped state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '-' ->
                state.Cons()
                scriptDataEscapedDash state
            | '<' -> scriptDataEscapedLessThanSign state
            | _ ->
                state.Cons()
                scriptDataEscaped state

        and scriptDataEscapedDash state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '-' ->
                state.Cons()
                scriptDataEscapedDashDash state
            | '<' -> scriptDataEscapedLessThanSign state
            | _ ->
                state.Cons()
                scriptDataEscaped state

        and scriptEndTagOpen state =
            match state.Peek() with
            | TextParser.Letter _ -> scriptEndTagName state
            | _ ->
                state.Cons('<')
                state.Cons('/')
                script state

        and scriptEndTagName state =
            match state.Peek() with
            | TextParser.Whitespace _ ->
                state.Pop()
                beforeAttributeName state
            | '/' when state.IsScriptTag ->
                state.Pop()
                selfClosingStartTag state
            | '>' when state.IsScriptTag ->
                state.Pop()
                state.EmitTag(true)
            | TextParser.Letter _ ->
                state.ConsTag()
                scriptEndTagName state
            | _ ->
                state.Cons([| '<'; '/' |])
                state.Cons(state.CurrentTagName())
                state.CurrentTag.Clear()
                script state

        and charRef state =
            match state.Peek() with
            | ';' ->
                state.Cons()
                state.Emit()
            | '<' -> state.Emit()
            // System.IO.TextReader.Read() returns -1
            // at end of stream, and -1 cast to char is \uffff.
            | '\uffff' -> state.Emit()
            | _ ->
                state.Cons()
                charRef state

        and tagOpen state =
            match state.Peek() with
            | '!' ->
                state.Pop()
                markupDeclaration state
            | '/' ->
                state.Pop()
                endTagOpen state
            | '?' ->
                state.Pop()
                bogusComment state
            | TextParser.Letter _ ->
                state.ConsTag()
                tagName false state
            | _ ->
                state.Cons('<')
                data state

        and bogusComment state =
            let rec bogusComment' (state: HtmlState) =
                let exitBogusComment state =
                    state.InsertionMode <- CommentMode
                    state.Emit()

                match state.Peek() with
                | '>' ->
                    state.Cons()
                    exitBogusComment state
                | TextParser.EndOfFile _ -> exitBogusComment state
                | _ ->
                    state.Cons()
                    bogusComment' state

            bogusComment' state

        and markupDeclaration state =
            match state.Pop(2) with
            | [| '-'; '-' |] -> comment state
            | current ->
                match new String(Array.append current (state.Pop(5))) with
                | "DOCTYPE" -> docType state
                | "[CDATA[" ->
                    state.Cons("<![CDATA[".ToCharArray())
                    cData 0 state
                | _ -> bogusComment state

        and cData i (state: HtmlState) =
            match state.Peek() with
            | ']' when i = 0 || i = 1 ->
                state.Cons()
                cData (i + 1) state
            | '>' when i = 2 ->
                state.Cons()
                state.InsertionMode <- CDATAMode
                state.Emit()
            | TextParser.EndOfFile _ ->
                state.InsertionMode <- CDATAMode
                state.Emit()
            | _ ->
                state.Cons()
                cData 0 state

        and docType state =
            match state.Peek() with
            | '>' ->
                state.Pop()
                state.InsertionMode <- DocTypeMode
                state.Emit()
            | _ ->
                state.Cons()
                docType state

        and comment state =
            match state.Peek() with
            | '-' ->
                state.Pop()
                commentEndDash state
            | TextParser.EndOfFile _ ->
                state.InsertionMode <- CommentMode
                state.Emit()
            | _ ->
                state.Cons()
                comment state

        and commentEndDash state =
            match state.Peek() with
            | '-' ->
                state.Pop()
                commentEndState state
            | TextParser.EndOfFile _ ->
                state.InsertionMode <- CommentMode
                state.Emit()
            | _ ->
                state.Cons()
                comment state

        and commentEndState state =
            match state.Peek() with
            | '>' ->
                state.Pop()
                state.InsertionMode <- CommentMode
                state.Emit()
            | TextParser.EndOfFile _ ->
                state.InsertionMode <- CommentMode
                state.Emit()
            | _ ->
                state.Cons()
                comment state

        and tagName isEndTag state =
            match state.Peek() with
            | TextParser.Whitespace _ ->
                state.Pop()
                beforeAttributeName state
            | TextParser.EndOfFile _ -> state.EmitTag(isEndTag)
            | '/' ->
                state.Pop()
                selfClosingStartTag state
            | '>' ->
                state.Pop()
                state.EmitTag(isEndTag)
            | _ ->
                state.ConsTag()
                tagName isEndTag state

        and selfClosingStartTag state =
            match state.Peek() with
            | '>' ->
                state.Pop()
                state.EmitSelfClosingTag()
            | TextParser.EndOfFile _ -> data state
            | _ -> beforeAttributeName state

        and endTagOpen state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | TextParser.Letter _ ->
                state.ConsTag()
                tagName true state
            | '>' ->
                state.Pop()
                data state
            | _ -> comment state

        and beforeAttributeName state =
            match state.Peek() with
            | TextParser.Whitespace _ ->
                state.Pop()
                beforeAttributeName state
            | '/' ->
                state.Pop()
                selfClosingStartTag state
            | '>' ->
                state.Pop()
                state.EmitTag(false)
            | _ -> attributeName state

        and attributeName state =
            match state.Peek() with
            | '=' ->
                state.Pop()
                beforeAttributeValue state
            | '/' ->
                state.Pop()
                selfClosingStartTag state
            | '>' ->
                state.Pop()
                state.EmitTag(false)
            | TextParser.LetterDigit _ ->
                state.ConsAttrName()
                attributeName state
            | TextParser.Whitespace _ -> afterAttributeName state
            | TextParser.EndOfFile _ -> state.EmitTag(false)
            | _ ->
                state.ConsAttrName()
                attributeName state

        and afterAttributeName state =
            match state.Peek() with
            | TextParser.Whitespace _ ->
                state.Pop()
                afterAttributeName state
            | '/' ->
                state.Pop()
                selfClosingStartTag state
            | '>' ->
                state.Pop()
                state.EmitTag(false)
            | '=' ->
                state.Pop()
                beforeAttributeValue state
            | _ ->
                state.NewAttribute()
                attributeName state

        and beforeAttributeValue state =
            match state.Peek() with
            | TextParser.Whitespace _ ->
                state.Pop()
                beforeAttributeValue state
            | TextParser.EndOfFile _ -> state.EmitTag(false)
            | '/' ->
                state.Pop()
                selfClosingStartTag state
            | '>' ->
                state.Pop()
                state.EmitTag(false)
            | '"' ->
                state.Pop()
                attributeValueQuoted '"' state
            | '\'' ->
                state.Pop()
                attributeValueQuoted '\'' state
            | _ -> attributeValueUnquoted state

        and attributeValueUnquoted state =
            match state.Peek() with
            | TextParser.Whitespace _ ->
                state.Pop()
                state.NewAttribute()
                beforeAttributeName state
            | '/' ->
                state.Pop()
                attributeValueUnquotedSlash state
            | '>' ->
                state.Pop()
                state.EmitTag(false)
            | '&' ->
                assert (state.ContentLength = 0)
                state.InsertionMode <- InsertionMode.CharRefMode
                attributeValueUnquotedCharRef [ '/'; '>' ] state
            | _ ->
                state.ConsAttrValue()
                attributeValueUnquoted state

        and attributeValueUnquotedSlash state =
            match state.Peek() with
            | '>' -> selfClosingStartTag state
            | _ ->
                state.ConsAttrValue('/')
                state.ConsAttrValue()
                attributeValueUnquoted state

        and attributeValueQuoted quote state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | c when c = quote ->
                state.Pop()
                afterAttributeValueQuoted state
            | '&' ->
                assert (state.ContentLength = 0)
                state.InsertionMode <- InsertionMode.CharRefMode
                attributeValueQuotedCharRef quote state
            | _ ->
                state.ConsAttrValue()
                attributeValueQuoted quote state

        and attributeValueQuotedCharRef quote state =
            match state.Peek() with
            | ';' ->
                state.Cons()
                state.EmitToAttributeValue()
                attributeValueQuoted quote state
            | TextParser.EndOfFile _ ->
                state.EmitToAttributeValue()
                attributeValueQuoted quote state
            | c when c = quote ->
                state.EmitToAttributeValue()
                attributeValueQuoted quote state
            | _ ->
                state.Cons()
                attributeValueQuotedCharRef quote state

        and attributeValueUnquotedCharRef stop state =
            match state.Peek() with
            | ';' ->
                state.Cons()
                state.EmitToAttributeValue()
                attributeValueUnquoted state
            | TextParser.EndOfFile _ ->
                state.EmitToAttributeValue()
                attributeValueUnquoted state
            | c when List.exists ((=) c) stop ->
                state.EmitToAttributeValue()
                attributeValueUnquoted state
            | _ ->
                state.Cons()
                attributeValueUnquotedCharRef stop state

        and afterAttributeValueQuoted state =
            match state.Peek() with
            | TextParser.Whitespace _ ->
                state.Pop()
                state.NewAttribute()
                afterAttributeValueQuoted state
            | '/' ->
                state.Pop()
                selfClosingStartTag state
            | '>' ->
                state.Pop()
                state.EmitTag(false)
            | _ ->
                state.NewAttribute()
                attributeName state

        let mutable next = state.Reader.Peek()

        while next <> -1 do
            data state
            next <- state.Reader.Peek()

        state.Tokens |> List.rev

    // Block-level HTML elements. Whitespace-only text nodes that are siblings of
    // these elements are inter-element whitespace and are insignificant.
    let private blockLevelElements =
        set
            [ "address"
              "article"
              "aside"
              "blockquote"
              "body"
              "caption"
              "col"
              "colgroup"
              "dd"
              "details"
              "dialog"
              "dir"
              "div"
              "dl"
              "dt"
              "fieldset"
              "figcaption"
              "figure"
              "footer"
              "form"
              "frameset"
              "h1"
              "h2"
              "h3"
              "h4"
              "h5"
              "h6"
              "head"
              "header"
              "hgroup"
              "html"
              "legend"
              "li"
              "link"
              "main"
              "menu"
              "meta"
              "nav"
              "noscript"
              "ol"
              "optgroup"
              "option"
              "p"
              "pre"
              "script"
              "section"
              "style"
              "summary"
              "table"
              "tbody"
              "td"
              "tfoot"
              "th"
              "thead"
              "title"
              "tr"
              "ul" ]

    let private parse reader =
        let canNotHaveChildren (name: string) =
            match name with
            | "area"
            | "base"
            | "br"
            | "col"
            | "embed"
            | "hr"
            | "img"
            | "input"
            | "keygen"
            | "link"
            | "menuitem"
            | "meta"
            | "param"
            | "source"
            | "track"
            | "wbr" -> true
            | _ -> false

        let isImplicitlyClosedByStartTag expectedTagEnd startTag =
            match expectedTagEnd, startTag with
            | ("td" | "th"), ("tr" | "td" | "th") -> true
            | "tr", "tr" -> true
            | "li", "li" -> true
            | _ -> false

        let implicitlyCloseByStartTag expectedTagEnd startTag tokens =
            match expectedTagEnd, startTag with
            | ("td" | "th"), "tr" ->
                // the new tr is closing the cell and previous row
                TagEnd expectedTagEnd :: TagEnd "tr" :: tokens
            | ("td" | "th"), ("td" | "th")
            | "tr", "tr"
            | "li", "li" ->
                // tags are on same level, just close
                TagEnd expectedTagEnd :: tokens
            | _ -> tokens

        let isImplicitlyClosedByEndTag expectedTagEnd startTag =
            match expectedTagEnd, startTag with
            | ("td" | "th" | "tr"), ("thead" | "tbody" | "tfoot" | "table") -> true
            | "li", "ul" -> true
            | _ -> false

        let implicitlyCloseByEndTag expectedTagEnd tokens =
            match expectedTagEnd with
            | "td"
            | "th" ->
                // the end tag closes the cell and the row
                TagEnd expectedTagEnd :: TagEnd "tr" :: tokens
            | "tr"
            | "li" ->
                // Only on level need to be closed
                TagEnd expectedTagEnd :: tokens
            | _ -> tokens


        let rec parse'
            (callstack: Stack<string * HtmlNode list * string * string * string * HtmlAttribute list>)
            docType
            elements
            expectedTagEnd
            parentTagName
            (tokens: HtmlToken list)
            =
            let parse' = parse' callstack

            let recursiveReturn (dt, tokens, content) =
                if callstack.Count = 0 then
                    (dt, tokens, content)
                else
                    let _, elements, expectedTagEnd, parentTagName, name, attributes = callstack.Pop()
                    let e = HtmlNode.HtmlElement(name, attributes, content)
                    parse' dt (e :: elements) expectedTagEnd parentTagName tokens

            match tokens with
            | DocType dt :: rest -> parse' (dt.Trim()) elements expectedTagEnd parentTagName rest
            | Tag(_, "br", []) :: rest ->
                let t = HtmlNode.HtmlText Environment.NewLine
                parse' docType (t :: elements) expectedTagEnd parentTagName rest
            | Tag(true, name, attributes) :: rest ->
                let e = HtmlNode.HtmlElement(name, attributes, [])
                parse' docType (e :: elements) expectedTagEnd parentTagName rest
            | Tag(false, name, attributes) :: rest when canNotHaveChildren name ->
                let e = HtmlNode.HtmlElement(name, attributes, [])
                parse' docType (e :: elements) expectedTagEnd parentTagName rest
            | Tag(_, name, _) :: _ when isImplicitlyClosedByStartTag expectedTagEnd name ->
                // insert missing </tr> </td> or </th> when starting new row/cell/header
                parse'
                    docType
                    elements
                    expectedTagEnd
                    parentTagName
                    (implicitlyCloseByStartTag expectedTagEnd name tokens)
            | TagEnd(name) :: _ when isImplicitlyClosedByEndTag expectedTagEnd name ->
                // insert missing </tr> </td> or </th> when starting new row/cell/header
                parse' docType elements expectedTagEnd parentTagName (implicitlyCloseByEndTag expectedTagEnd tokens)

            | Tag(_, name, attributes) :: rest ->
                (docType, elements, expectedTagEnd, parentTagName, name, attributes)
                |> callstack.Push

                parse' docType [] name expectedTagEnd rest
            | TagEnd name :: _ when name <> expectedTagEnd && name = parentTagName ->
                // insert missing closing tag
                parse' docType elements expectedTagEnd parentTagName (TagEnd expectedTagEnd :: tokens)
            | TagEnd name :: rest when
                name <> expectedTagEnd
                && (name <> (new String(expectedTagEnd.ToCharArray() |> Array.rev)))
                ->
                // ignore this token if not the expected end tag (or it's reverse, eg: <li></il>)
                parse' docType elements expectedTagEnd parentTagName rest
            | TagEnd _ :: rest -> recursiveReturn (docType, rest, List.rev elements)
            | InlineWhitespace :: rest ->
                // This is normalised whitespace-only content from DefaultMode (e.g. the space
                // between "</span> <span>").  Keep it as a space text node only when BOTH the
                // previous accumulated node and the next token represent inline content.
                let prevIsInline =
                    match elements with
                    | HtmlNode.HtmlElement(name, _, _) :: _ -> not (Set.contains name blockLevelElements)
                    | HtmlNode.HtmlText t :: _ -> not (String.IsNullOrWhiteSpace t)
                    | _ -> false

                let nextIsInline =
                    match rest with
                    | Text t :: _ when t <> "" -> true
                    | Tag(_, name, _) :: _ -> not (Set.contains name blockLevelElements)
                    | _ -> false

                if prevIsInline && nextIsInline then
                    parse' docType (HtmlNode.HtmlText " " :: elements) expectedTagEnd parentTagName rest
                else
                    parse' docType elements expectedTagEnd parentTagName rest
            | Text a :: Text b :: rest ->
                if a = "" && b = "" then
                    // ignore this token
                    parse' docType elements expectedTagEnd parentTagName rest
                else
                    let t = HtmlNode.HtmlText(a + b)
                    parse' docType (t :: elements) expectedTagEnd parentTagName rest
            | Text cont :: rest ->
                if cont = "" then
                    // ignore this token
                    parse' docType elements expectedTagEnd parentTagName rest
                else
                    let t = HtmlNode.HtmlText cont
                    parse' docType (t :: elements) expectedTagEnd parentTagName rest
            | Comment cont :: rest ->
                let c = HtmlNode.HtmlComment cont
                parse' docType (c :: elements) expectedTagEnd parentTagName rest
            | CData cont :: rest ->
                let c = HtmlNode.HtmlCData cont
                parse' docType (c :: elements) expectedTagEnd parentTagName rest
            | EOF :: _ -> recursiveReturn (docType, [], List.rev elements)
            | [] -> recursiveReturn (docType, [], List.rev elements)

        let tokens = tokenise reader
        let docType, _, elements = tokens |> parse' (new Stack<_>()) "" [] "" ""

        if List.isEmpty elements then
            failwith "Invalid HTML"

        docType, elements

    /// All attribute names and tag names will be normalized to lowercase
    /// All html entities will be replaced by the corresponding characters
    /// All the consecutive whitespace (except for `&nbsp;`) will be collapsed to a single space
    /// All br tags will be replaced by newlines
    let parseDocument reader = HtmlDocument(parse reader)

    /// All attribute names and tag names will be normalized to lowercase
    /// All html entities will be replaced by the corresponding characters
    /// All the consecutive whitespace (except for `&nbsp;`) will be collapsed to a single space
    /// All br tags will be replaced by newlines
    let parseFragment reader = parse reader |> snd

[<AutoOpen>]
module HtmlAutoOpens =
    // --------------------------------------------------------------------------------------

    type HtmlDocument with

        /// Parses the specified HTML string
        static member Parse(text) =
            use reader = new StringReader(text)
            HtmlParser.parseDocument reader

        /// Loads HTML from the specified stream
        static member Load(stream: Stream) =
            use reader = new StreamReader(stream)
            HtmlParser.parseDocument reader

        /// Loads HTML from the specified reader
        static member Load(reader: TextReader) = HtmlParser.parseDocument reader

        /// Loads HTML from the specified uri asynchronously
        static member AsyncLoad(uri: string, [<Optional>] ?encoding) =
            async {
                let encoding = defaultArg encoding Encoding.UTF8
                let! reader = IO.asyncReadTextAtRuntime false "" "" "HTML" encoding.WebName uri
                return HtmlParser.parseDocument reader
            }

        /// Loads HTML from the specified uri
        static member Load(uri: string, [<Optional>] ?encoding) =
            HtmlDocument.AsyncLoad(uri, ?encoding = encoding) |> Async.RunSynchronously

    type HtmlNode with

        /// Parses the specified HTML string to a list of HTML nodes
        static member Parse(text) =
            use reader = new StringReader(text)
            HtmlParser.parseFragment reader

        /// Parses the specified HTML string to a list of HTML nodes
        static member ParseRooted(rootName, text) =
            use reader = new StringReader(text)
            HtmlNode.HtmlElement(rootName, [], HtmlParser.parseFragment reader)
