namespace FSharp.Data.Runtime

open System
open System.Globalization
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Xml.Linq
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.HtmlExtensions
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralTypes
open ProviderImplementation.HtmlInference
open System.ComponentModel

#nowarn "10001"

type HtmlElement =
     { Html : HtmlNode }

       /// [omit]
     [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
     [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
     member x._Print = x.Html.ToString()
     
     /// [omit]
     [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
     [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
     override x.ToString() = x._Print
     
     /// [omit]
     [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
     [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
     static member Create(element) =
       { Html = element }

type HtmlRuntime =

    static member TryGetValue(n:HtmlElement) = 
      if String.IsNullOrEmpty(n.Html.InnerText()) then None else Some (n.Html.InnerText())
    
    static member TryGetAttribute(n:HtmlElement, name) = 
      n.Html.TryGetAttribute(name) |> Option.map (fun a -> a.Value) 
    
    // Operations that obtain children - depending on the inference, we may
    // want to get an array, option (if it may or may not be there) or 
    // just the value (if we think it is always there)  
    static member private GetChildrenArray(n:HtmlElement, name:string) =
      n.Html.Descendants(name) |> Seq.map (fun x -> { Html = x }) |> Seq.toArray
    
    static member private GetChildOption(value:HtmlElement, name) =
      match HtmlRuntime.GetChildrenArray(value, name) with
      | [| it |] -> Some it
      | [| |] -> None
      | array -> failwithf "XML mismatch: Expected zero or one '%s' child, got %d" name array.Length
    
    static member GetChild(value:HtmlElement, name) =
      let result = HtmlRuntime.GetChildrenArray(value, name)
      match result with
      | [| it |] -> it
      | array -> failwithf "XML mismatch: Expected exactly one '%s' child, got %d" name array.Length
    
    // Functions that transform specified chidlrens using a transformation
    // function - we need a version for array and option
    // (This is used e.g. when transforming `<a>1</a><a>2</a>` to `int[]`)
    
    static member ConvertArray<'R>(n:HtmlElement, name, f:Func<HtmlElement,'R>) : 'R[] = 
      HtmlRuntime.GetChildrenArray(n, name) |> Array.map f.Invoke
    
    static member ConvertOptional<'R>(n:HtmlElement, name, f:Func<HtmlElement,'R>) =
      HtmlRuntime.GetChildOption(n, name) |> Option.map f.Invoke
    
    static member ConvertOptional2<'R>(n:HtmlElement, name, f:Func<HtmlElement,'R option>) =
      HtmlRuntime.GetChildOption(n, name) |> Option.bind f.Invoke
    
    /// Returns Some if the specified HtmlNode has the specified name
    /// (otherwise None is returned). This is used when the current element
    /// can be one of multiple elements.
    static member ConvertAsName<'R>(n:HtmlElement, name, f:Func<HtmlElement,'R>) = 
      if n.Html.Name = name then Some(f.Invoke n)
      else None
            
    /// Creates a XElement with a scalar value and wraps it in a HtmlNode
    static member CreateValue(name, value:obj, cultureStr) =
      HtmlRuntime.CreateRecord(name, [| |], [| "", value |], cultureStr)
    
    // Creates a HtmlElement with the given attributes and elements and wraps it as a HtmlNode
    static member CreateRecord(name, attributes:(string * obj)[], elements:(string * obj)[], cultureStr) =
      let cultureInfo = TextRuntime.GetCulture cultureStr
      let inline strWithCulture v =
              (^a : (member ToString : IFormatProvider -> string) (v, cultureInfo)) 
      let serialize (v:obj) =
          match v with
          | :? HtmlNode as v -> box v
          | _ ->
              match v with
              | :? string        as v -> v
              | :? DateTime      as v -> strWithCulture v
              | :? int           as v -> strWithCulture v
              | :? int64         as v -> strWithCulture v
              | :? float         as v -> strWithCulture v
              | :? decimal       as v -> strWithCulture v
              | :? bool          as v -> if v then "true" else "false"
              | :? Guid          as v -> v.ToString()
              | _ -> failwithf "Unexpected value: %A" v
              |> box
      
      let elements = 
          [
             for (name, value) in elements ->
                match serialize value with
                | :? string as v -> HtmlElement(name, [], [HtmlText(v)])
                | :? HtmlNode as n -> HtmlElement(name, [], [n])
                | _ -> failwithf "Unexpected value"
          ]

      HtmlElement(name,
        attributes |> Array.map (fun (n,v) -> HtmlAttribute.New(n, match serialize v with | :? string as  v'-> v' | _ -> "")) |> List.ofArray,
        elements
      )

// --------------------------------------------------------------------------------------

namespace FSharp.Data.Runtime.BaseTypes

open System
open System.ComponentModel
open System.IO
open FSharp.Data
open FSharp.Data.Runtime

/// Underlying representation of the root types generated by HtmlProvider
type HtmlDocument internal (doc:FSharp.Data.HtmlDocument, htmlObjects:Map<string,HtmlDom.HtmlObject>) =

    member __.Html = doc

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member Create(includeLayoutTables, reader:TextReader) =
        let doc = 
            reader 
            |> HtmlDocument.Load
        let htmlObjects = 
            doc.GetObjects(None,includeLayoutTables)
            |> List.map (fun e -> e.Name, e) 
            |> Map.ofList
        HtmlDocument(doc, htmlObjects)

    /// [omit]
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsError=false)>]
    member __.GetObject(id:string) = 
        htmlObjects |> Map.find id

type HtmlRuntimeWrapper() = 
    
    [<EditorBrowsableAttribute(EditorBrowsableState.Never)>]
    [<CompilerMessageAttribute("This method is intended for use in generated code only.", 10001, IsHidden=true, IsError=false)>]
    static member Create(doc:HtmlDocument, id:string, hasHeaders:bool, headers:string[]) =
        HtmlElement.Create((doc.GetObject id).ToHtmlElement(hasHeaders, headers))
        