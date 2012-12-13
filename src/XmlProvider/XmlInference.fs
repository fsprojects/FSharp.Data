// --------------------------------------------------------------------------------------
// Implements type inference for JSON
// --------------------------------------------------------------------------------------

module ProviderImplementation.XmlInference

open System
open System.Xml.Linq
open FSharp.Web
open ProviderImplementation.StructureInference

/// Infers the type of a simple string value (this is either
/// the value inside a node or value of an attribute)
let private inferPrimitiveType value =
  match value with 
  | StringEquals "true" | StringEquals "false"
  | StringEquals "yes" | StringEquals "no" -> Primitive typeof<bool>
  | Parse Int32.TryParse _ -> Primitive typeof<int>
  | Parse Int64.TryParse _ -> Primitive typeof<int64>
  | Parse Decimal.TryParse _ -> Primitive typeof<decimal>
  | Parse Double.TryParse _ -> Primitive typeof<float>
  | _ -> Primitive typeof<string>

/// The type of XML element is always a record with a field
/// for every attribute. If it has some content, then it also 
/// contains a special field named "" which is either a collection
/// (of other records etc.) or a primitive with the type of the content
let rec inferType (element:XElement) = 
  let props = 
    [ // Generate fields for all attributes
      for attr in element.Attributes() do
        yield { Name = attr.Name.LocalName; Optional = false; Type = inferPrimitiveType attr.Value }
      
      // If it has children, add collection content
      let children = element.Elements()
      if Seq.length children > 0 then
        let collection = inferCollectionType (Seq.map inferType children)
        yield { Name = ""; Optional = false; Type = collection } 

      // If it has value, add primtiive content
      elif not (String.IsNullOrEmpty(element.Value)) then
        let primitive = inferPrimitiveType element.Value
        yield { Name = ""; Optional = false; Type = primitive } ]  
  Record(Some element.Name.LocalName, props)