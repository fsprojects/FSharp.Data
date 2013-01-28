// --------------------------------------------------------------------------------------
// Implements type inference for XML
// --------------------------------------------------------------------------------------

module ProviderImplementation.XmlInference

open System
open System.Xml.Linq
open FSharp.Data.RuntimeImplementation.TypeInference
open ProviderImplementation.StructureInference

// The type of XML element is always a record with a field
// for every attribute. If it has some content, then it also 
// contains a special field named "" which is either a collection
// (of other records etc.) or a primitive with the type of the content

/// Generates record fields for all attributes
let private getAttributes culture (element:XElement) =
  [ for attr in element.Attributes() do
      yield { Name = attr.Name.LocalName; Optional = false; 
              Type = inferPrimitiveType culture attr.Value None } ]


/// Infers type for the element, unifying nodes of the same name
/// accross the entire document (we first get information based
/// on just attributes and then use a fixed point)
let inferGlobalType culture (element:XElement) =

  // Initial state contains types with attributes but all 
  // children are ignored (bodies are based on just body values)
  let initialTypes =
    element.Document.Descendants() 
    |> Seq.groupBy (fun el -> el.Name)
    |> Seq.map (fun (name, elements) ->
        // Get attributes for all `name` named elements 
        let attributes =
          elements
          |> Seq.map (getAttributes culture)
          |> Seq.reduce unionRecordTypes 

        // Get type of body based on primitive values only
        let bodyType = 
          [ for e in elements do
              if not (String.IsNullOrEmpty(e.Value)) then
                yield inferPrimitiveType culture e.Value None ]
          |> Seq.fold subtypeInfered Top
        let body = { Name = ""; Optional = false; Type = bodyType }

        let record = Record(Some name.LocalName, body::attributes)
        name.LocalName, (elements, record) )
    |> Map.ofSeq

  /// Updates the types representing body in a given assignment
  /// (This is done repeatedly until we reach a fixed point)
  let assignment = initialTypes
  let mutable changed = true
  while changed do 
    changed <- false
    for (KeyValue(_, value)) in assignment do
      match value with 
      | elements, Record(Some name, body::attributes) -> 
          if body.Name <> "" then failwith "inferGlobalType: Assumed body element first"
          let children = [ for e in elements.Elements() -> assignment.[e.Name.LocalName] |> snd ]
          let bodyType = 
            if children = [] then body.Type
            else subtypeInfered (inferCollectionType children) body.Type
          changed <- changed || (body.Type <> bodyType)
          body.Type <- bodyType
      | _ -> failwith "inferGlobalType: Expected Record type with a name"

  assignment.[element.Name.LocalName] |> snd


/// Get information about type locally (the type of children is infered
/// recursively, so same elements in different positions have different types)
let rec inferLocalType culture (element:XElement) = 
  let props = 
    [ // Generate record fields for attributes
      yield! getAttributes culture element
      
      // If it has children, add collection content
      let children = element.Elements()
      if Seq.length children > 0 then
        let collection = inferCollectionType (Seq.map (inferLocalType culture) children)
        yield { Name = ""; Optional = false; Type = collection } 

      // If it has value, add primtiive content
      elif not (String.IsNullOrEmpty(element.Value)) then
        let primitive = inferPrimitiveType culture element.Value None
        yield { Name = ""; Optional = false; Type = primitive } ]  
  Record(Some element.Name.LocalName, props)

/// A type is infered either using `inferLocalType` which only looks
/// at immediate children or using `inferGlobalType` which unifies nodes
/// of the same name in the entire document
let inferType culture globalInference (element:XElement) = 
  if globalInference then inferGlobalType culture element
  else inferLocalType culture element
