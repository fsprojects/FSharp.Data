#r "System.Xml.Linq"

open System
open System.IO
open System.Xml.Linq

let generateSetupScript dir = 
    let proj = Directory.GetFiles(dir, "*.fsproj") |> Seq.head

    let getElemName name = XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003")

    let getElemValue name (parent:XElement) =
        let elem = parent.Element(getElemName name)
        if elem = null || String.IsNullOrEmpty elem.Value then None else Some(elem.Value)
    
    let getAttrValue name (elem:XElement) =
        let attr = elem.Attribute(XName.Get name)
        if attr = null || String.IsNullOrEmpty attr.Value then None else Some(attr.Value)

    let (|??) (option1: 'a Option) option2 =
        if option1.IsSome then option1 else option2

    let fsProjFile = Directory.GetFiles(dir, "*.fsproj") |> Seq.head
    let fsProjXml = XDocument.Load fsProjFile

    let refs = 
        fsProjXml.Document.Descendants(getElemName "Reference")
        |> Seq.choose (fun elem -> getElemValue "HintPath" elem |?? getAttrValue "Include" elem)
        |> Seq.map (fun ref -> "#r \"" + ref.Replace(@"\", @"\\") + "\"")
        |> Seq.toList

    let fsFiles = 
        fsProjXml.Document.Descendants(getElemName "Compile")
        |> Seq.choose (fun elem -> getAttrValue "Include" elem)
        |> Seq.map (fun fsFile -> "#load \"" + fsFile + "\"")
        |> Seq.toList
    
    let tempFile = Path.Combine(dir, "__setup__.fsx")
    File.WriteAllLines(tempFile, refs @ fsFiles)    
