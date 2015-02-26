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

let getInferedTypeFromValue inferTypesFromValues (parameters:HtmlDom.InferenceParameters) (node:HtmlNode) =
    if inferTypesFromValues then
        let value = HtmlNode.innerText node
        if String.IsNullOrWhiteSpace value || value = "&nbsp;" || value = "&nbsp" 
        then InferedType.Null
        elif Array.exists ((=) <| value.Trim()) parameters.MissingValues 
        then if parameters.PreferOptionals 
             then InferedType.Null 
             else getInferedTypeFromString parameters.CultureInfo value None
        else getInferedTypeFromString parameters.CultureInfo value None
    else
        InferedType.Primitive(typeof<string>, None, false)

/// Get information about type locally (the type of children is infered
/// recursively, so same elements in different positions have different types)
let rec inferNodeType inferTypesFromValues (parameters:HtmlDom.InferenceParameters)  allowEmptyValues (node:HtmlNode) =
  match node with
  | HtmlElement(name, _, _) -> 
        let props = 
          [ // Generate record fields for attributes
            yield! getAttributes inferTypesFromValues parameters.CultureInfo node
            
            // If it has children, add collection content
            let children = node.Elements(function | HtmlElement _ -> true | _ -> false)
            if Seq.length children > 0 
            then
                let collection = inferCollectionType allowEmptyValues (Seq.map (inferNodeType inferTypesFromValues parameters allowEmptyValues) children)
                yield { Name = ""
                        Type = collection } 

            // If it has value, add primitive content
            elif not (String.IsNullOrEmpty (HtmlNode.innerText node)) then
              let primitive = getInferedTypeFromValue inferTypesFromValues parameters node
              yield { Name = ""
                      Type = primitive } ]

        InferedType.Record(Some name, props, false)
    | HtmlText _ ->  getInferedTypeFromValue inferTypesFromValues parameters node
    | _ -> InferedType.Null 