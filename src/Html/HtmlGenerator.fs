// --------------------------------------------------------------------------------------
// HTML type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Collections.Generic
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralTypes

module internal HtmlGenerator =

    type private FieldInfo = 
      { TypeForTuple : Type
        Property : ProvidedProperty
        Convert: Expr -> Expr }

    let private getPropertyName = NameUtils.capitalizeFirstLetter
    
    let private typeNameGenerator() =
        NameUtils.uniqueGenerator <| fun s ->
            HtmlParser.invalidTypeNameRegex.Value.Replace(s, " ")
            |> NameUtils.nicePascalName

    let private createTableType replacer getTableTypeName (inferenceParameters, missingValuesStr, cultureStr) (table:HtmlTable) = 

        let columns =  
            match table.InferedProperties with
            | Some inferedProperties -> inferedProperties
            | None -> 
                HtmlInference.inferColumns inferenceParameters 
                                           table.HeaderNamesAndUnits.Value 
                                           (if table.HasHeaders.Value then table.Rows.[1..] else table.Rows)

        let fields = columns |> List.mapi (fun index field ->
            let typ, typWithoutMeasure, conv, _convBack = ConversionsGenerator.convertStringValue replacer missingValuesStr cultureStr field
            { TypeForTuple = typWithoutMeasure
              Property = ProvidedProperty(field.Name, typ, GetterCode = fun (Singleton row) -> if columns.Length = 1 then row else Expr.TupleGet(row, index))
              Convert = fun rowVarExpr -> conv <@ TextConversions.AsString((%%rowVarExpr:string[]).[index]) @> } )
        
        // The erased row type will be a tuple of all the field types (without the units of measure)
        let rowErasedType =
            if fields.Length = 1
            then fields.Head.TypeForTuple
            else
                FSharpType.MakeTupleType([| for field in fields -> field.TypeForTuple |])
            |> replacer.ToRuntime
        
        let rowType = ProvidedTypeDefinition("Row", Some rowErasedType, HideObjectMethods = true, NonNullable = true)
        
        // Each property of the generated row type will simply be a tuple get
        for field in fields do
            rowType.AddMember field.Property
        
        let tableErasedWithRowErasedType = (replacer.ToRuntime typedefof<HtmlTable<_>>).MakeGenericType(rowErasedType)
        let tableErasedTypeWithGeneratedRow = (replacer.ToRuntime typedefof<HtmlTable<_>>).MakeGenericType(rowType)
        
        let rowConverter =
            let rowVar = Var("row", typeof<string[]>)
            let rowVarExpr = Expr.Var rowVar
            let body =
              if fields.Length = 1
              then fields.Head.Convert rowVarExpr
              else Expr.NewTuple [ for field in fields -> field.Convert rowVarExpr ]
              |> replacer.ToRuntime
        
            let delegateType = 
              typedefof<Func<_,_>>.MakeGenericType(typeof<string[]>, rowErasedType)
        
            Expr.NewDelegate(delegateType, [rowVar], body)
        
        let create (htmlDoc:Expr) =
            let rowConverterVar = Var("rowConverter", rowConverter.Type)
            let body = tableErasedWithRowErasedType?Create () (Expr.Var rowConverterVar, htmlDoc, table.Name, table.HasHeaders.Value)
            Expr.Let(rowConverterVar, rowConverter, body)
        
        let tableType = ProvidedTypeDefinition(getTableTypeName table.Name, Some tableErasedTypeWithGeneratedRow, HideObjectMethods = true, NonNullable = true)
        tableType.AddMember rowType
        
        create, tableType

    let private createListType replacer getListTypeName (inferenceParameters, missingValuesStr, cultureStr) (list:HtmlList) =
        
        let columns = HtmlInference.inferListType inferenceParameters list.Values

        let listItemType, conv =
            match columns with
            | InferedType.Primitive(typ,_, optional) -> 
                let typ, _, conv, _convBack = ConversionsGenerator.convertStringValue replacer missingValuesStr cultureStr (StructuralTypes.PrimitiveInferedProperty.Create("", typ, optional, None))
                typ, conv
            | _ -> 
                let typ, _, conv, _convBack = ConversionsGenerator.convertStringValue replacer missingValuesStr cultureStr (StructuralTypes.PrimitiveInferedProperty.Create("", typeof<string>, false, None))
                typ, conv
                        
        let listTypeWithErasedType = (replacer.ToRuntime typedefof<HtmlList<_>>).MakeGenericType(listItemType)
        
        let rowConverter =
            
            let rowVar = Var("row", typeof<string>)
            let rowVarExpr = Expr.Var rowVar
            let body = 
              conv <@ TextConversions.AsString(%%rowVarExpr:string) @>
              |> replacer.ToRuntime
        
            let delegateType = 
              typedefof<Func<_,_>>.MakeGenericType(typeof<string>, listItemType)
        
            Expr.NewDelegate(delegateType, [rowVar], body)
        
        let create (htmlDoc:Expr) =
            let rowConverterVar = Var("rowConverter", rowConverter.Type)
            let body = listTypeWithErasedType?Create () (Expr.Var rowConverterVar, htmlDoc, list.Name)
            Expr.Let(rowConverterVar, rowConverter, body)

        let listType = ProvidedTypeDefinition(getListTypeName list.Name, Some listTypeWithErasedType, HideObjectMethods = true, NonNullable = true)
        create, listType

    let private createDefinitionListType (replacer:AssemblyReplacer) getDefinitionListTypeName (inferenceParameters, missingValuesStr, cultureStr) (definitionList:HtmlDefinitionList) =

        let getListTypeName = typeNameGenerator()

        let createListType index (list:HtmlList) =
            
            let columns = HtmlInference.inferListType inferenceParameters list.Values

            let listItemType, conv =
                match columns with
                | StructuralTypes.InferedType.Primitive(typ,_, optional) -> 
                    let typ, _, conv, _convBack = ConversionsGenerator.convertStringValue replacer missingValuesStr cultureStr (StructuralTypes.PrimitiveInferedProperty.Create("", typ, optional, None))
                    typ, conv
                | _ -> 
                    let typ, _, conv, _convBack = ConversionsGenerator.convertStringValue replacer missingValuesStr cultureStr (StructuralTypes.PrimitiveInferedProperty.Create("", typeof<String>, false, None))
                    typ, conv
                        
            let listTypeWithErasedType = (replacer.ToRuntime typedefof<HtmlList<_>>).MakeGenericType(listItemType)

            let rowConverter =
                
                let rowVar = Var("row", typeof<string>)
                let rowVarExpr = Expr.Var rowVar
                let body = 
                  conv <@ TextConversions.AsString(%%rowVarExpr:string) @>
                  |> replacer.ToRuntime
            
                let delegateType = 
                  typedefof<Func<_,_>>.MakeGenericType(typeof<string>, listItemType)
            
                Expr.NewDelegate(delegateType, [rowVar], body)
            
            let create (htmlDoc:Expr) =
                let rowConverterVar = Var("rowConverter", rowConverter.Type)
                let body = listTypeWithErasedType?CreateNested () (Expr.Var rowConverterVar, htmlDoc, definitionList.Name, index)
                Expr.Let(rowConverterVar, rowConverter, body) 

            let listType = ProvidedTypeDefinition(getListTypeName list.Name, Some listTypeWithErasedType, HideObjectMethods = true, NonNullable = true)
            let prop = ProvidedProperty(getPropertyName list.Name, listType, GetterCode = fun (Singleton doc) -> create doc)

            prop, listType
            
        let definitionListType = ProvidedTypeDefinition(getDefinitionListTypeName definitionList.Name, Some (replacer.ToRuntime typeof<HtmlDocument>), HideObjectMethods = true, NonNullable = true)
        
        for prop, listType in List.mapi createListType definitionList.Definitions do
            definitionListType.AddMember listType
            definitionListType.AddMember prop

        definitionListType

    let generateTypes asm ns typeName parameters (replacer:AssemblyReplacer) htmlObjects =

        let htmlType = ProvidedTypeDefinition(asm, ns, typeName, Some (replacer.ToRuntime typeof<HtmlDocument>), HideObjectMethods = true, NonNullable = true)
        
        let containerTypes = Dictionary<string, ProvidedTypeDefinition>()

        let getTypeName = typeNameGenerator()

        let getOrCreateContainer name = 
            match containerTypes.TryGetValue(name) with
            | true, t -> t
            | false, _ ->
                let containerType = ProvidedTypeDefinition(name + "Container", Some (replacer.ToRuntime typeof<HtmlDocument>), HideObjectMethods = true, NonNullable = true)
                htmlType.AddMember <| ProvidedProperty(name, containerType, GetterCode = fun (Singleton doc) -> doc)
                htmlType.AddMember containerType
                containerTypes.Add(name, containerType)
                containerType

        for htmlObj in htmlObjects do
            match htmlObj with
            | Table table ->
                 let containerType = getOrCreateContainer "Tables"
                 let create, tableType = createTableType replacer getTypeName parameters table
                 htmlType.AddMember tableType
                 containerType.AddMember <| ProvidedProperty(getPropertyName table.Name, tableType, GetterCode = fun (Singleton doc) -> create doc)
            | List list ->
                let containerType = getOrCreateContainer "Lists"
                let create, tableType = createListType replacer getTypeName parameters list
                htmlType.AddMember tableType
                containerType.AddMember <| ProvidedProperty(getPropertyName list.Name, tableType, GetterCode = fun (Singleton doc) -> create doc)
            | DefinitionList definitionList ->
                let containerType = getOrCreateContainer "DefinitionLists"
                let tableType = createDefinitionListType replacer getTypeName parameters definitionList
                htmlType.AddMember tableType
                containerType.AddMember <| ProvidedProperty(getPropertyName definitionList.Name, tableType, GetterCode = fun (Singleton doc) -> doc)

        htmlType
