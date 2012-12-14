// --------------------------------------------------------------------------------------
// XML type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Xml.Linq
open System.Globalization
open ProviderImplementation.JsonInference
open ProviderImplementation.StructureInference

// --------------------------------------------------------------------------------------
// Runtime components used by the generated XML types
// --------------------------------------------------------------------------------------

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

// --------------------------------------------------------------------------------------
// Compile-time components that are used to generate XML types
// --------------------------------------------------------------------------------------

open System
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes

/// Context that is used to generate the XML types.
type internal XmlGenerationContext =
  { DomainType : ProvidedTypeDefinition
    UniqueNiceName : string -> string }
  static member Create(domainTy) =
    { DomainType = domainTy
      UniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName }

module internal XmlTypeBuilder = 

  /// Recognizes different valid infered types of content:
  ///
  ///  - `Primitive` means that the content is a value and there are no children
  ///  - `Collection` means that there are always just children but no value
  ///  - `Heterogeneous` means that there may be either children or value(s)
  ///
  /// We return a list with all possible primitive types and all possible
  /// children types (both may be empty)
  let (|ContentType|_|) content = 
    match content with 
    | { Type = (Primitive _) as typ } -> Some([typ], Map.empty)
    | { Type = Collection nodes } -> Some([], nodes)
    | { Type = Heterogeneous cases } ->
        let collections, others = 
          Map.toList cases |> List.partition (fst >> ((=) InferedTypeTag.Collection))
        match collections with
        | [InferedTypeTag.Collection, Collection nodes] -> Some(List.map snd others, nodes)
        | [] -> Some(List.map snd others, Map.empty)
        | _ -> failwith "generateXmlType: Only one collection type expected"
    | _ -> None

  /// Recursively walks over inferred type information and 
  /// generates types for read-only access to the document
  let rec generateXmlType ctx = function
    
    // If the node does not have any children and always contains only primitive type
    // then we turn it into a primitive value of type such as int/string/etc.
    | InferedType.Record(Some name, [{ Name = ""; Optional = opt; Type = Primitive(typ, _) }]) ->
        let opt = opt && typ <> typeof<string>
        let resTyp, convFunc = Conversions.convertValue "Value" opt typ 
        resTyp, fun xml -> convFunc <@@ XmlOperations.TryGetValue(%%xml) @@>

    // If the node is more complicated, then we generate a type to represent it properly
    | InferedType.Record(Some name, props) -> 
        let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName name, Some(typeof<XmlElement>))
        ctx.DomainType.AddMember(objectTy)

        // Split the properties into attributes and a 
        // special property representing the content
        let attrs, content =
          props |> List.partition (fun prop -> prop.Name <> "")

        // Generate properties for all XML attributes
        for attr in attrs do
          let name = attr.Name
          let typ = match attr.Type with Primitive(t, _) -> t | _ -> failwith "generateXmlType: Expected Primitive type"
          let opt = attr.Optional && (attr.Type <> Primitive(typeof<string>, None)) 
          let resTyp, convFunc = Conversions.convertValue ("Attribute " + name) opt typ
          
          // Add property with PascalCased name
          let p = ProvidedProperty(NameUtils.nicePascalName attr.Name, resTyp)
          p.GetterCode <- fun (Singleton xml) -> convFunc <@@ XmlOperations.TryGetAttribute(%%xml, name) @@>
          objectTy.AddMember(p)          


        // Add properties that can be used to access content of the node
        // (either child nodes or primitive values - if the node contains simple values)
        match content with 
        | [ContentType(primitives, nodes)] ->

            // For every possible primitive type add '<Kind>Value' property that 
            // returns it converted to the right type (or an option)
            for primitive in primitives do 
              match primitive with 
              | Primitive(typ, _) -> 
                  // If there may be other primitives or nodes, it is optional
                  let opt = nodes.Count > 0 || primitives.Length > 1
                  let resTyp, convFunc = Conversions.convertValue "Value" opt typ 
                  let name = 
                    if primitives.Length = 1 then "Value" else
                    (typeTag primitive).NiceName + NameUtils.nicePascalName "Value"
                  let p = ProvidedProperty(name, resTyp)
                  p.GetterCode <- fun (Singleton xml) -> convFunc <@@ XmlOperations.TryGetValue(%%xml) @@>
                  objectTy.AddMember(p)          
              | _ -> failwith "generateXmlType: Primitive type expected"

            // For every possible child node, generate 'GetXyz()' method (if there
            // is multiple of them) or just a getter property if there is one or none
            for node in nodes do
              match node with
              | (KeyValue(InferedTypeTag.Record(Some name), (multiplicity, typ))) ->
                  
                  let childTy, childConv = generateXmlType ctx typ 
                  match multiplicity with
                  | InferedMultiplicity.Single ->
                      let p = ProvidedProperty(NameUtils.nicePascalName name, childTy)
                      p.GetterCode <- fun (Singleton xml) -> childConv <@@ XmlOperations.GetChild(%%xml, name) @@>
                      objectTy.AddMember(p)

                  // For options and arrays, we need to generate call to ConvertArray or ConvertOption
                  // (because the node may be represented as primitive type - so we cannot just
                  // return array of XmlElement - it might be for example int[])
                  | InferedMultiplicity.Multiple ->
                      let m = ProvidedMethod("Get" + NameUtils.nicePascalName (NameUtils.pluralize name), [], childTy.MakeArrayType())
                      let convTyp, convFunc = ReflectionHelpers.makeFunc childConv typeof<XmlElement>
                      m.InvokeCode <- fun (Singleton xml) -> 
                        ReflectionHelpers.makeMethodCall 
                          typeof<XmlOperations> "ConvertArray"
                          [ convTyp ] [ xml; Expr.Value(name); convFunc ]
                      objectTy.AddMember(m)

                  | InferedMultiplicity.OptionalSingle ->
                      let p = ProvidedProperty(NameUtils.nicePascalName name, typedefof<option<_>>.MakeGenericType [| childTy |])
                      let convTyp, convFunc = ReflectionHelpers.makeFunc childConv typeof<XmlElement>
                      p.GetterCode <- fun (Singleton xml) -> 
                        ReflectionHelpers.makeMethodCall 
                          typeof<XmlOperations> "ConvertOption"
                          [ convTyp ] [ xml; Expr.Value(name); convFunc ]
                      objectTy.AddMember(p)

              | _ -> failwith "generateXmlType: Child nodes should be named record types"
        | [_] -> failwith "generateXmlType: Children should be collection or heterogeneous"
        | _::_ -> failwith "generateXmlType: Only one child collection expected"
        | [] -> ()
        objectTy :> Type, id

    | _ -> failwith "generateXmlType: Infered type should be record type."