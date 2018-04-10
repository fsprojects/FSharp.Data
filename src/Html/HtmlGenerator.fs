// --------------------------------------------------------------------------------------
// HTML type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Collections.Generic
open FSharp.Quotations
open FSharp.Reflection
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralTypes

module internal HtmlGenerator =

    type private FieldInfo = 
      { /// The representation type that is part of the tuple we extract the field from
        TypeForTuple : Type
        /// The provided property corresponding to the field
        ProvidedProperty : ProvidedProperty
        Convert: Expr -> Expr }

    let private getPropertyName = NameUtils.capitalizeFirstLetter
    
    let private typeNameGenerator() =
        NameUtils.uniqueGenerator <| fun s ->
            HtmlParser.invalidTypeNameRegex.Value.Replace(s, " ")
            |> NameUtils.nicePascalName

    let private createTableType getTableTypeName (inferenceParameters, missingValuesStr, cultureStr) (table:HtmlTable) = 

        let columns =  
            match table.InferedProperties with
            | Some inferedProperties -> inferedProperties
            | None -> 
                HtmlInference.inferColumns inferenceParameters 
                                           table.HeaderNamesAndUnits.Value 
                                           (if table.HasHeaders.Value then table.Rows.[1..] else table.Rows)

        let fields = columns |> List.mapi (fun index field ->
            let typ, typWithoutMeasure, conv, _convBack = ConversionsGenerator.convertStringValue missingValuesStr cultureStr field
            { TypeForTuple = typWithoutMeasure
              ProvidedProperty = 
                  ProvidedProperty(field.Name, typ, getterCode = fun (Singleton row) -> 
                           if columns.Length = 1 then row else Expr.TupleGet(row, index))
              Convert = fun rowVarExpr -> conv <@ TextConversions.AsString((%%rowVarExpr:string[]).[index]) @> } )
        
        // The erased row type will be a tuple of all the field types (without the units of measure)
        let rowErasedType =
            (match fields with 
             | [ x ] -> x.TypeForTuple
             | _ -> FSharpType.MakeTupleType [| for field in fields -> field.TypeForTuple |])
        
        let rowType = ProvidedTypeDefinition("Row", Some rowErasedType, hideObjectMethods = true, nonNullable = true)
        
        // Each property of the generated row type will simply be a tuple get
        for field in fields do
            rowType.AddMember field.ProvidedProperty
        
        let tableErasedWithRowErasedType = typedefof<HtmlTable<_>>.MakeGenericType(rowErasedType)
        let tableErasedTypeWithGeneratedRow = typedefof<HtmlTable<_>>.MakeGenericType(rowType)
        
        let rowConverter =
            let rowVar = Var("row", typeof<string[]>)
            let rowVarExpr = Expr.Var rowVar
            let body =
              if fields.Length = 1
              then fields.Head.Convert rowVarExpr
              else Expr.NewTuple [ for field in fields -> field.Convert rowVarExpr ]
        
            let delegateType = 
              typedefof<Func<_,_>>.MakeGenericType(typeof<string[]>, rowErasedType)
        
            Expr.NewDelegate(delegateType, [rowVar], body)
        
        let create (htmlDoc:Expr) =
            let rowConverterVar = Var("rowConverter", rowConverter.Type)
            let body = tableErasedWithRowErasedType?Create () (Expr.Var rowConverterVar, htmlDoc, table.Name, table.HasHeaders.Value)
            Expr.Let(rowConverterVar, rowConverter, body)
        
        let tableType = ProvidedTypeDefinition(getTableTypeName table.Name, Some tableErasedTypeWithGeneratedRow, hideObjectMethods = true, nonNullable = true)
        tableType.AddMember rowType
        
        create, tableType

    let private createListType getListTypeName (inferenceParameters, missingValuesStr, cultureStr) (list:HtmlList) =
        
        let columns = HtmlInference.inferListType inferenceParameters list.Values

        let listItemType, conv =
            match columns with
            | InferedType.Primitive(typ,_, optional) -> 
                let typ, _, conv, _convBack = ConversionsGenerator.convertStringValue missingValuesStr cultureStr (StructuralTypes.PrimitiveInferedProperty.Create("", typ, optional, None))
                typ, conv
            | _ -> 
                let typ, _, conv, _convBack = ConversionsGenerator.convertStringValue missingValuesStr cultureStr (StructuralTypes.PrimitiveInferedProperty.Create("", typeof<string>, false, None))
                typ, conv
                        
        let listTypeWithErasedType = typedefof<HtmlList<_>>.MakeGenericType(listItemType)
        
        let rowConverter =
            
            let rowVar = Var("row", typeof<string>)
            let rowVarExpr = Expr.Var rowVar
            let body = 
              conv <@ TextConversions.AsString(%%rowVarExpr:string) @>
        
            let delegateType = typedefof<Func<_,_>>.MakeGenericType(typeof<string>, listItemType)
        
            Expr.NewDelegate(delegateType, [rowVar], body)
        
        let create (htmlDoc:Expr) =
            let rowConverterVar = Var("rowConverter", rowConverter.Type)
            let body = listTypeWithErasedType?Create () (Expr.Var rowConverterVar, htmlDoc, list.Name)
            Expr.Let(rowConverterVar, rowConverter, body)

        let listType = ProvidedTypeDefinition(getListTypeName list.Name, Some listTypeWithErasedType, hideObjectMethods = true, nonNullable = true)
        create, listType

    let private createDefinitionListType getDefinitionListTypeName (inferenceParameters, missingValuesStr, cultureStr) (definitionList:HtmlDefinitionList) =

        let getListTypeName = typeNameGenerator()

        let createListType index (list:HtmlList) =
            
            let columns = HtmlInference.inferListType inferenceParameters list.Values

            let listItemType, conv =
                match columns with
                | StructuralTypes.InferedType.Primitive(typ,_, optional) -> 
                    let typ, _, conv, _convBack = ConversionsGenerator.convertStringValue missingValuesStr cultureStr (StructuralTypes.PrimitiveInferedProperty.Create("", typ, optional, None))
                    typ, conv
                | _ -> 
                    let typ, _, conv, _convBack = ConversionsGenerator.convertStringValue missingValuesStr cultureStr (StructuralTypes.PrimitiveInferedProperty.Create("", typeof<String>, false, None))
                    typ, conv
                        
            let listTypeWithErasedType = typedefof<HtmlList<_>>.MakeGenericType(listItemType)

            let rowConverter =
                
                let rowVar = Var("row", typeof<string>)
                let rowVarExpr = Expr.Var rowVar
                let body = conv <@ TextConversions.AsString(%%rowVarExpr:string) @>
            
                let delegateType = 
                  typedefof<Func<_,_>>.MakeGenericType(typeof<string>, listItemType)
            
                Expr.NewDelegate(delegateType, [rowVar], body)
            
            let create doc =
                let rowConverterVar = Var("rowConverter", rowConverter.Type)
                let body = listTypeWithErasedType?CreateNested () (Expr.Var rowConverterVar, doc, definitionList.Name, index)
                Expr.Let(rowConverterVar, rowConverter, body) 
               

            let listType = ProvidedTypeDefinition(getListTypeName list.Name, Some listTypeWithErasedType, hideObjectMethods = true, nonNullable = true)
            let prop = ProvidedProperty(getPropertyName list.Name, listType, getterCode = fun (Singleton doc) -> create doc)

            prop, listType
            
        let definitionListType = ProvidedTypeDefinition(getDefinitionListTypeName definitionList.Name, Some typeof<HtmlDocument>, hideObjectMethods = true, nonNullable = true)
        
        for prop, listType in List.mapi createListType definitionList.Definitions do
            definitionListType.AddMember listType
            definitionListType.AddMember prop

        definitionListType

    let generateTypes asm ns typeName parameters htmlObjects =

        let htmlType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<HtmlDocument>, hideObjectMethods = true, nonNullable = true)
        
        let containerTypes = Dictionary<string, ProvidedTypeDefinition>()

        let getTypeName = typeNameGenerator()

        let getOrCreateContainer name = 
            match containerTypes.TryGetValue(name) with
            | true, t -> t
            | false, _ ->
                let containerType = ProvidedTypeDefinition(name + "Container", Some typeof<HtmlDocument>, hideObjectMethods = true, nonNullable = true)
                htmlType.AddMember <| ProvidedProperty(name, containerType, getterCode = fun (Singleton doc) -> doc)
                htmlType.AddMember containerType
                containerTypes.Add(name, containerType)
                containerType

        for htmlObj in htmlObjects do
            match htmlObj with
            | Table table ->
                 let containerType = getOrCreateContainer "Tables"
                 let create, tableType = createTableType getTypeName parameters table
                 htmlType.AddMember tableType
                 containerType.AddMember <| ProvidedProperty(getPropertyName table.Name, tableType, getterCode = fun (Singleton doc) -> create doc)
                
            | List list ->
                let containerType = getOrCreateContainer "Lists"
                let create, tableType = createListType getTypeName parameters list
                htmlType.AddMember tableType
                containerType.AddMember <| ProvidedProperty(getPropertyName list.Name, tableType, getterCode = fun (Singleton doc) -> create doc)
            | DefinitionList definitionList ->
                let containerType = getOrCreateContainer "DefinitionLists"
                let tableType = createDefinitionListType getTypeName parameters definitionList
                htmlType.AddMember tableType
                containerType.AddMember <| ProvidedProperty(getPropertyName definitionList.Name, tableType, getterCode = fun (Singleton doc) -> doc)

        htmlType
