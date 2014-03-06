module FSharp.Data.Html

// TODO: document all of these

open System
open System.IO
open System.Xml

let name (x:HtmlElement) =
    match x with
    | HtmlElement(name, _, _) -> name.ToLowerInvariant()
    | HtmlScript(_) -> "script"
    | _ -> String.Empty

let tryGetAttribute (name : string) (x : HtmlElement) =
    match x with
    | HtmlElement(_,attr,_) -> attr |> List.tryFind (fun a -> a.Name.ToLowerInvariant() = (name.ToLowerInvariant()))
    | _ -> None

let getAttributeAs parseF name (e:HtmlElement) = 
    match tryGetAttribute name e with
    | Some(HtmlAttribute(_, colspan)) -> parseF(colspan)
    | None -> 0

let children (x:HtmlElement) =
    match x with
    | HtmlElement(_, _, children) -> children
    | _ -> []

let hasAttribute name (value:string) (x:HtmlElement) =
    tryGetAttribute name x
    |> function 
        | Some(attr) ->  attr.Value.ToLowerInvariant() = (value.ToLowerInvariant())
        | None -> false 

let getElementsNamed (names:seq<string>) (e:HtmlElement) =
    let nameSet = Set.ofSeq (names |> Seq.map (fun n -> n.ToLowerInvariant()))
    let rec named' (e:HtmlElement) = 
        [
                if nameSet.Contains(name e)
                then yield e   
                else 
                for child in (children e) do
                    yield! named' child     
        ]
    named' e

let hasChild (names:seq<string>) (e:HtmlElement) =
    let nameSet = Set.ofSeq (names |> Seq.map (fun n -> n.ToLowerInvariant()))
    (children e) |> List.exists (name >> nameSet.Contains)
    
let rec getValue = function
    | HtmlElement(_,_, content) ->
        String.Join(" ", seq { for e in content do
                                    match e with
                                    | HtmlText(text) -> yield text.Trim()
                                    | elem -> yield getValue elem })
    | HtmlText(text) | HtmlCharRef(text) -> text.Trim()
    | HtmlScript _ | HtmlComment _ | HtmlStyle _ -> String.Empty

let tryGetBody (HtmlDocument(_, es)) = 
    es
    |> List.tryPick (fun e ->
        match getElementsNamed ["body"] e with
        | [] -> None
        | h::_ -> Some(h)
    )
            

let write (writer:TextWriter) (element:HtmlElement) =
    let createXmlWriter(baseWriter:TextWriter) =
        let s = new System.Xml.XmlWriterSettings(Indent = false,
                                                    OmitXmlDeclaration = true, 
                                                    ConformanceLevel = System.Xml.ConformanceLevel.Auto)
        XmlWriter.Create(baseWriter, s)
        
    let rec writeElement (writer:XmlWriter) = function
        | HtmlText(c) -> writer.WriteValue(c)
        | HtmlCharRef(c) -> writer.WriteValue(c)
        | HtmlComment(c) -> writer.WriteComment(c)
        | HtmlScript(c) -> writer.WriteCData(c)
        | HtmlStyle(c) -> writer.WriteCData(c)
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
