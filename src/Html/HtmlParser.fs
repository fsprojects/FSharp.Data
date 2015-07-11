#nowarn "10001"
namespace FSharp.Data

open System
open System.ComponentModel
open System.IO
open System.Text
open System.Text.RegularExpressions
open FSharp.Data
open FSharp.Data.Runtime

// --------------------------------------------------------------------------------------

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

[<StructuredFormatDisplay("{_Print}")>]
/// Represents an HTML node. The names of elements are always normalized to lowercase
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

[<StructuredFormatDisplay("{_Print}")>]
/// Represents an HTML document
type HtmlDocument = 
    internal | HtmlDocument of docType:string * elements:HtmlNode list
  
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

// --------------------------------------------------------------------------------------

module private TextParser = 

    let toPattern f c = if f c then Some c else None

    let (|EndOfFile|_|) (c : char) =
        let value = c |> int
        if (value = -1 || value = 65535) then Some c else None

    let (|Whitespace|_|) = toPattern Char.IsWhiteSpace
    let (|LetterDigit|_|) = toPattern Char.IsLetterOrDigit
    let (|Letter|_|) = toPattern Char.IsLetter

// --------------------------------------------------------------------------------------

module internal HtmlParser =

    let private regexOptions = 
#if FX_NO_REGEX_COMPILATION
        RegexOptions.None
#else
        RegexOptions.Compiled
