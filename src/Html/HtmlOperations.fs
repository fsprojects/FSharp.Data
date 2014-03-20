module FSharp.Data.Html

// TODO: document all of these

open System
open System.IO
open System.Xml
open FSharp.Data

type HtmlAttribute with
    
    member x.As<'a>(parseF : string -> 'a)= 
        x.Value |> parseF 

    member x.TryAs<'a>(defaultValue, parseF : string -> (bool * 'a))= 
        match x.Value |> parseF with
        | true, v -> v
        | false, _ -> defaultValue


type HtmlElement with
    member x.Name 
        with get() =
            match x with
            | HtmlElement(name, _, _) -> name.ToLowerInvariant()
            | HtmlScript(_) -> "script"
            | HtmlStyle(_) -> "style"
            | _ -> String.Empty

    member x.Children
        with get() = 
            match x with
            | HtmlElement(_, _, children) -> children
            | _ -> []
    
    member x.Descendants(f) = 
        let rec descendantsBy f (x:HtmlElement) =
                seq {
                    
                    for element in x.Children do
                        if f element then yield element
                        yield! descendantsBy f element
                }
        descendantsBy f x

    member x.Descendants() = x.Descendants(fun _ -> true)

    member x.TryGetAttribute (name : string) =
        match x with
        | HtmlElement(_,attr,_) -> attr |> List.tryFind (fun a -> a.Name.ToLowerInvariant() = (name.ToLowerInvariant()))
        | _ -> None

    member x.GetAttributeValue (defaultValue,parseF,name) =
        match x.TryGetAttribute(name) with
        | Some(v) -> v.TryAs(defaultValue, parseF)
        | None -> defaultValue
    
    member x.Attribute name = 
        match x.TryGetAttribute name with
        | Some(v) -> v
        | None -> failwithf "Unable to find attribute (%s)" name

    member x.HasAttribute (name,value:string) =
        x.TryGetAttribute(name)
        |> function 
            | Some(attr) ->  attr.Value.ToLowerInvariant() = (value.ToLowerInvariant())
            | None -> false 

    member x.ElementsBy(f) = 
        let rec elementsBy f (x:HtmlElement) =
             seq {
                 if f x then yield x
                 for element in x.Children do
                     yield! elementsBy f element
             }
        elementsBy f x

    member x.Elements (names:seq<string>) =
        let nameSet = Set.ofSeq (names |> Seq.map (fun n -> n.ToLowerInvariant()))
        x.ElementsBy(fun x -> nameSet.Contains x.Name) |> Seq.toList

    member x.HasElement (names:seq<string>) =
        let nameSet = Set.ofSeq (names |> Seq.map (fun n -> n.ToLowerInvariant()))
        x.Children |> List.exists (fun x -> nameSet.Contains(x.Name))
    
    member x.InnerText
        with get() =
            let rec innerText' = function
                | HtmlElement(_,_, content) ->
                    String.Join(" ", seq { for e in content do
                                                match e with
                                                | HtmlText(text) -> yield text
                                                | elem -> yield innerText' elem })
                | HtmlText(text) -> text
                | HtmlScript _ | HtmlComment _ | HtmlStyle _ -> String.Empty
            innerText' x

    static member Write(writer:TextWriter, element:HtmlElement) = 
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

type HtmlDocument with
    member x.Body
        with get() =
            x.Elements
            |> List.tryPick (fun e -> 
                match e.Elements ["body"] with
                | [] -> None
                | h::_ -> Some(h)
            )
            


