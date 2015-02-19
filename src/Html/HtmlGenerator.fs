// --------------------------------------------------------------------------------------
// HTML type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Xml.Linq
open System.Collections.Generic
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralTypes

type internal HtmlGenerationContext =
    { CultureStr : string
      TypeProviderType : ProvidedTypeDefinition
      Replacer : AssemblyReplacer
      // to nameclash type names
      UniqueNiceName : string -> string }
    static member Create(cultureStr, tpType, replacer) =
        let uniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName
        uniqueNiceName "Html" |> ignore
        { CultureStr = cultureStr
          TypeProviderType = tpType
          Replacer = replacer
          UniqueNiceName = uniqueNiceName }

module internal HtmlTypeBuilder = 
    
    let getTypesForPrimitives (ctx:XmlGenerationContext) forceOptional (primitives:_ list) = [

        for primitive in primitives ->
        
            let name = 
                if primitives.Length = 1 
                then "Value" 
                else (StructuralInference.typeTag primitive).NiceName
            
            match primitive with 
            | InferedType.Primitive(typ, unit, optional) -> 
            
                let optional = optional || forceOptional
                let optionalJustBecauseThereAreMultiple = primitives.Length > 1 && not optional
                let optional = optional || primitives.Length > 1

                let typ, conv = ctx.ConvertValue <| PrimitiveInferedProperty.Create("Value", typ, optional, unit)
                let conv = fun xml -> conv <@ HtmlNode.Try(%%xml) @>
                
                typ, name, ctx.Replacer.ToDesignTime >> conv, optionalJustBecauseThereAreMultiple
            
            | InferedType.Json(typ, optional) -> 

                let cultureStr = ctx.CultureStr
                let ctx = JsonGenerationContext.Create(cultureStr, ctx.TypeProviderType, ctx.Replacer, ctx.UniqueNiceName, ctx.JsonTypeCache)
                let result = JsonTypeBuilder.generateJsonType ctx (*canPassAllConversionCallingTypes*)false (*optionalityHandledByParent*)true "" typ          
                
                let optional = optional || forceOptional
                let optionalJustBecauseThereAreMultiple = primitives.Length > 1 && not optional
                let optional = optional || primitives.Length > 1

                let typ = 
                    if optional
                    then ctx.MakeOptionType result.ConvertedType
                    else result.ConvertedType

                let conv = fun xml ->
                    if optional
                    then <@@ XmlRuntime.TryGetJsonValue(%%xml, cultureStr) @@>
                    else <@@ XmlRuntime.GetJsonValue(%%xml, cultureStr) @@>
                    |> result.GetConverter ctx

                typ, name, ctx.Replacer.ToDesignTime >> conv, optionalJustBecauseThereAreMultiple
            
            | _ -> failwithf "generatePropertiesForValue: Primitive or Json type expected: %A" primitive
    ]

    let rec generateType (ctx:HtmlGenerationContext) (inferedType:InferedType) = 
        match inferedType with

module internal HtmlGenerator =

    let private getPropertyName = NameUtils.capitalizeFirstLetter
    
    let private typeNameGenerator() =
        NameUtils.uniqueGenerator <| fun s ->
            HtmlParser.invalidTypeNameRegex.Value.Replace(s, " ")
            |> NameUtils.nicePascalName
  
    let private createTableType (replacer:AssemblyReplacer) (inferenceParameters:HtmlInference.Parameters, missingValuesStr, cultureStr) tableType (table:HtmlTable) = 
               
        let tableName = table.Name
        let tableHeaders = table.HeaderNamesAndUnits |> Array.map fst
        let hasHeaders = table.HasHeaders
        let inferedType = HtmlInference.inferType true inferenceParameters.CultureInfo true (table.ToHtmlElement(hasHeaders, tableHeaders))
        let ctx = HtmlGenerationContext.Create(cultureStr,tableType, replacer)
        let result = HtmlTypeBuilder.generateType ctx inferedType
        let runtimeTypeWrapper = replacer.ToRuntime (typeof<HtmlRuntimeTable>)
        
        let create (htmlDoc:Expr) =
            runtimeTypeWrapper?Create () (htmlDoc, tableName, hasHeaders, tableHeaders)

        (fun doc -> create doc |> result.Converter), result.ConvertedType

