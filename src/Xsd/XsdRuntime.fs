namespace FSharp.Data.Runtime

open System
open System.ComponentModel
open System.IO
open System.Xml.Schema
open System.Xml.Linq
open System.Globalization
open FSharp.Data.Runtime.BaseTypes

type XsdSchema = 
  
  // NOTE: Using a record here to hide the ToString, GetHashCode & Equals
  // (but since this is used across multiple files, we have explicit Create method)
  { Schema : System.Xml.Schema.XmlSchema}
  
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
      text.Split('\n', '\r')
      |> Array.filter (not << String.IsNullOrWhiteSpace)
      |> Array.map (fun text -> { XElement = XDocument.Parse(text).Root })