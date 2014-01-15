namespace FSharp.Data.Runtime

open System
open System.IO
open System.Xml
open System.Reflection
open System.Text
open FSharp.Data.Runtime.StructuralTypes
#if INTERACTIVE 
open FSharp.Data.Runtime
#endif

type HtmlAttribute = | HtmlAttribute of string * string
    with
        member x.Name = 
            match x with
            | HtmlAttribute(name,_) -> name
        member x.Value = 
            match x with
            | HtmlAttribute(_,value) -> value

type HtmlToken =
    | Tag of bool * string * HtmlAttribute list
    | TagEnd of string
    | Text of string

type HtmlElement =
    | HtmlDocument of HtmlElement list
    | HtmlElement of string * HtmlAttribute list * HtmlElement list
    | HtmlContent of string
    with
        member x.Name 
            with get() =
                match x with
                | HtmlElement(name, _, _) -> name.ToLower()
                | _ -> String.Empty
        member x.Value 
            with get() =
               let rec getValues = function
                   | HtmlDocument(content)
                   | HtmlElement(_, _, content) -> List.collect getValues content
                   | HtmlContent c -> [c.Trim()]
               getValues x |> List.filter (fun s -> String.IsNullOrEmpty(s) |> not) |> Seq.ofList
        member x.Children
            with get() =
                    match x with
                    | HtmlElement(_, _, children) -> children
                    | _ -> []
        member x.TryGetAttribute(name : string) =
            match x with
            | HtmlDocument(_) -> None
            | HtmlElement(_,attr,_) ->
                attr |> List.tryFind (fun a -> a.Name.ToLowerInvariant() = (name.ToLowerInvariant()))
            | HtmlContent _ -> None
        member x.HasAttribute(name, value : string) =
            x.TryGetAttribute(name)
            |> function 
               | Some(attr) ->  attr.Value.ToLowerInvariant() = (value.ToLowerInvariant())
               | None -> false

module internal HtmlHelpers = 
    
    type System.IO.StreamReader with
   
        static member NullChar = Convert.ToChar(0x0)
        member x.PeekChar() = x.Peek() |> char
        member x.ReadChar() = x.Read() |> char
        member x.ReadNChar(n) = 
            let buffer = Array.zeroCreate n
            x.ReadBlock(buffer, 0, n) |> ignore
            String(buffer)


    type CharList = {
        Contents : char list ref
    }
    with
        static member Empty = { Contents = ref [] }
        override x.ToString() = String(!x.Contents |> List.rev |> Seq.toArray)
        member x.Cons(c) = 
            x.Contents := c :: !x.Contents
        member x.Length = x.Contents.Value.Length

    type HtmlState = {
        Attributes : (CharList * CharList) list ref
        CurrentTag : CharList ref
        Content : CharList ref
        Reader : StreamReader
    }
    with
        static member Create(reader:StreamReader) = {
            Attributes = ref []
            CurrentTag = ref CharList.Empty
            Content = ref CharList.Empty
            Reader = reader
        }
        member x.Pop() = x.Reader.Read() |> ignore
        member x.Peek() = x.Reader.PeekChar()

        member x.NewAttribute() = x.Attributes := (CharList.Empty, CharList.Empty) :: (!x.Attributes)

        member x.EmitCurrentAttrName() =
            match !x.Attributes with
            | [] -> x.NewAttribute(); x.EmitCurrentAttrName()
            | (h,_) :: _ -> h.Cons(x.Reader.ReadChar())

        member x.CurrentTagName() = 
            match (!(!x.CurrentTag).Contents) with
            | [] ->  String.Empty
            | h :: _ -> h.ToString()

        member x.CurrentAttrName() = 
            match !x.Attributes with
            | [] ->  String.Empty
            | (h,_) :: _ -> h.ToString() 

        member x.EmitCurrentAttrValue() =
            match !x.Attributes with
            | [] -> x.NewAttribute(); x.EmitCurrentAttrValue()
            | (_,h) :: _ -> h.Cons(x.Reader.ReadChar())

        member x.GetAttributes() = 
            !x.Attributes |> List.map (fun (key,value) -> HtmlAttribute(key.ToString(), value.ToString()))

        member x.EmitSelfClosingTag() = 
            let result = Tag(true, (!x.CurrentTag).ToString(), x.GetAttributes()) 
            x.CurrentTag := CharList.Empty
            x.Attributes := []
            result 

        member x.EmitTag() = 
            let result = Tag(false, (!x.CurrentTag).ToString(), x.GetAttributes()) 
            x.CurrentTag := CharList.Empty
            x.Attributes := []
            result

        member x.EmitEndTag() = 
            let result = TagEnd((!x.CurrentTag).ToString()) 
            x.CurrentTag := CharList.Empty
            result

        member x.EmitContent() = 
            let result = Text((!x.Content).ToString())
            x.Content := CharList.Empty
            result

        member x.ConsContent() = (!x.Content).Cons(x.Reader.ReadChar())
        member x.ConsTag() = (!x.CurrentTag).Cons(x.Reader.ReadChar())