#endif
    let wsRegex = lazy Regex("\\s+", regexOptions)
    let invalidTypeNameRegex = lazy Regex("[^0-9a-zA-Z_]+", regexOptions)
    let headingRegex = lazy Regex("""h\d""", regexOptions)

    type HtmlToken =
        | DocType of string
        | Tag of isSelfClosing:bool * name:string * attrs:HtmlAttribute list
        | TagEnd of string
        | Text of string
        | Comment of string
        | CData of string
        | EOF
        override x.ToString() =
            match x with
            | DocType dt -> sprintf "doctype %s" dt
            | Tag(selfClose,name,_) -> sprintf "tag %b %s" selfClose name
            | TagEnd name -> sprintf "tagEnd %s" name
            | Text _ -> "text"
            | Comment _ -> "comment"
            | EOF -> "eof"
            | CData _ -> "cdata"
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
        { mutable Contents : char list }
        static member Empty = { Contents = [] }
        override x.ToString() = String(x.Contents |> List.rev |> List.toArray)
        member x.Cons(c) = x.Contents <- c :: x.Contents
        member x.Length = x.Contents.Length
        member x.Clear() = x.Contents <- []

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
        { Attributes : (CharList * CharList) list ref
          CurrentTag : CharList ref
          Content : CharList ref
          InsertionMode : InsertionMode ref
          Tokens : HtmlToken list ref
          Reader : TextReader }
        static member Create (reader:TextReader) = 
            { Attributes = ref []
              CurrentTag = ref CharList.Empty
              Content = ref CharList.Empty
              InsertionMode = ref DefaultMode
              Tokens = ref []
              Reader = reader }

        member x.Pop() = x.Reader.Read() |> ignore
        member x.Peek() = x.Reader.PeekChar()
        member x.Pop(count) = 
            [|0..(count-1)|] |> Array.map (fun _ -> x.Reader.ReadChar())
        
        member x.Contents = (!x.Content).ToString()  
        member x.ContentLength = (!x.Content).Length
    
        member x.NewAttribute() = x.Attributes := (CharList.Empty, CharList.Empty) :: (!x.Attributes)
    
        member x.ConsAttrName() =
            match !x.Attributes with
            | [] -> x.NewAttribute(); x.ConsAttrName()
            | (h,_) :: _ -> h.Cons(Char.ToLowerInvariant(x.Reader.ReadChar()))
    
        member x.CurrentTagName() = 
            (!x.CurrentTag).ToString().Trim()
    
        member x.CurrentAttrName() = 
            match !x.Attributes with
            | [] -> String.Empty
            | (h,_) :: _ -> h.ToString() 

        member private x.ConsAttrValue(c) =
            match !x.Attributes with
            | [] -> x.NewAttribute(); x.ConsAttrValue(c)
            | (_,h) :: _ -> h.Cons(c)

        member x.ConsAttrValue() = 
            x.ConsAttrValue(x.Reader.ReadChar())
    
        member x.GetAttributes() = 
            !x.Attributes 
            |> List.choose (fun (key, value) -> 
                if key.Length > 0
                then Some <| HtmlAttribute(key.ToString(), value.ToString())
                else None)
            |> List.rev
    
        member x.EmitSelfClosingTag() = 
            let name = (!x.CurrentTag).ToString().Trim()
            let result = Tag(true, name, x.GetAttributes()) 
            x.CurrentTag := CharList.Empty
            x.InsertionMode := DefaultMode
            x.Attributes := []
            x.Tokens := result :: !x.Tokens 

        member x.IsScriptTag 
            with get() = 
               match x.CurrentTagName() with
               | "script" | "style" -> true
               | _ -> false

        member x.EmitTag(isEnd) =
            let name = (!x.CurrentTag).ToString().Trim()
            let result = 
                if isEnd
                then 
                    if x.ContentLength > 0
                    then x.Emit(); TagEnd(name)
                    else TagEnd(name)
                else Tag(false, name, x.GetAttributes())

            x.InsertionMode :=
                if x.IsScriptTag && (not isEnd)
                then ScriptMode
                else DefaultMode

            x.CurrentTag := CharList.Empty
            x.Attributes := []
            x.Tokens := result :: !x.Tokens 
    
        member x.EmitToAttributeValue() =
            assert (!x.InsertionMode = InsertionMode.CharRefMode)
            let content = (!x.Content).ToString() |> HtmlCharRefs.substitute
            for c in content.ToCharArray() do
                x.ConsAttrValue c
            x.Content := CharList.Empty
            x.InsertionMode := DefaultMode

        member x.Emit() : unit =
            let result = 
                let content = (!x.Content).ToString()
                match !x.InsertionMode with
                | DefaultMode -> 
                    let normalizedContent = wsRegex.Value.Replace(content, " ")
                    if normalizedContent = " " then Text "" else Text normalizedContent
                | ScriptMode -> content |> Text
                | CharRefMode -> content.Trim() |> HtmlCharRefs.substitute |> Text
                | CommentMode -> Comment content
                | DocTypeMode -> DocType content
                | CDATAMode -> CData (content.Replace("<![CDATA[", "").Replace("]]>", ""))
            x.Content := CharList.Empty
            x.InsertionMode := DefaultMode
            match result with
            | Text t when String.IsNullOrEmpty(t) -> ()
            | _ -> x.Tokens := result :: !x.Tokens 
    
        member x.Cons() = (!x.Content).Cons(x.Reader.ReadChar())
        member x.Cons(char) = (!x.Content).Cons(char)
        member x.Cons(char) = Array.iter ((!x.Content).Cons) char
        member x.Cons(char : string) = x.Cons(char.ToCharArray())
        member x.ConsTag() = 
            match x.Reader.ReadChar() with
            | TextParser.Whitespace _ -> ()
            | a -> (!x.CurrentTag).Cons(Char.ToLowerInvariant a)
        member x.ClearContent() = 
            (!x.Content).Clear()

    // Tokenises a stream into a sequence of HTML tokens. 
    let private tokenise reader =
        let state = HtmlState.Create reader
        let rec data (state:HtmlState) =
            match state.Peek() with
            | '<' -> 
                if state.ContentLength > 0
                then state.Emit();
                else state.Pop(); tagOpen state
            | TextParser.EndOfFile _ -> state.Tokens := EOF :: !state.Tokens
            | '&' ->
                if state.ContentLength > 0
                then state.Emit();
                else
                    state.InsertionMode := CharRefMode
                    charRef state
            | _ ->
                match !state.InsertionMode with
                | DefaultMode -> state.Cons(); data state
                | ScriptMode -> script state;
                | CharRefMode -> charRef state
                | DocTypeMode -> docType state
                | CommentMode -> comment state
                | CDATAMode -> data state
        and script state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '<' -> state.Pop(); scriptLessThanSign state
            | _ -> state.Cons(); script state
        and scriptLessThanSign state =
            match state.Peek() with
            | '/' -> state.Pop(); scriptEndTagOpen state
            | '!' -> state.Cons('<'); state.Cons(); scriptDataEscapeStart state
            | _ -> state.Cons('<'); state.Cons(); script state
        and scriptDataEscapeStart state = 
            match state.Peek() with
            | '-' -> state.Cons(); scriptDataEscapeStartDash state
            | _ -> script state
        and scriptDataEscapeStartDash state =
            match state.Peek() with
            | '-' -> state.Cons(); scriptDataEscapedDashDash state
            | _ -> script state
        and scriptDataEscapedDashDash state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '-' -> state.Cons(); scriptDataEscapedDashDash state
            | '<' -> state.Pop(); scriptDataEscapedLessThanSign state
            | '>' -> state.Cons(); script state
            | _ -> state.Cons(); scriptDataEscaped state
        and scriptDataEscapedLessThanSign state =
            match state.Peek() with
            | '/' -> state.Pop(); scriptDataEscapedEndTagOpen state
            | TextParser.Letter _ -> state.Cons('<'); state.Cons(); scriptDataDoubleEscapeStart state
            | _ -> state.Cons('<'); state.Cons(); scriptDataEscaped state
        and scriptDataDoubleEscapeStart state = 
            match state.Peek() with
            | TextParser.Whitespace _ | '/' | '>' when state.IsScriptTag -> state.Cons(); scriptDataDoubleEscaped state
            | TextParser.Letter _ -> state.Cons(); scriptDataDoubleEscapeStart state
            | _ -> state.Cons(); scriptDataEscaped state
        and scriptDataDoubleEscaped state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '-' -> state.Cons(); scriptDataDoubleEscapedDash state
            | '<' -> state.Cons(); scriptDataDoubleEscapedLessThanSign state
            | _ -> state.Cons(); scriptDataDoubleEscaped state
        and scriptDataDoubleEscapedDash state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '-' -> state.Cons(); scriptDataDoubleEscapedDashDash state
            | '<' -> state.Cons(); scriptDataDoubleEscapedLessThanSign state
            | _ -> state.Cons(); scriptDataDoubleEscaped state
        and scriptDataDoubleEscapedLessThanSign state =
            match state.Peek() with
            | '/' -> state.Cons(); scriptDataDoubleEscapeEnd state
            | _ -> state.Cons(); scriptDataDoubleEscaped state
        and scriptDataDoubleEscapeEnd state = 
            match state.Peek() with
            | TextParser.Whitespace _ | '/' | '>' when state.IsScriptTag -> state.Cons(); scriptDataDoubleEscaped state
            | TextParser.Letter _ -> state.Cons(); scriptDataDoubleEscapeEnd state
            | _ -> state.Cons(); scriptDataDoubleEscaped state
        and scriptDataDoubleEscapedDashDash state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '-' -> state.Cons(); scriptDataDoubleEscapedDashDash state
            | '<' -> state.Cons(); scriptDataDoubleEscapedLessThanSign state
            | '>' -> state.Cons(); script state
            | _ -> state.Cons(); scriptDataDoubleEscaped state
        and scriptDataEscapedEndTagOpen state = 
            match state.Peek() with
            | TextParser.Letter _ -> scriptDataEscapedEndTagName state
            | _ -> state.Cons([|'<';'/'|]); state.Cons(); scriptDataEscaped state
        and scriptDataEscapedEndTagName state =
            match state.Peek() with
            | TextParser.Whitespace _ when state.IsScriptTag -> state.Pop(); beforeAttributeName state
            | '/' when state.IsScriptTag -> state.Pop(); selfClosingStartTag state
            | '>' when state.IsScriptTag -> state.Pop(); state.EmitTag(true);
            | '>' -> 
                state.Cons([|'<'; '/'|]); 
                state.Cons(state.CurrentTagName()); 
                (!state.CurrentTag).Clear()
                script state
            | TextParser.Letter _ -> state.ConsTag(); scriptDataEscapedEndTagName state
            | _ -> state.Cons([|'<';'/'|]); state.Cons(); scriptDataEscaped state
        and scriptDataEscaped state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '-' -> state.Cons(); scriptDataEscapedDash state
            | '<' -> scriptDataEscapedLessThanSign state
            | _ -> state.Cons(); scriptDataEscaped state
        and scriptDataEscapedDash state = 
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | '-' -> state.Cons(); scriptDataEscapedDashDash state
            | '<' -> scriptDataEscapedLessThanSign state
            | _ -> state.Cons(); scriptDataEscaped state
        and scriptEndTagOpen state = 
            match state.Peek() with
            | TextParser.Letter _ -> scriptEndTagName state
            | _ -> state.Cons('<'); state.Cons('/'); script state
        and scriptEndTagName state =
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); beforeAttributeName state
            | '/' when state.IsScriptTag -> state.Pop(); selfClosingStartTag state
            | '>' when state.IsScriptTag -> state.Pop(); state.EmitTag(true);
            | '>' -> 
                state.Cons([|'<'; '/'|]); 
                state.Cons(state.CurrentTagName()); 
                (!state.CurrentTag).Clear()
                script state
            | TextParser.Letter _ -> state.ConsTag(); scriptEndTagName state
            | _ -> state.Cons('<'); state.Cons('/'); script state
        and charRef state = 
            match state.Peek() with
            | ';' -> state.Cons(); state.Emit()
            | '<' -> state.Emit()
            | _ -> state.Cons(); charRef state
        and tagOpen state =
            match state.Peek() with
            | '!' -> state.Pop(); markupDeclaration state
            | '/' -> state.Pop(); endTagOpen state
            | '?' -> state.Pop(); bogusComment state
            | TextParser.Letter _ -> state.ConsTag(); tagName false state
            | _ -> state.Cons('<'); data state
        and bogusComment state =
            let rec bogusComment' (state:HtmlState) = 
                let exitBogusComment state = 
                    state.InsertionMode := CommentMode
                    state.Emit()
                match state.Peek() with
                | '>' -> state.Cons(); exitBogusComment state 
                | TextParser.EndOfFile _ -> exitBogusComment state
                | _ -> state.Cons(); bogusComment' state
            bogusComment' state
        and markupDeclaration state =
            match state.Pop(2) with
            | [|'-';'-'|] -> comment state
            | current -> 
                match new String(Array.append current (state.Pop(5))) with
                | "DOCTYPE" -> docType state
                | "[CDATA[" -> state.Cons("<![CDATA[".ToCharArray()); cData state
                | _ -> bogusComment state
        and cData (state:HtmlState) = 
            if ((!state.Content).ToString().EndsWith("]]>"))
            then 
               state.InsertionMode := CDATAMode
               state.Emit()
            else 
               match state.Peek() with
               | ']' -> state.Cons();  cData state 
               | '>' -> state.Cons();  cData state
               | TextParser.EndOfFile _ -> 
                    state.InsertionMode := CDATAMode
                    state.Emit()
               | _ -> state.Cons(); cData state
        and docType state =
            match state.Peek() with
            | '>' -> 
                state.Pop(); 
                state.InsertionMode := DocTypeMode
                state.Emit()
            | _ -> state.Cons(); docType state
        and comment state = 
            match state.Peek() with
            | '-' -> state.Pop(); commentEndDash state;
            | TextParser.EndOfFile _ -> 
                state.InsertionMode := CommentMode 
                state.Emit();
            | _ -> state.Cons(); comment state
        and commentEndDash state = 
            match state.Peek() with
            | '-' -> state.Pop(); commentEndState state
            | TextParser.EndOfFile _ -> 
                state.InsertionMode := CommentMode 
                state.Emit();
            | _ -> 
                state.Cons(); comment state;
        and commentEndState state = 
            match state.Peek() with
            | '>' -> 
                state.Pop();
                state.InsertionMode := CommentMode 
                state.Emit();
            | TextParser.EndOfFile _ -> 
                state.InsertionMode := CommentMode 
                state.Emit();
            | _ -> state.Cons(); comment state 
        and tagName isEndTag state =
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); beforeAttributeName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> state.Pop(); state.EmitTag(isEndTag)
            | _ -> state.ConsTag(); tagName isEndTag state
        and selfClosingStartTag state =
            match state.Peek() with
            | '>' -> state.Pop(); state.EmitSelfClosingTag()
            | TextParser.EndOfFile _ -> data state
            | _ -> beforeAttributeName state
        and endTagOpen state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | TextParser.Letter _ -> state.ConsTag(); tagName true state
            | '>' -> state.Pop(); data state
            | _ -> comment state
        and beforeAttributeName state =
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); beforeAttributeName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> state.Pop(); state.EmitTag(false)
            | _ -> attributeName state
        and attributeName state =
            match state.Peek() with
            | '=' -> state.Pop(); beforeAttributeValue state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> state.Pop(); state.EmitTag(false)
            | TextParser.LetterDigit _ -> state.ConsAttrName(); attributeName state
            | TextParser.Whitespace _ -> afterAttributeName state
            | _ -> state.ConsAttrName(); attributeName state
        and afterAttributeName state =
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); afterAttributeName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> state.Pop(); state.EmitTag(false)
            | '=' -> state.Pop(); beforeAttributeValue state
            | _ -> state.NewAttribute(); attributeName state
        and beforeAttributeValue state =
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); beforeAttributeValue state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> state.Pop(); state.EmitTag(false)
            | '"' -> state.Pop(); attributeValueQuoted '"' state
            | '\'' -> state.Pop(); attributeValueQuoted '\'' state
            | _ -> state.ConsAttrValue(); attributeValueUnquoted state
        and attributeValueUnquoted state =
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); state.NewAttribute(); beforeAttributeName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> state.Pop(); state.EmitTag(false)
            | '&' -> 
                assert (state.ContentLength = 0)
                state.InsertionMode := InsertionMode.CharRefMode
                attributeValueCharRef ['/'; '>'] attributeValueUnquoted state
            | _ -> state.ConsAttrValue(); attributeValueUnquoted state
        and attributeValueQuoted quote state =
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state
            | c when c = quote -> state.Pop(); afterAttributeValueQuoted state
            | '&' -> 
                assert (state.ContentLength = 0)
                state.InsertionMode := InsertionMode.CharRefMode
                attributeValueCharRef [quote] (attributeValueQuoted quote) state
            | _ -> state.ConsAttrValue(); attributeValueQuoted quote state
        and attributeValueCharRef stop continuation (state:HtmlState) = 
            match state.Peek() with
            | ';' ->
                state.Cons()
                state.EmitToAttributeValue()
                continuation state
            | TextParser.EndOfFile _ ->
                state.EmitToAttributeValue()
                continuation state
            | c when List.exists ((=) c) stop ->
                state.EmitToAttributeValue()
                continuation state
            | _ ->
                state.Cons()
                attributeValueCharRef stop continuation state
        and afterAttributeValueQuoted state =
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); state.NewAttribute(); afterAttributeValueQuoted state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> state.Pop(); state.EmitTag(false)
            | _ -> attributeName state
        
        let next = ref (state.Reader.Peek())
        while !next <> -1 do
               data state
               next := state.Reader.Peek()
        
        !state.Tokens |> List.rev

    let private parse reader =
        let canNotHaveChildren (name:string) = 
            match name with
            | "area" | "base" | "br" | "col" | "embed"| "hr" | "img" | "input" | "keygen" | "link" | "menuitem" | "meta" | "param" 
            | "source" | "track" | "wbr" -> true
            | _ -> false
        let rec parse' docType elements expectedTagEnd parentTagName (tokens:HtmlToken list) =
            match tokens with
            | DocType dt :: rest -> parse' (dt.Trim()) elements expectedTagEnd parentTagName rest
            | Tag(_, "br", []) :: rest ->
                let t = HtmlText Environment.NewLine
                parse' docType (t :: elements) expectedTagEnd parentTagName rest
            | Tag(true, name, attributes) :: rest ->
               let e = HtmlElement(name, attributes, [])
               parse' docType (e :: elements) expectedTagEnd parentTagName rest
            | Tag(false, name, attributes) :: rest when canNotHaveChildren name ->
               let e = HtmlElement(name, attributes, [])
               parse' docType (e :: elements) expectedTagEnd parentTagName rest
            | Tag(_, name, attributes) :: rest ->
                let dt, tokens, content = parse' docType [] name expectedTagEnd rest
                let e = HtmlElement(name, attributes, content)
                parse' dt (e :: elements) expectedTagEnd parentTagName tokens
            | TagEnd name :: _ when name <> expectedTagEnd && name = parentTagName ->
                // insert missing closing tag
                parse' docType elements expectedTagEnd parentTagName (TagEnd expectedTagEnd :: tokens)
            | TagEnd name :: rest when name <> expectedTagEnd && (name <> (new String(expectedTagEnd.ToCharArray() |> Array.rev))) -> 
                // ignore this token if not the expected end tag (or it's reverse, eg: <li></il>)
                parse' docType elements expectedTagEnd parentTagName rest
            | TagEnd _ :: rest -> 
                docType, rest, List.rev elements
            | Text cont :: rest ->
                if cont = "" then
                    // ignore this token
                    parse' docType elements expectedTagEnd parentTagName rest
                else
                    let t = HtmlText cont
                    parse' docType (t :: elements) expectedTagEnd parentTagName rest
            | Comment cont :: rest -> 
                let c = HtmlComment cont
                parse' docType (c :: elements) expectedTagEnd parentTagName rest
            | CData cont :: rest -> 
                let c = HtmlCData cont
                parse' docType (c :: elements) expectedTagEnd parentTagName rest
            | EOF :: _ -> docType, [], List.rev elements
            | [] -> docType, [], List.rev elements
        let tokens = tokenise reader 
        let docType, _, elements = tokens |> parse' "" [] "" ""
        if List.isEmpty elements then
            failwith "Invalid HTML" 
        docType, elements

    /// All attribute names and tag names will be normalized to lowercase
    /// All html entities will be replaced by the corresponding characters
    /// All the consecutive whitespace (except for `&nbsp;`) will be collapsed to a single space
    /// All br tags will be replaced by newlines
    let parseDocument reader = 
        HtmlDocument(parse reader)

    /// All attribute names and tag names will be normalized to lowercase
    /// All html entities will be replaced by the corresponding characters
    /// All the consecutive whitespace (except for `&nbsp;`) will be collapsed to a single space
    /// All br tags will be replaced by newlines
    let parseFragment reader = 
        parse reader |> snd

// --------------------------------------------------------------------------------------

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

type HtmlNode with

    /// Parses the specified HTML string to a list of HTML nodes
    static member Parse(text) = 
        use reader = new StringReader(text)
        HtmlParser.parseFragment reader

    /// Parses the specified HTML string to a list of HTML nodes
    static member ParseRooted(rootName, text) = 
        use reader = new StringReader(text)
        HtmlElement(rootName, [], HtmlParser.parseFragment reader)