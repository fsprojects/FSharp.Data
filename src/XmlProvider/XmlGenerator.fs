// --------------------------------------------------------------------------------------
// XML type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Collections.Generic
open System.Reflection
open System.Xml.Linq
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.JsonInference
open ProviderImplementation.QuotationBuilder
open ProviderImplementation.StructureInference
open FSharp.Data
open FSharp.Data.RuntimeImplementation
open FSharp.Data.RuntimeImplementation.StructuralTypes

/// Context that is used to generate the XML types.
type internal XmlGenerationContext =
  { DomainType : ProvidedTypeDefinition
    Replacer : AssemblyReplacer
    UniqueNiceName : string -> string 
    UnifyGlobally : bool
    GeneratedResults : IDictionary<string, System.Type * (Expr -> Expr)> }
  static member Create(domainTy, unifyGlobally, replacer) =
    let uniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName
    uniqueNiceName "XElement" |> ignore
    { DomainType = domainTy
      Replacer = replacer
      GeneratedResults = new Dictionary<_, _>()
      UnifyGlobally = unifyGlobally
      UniqueNiceName = uniqueNiceName }

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
        | _ -> failwith "(|ContentType|_|): Only one collection type expected"
    | _ -> None

  /// Recursively walks over inferred type information and 
  /// generates types for read-only access to the document
  let rec generateXmlType culture ctx = function

    // If we already generated object for this type, return it
    | Record(Some nameWithNs, props) when ctx.GeneratedResults.ContainsKey(nameWithNs) -> 
        ctx.GeneratedResults.[nameWithNs]
    
    // If the node does not have any children and always contains only primitive type
    // then we turn it into a primitive value of type such as int/string/etc.
    | Record(Some nameWithNs, [{ Name = ""; Optional = opt; Type = Primitive(typ, _) }]) ->
        let resTyp, _, convFunc, _ = 
          Conversions.convertValue ctx.Replacer ("", culture) (PrimitiveInferedProperty.Create("Value", typ, opt))
        resTyp, fun xml -> let xml = ctx.Replacer.ToDesignTime xml in convFunc <@@ XmlOperations.TryGetValue(%%xml) @@>

    // If the node is more complicated, then we generate a type to represent it properly
    | Record(Some nameWithNS, props) -> 
        let name = XName.Get(nameWithNS).LocalName
        let objectTy = ProvidedTypeDefinition(ctx.UniqueNiceName name, Some(ctx.Replacer.ToRuntime typeof<XmlElement>), HideObjectMethods = true)
        ctx.DomainType.AddMember(objectTy)

        // If we unify types globally, then save type for this record
        if ctx.UnifyGlobally then
          ctx.GeneratedResults.Add(nameWithNS, (objectTy :> System.Type, ctx.Replacer.ToRuntime))

        // Split the properties into attributes and a 
        // special property representing the content
        let attrs, content =
          props |> List.partition (fun prop -> prop.Name <> "")

        let gen = NameUtils.uniqueGenerator NameUtils.nicePascalName

        // Generate properties for all XML attributes
        for attr in attrs do
          let nameWithNS = attr.Name
          let name = XName.Get(nameWithNS).LocalName
          match attr.Type with 
          | Heterogeneous types ->
              // If the attribute has multiple possible type (e.g. "bool|int") then we generate
              // a choice type that is erased to 'option<string>' (for simplicity, assuming that
              // the attribute is always optional)
              let choiceTy = ProvidedTypeDefinition(ctx.UniqueNiceName (name + "Choice"), Some(typeof<option<string>>), HideObjectMethods = true)
              ctx.DomainType.AddMember(choiceTy)
              for KeyValue(kind, typ) in types do 
                match typ with
                | InferedType.Null -> ()
                | InferedType.Primitive(primTyp, _) ->
                    // Conversion function takes 'option<string>' to the required type
                    let valTy, _, convFunc, _ = 
                      Conversions.convertValue ctx.Replacer ("", culture) (PrimitiveInferedProperty.Create(kind.NiceName, primTyp, true))
                    let p = ProvidedProperty(kind.NiceName, valTy)
                    p.GetterCode <- fun (Singleton attrVal) -> 
                      let attrVal = ctx.Replacer.ToDesignTime attrVal
                      convFunc attrVal
                    choiceTy.AddMember(p)   
                | _ -> failwith "generateXmlType: A choice type of an attribute can only contain primitive types"

              // Generate a property for the attribute which has a type 'choiceTy'
              // (and returns 'string option' value that we get from 'TryGetAttribute'
              let primProp = PrimitiveInferedProperty.Create("Attribute " + name, typeof<string>, attr.Optional)
              let resTyp, _, convFunc, _ = Conversions.convertValue ctx.Replacer ("", culture) primProp

              let p = ProvidedProperty(gen name, choiceTy)
              p.GetterCode <- fun (Singleton xml) -> 
                let xml = ctx.Replacer.ToDesignTime xml 
                ctx.Replacer.ToRuntime <@@ XmlOperations.TryGetAttribute(%%xml, nameWithNS) @@>
              objectTy.AddMember(p)

          | Primitive(typ, _) ->
              // 
              let resTyp, _, convFunc, _ = 
                Conversions.convertValue ctx.Replacer ("", culture) (PrimitiveInferedProperty.Create("Attribute " + name, typ, attr.Optional))

              // Add property with PascalCased name
              let p = ProvidedProperty(gen name, resTyp)
              p.GetterCode <- fun (Singleton xml) -> let xml = ctx.Replacer.ToDesignTime xml in convFunc <@@ XmlOperations.TryGetAttribute(%%xml, nameWithNS) @@>
              objectTy.AddMember(p)          
          | _ -> failwith "generateXmlType: Expected Primitive or Choice type"


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
                  let resTyp, _, convFunc, _ = 
                    Conversions.convertValue ctx.Replacer ("", culture) (PrimitiveInferedProperty.Create("Value", typ, opt))
                  let name = 
                    gen <| if primitives.Length = 1 then "Value" else
                           (typeTag primitive).NiceName + NameUtils.nicePascalName "Value"
                  let p = ProvidedProperty(name, resTyp)
                  p.GetterCode <- fun (Singleton xml) -> let xml = ctx.Replacer.ToDesignTime xml in convFunc <@@ XmlOperations.TryGetValue(%%xml) @@>
                  objectTy.AddMember(p)          
              | _ -> failwith "generateXmlType: Primitive type expected"

            // For every possible child node, generate 'GetXyz()' method (if there
            // is multiple of them) or just a getter property if there is one or none
            [ for node in nodes ->
                match node with
                | KeyValue(InferedTypeTag.Record(Some nameWithNS), (multiplicity, typ)) ->
                
                    let name = XName.Get(nameWithNS).LocalName
                    let childTy, childConv = generateXmlType culture ctx typ 
                    match multiplicity with
                    | InferedMultiplicity.Single ->
                        let p = ProvidedProperty(gen name, childTy)
                        p.GetterCode <- fun (Singleton xml) -> let xml = ctx.Replacer.ToDesignTime xml in childConv <@@ XmlOperations.GetChild(%%xml, nameWithNS) @@>
                        p :> MemberInfo

                    // For options and arrays, we need to generate call to ConvertArray or ConvertOption
                    // (because the node may be represented as primitive type - so we cannot just
                    // return array of XmlElement - it might be for example int[])
                    | InferedMultiplicity.Multiple ->
                        let m = ProvidedMethod(gen ("Get" + NameUtils.nicePascalName (NameUtils.pluralize name)), [], childTy.MakeArrayType())
                        let convTyp, convFunc = ReflectionHelpers.makeDelegate childConv (ctx.Replacer.ToRuntime typeof<XmlElement>)
                        m.InvokeCode <- fun (Singleton xml) -> 
                          let operationsTyp = ctx.Replacer.ToRuntime typeof<XmlOperations>
                          operationsTyp?ConvertArray (convTyp) (xml, nameWithNS, convFunc)
                        m :> MemberInfo

                    | InferedMultiplicity.OptionalSingle ->
                        let p = ProvidedProperty(gen name, typedefof<option<_>>.MakeGenericType [| childTy |])
                        let convTyp, convFunc = ReflectionHelpers.makeDelegate childConv (ctx.Replacer.ToRuntime typeof<XmlElement>)
                        p.GetterCode <- fun (Singleton xml) -> 
                          let operationsTyp = ctx.Replacer.ToRuntime typeof<XmlOperations>
                          operationsTyp?ConvertOptional (convTyp) (xml, nameWithNS, convFunc)
                        p :> MemberInfo

                | _ -> failwith "generateXmlType: Child nodes should be named record types" ]

            |> objectTy.AddMembers 

        | [_] -> failwith "generateXmlType: Children should be collection or heterogeneous"
        | _::_ -> failwith "generateXmlType: Only one child collection expected"
        | [] -> ()
        objectTy :> Type, ctx.Replacer.ToRuntime

    | _ -> failwith "generateXmlType: Infered type should be record type."
