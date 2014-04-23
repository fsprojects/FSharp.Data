#nowarn "10001"
namespace FSharp.Data

open System
open System.Collections.Generic
open System.IO
open System.Text
open FSharp.Data
open FSharp.Data.Runtime

// --------------------------------------------------------------------------------------

type HtmlAttribute = 
    | HtmlAttribute of name:string * value:string    

[<CustomEquality; NoComparison>]
type HtmlNode =
    | HtmlElement of parent:HtmlNode option ref * name:string * attributes:HtmlAttribute list * elements:HtmlNode list
    | HtmlText of parent:HtmlNode option ref * content:string
    | HtmlComment of parent:HtmlNode option ref * content:string
    override x.Equals(y:obj) = 
        match y with
        | :? HtmlNode as y ->
            match x, y with
            | HtmlElement(_, name', attributes', content'), HtmlElement(_, name, attributes, content) -> name' = name && attributes = attributes' && content = content'
            | HtmlText(_, content'), HtmlText(_, content) -> content = content'
            | HtmlComment(_, content'), HtmlComment(_, content) -> content = content'
            | _, _ -> false 
        | _ -> false
    override x.GetHashCode() = 
        match x with
        | HtmlElement(_, name, attrs, content) -> 397 ^^^ hash(name) ^^^ hash(attrs) ^^^ hash(content)
        | HtmlText(_, content) -> 397 ^^^ hash(127) ^^^ hash(content)
        | HtmlComment(_, comment) -> 397 ^^^ hash(143) ^^^ hash(comment)
    override x.ToString() =
        let rec serialize (sb:StringBuilder) indentation html =
            let append (str:string) = sb.Append str |> ignore
            let newLine plus =
                sb.AppendLine() |> ignore
                System.String(' ', indentation + plus) |> append
            match html with
            | HtmlElement(_, name, attributes, elements) ->
                append "<"
                append name
                for HtmlAttribute(name, value) in attributes do
                    append " "
                    append name
                    append "=\""
                    append value
                    append "\""
                if elements.IsEmpty
                then append " />"; newLine 0
                else
                    append ">"
                    newLine 2
                    for element in elements do
                        serialize sb (indentation + 2) element
                    newLine 0
                    append "</"
                    append name
                    append ">"
            | HtmlText(_, str) -> append str
            | HtmlComment(_, str) -> 
                    append "<!-- "
                    append str
                    append " -->"
        
        let sb = StringBuilder()
        serialize sb 0 x
        sb.ToString()

type HtmlDocument = 
    | HtmlDocument of docType:string * elements:HtmlNode list
    override x.ToString() =
        match x with
        | HtmlDocument(docType, elements) ->
            (if String.IsNullOrEmpty docType then "" else "<!" + docType + ">\n")
            +
            (elements |> List.map (fun x -> x.ToString()) |> String.concat "\n")

// --------------------------------------------------------------------------------------

module private TextParser = 

    let toPattern f c = if f c then Some c else None

    let (|Parse|_|) func value = func value

    let (|NullChar|_|) (c : char) =
        if (c |> int) = 0 then Some c else None

    let (|EndOfFile|_|) (c : char) =
        let value = c |> int
        if (value = -1 || value = 65535) then Some c else None

    let (|UpperAtoZ|_|) = toPattern Char.IsUpper

    let (|LowerAtoZ|_|) = toPattern Char.IsLower

    let (|Number|_|) = toPattern Char.IsNumber

    let (|Symbol|_|) = toPattern Char.IsPunctuation

    let (|Whitespace|_|) = toPattern Char.IsWhiteSpace

    let (|LetterDigit|_|) = function
        | LowerAtoZ c -> Some c
        | Number c -> Some c
        | UpperAtoZ c -> Some (Char.ToLower c)
        | _ -> None

    let (|Letter|_|) = function
        | LowerAtoZ c -> Some c
        | UpperAtoZ c -> Some (Char.ToLower c)
        | _ -> None

    let (|LetterDigitSymbol|_|) = function
        | LowerAtoZ c -> Some c
        | Number c -> Some c
        | UpperAtoZ c -> Some (Char.ToLower c)
        | Symbol c -> Some c
        | _ -> None

// --------------------------------------------------------------------------------------

module internal HtmlParser =

    type HtmlToken =
        | DocType of string
        | Tag of isSelfClosing:bool * name:string * attrs:HtmlAttribute list
        | TagEnd of string
        | Text of string
        | Comment of string
        | EOF
        override x.ToString() =
            match x with
            | DocType dt -> sprintf "doctype %s" dt
            | Tag(selfClose,name,_) -> sprintf "tag %b %s" selfClose name
            | TagEnd name -> sprintf "tagEnd %s" name
            | Text _ -> "text"
            | Comment _ -> "comment"
            | EOF -> "eof"
        member x.IsEndTag name =
            match x with
            | TagEnd(endName) when name = endName -> true
            | _ -> false

    type TextReader with
       
        static member NullChar = Convert.ToChar(0x0)
        member x.PeekChar() = x.Peek() |> char
        member x.ReadChar() = x.Read() |> char
        member x.ReadNChar(n) = 
            let buffer = Array.zeroCreate n
            x.ReadBlock(buffer, 0, n) |> ignore
            String(buffer)
    
    type CharList = 
        { Contents : char list ref }
        static member Empty = { Contents = ref [] }
        override x.ToString() = String(!x.Contents |> List.rev |> Seq.toArray)
        member x.Cons(c) = x.Contents := c :: !x.Contents
        member x.Length = x.Contents.Value.Length
        member x.Clear() = x.Contents := []

    type InsertionMode = 
        | DefaultMode
        | ScriptMode
        | CharRefMode
        | CommentMode
        | DocTypeMode
        override x.ToString() =
            match x with
            | DefaultMode -> "default"
            | ScriptMode -> "script"
            | CharRefMode -> "charref"
            | CommentMode -> "comment"
            | DocTypeMode -> "doctype"
    
    type HtmlState = 
        { Attributes : (CharList * CharList) list ref
          CurrentTag : CharList ref
          Content : CharList ref
          InsertionMode : InsertionMode ref
          Reader : TextReader }
        static member Create (reader:TextReader) = 
            { Attributes = ref []
              CurrentTag = ref CharList.Empty
              Content = ref CharList.Empty
              InsertionMode = ref DefaultMode
              Reader = reader }

        member x.Pop() = x.Reader.Read() |> ignore
        member x.Peek() = x.Reader.PeekChar()
        member x.Pop(count) = 
            [|0..(count-1)|] |> Array.map (fun _ -> x.Reader.ReadChar())
            
        member x.GetContent() = (!x.Content).ToString() 
    
        member x.NewAttribute() = x.Attributes := (CharList.Empty, CharList.Empty) :: (!x.Attributes)
    
        member x.ConsAttrName() =
            match !x.Attributes with
            | [] -> x.NewAttribute(); x.ConsAttrName()
            | (h,_) :: _ -> h.Cons(x.Reader.ReadChar())
    
        member x.CurrentTagName() = 
            match (!(!x.CurrentTag).Contents) with
            | [] -> String.Empty
            | h :: _ -> h.ToString()
    
        member x.CurrentAttrName() = 
            match !x.Attributes with
            | [] -> String.Empty
            | (h,_) :: _ -> h.ToString() 
    
        member x.ConsAttrValue() =
            match !x.Attributes with
            | [] -> x.NewAttribute(); x.ConsAttrValue()
            | (_,h) :: _ -> h.Cons(x.Reader.ReadChar())
    
        member x.GetAttributes() = 
            !x.Attributes 
            |> List.choose (fun (key, value) -> 
                if key.Length > 0
                then Some <| HtmlAttribute(key.ToString(), value.ToString())
                else None)
            |> List.rev
    
        member x.EmitSelfClosingTag() = 
            let name = (!x.CurrentTag).ToString()
            let result = Tag(true, name, x.GetAttributes()) 
            x.CurrentTag := CharList.Empty
            x.InsertionMode := DefaultMode
            x.Attributes := []
            result 

        member x.IsScriptTag 
            with get() = 
               match x.CurrentTagName() with
               | "script" -> true
               | _ -> false

        member x.EmitTag(isEnd) =
            let name = (!x.CurrentTag).ToString().Trim().ToLower()
            let result = 
                if isEnd
                then TagEnd(name)
                else Tag(false, name, x.GetAttributes()) 

            x.CurrentTag := CharList.Empty
            x.InsertionMode :=
                if x.IsScriptTag
                then ScriptMode
                else DefaultMode

            x.Attributes := []
            result
    
        member x.Emit() =
            let substituteCharRef ref =
                match HtmlCharRefs.substitute ref with
                | Some(r) -> r
                | None -> ref
            let result = 
                let content = (!x.Content).ToString()
                match !x.InsertionMode with
                | DefaultMode | ScriptMode -> Text content
                | CharRefMode -> Text (substituteCharRef (content.Trim()))
                | CommentMode -> Comment content
                | DocTypeMode -> DocType content
            x.Content := CharList.Empty
            x.InsertionMode := DefaultMode
            result
    
        member x.Cons() = (!x.Content).Cons(x.Reader.ReadChar())
        member x.Cons(char) = (!x.Content).Cons(char)
        member x.Cons(char) = Array.iter ((!x.Content).Cons) char
        member x.ConsTag() = 
            match x.Reader.ReadChar() with
            | TextParser.Whitespace _ -> ()
            | a -> (!x.CurrentTag).Cons(Char.ToLower a)
        member x.ClearContent() = 
            (!x.Content).Clear()

    // Tokenises a stream into a sequence of HTML tokens. 
    let private tokenise reader =
        let state = HtmlState.Create reader
        let rec data (state:HtmlState) =
            match state.Peek() with
            | '<' -> 
                if state.GetContent().Length > 0
                then state.Emit();
                else state.Pop(); tagOpen state
            | TextParser.EndOfFile _ -> EOF
            | '&' ->
                if state.GetContent().Length > 0
                then state.Emit();
                else
                    state.InsertionMode := CharRefMode
                    charRef state
            | TextParser.Whitespace _ when state.GetContent().Length = 0 -> state.Pop(); data state
            | _ ->
                match !state.InsertionMode with
                | DefaultMode -> state.Cons(); data state
                | ScriptMode -> script state;
                | CharRefMode -> charRef state
                | DocTypeMode -> docType state
                | CommentMode -> comment state
        and script state = 
            match state.Peek() with
            | '<' -> state.Pop(); scriptLessThanSign state
            | TextParser.EndOfFile _ -> data state
            | _ -> state.Cons(); script state
        and scriptLessThanSign state =
            match state.Peek() with
            | '/' -> state.Pop(); scriptEndTagOpen state
            | '!' -> state.Pop(); scriptDataEscapeStart state
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
            | '-' -> state.Cons(); scriptDataEscapedDashDash state
            | '<' -> state.Pop(); scriptDataEscapedLessThanSign state
            | '>' -> state.Cons(); script state
            | TextParser.EndOfFile _ -> data state
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
            | '-' -> state.Cons(); scriptDataDoubleEscapedDash state
            | '<' -> state.Cons(); scriptDataDoubleEscapedLessThanSign state
            | TextParser.EndOfFile _ -> data state
            | _ -> state.Cons(); scriptDataDoubleEscaped state
        and scriptDataDoubleEscapedDash state = 
            match state.Peek() with
            | '-' -> state.Cons(); scriptDataDoubleEscapedDashDash state
            | '<' -> state.Cons(); scriptDataDoubleEscapedLessThanSign state
            | TextParser.EndOfFile _ -> data state
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
            | '-' -> state.Cons(); scriptDataDoubleEscapedDashDash state
            | '<' -> state.Cons(); scriptDataDoubleEscapedLessThanSign state
            | '>' -> state.Cons(); script state
            | TextParser.EndOfFile _ -> data state
            | _ -> state.Cons(); scriptDataDoubleEscaped state
        and scriptDataEscapedEndTagOpen state = 
            match state.Peek() with
            | TextParser.Letter _ -> state.ConsTag(); scriptDataEscapedEndTagName state
            | _ -> state.Cons([|'<';'/'|]); state.Cons(); scriptDataEscaped state
        and scriptDataEscapedEndTagName state =
            match state.Peek() with
            | TextParser.Whitespace _ when state.IsScriptTag -> state.Pop(); beforeAttributeName state
            | '/' when state.IsScriptTag -> state.Pop(); selfClosingStartTag state
            | '>' when state.IsScriptTag -> state.EmitTag(true)
            | TextParser.Letter _ -> state.ConsTag(); scriptDataEscapedEndTagName state
            | _ -> state.Cons([|'<';'/'|]); state.Cons(); scriptDataEscaped state
        and scriptDataEscaped state =
            match state.Peek() with
            | '-' -> state.Cons(); scriptDataEscapedDash state
            | '<' -> scriptDataEscapedLessThanSign state
            | TextParser.EndOfFile _ -> data state
            | _ -> state.Cons(); scriptDataEscaped state
        and scriptDataEscapedDash state = 
            match state.Peek() with
            | '-' -> state.Cons(); scriptDataEscapedDashDash state
            | '<' -> scriptDataEscapedLessThanSign state
            | TextParser.EndOfFile _ -> data state
            | _ -> state.Cons(); scriptDataEscaped state
        and scriptEndTagOpen state = 
            match state.Peek() with
            | TextParser.Letter _ -> state.ConsTag(); scriptEndTagName state
            | _ -> script state
        and scriptEndTagName state = 
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); scriptEndTagName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> state.Pop();  state.EmitTag(true); 
            | TextParser.EndOfFile _ -> data state;
            | _ -> state.ConsTag(); scriptEndTagName state
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
                | "[CDATA[" -> cData [||] state
                | _ -> bogusComment state
        and cData prev state = 
            if (string prev) = "]]>"
            then 
               state.InsertionMode := CommentMode
               state.Emit()
            else 
               match prev, state.Peek() with
               | [||], ']' -> state.Pop();  cData [|']'|] state 
               | [|']'|], ']' -> state.Pop(); cData [|']'|] state
               | [|']';']'|], '>' -> state.Pop();  cData [|']';']';'>'|] state
               | _, TextParser.EndOfFile _ -> 
                    state.InsertionMode := CommentMode
                    state.Emit()
               | _, _ -> state.Cons(); cData [||] state
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
            | TextParser.EndOfFile _ -> data state
            | _ -> state.ConsTag(); tagName isEndTag state
        and selfClosingStartTag state = 
            match state.Peek() with
            | '>' -> state.Pop(); state.EmitSelfClosingTag()
            | TextParser.EndOfFile _ -> data state
            | _ -> beforeAttributeName state
        and endTagOpen state = 
            match state.Peek() with
            | TextParser.Letter _ -> state.ConsTag(); tagName true state
            | TextParser.EndOfFile _ -> data state
            | '>' -> state.Pop(); data state
            | _ -> comment state
        and beforeAttributeName state = 
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); beforeAttributeName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | TextParser.EndOfFile _ -> data state
            | '>' -> state.EmitTag(false)
            | _ -> attributeName state
        and attributeName state =
            match state.Peek() with
            | '/' -> state.Pop(); selfClosingStartTag state
            | '=' -> state.Pop(); beforeAttributeValue state
            | '>' -> state.Pop(); state.EmitTag(false)
            | TextParser.LetterDigit _ -> state.ConsAttrName(); attributeName state
            | TextParser.Whitespace _ -> state.ConsAttrName(); afterAttributeName state
            | TextParser.EndOfFile _ -> data state;
            | _ -> state.ConsAttrName(); attributeName state
        and afterAttributeName state = 
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); afterAttributeName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '=' -> state.Pop(); beforeAttributeValue state
            | '>' -> state.EmitTag(false)
            | TextParser.EndOfFile _ -> data state;
            | _ -> attributeName state
        and beforeAttributeValue state = 
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); beforeAttributeValue state
            | TextParser.EndOfFile _ -> data state;
            | '"' -> state.Pop(); attributeValueQuoted state
            | '&' -> state.Pop(); attributeValueUnquoted state
            | '\'' -> state.Pop(); attributeValueSingleQuoted state
            | '>' -> state.Pop(); state.EmitTag(false);
            | _ -> state.ConsAttrValue(); attributeValueUnquoted state
        and attributeValueUnquoted state = 
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); state.NewAttribute(); attributeName state
            | TextParser.EndOfFile _ -> data state;
            | '>' -> state.Pop(); state.EmitTag(false)
            | _ -> state.ConsAttrValue(); attributeValueUnquoted state
        and attributeValueSingleQuoted state = 
            match state.Peek() with
            | TextParser.EndOfFile _ -> data state;
            | '\'' -> state.Pop(); afterAttributeValueQuoted state
            | '/' -> state.EmitSelfClosingTag()
            | _ -> state.ConsAttrValue(); attributeValueSingleQuoted state
        and attributeValueQuoted state = 
            match state.Peek() with
            | '"' -> state.Pop(); afterAttributeValueQuoted state
            | TextParser.EndOfFile _ -> data state;
            | _ -> state.ConsAttrValue(); attributeValueQuoted state
        and afterAttributeValueQuoted state = 
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); state.NewAttribute(); attributeName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> state.Pop(); state.EmitTag(false)
            | TextParser.EndOfFile _ -> data state
            | _ -> attributeName state
        [
           while state.Reader.Peek() <> -1 do
               yield data state
        ]
    
    let private parse parent reader =
        let parentStack = new Stack<HtmlNode option ref>([parent])
        let isVoid (name:string) = 
            match name with
            | "area" | "base" | "br" | "col" | "embed"| "hr" | "img" | "input" | "keygen" | "link" | "menuitem" | "meta" | "param" 
            | "source" | "track" | "wbr" -> true
            | _ -> false
        let rec parse' docType elements (tokens:HtmlToken list) =
            match tokens with
            | DocType dt :: rest -> parse' (dt.Trim()) elements rest
            | Tag(true, name, attributes) :: TagEnd(endName) :: rest when name = endName ->
               let e = HtmlElement(parentStack.Peek(), name.ToLower(), attributes, [])
               parse' docType (e :: elements) rest
            | Tag(true, name, attributes) :: rest ->
               let e = HtmlElement(parentStack.Peek(), name.ToLower(), attributes, [])
               parse' docType (e :: elements) rest
            | Tag(false, name, attributes) :: (Tag(_, nextName, _) as next) :: rest when (name <> nextName) && (isVoid name) ->
               let e = HtmlElement(parentStack.Peek(), name.ToLower(), attributes, [])
               parse' docType (e :: elements) (next :: rest)  
            | Tag(false, name, attributes) :: (TagEnd(nextName) as next) :: rest when (name <> nextName) && (isVoid name) ->
               let e = HtmlElement(parentStack.Peek(), name.ToLower(), attributes, [])
               parse' docType (e :: elements) (next :: rest)  
            | Tag(_, name, attributes) :: rest ->
                let refCell = ref None
                let currentParent = parentStack.Peek()
                parentStack.Push(refCell);
                let dt, tokens, content = parse' docType [] rest
                let e = HtmlElement(currentParent, name.ToLower(), attributes, content)
                refCell := Some e
                parse' dt (e :: elements) tokens
            | TagEnd _ :: rest -> 
                parentStack.Pop() |> ignore;
                docType, rest, (elements |> List.rev)
            | Text cont :: rest ->
                if cont <> " " && (String.IsNullOrWhiteSpace cont)
                then parse' docType elements rest
                else parse' docType (HtmlText(parentStack.Peek(), cont.Trim()) :: elements) rest
            | Comment(cont) :: rest -> parse' docType (HtmlComment(parentStack.Peek(), cont.Trim()) :: elements) rest
            | EOF :: _ -> docType, [], (elements |> List.rev)
            | [] -> docType, [], (elements |> List.rev)
        let docType, _, elements = tokenise reader |> parse' "" []
        docType, elements

    let parseDocument reader = 
        HtmlDocument(parse (ref None) reader)

    let parseFragment reader parent = 
        parse parent reader |> snd
