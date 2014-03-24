module FSharp.Data.Html

// TODO: document all of these

open System
open System.IO
open System.Xml
open FSharp.Data

module HtmlAttribute = 
    
    let name = function
        | HtmlAttribute(name = name) -> name
     
    let value = function
        | HtmlAttribute(value = value) -> value   

    let parseValue parseF attr = 
        value attr |> parseF

    let tryParseValue defaultValue parseF attr = 
        match value attr |> parseF with
        | true, v -> v
        | false, _ -> defaultValue

type HtmlAttribute with

    member x.Name with get() = HtmlAttribute.name x
    member x.Value() = HtmlAttribute.value x
    member x.Value<'a>(parseF : string -> 'a)= 
        HtmlAttribute.parseValue parseF x
    member x.Value<'a>(defaultValue, parseF : string -> (bool * 'a))= 
        HtmlAttribute.tryParseValue defaultValue parseF x

module HtmlTag = 
    
    let name = function
        | HtmlTag(name, _, _) -> name.ToLowerInvariant()
        | HtmlScript(_) -> "script"
        | HtmlStyle(_) -> "style"
        | _ -> String.Empty
        
    let children = function 
        | HtmlTag(_, _, children) -> children
        | _ -> []

    let descendents f x = 
        let rec descendantsBy f (x:HtmlTag) =
                seq {
                    
                    for element in children x do
                        if f element then yield element
                        yield! descendantsBy f element
                }
        descendantsBy f x
     
    let tryGetAttribute (name:string) = function
        | HtmlTag(_,attr,_) -> attr |> List.tryFind (fun a -> a.Name.ToLowerInvariant() = (name.ToLowerInvariant()))
        | _ -> None   

    let getAttributeValue defaultValue parseF name x = 
        match tryGetAttribute name x with
        | Some(v) -> v.Value(defaultValue, parseF)
        | None -> defaultValue

    let attribute name x = 
        match tryGetAttribute name x with
        | Some(v) -> v
        | None -> failwithf "Unable to find attribute (%s)" name
    
    let hasAttribute name (value:string) x = 
        tryGetAttribute name x
        |> function 
            | Some(attr) ->  attr.Value().ToLowerInvariant() = (value.ToLowerInvariant())
            | None -> false

    let elements f x = 
        let rec elementsBy f (x:HtmlTag) =
             seq {
                 if f x then yield x
                 for element in children x do
                     yield! elementsBy f element
             }
        elementsBy f x

    let elementsNamed names x = 
        let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
        elements (fun x -> name x |> nameSet.Contains) x |> Seq.toList

    let hasElements names x = 
        let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
        children x |> List.exists (fun x -> nameSet.Contains(name x))

    let innerText x = 
        let rec innerText' = function
            | HtmlTag(_,_, content) ->
                String.Join(" ", seq { for e in content do
                                            match e with
                                            | HtmlText(text) -> yield text
                                            | elem -> yield innerText' elem })
            | HtmlText(text) -> text
            | HtmlScript _ | HtmlComment _ | HtmlStyle _ -> String.Empty
        innerText' x

    let write writer x = 
        let createXmlWriter(baseWriter:TextWriter) =
            let s = new System.Xml.XmlWriterSettings(Indent = false,
                                                        OmitXmlDeclaration = true, 
                                                        ConformanceLevel = System.Xml.ConformanceLevel.Auto)
            XmlWriter.Create(baseWriter, s)
            
        let rec writeElement (writer:XmlWriter) = function
            | HtmlText(c) -> writer.WriteValue(c)
            | HtmlComment(c) -> writer.WriteComment(c)
            | HtmlScript(c) -> writer.WriteCData(c)
            | HtmlStyle(c) -> writer.WriteCData(c)
            | HtmlTag(name, attrs, elems) ->
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
        writeElement writer x

    let toString x =
        let sb = new Text.StringBuilder()
        use sw = new StringWriter()
        write sw x
        sb.ToString()
        

type HtmlTag with
    member x.Name with get() = HtmlTag.name x            
    member x.Children with get() = HtmlTag.children x
    member x.Descendants(f) = HtmlTag.descendents f x
    member x.Descendants() = HtmlTag.descendents (fun _ -> true) x
    member x.TryGetAttribute (name : string) = HtmlTag.tryGetAttribute name x
    member x.GetAttributeValue (defaultValue,parseF,name) = HtmlTag.getAttributeValue defaultValue parseF name x
    member x.Attribute name = HtmlTag.attribute name x
    member x.HasAttribute (name,value:string) = HtmlTag.hasAttribute name value x
    member x.Elements(f) = HtmlTag.elements f x 
    member x.Elements (names:seq<string>) = HtmlTag.elementsNamed names x
    member x.HasElement (names:seq<string>) = HtmlTag.hasElements names x
    member x.InnerText with get() = HtmlTag.innerText x
    static member Write(writer:TextWriter, tag:HtmlTag) = HtmlTag.write writer tag

module HtmlDocument = 
    
    let docType = function
        | HtmlDocument(docType = docType) -> docType

    let elements = function
        | HtmlDocument(elements = elements) -> elements

    let toString x =
        let docType = docType x
        (if String.IsNullOrEmpty docType then "" else "<!" + docType + ">\n")
        +
        (elements x |> List.map (HtmlTag.toString) |> String.concat "\n")

    let body (x:HtmlDocument) = 
        elements x
        |> List.tryPick (fun e -> 
            match e.Elements ["body"] with
            | [] -> None
            | h::_ -> Some(h)
        )

type HtmlDocument with
    member x.Body with get() = HtmlDocument.body x
    member x.Elements with get() = HtmlDocument.elements x
    member x.DocType with get() = HtmlDocument.docType x
            
            


