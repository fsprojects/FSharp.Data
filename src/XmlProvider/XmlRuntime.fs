namespace FSharp.Data.RuntimeImplementation

open System
open System.Xml.Linq
open System.Globalization

/// Underlying representation of the generated XML types
type XmlElement private (node:XElement) =
  /// Returns the raw XML element that is represented by the generated type
  member x.XElement = node
  static member Create(node:XElement) =
    XmlElement(node)

type XmlOperations = 
  // Operations for getting node values and values of attributes
  static member TryGetValue(xml:XmlElement) = 
    if String.IsNullOrEmpty(xml.XElement.Value) then None else Some xml.XElement.Value
  static member TryGetAttribute(xml:XmlElement, name) = 
    let attr = xml.XElement.Attribute(XName.Get(name))
    if attr = null then None else Some attr.Value

  // Operations that obtain children - depending on the inference, we may
  // want to get an array, option (if it may or may not be there) or 
  // just the value (if we think it is always there)
  static member GetChildrenArray(value:XmlElement, name) =
    [| for c in value.XElement.Elements(XName.Get(name)) ->
         XmlElement.Create(c) |]
  static member GetChildOption(value:XmlElement, name) =
    match XmlOperations.GetChildrenArray(value, name) with
    | [| it |] -> Some it
    | [| |] -> None
    | _ -> failwithf "XML mismatch: More than single '%s' child" name
  static member GetChild(value:XmlElement, name) =
    match XmlOperations.GetChildrenArray(value, name) with
    | [| it |] -> it
    | _ -> failwithf "XML mismatch: Expected exactly one '%s' child" name

  // Functions that transform specified chidlrens using a transformation
  // function - we need a version for array and option
  // (This is used e.g. when transforming `<a>1</a><a>2</a>` to `int[]`)
  static member ConvertArray<'R>(xml:XmlElement, name, f:XmlElement -> 'R) : 'R[] = 
    XmlOperations.GetChildrenArray(xml, name) |> Array.map f
  static member ConvertOptional<'R>(xml:XmlElement, name, f:XmlElement -> 'R) =
    XmlOperations.GetChildOption(xml, name) |> Option.map f
