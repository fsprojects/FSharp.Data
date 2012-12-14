// --------------------------------------------------------------------------------------
// Implements type inference for XML
// --------------------------------------------------------------------------------------

module ProviderImplementation.XmlInference

open System
open System.Xml.Linq
open ProviderImplementation.StructureInference
  
/// The type of XML element is always a record with a field
/// for every attribute. If it has some content, then it also 
/// contains a special field named "" which is either a collection
/// (of other records etc.) or a primitive with the type of the content
let rec inferType (element:XElement) = 
  let props = 
    [ // Generate fields for all attributes
      for attr in element.Attributes() do
        yield { Name = attr.Name.LocalName; Optional = false; Type = inferPrimitiveType attr.Value None }
      
      // If it has children, add collection content
      let children = element.Elements()
      if Seq.length children > 0 then
        let collection = inferCollectionType (Seq.map inferType children)
        yield { Name = ""; Optional = false; Type = collection } 

      // If it has value, add primtiive content
      elif not (String.IsNullOrEmpty(element.Value)) then
        let primitive = inferPrimitiveType element.Value None
        yield { Name = ""; Optional = false; Type = primitive } ]  
  Record(Some element.Name.LocalName, props)