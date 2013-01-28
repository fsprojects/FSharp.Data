(**
# F# Data: Converting between JSON and XML
*)

#r "System.Xml.Linq.dll"
#r "../bin/FSharp.Data.dll"
open System.Xml.Linq
open FSharp.Data.Json

/// Creates a json representation of the xml
static member fromXml (xml:XElement) =
      
  let rec createObject (elem:XElement) =

    let j = 
      JsonValue.Object
      (JsonValue.emptyObject, elem.Attributes()) 
      ||> Seq.fold (fun j attr -> j.Add(attr.Name.LocalName, attr.Value)) 
      |> ref

    let createArray xelems =
      (JsonValue.emptyArray, xelems) ||> Seq.fold (fun j xelem -> j.Add(createObject xelem)) 

    elem.Elements()
    |> Seq.groupBy (fun x -> x.Name.LocalName)
    |> Seq.iter (fun (key, childs) ->
      match Seq.toList childs with
      | [child] -> j := (!j).Add(NameUtils.singularize key, createObject child)
      | children -> j := (!j).Add(NameUtils.pluralize key, createArray children))
        
    !j
      
  createObject xml

/// Creates a json representation of the xml
static member fromXml (xml:XDocument) = JsonValue.fromXml xml.Root

/// Creates a xml representation of the JsonValue (only valid on Objects and Arrays)
member x.ToXml() =
  let attr name value = XAttribute(XName.Get name, value) :> XObject
  let elem name (value:obj) = XElement(XName.Get name, value) :> XObject
  let rec toXml = function
    | JsonValue.Null -> null
    | JsonValue.Boolean b -> b :> obj
    | JsonValue.Number number -> number :> obj
    | JsonValue.BigNumber number -> number :> obj
    | JsonValue.String s -> s :> obj
    | JsonValue.Object properties -> 
      properties |> Seq.map (fun (KeyValue(key,value)) ->
        match value with
        | JsonValue.String s -> attr key s
        | JsonValue.Boolean b -> attr key b
        | JsonValue.Number n -> attr key n
        | JsonValue.BigNumber n -> attr key n
        | _ -> elem key (toXml value)) :> obj
    | JsonValue.Array elements -> 
      elements |> Seq.map (fun item -> elem "item" (toXml item)) :> obj
  (toXml x) :?> XObject seq