module Html = 
    
    open HtmlHelpers

    //Tokenises a stream into a sequence of HTML tokens. 
    let private tokenise (sr : #StreamReader) =
            let state = HtmlState.Create(sr)
            let rec data (state:HtmlState) =
                    match state.Reader.PeekChar() with
                    | '<' when (!state.Content).Length > 0 -> state.EmitContent()// emitContent content
                    | '<' -> 
                        state.Pop(); 
                        match state.Peek() with
                        | '/' -> state.Pop(); endTag state
                        | '!' -> docType state
                        | _ -> openTag state
                    | '>' -> state.Pop(); state.EmitTag()
                    | TextParser.EndOfFile _ -> state.EmitTag()
                    | other -> 
                        state.ConsContent()
                        data state
            and docType state =
                match state.Peek() with
                | '>' -> state.Pop(); state.EmitTag()
                | other -> state.ConsTag(); docType state
            and openTag state =
                match state.Peek() with
                | TextParser.LetterDigit c -> state.ConsTag(); openTag state
                | '!' ->  state.Pop(); openTag state
                | '/'  -> state.Pop(); state.Pop(); state.EmitSelfClosingTag()
                | TextParser.Whitespace _ -> state.Pop(); attributeName state
                | '>' -> 
                    state.Pop();
                    state.EmitTag()
                | TextParser.EndOfFile _ -> data state
                | other -> state.Pop(); data state
            and endTag state =
                match state.Peek() with
                | TextParser.LetterDigit c -> state.ConsTag(); endTag state
                | '>' -> state.Pop(); state.EmitEndTag()
                | other -> state.Pop(); state.EmitEndTag()
            and attributeName state =
                match state.Peek() with
                | '\'' | '"'  -> state.Pop(); attributeName state
                | TextParser.LetterDigit c -> state.EmitCurrentAttrName(); attributeName state;
                | '-' | '/' | ':' -> state.EmitCurrentAttrName(); attributeName state;
                | '=' -> state.Pop(); attributeValue state;
                | other -> openTag state
            and attributeValue state =
                match state.Peek() with
                | '\'' | '"'  -> state.Pop(); attributeValue state
                | '>' -> openTag state
                | TextParser.Whitespace _ -> 
                    if state.CurrentAttrName() = "style"
                    then state.EmitCurrentAttrValue(); attributeValue state;
                    else state.NewAttribute(); openTag state
                | TextParser.LetterDigitSymbol c -> state.EmitCurrentAttrValue(); attributeValue state
                | other -> state.EmitCurrentAttrValue(); attributeValue state;
            
            seq {
               while state.Reader.EndOfStream |> not do
                   yield data state
            }
    
    let parse sr = 
        let rec parse' elements tokens =
            match tokens with
            | Tag(true, name, attributes) :: rest ->
               let e = HtmlElement(name, attributes, [])
               parse' (e :: elements) rest
            | Tag(false, name, attributes) :: rest ->
                let tokens, content = parse' [] rest
                let e = HtmlElement(name, attributes, content)
                parse' (e :: elements) tokens
            | TagEnd(name) :: rest -> rest, (elements |> List.rev)
            | Text(cont) :: rest -> parse' (HtmlContent(cont.Trim()) :: elements) rest
            | [] -> [], (elements |> List.rev)
        HtmlDocument(
                 tokenise sr
                 |> (Seq.toList >> parse' []) 
                 |> snd)

    let write (writer:TextWriter) (element:HtmlElement) =
        let createXmlWriter(baseWriter:TextWriter) =
            let s = new System.Xml.XmlWriterSettings(Indent = false,
                                                     OmitXmlDeclaration = true, 
                                                     ConformanceLevel = System.Xml.ConformanceLevel.Auto)
            XmlWriter.Create(baseWriter, s)
        
        let rec writeElement (writer:XmlWriter) = function
            | HtmlDocument(elems) ->
                for elem in elems do 
                    writeElement writer elem
            | HtmlContent(c) -> writer.WriteValue(c)
            | HtmlElement(name, attrs, elems) ->
                writer.WriteStartElement(name)
                for attr in attrs do
                    match attr with
                    | HtmlAttribute(key,value) -> 
                        if String.IsNullOrEmpty(value)
                        then writer.WriteStartAttribute(key); writer.WriteEndAttribute()
                        else writer.WriteAttributeString(key, value)
                for elem in elems do 
                    writeElement writer elem
                writer.WriteEndElement()
    
        use writer = createXmlWriter(writer)
        writeElement writer element
         
    
    let rec descendantsBy f = function
        | HtmlDocument(elements)
        | HtmlElement(_, _, elements) ->
            seq {
                for element in elements do
                    if f element then yield element
                    yield! descendantsBy f element
            }
        | HtmlContent _ -> Seq.empty
    
    let descendantsByName (name : string) =
        descendantsBy (fun e -> 
            e.Name.ToLowerInvariant() = (name.ToLowerInvariant())
            )
    
    let descendants = descendantsBy (fun _ -> true)
    
    let first f = descendants >> Seq.find f
    
    let values elems = Seq.map (fun (e:HtmlElement) -> e.Value) elems

    let inferRowType (headers:string[]) values = 
        let culture = System.Globalization.CultureInfo.CurrentCulture
        let inferProperty index value =
            {
                Name = (headers.[index])
                Optional = false
                Type = (StructuralInference.inferPrimitiveType culture value None)
            }
        StructuralTypes.InferedType.Record(None, values |> List.mapi inferProperty) 

    let getTables (element:HtmlElement) = 
        descendantsBy (fun e -> e.Name.ToLower() = "table") element

    let tryGetName (element:HtmlElement) = 
        let tryGetName' choices =
            choices
            |> List.tryPick (fun (attrName) -> 
                match element.TryGetAttribute(attrName) with
                | Some(HtmlAttribute(_,value)) -> Some <| value
                | None -> None
            )
        tryGetName' [ "id"; "name"; "title"]

type HtmlElement with            
//Parses a stream into a tree of HTML Elements that represents the document
    static member Parse (sr : #StreamReader) = Html.parse sr
    static member Parse (sr:string, ?enc:Encoding) = 
        use ms = new MemoryStream((defaultArg enc Encoding.UTF8).GetBytes(sr))
        use sr = new StreamReader(ms)
        HtmlElement.Parse(sr)

type HtmlTableRow private(data:seq<string>, headers:seq<string>) = 
    let culture = Globalization.CultureInfo.CurrentCulture
    member x.Data with get() = data |> Array.ofSeq

    static member Create(data, headers) = HtmlTableRow(data, headers)

    member x.GetColumn(name) = 
        let index = Seq.findIndex ((=) name) headers
        let data = x.Data.[index]
        data

    static member GetColumn(row:HtmlTableRow, name) = 
        row.GetColumn(name)

    member x.GetInferedRowType() = 
        Html.inferRowType (headers|>Seq.toArray) (data |> Seq.toList)

type HtmlTable<'rowType> internal(id:string, headers:seq<string>, data:seq<'rowType>) =
    let mutable rows = data
    
    member x.Id with get() = id
    member x.Headers with get() = headers
    member x.Rows with get() = rows and internal set(v) = rows <- v

    static member Create(rowConverter:Func<string[],'rowType>, id, headers, src:string) =
       let element = HtmlElement.Parse(src)
       let data =  
            Html.descendantsByName "tr" element 
            |> Seq.skip 1 
            |> Html.values 
            |> Seq.toArray 
            |> Array.map (fun r -> rowConverter.Invoke(r |> Seq.toArray))
       new HtmlTable<'rowType>(id, headers, data) 


type HtmlTable private(id, headers:seq<string>, data) = 
    inherit HtmlTable<HtmlTableRow>(id, headers, data |> Seq.map (fun r -> HtmlTableRow.Create(r, headers)))   

    static let getRows element = Html.descendantsByName "tr" element
    static let getHeaders (rows:seq<HtmlElement>) = (rows |> Seq.head).Value
    static let getData rows = rows |> Seq.skip 1 |> Html.values 

    static member Create(defaultName, element) =
        let rows = getRows element
        let headers = getHeaders rows
        let data = getData rows
        let name = Html.tryGetName element
        HtmlTable(defaultArg name defaultName, headers, data)

    member x.Load(src:string, ?enc:Encoding) = 
        let element = HtmlElement.Parse(src, ?enc = enc)
        x.Rows <- getData (getRows element) |> Seq.map (fun r -> HtmlTableRow.Create(r, headers))

    member x.GetInferedRowType() = 
        let inferedType =
            x.Rows 
            |> Seq.map (fun r -> r.GetInferedRowType())
            |> Seq.reduce (StructuralInference.subtypeInfered true) 
        match inferedType with
        | StructuralTypes.InferedType.Record(_, props) -> 
            inferedType, props |> List.map (fun p -> 
                                  match p.Type with
                                  | InferedType.Primitive(typ, unitType) -> PrimitiveInferedProperty.Create(p.Name, typ, p.Optional)
                                  | _ -> failwithf "expected primitive")
        | _ -> failwith "expected record" 

type HtmlElement with
    member x.Write(textWriter) = Html.write textWriter x
    member x.Tables() = 
        Html.getTables x
        |> Seq.mapi (fun i t -> HtmlTable.Create("Table_" + (string i), t))

