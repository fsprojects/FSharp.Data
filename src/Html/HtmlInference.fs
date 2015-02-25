module ProviderImplementation.HtmlInference

open System
open System.Xml.Linq
open System.Globalization
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralInference
open FSharp.Data.Runtime.StructuralTypes

/// Generates record fields for all attributes
let private getAttributes inferTypesFromValues cultureInfo (node:HtmlNode) =
  [ for attr in HtmlNode.attributes node do
        yield { Name = attr.Name
                Type =
                    if inferTypesFromValues then
                        getInferedTypeFromString cultureInfo attr.Value None
                    else
                        InferedType.Primitive(typeof<string>, None, false)
              } ]

let getInferedTypeFromValue inferTypesFromValues cultureInfo (node:HtmlNode) =
    if inferTypesFromValues then
        let value = HtmlNode.innerText node
        getInferedTypeFromString cultureInfo value None
    else
        InferedType.Primitive(typeof<string>, None, false)

/// Get information about type locally (the type of children is infered
/// recursively, so same elements in different positions have different types)
let rec inferType inferTypesFromValues cultureInfo allowEmptyValues (node:HtmlNode) =
  let props = 
    [ // Generate record fields for attributes
      yield! getAttributes inferTypesFromValues cultureInfo node
      
      // If it has children, add collection content
      let contents = HtmlNode.elements node
      if Seq.length contents > 0 then
        let collection = inferCollectionType allowEmptyValues (Seq.map (inferType inferTypesFromValues cultureInfo allowEmptyValues) contents)
        yield { Name = ""
                Type = collection } 

      // If it has value, add primitive content
      elif not (String.IsNullOrEmpty (HtmlNode.innerText node)) then
        let primitive = getInferedTypeFromValue inferTypesFromValues cultureInfo node
        yield { Name = ""
                Type = primitive } ]
  
  InferedType.Record(node.TryName, props, false)
