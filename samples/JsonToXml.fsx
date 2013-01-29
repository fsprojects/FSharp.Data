(**
# F# Data: Converting between JSON and XML
*)

#r "System.Xml.Linq.dll"
#r "../bin/FSharp.Data.dll"
open System.Xml.Linq
open FSharp.Data.Json

/// Creates a json representation of the xml
let fromXml (xml:XElement) =
      
  let rec createObject (elem:XElement) =

    let attrs = 
      [ for attr in elem.Attributes() ->
          (attr.Name.LocalName, JsonValue.String attr.Value) ]

    let createArray xelems =
      [| for xelem in xelems -> createObject xelem |]
      |> JsonValue.Array

    let children =
      let groups = elem.Elements() |> Seq.groupBy (fun x -> x.Name.LocalName)
      [ for (key, childs) in groups ->
          match Seq.toList childs with
          | [child] -> key, createObject child
          | children -> key + "s", createArray children ]
        
    attrs @ children
    |> Map.ofSeq
    |> JsonValue.Object
      
  createObject xml

/// Creates a xml representation of the JsonValue (only valid on Objects and Arrays)
let toXml(x:JsonValue) =
  let attr name value = XAttribute(XName.Get name, value) :> XObject
  let elem name (value:obj) = XElement(XName.Get name, value) :> XObject
  let rec toXml = function
    | JsonValue.Null -> null
    | JsonValue.Boolean b -> b :> obj
    | JsonValue.Number number -> number :> obj
    | JsonValue.Float number -> number :> obj
    | JsonValue.String s -> s :> obj
    | JsonValue.Object properties -> 
        properties 
        |> Seq.sortBy (fun (KeyValue(k, v)) -> k)
        |> Seq.map (fun (KeyValue(key,value)) ->
        match value with
        | JsonValue.String s -> attr key s
        | JsonValue.Boolean b -> attr key b
        | JsonValue.Number n -> attr key n
        | JsonValue.Float n -> attr key n
        | _ -> elem key (toXml value)) :> obj
    | JsonValue.Array elements -> 
        elements 
        |> Seq.map (fun item -> elem "item" (toXml item)) :> obj
  (toXml x) :?> XObject seq

