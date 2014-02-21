// --------------------------------------------------------------------------------------
// XML type provider - methods & types used by the generated erased code
// --------------------------------------------------------------------------------------
namespace FSharp.Data.Runtime

open System
open System.ComponentModel
open System.IO
open System.Xml.Linq
open System.Globalization

/// [omit]
/// Underlying representation of the generated XML types
[<StructuredFormatDisplay("{_Print}")>]
type XmlElement = 
  
  // NOTE: Using a record here to hide the ToString, GetHashCode & Equals
  // (but since this is used across multiple files, we have explicit Create method)
  { XElement : XElement }
  
  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  member x._Print = x.XElement.ToString()

  /// Creates a XmlElement representing the specified value
  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member Create(element) =
    { XElement = element }
  
  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member Create(reader:TextReader) =    
    use reader = reader
    let text = reader.ReadToEnd()
    let element = XDocument.Parse(text).Root 
    { XElement = element }
  
  [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
  [<CompilerMessageAttribute("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member CreateList(reader:TextReader) = 
    use reader = reader
    let text = reader.ReadToEnd()
    try
      XDocument.Parse(text).Root.Elements()
      |> Seq.map (fun value -> { XElement = value })
      |> Seq.toArray
    with _ ->
      XDocument.Parse("<root>" + text + "</root>").Root.Elements()
      |> Seq.map (fun value -> { XElement = value })
      |> Seq.toArray

/// [omit]
/// Static helper methods called from the generated code
type XmlRuntime = 

  // Operations for getting node values and values of attributes
  static member TryGetValue(xml:XmlElement) = 
    if String.IsNullOrEmpty(xml.XElement.Value) then None else Some xml.XElement.Value

  static member TryGetAttribute(xml:XmlElement, nameWithNS) = 
    let attr = xml.XElement.Attribute(XName.Get(nameWithNS))
    if attr = null then None else Some attr.Value

  // Operations that obtain children - depending on the inference, we may
  // want to get an array, option (if it may or may not be there) or 
  // just the value (if we think it is always there)
  static member private GetChildrenArray(value:XmlElement, nameWithNS) =
    [| for c in value.XElement.Elements(XName.Get(nameWithNS)) -> { XElement = c } |]
  
  static member private GetChildOption(value:XmlElement, nameWithNS) =
    match XmlRuntime.GetChildrenArray(value, nameWithNS) with
    | [| it |] -> Some it
    | [| |] -> None
    | array -> failwithf "XML mismatch: Expected zero or one '%s' child, got %d" nameWithNS array.Length

  static member GetChild(value:XmlElement, nameWithNS) =
    match XmlRuntime.GetChildrenArray(value, nameWithNS) with
    | [| it |] -> it
    | array -> failwithf "XML mismatch: Expected exactly one '%s' child, got %d" nameWithNS array.Length

  // Functions that transform specified chidlrens using a transformation
  // function - we need a version for array and option
  // (This is used e.g. when transforming `<a>1</a><a>2</a>` to `int[]`)
  static member ConvertArray<'R>(xml:XmlElement, nameWithNS, f:Func<XmlElement,'R>) : 'R[] = 
    XmlRuntime.GetChildrenArray(xml, nameWithNS) |> Array.map f.Invoke

  static member ConvertOptional<'R>(xml:XmlElement, nameWithNS, f:Func<XmlElement,'R>) =
    XmlRuntime.GetChildOption(xml, nameWithNS) |> Option.map f.Invoke

  /// Returns Some if the specified XmlElement has the specified name
  /// (otherwise None is returned). This is used when the current element
  /// can be one of multiple elements.
  static member ConvertAsName<'R>(xml:XmlElement, nameWithNS, f:Func<XmlElement,'R>) = 
    if xml.XElement.Name = XName.Get(nameWithNS) then Some(f.Invoke xml)
    else None