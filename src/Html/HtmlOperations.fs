namespace FSharp.Data

// TODO: document all of these

open System
open System.IO
open System.Xml
open FSharp.Data
open FSharp.Data.Runtime

module Html =
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
    
    module HtmlNode = 
        
        let name = function
            | HtmlElement(_, name, _, _) -> name.ToLowerInvariant()
            | HtmlContent(_, HtmlContentType.Script, _ ) -> "script"
            | HtmlContent(_, HtmlContentType.Style, _) -> "style"
            | _ -> String.Empty
            
        let children = function 
            | HtmlElement(_, _, _, children) -> children
            | _ -> []
    
        let parent = function
            | HtmlElement(parent = parent) 
            | HtmlContent(parent = parent) -> !parent
        
        let descendantsAndSelf f x = 
            let rec descendantsBy f (x:HtmlNode) =
                    [   
                        if f x then yield x
                        for element in children x do
                            yield! descendantsBy f element
                    ]
            descendantsBy f x
    
        let descendantsAndSelfNamed names x = 
            let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
            descendantsAndSelf (fun x -> name x |> nameSet.Contains) x
    
        let descendants f x = 
            let rec descendantsBy f (x:HtmlNode) =
                    [   for element in children x do
                            if f element then yield element
                            yield! descendantsBy f element
                    ]
            descendantsBy f x
    
        let descendantsNamed names x = 
            let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
            descendants (fun x -> name x |> nameSet.Contains) x
    
        let hasDescendants names x = 
            let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
            descendants (fun x -> nameSet.Contains(name x)) x |> Seq.isEmpty |> not
         
        let tryGetAttribute (name:string) = function
            | HtmlElement(_,_,attr,_) -> attr |> List.tryFind (fun a -> a.Name.ToLowerInvariant() = (name.ToLowerInvariant()))
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
            [
                for element in children x do
                    if f element 
                    then yield element
                    else ()
            ]
    
        let elementsAndSelf f x = 
            [
                if f x then yield x
                for element in children x do
                    if f element 
                    then yield element
                    else ()
            ]
    
        let elementsAndSelfNamed names x = 
            let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
            elementsAndSelf (fun x -> name x |> nameSet.Contains) x
    
        let hasElementsAndSelf names x = 
            elementsAndSelfNamed names x |> List.isEmpty |> not
    
        let elementsNamed names x = 
            let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
            elements (fun x -> name x |> nameSet.Contains) x
    
        let hasElements names x = 
            elementsNamed names x |> List.isEmpty |> not
    
    
        let innerText x = 
            let rec innerText' = function
                | HtmlElement(_,_,_, content) ->
                    String.Join(" ", seq { for e in content do
                                                match e with
                                                | HtmlContent(_,HtmlContentType.Content,text) -> yield text
                                                | elem -> yield innerText' elem })
                | HtmlContent(_,HtmlContentType.Content,text) -> text
                | _ -> String.Empty
            innerText' x
         
        let siblings x =
            match x with
            | HtmlElement(parent = parent)
            | HtmlContent(parent = parent) ->
                 match !parent with
                 | Some(p) -> 
                    elements ((<>) x) p
                 | None -> []
               
    
    type HtmlNode with
        member x.Name with get() = HtmlNode.name x            
        member x.Children with get() = HtmlNode.children x
        member x.Parent with get() = HtmlNode.parent x
        member x.Descendants(?f, ?includeSelf) = 
            let f = defaultArg f (fun _ -> true)
            if (defaultArg includeSelf false)
            then HtmlNode.descendantsAndSelf f x
            else HtmlNode.descendants f x
        member x.Descendants(names:seq<string>, ?includeSelf) = 
            if (defaultArg includeSelf false)
            then HtmlNode.descendantsAndSelfNamed names x
            else HtmlNode.descendantsNamed names x
        member x.TryGetAttribute (name : string) = HtmlNode.tryGetAttribute name x
        member x.GetAttributeValue (defaultValue,parseF,name) = HtmlNode.getAttributeValue defaultValue parseF name x
        member x.Attribute name = HtmlNode.attribute name x
        member x.HasAttribute (name,value:string) = HtmlNode.hasAttribute name value x
        member x.Elements(?f, ?includeSelf) = 
            let f = defaultArg f (fun _ -> true)
            if (defaultArg includeSelf false)
            then HtmlNode.elementsAndSelf f x
            else HtmlNode.elements f x 
        member x.Elements(names:seq<string>, ?includeSelf) = 
            if (defaultArg includeSelf false)
            then HtmlNode.elementsAndSelfNamed names x
            else HtmlNode.elementsNamed names x 
        member x.HasElement (names:seq<string>) = HtmlNode.hasElements names x
        member x.Siblings() = HtmlNode.siblings x
        member x.InnerText with get() = HtmlNode.innerText x
    
    module HtmlDocument = 
        
        let docType = function
            | HtmlDocument(docType = docType) -> docType 
    
        let elements f = function
            | HtmlDocument(elements = elements) ->
                [
                    for e in elements do
                        if f e then yield e
                ]
                    
        let elementsNamed names x = 
            let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
            elements (fun x -> HtmlNode.name x |> nameSet.Contains) x
    
        let hasElements names x = 
            elementsNamed names x |> List.isEmpty |> not

        let descendants f x =
            [
               for e in elements (fun _ -> true) x do
                   if f e then yield e
                   yield! HtmlNode.descendants f e
            ] 

        let descendantsNamed names x = 
            let nameSet = Set.ofSeq (names |> Seq.map (fun (n:string) -> n.ToLowerInvariant()))
            descendants (fun x -> HtmlNode.name x |> nameSet.Contains) x
    
        let hasDescendants names x = 
            descendantsNamed names x |> List.isEmpty |> not

        let body (x:HtmlDocument) = 
            match descendantsNamed ["body"] x with
            | [] -> failwith "No element body found!"
            | h:: _ -> h
    
        let tryBody (x:HtmlDocument) = 
            match descendantsNamed ["body"] x with
            | [] -> None
            | h:: _ -> Some(h)
    
    type HtmlDocument with
        member x.Body with get() = HtmlDocument.body x
        member x.TryBody() = HtmlDocument.tryBody x
        member x.Descendants(?f) = 
            let f = defaultArg f (fun _ -> true)
            HtmlDocument.descendants f x
        member x.Descendants(names) = 
            HtmlDocument.descendantsNamed names x
        member x.Elements(?f) = 
            let f = defaultArg f (fun _ -> true)
            HtmlDocument.elements f x
        member x.Elements(names) = 
            HtmlDocument.elementsNamed names x
    
        /// Parses the specified HTML string
        static member Parse(text) = 
          use reader = new StringReader(text)
          HtmlParser.parse reader
    
        /// Loads HTML from the specified stream
        static member Load(stream:Stream) = 
          use reader = new StreamReader(stream)
          HtmlParser.parse reader
    
        /// Loads HTML from the specified reader
        static member Load(reader:TextReader) = 
          HtmlParser.parse reader
    
        /// Loads HTML from the specified uri asynchronously
        static member AsyncLoad(uri:string) = async {
          let! reader = IO.asyncReadTextAtRuntime false "" "" "HTML" uri
          return HtmlParser.parse reader
        }
    
        /// Loads HTML from the specified uri
        static member Load(uri:string) =
            HtmlDocument.AsyncLoad(uri)
            |> Async.RunSynchronously
    
    [<AutoOpen>]
    module Dsl =
        
        let element name attrs children =
            (fun parent ->  
                let this = ref None
                let attrs = Seq.map HtmlAttribute attrs |> Seq.toList
                let e = HtmlElement(parent, name, attrs, children |> List.map (fun c -> c this))
                this := Some e
                e
            )
        
        let content contentType content = 
            (fun parent -> 
                HtmlContent(parent, contentType, content)
            )
        
        let doc docType children = 
            let this = ref None
            HtmlDocument(docType, children |> List.map (fun c -> c this))