//    let private createListType replacer getListTypeName (inferenceParameters, missingValuesStr, cultureStr) (list:HtmlList) =
//        
//        let columns = HtmlInference.inferListType inferenceParameters list.Values
//
//        let listItemType, conv =
//            match columns with
//            | InferedType.Primitive(typ,_, optional) -> 
//                let typ, _, conv, _convBack = ConversionsGenerator.convertStringValue replacer missingValuesStr cultureStr (StructuralTypes.PrimitiveInferedProperty.Create("", typ, optional, None))
//                typ, conv
//            | _ -> 
//                let typ, _, conv, _convBack = ConversionsGenerator.convertStringValue replacer missingValuesStr cultureStr (StructuralTypes.PrimitiveInferedProperty.Create("", typeof<string>, false, None))
//                typ, conv
//                        
//        let listTypeWithErasedType = (replacer.ToRuntime typedefof<HtmlList<_>>).MakeGenericType(listItemType)
//        
//        let rowConverter =
//            
//            let rowVar = Var("row", typeof<string>)
//            let rowVarExpr = Expr.Var rowVar
//            let body = 
//              conv <@ TextConversions.AsString(%%rowVarExpr:string) @>
//              |> replacer.ToRuntime
//        
//            let delegateType = 
//              typedefof<Func<_,_>>.MakeGenericType(typeof<string>, listItemType)
//        
//            Expr.NewDelegate(delegateType, [rowVar], body)
//        
//        let create (htmlDoc:Expr) =
//            let rowConverterVar = Var("rowConverter", rowConverter.Type)
//            let body = listTypeWithErasedType?Create () (Expr.Var rowConverterVar, htmlDoc, list.Name)
//            Expr.Let(rowConverterVar, rowConverter, body)
//
//        let listType = ProvidedTypeDefinition(getListTypeName list.Name, Some listTypeWithErasedType, HideObjectMethods = true)
//        create, listType
//
//    let private createDefinitionListType (replacer:AssemblyReplacer) getDefinitionListTypeName (inferenceParameters, missingValuesStr, cultureStr) (definitionList:HtmlDefinitionList) =
//
//        let getListTypeName = typeNameGenerator()
//
//        let createListType index (list:HtmlList) =
//            
//            let columns = HtmlInference.inferListType inferenceParameters list.Values
//
//            let listItemType, conv =
//                match columns with
//                | StructuralTypes.InferedType.Primitive(typ,_, optional) -> 
//                    let typ, _, conv, _convBack = ConversionsGenerator.convertStringValue replacer missingValuesStr cultureStr (StructuralTypes.PrimitiveInferedProperty.Create("", typ, optional, None))
//                    typ, conv
//                | _ -> 
//                    let typ, _, conv, _convBack = ConversionsGenerator.convertStringValue replacer missingValuesStr cultureStr (StructuralTypes.PrimitiveInferedProperty.Create("", typeof<String>, false, None))
//                    typ, conv
//                        
//            let listTypeWithErasedType = (replacer.ToRuntime typedefof<HtmlList<_>>).MakeGenericType(listItemType)
//
//            let rowConverter =
//                
//                let rowVar = Var("row", typeof<string>)
//                let rowVarExpr = Expr.Var rowVar
//                let body = 
//                  conv <@ TextConversions.AsString(%%rowVarExpr:string) @>
//                  |> replacer.ToRuntime
//            
//                let delegateType = 
//                  typedefof<Func<_,_>>.MakeGenericType(typeof<string>, listItemType)
//            
//                Expr.NewDelegate(delegateType, [rowVar], body)
//            
//            let create (htmlDoc:Expr) =
//                let rowConverterVar = Var("rowConverter", rowConverter.Type)
//                let body = listTypeWithErasedType?CreateNested () (Expr.Var rowConverterVar, htmlDoc, definitionList.Name, index)
//                Expr.Let(rowConverterVar, rowConverter, body) 
//
//            let listType = ProvidedTypeDefinition(getListTypeName list.Name, Some listTypeWithErasedType, HideObjectMethods = true)
//            let prop = ProvidedProperty(getPropertyName list.Name, listType, GetterCode = fun (Singleton doc) -> create doc)
//
//            prop, listType
//            
//        let definitionListType = ProvidedTypeDefinition(getDefinitionListTypeName definitionList.Name, Some (replacer.ToRuntime typeof<HtmlDocument>), HideObjectMethods = true)
//        
//        for prop, listType in List.mapi createListType definitionList.Definitions do
//            definitionListType.AddMember listType
//            definitionListType.AddMember prop
//
//        definitionListType

    let generateTypes asm ns typeName parameters (replacer:AssemblyReplacer) (htmlObjects:HtmlObject list) =

        let htmlType = ProvidedTypeDefinition(asm, ns, typeName, Some (replacer.ToRuntime typeof<HtmlDocument>), HideObjectMethods = true)
        
        let containerTypes = Dictionary<string, ProvidedTypeDefinition>()

        let getTableTypeName = typeNameGenerator()
        let getListTypeName = typeNameGenerator()
        let getDefinitionListTypeName = typeNameGenerator()

        let getOrCreateContainer name = 
            match containerTypes.TryGetValue(name) with
            | true, t -> t
            | false, _ ->
                let containerType = ProvidedTypeDefinition(name + "Container", Some (replacer.ToRuntime typeof<HtmlDocument>), HideObjectMethods = true)
                htmlType.AddMember <| ProvidedProperty(name, containerType, GetterCode = fun (Singleton doc) -> doc)
                htmlType.AddMember containerType
                containerTypes.Add(name, containerType)
                containerType

        for htmlObj in htmlObjects do
            match htmlObj with
            | Table table ->
                 let containerType = getOrCreateContainer "Tables"
                 let tableType = ProvidedTypeDefinition(getTableTypeName table.Name, Some typeof<obj>)
                 htmlType.AddMember tableType
                 let create, tableType = createTableType replacer parameters tableType table
                 containerType.AddMember <| ProvidedProperty(getPropertyName table.Name, tableType, GetterCode = fun (Singleton doc) -> create doc)
            | List list -> ()
//                let containerType = getOrCreateContainer "Lists"
//                let create, tableType = createListType replacer getListTypeName parameters list
//                htmlType.AddMember tableType
//                containerType.AddMember <| ProvidedProperty(getPropertyName list.Name, tableType, GetterCode = fun (Singleton doc) -> create doc)
            | DefinitionList definitionList -> ()
//                let containerType = getOrCreateContainer "DefinitionLists"
//                let tableType = createDefinitionListType replacer getDefinitionListTypeName parameters definitionList
//                htmlType.AddMember tableType
//                containerType.AddMember <| ProvidedProperty(getPropertyName definitionList.Name, tableType, GetterCode = fun (Singleton doc) -> doc)

        htmlType
