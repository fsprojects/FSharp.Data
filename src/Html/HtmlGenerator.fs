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

module internal HtmlGenerator =

    type private FieldInfo = 
      { TypeForTuple : Type
        Property : ProvidedProperty
        Convert: Expr -> Expr }
    
    let private createTableType replacer preferOptionals missingValuesStr cultureStr (htmlTable : HtmlTable) = 
        let columns = 
            HtmlInference.inferColumns 
                preferOptionals 
                (TextRuntime.GetMissingValues missingValuesStr) 
                (TextRuntime.GetCulture cultureStr) htmlTable.HeaderNamesAndUnits htmlTable.Rows

        let uniqueNiceName = NameUtils.uniqueGenerator <| fun s ->
                HtmlParser.invalidTypeNameRegex.Value.Replace(s, " ")
                |> NameUtils.nicePascalName

        let fields = columns |> List.mapi (fun index field ->
            let typ, typWithoutMeasure, conv, _convBack = ConversionsGenerator.convertStringValue replacer missingValuesStr cultureStr field
            { TypeForTuple = typWithoutMeasure
              Property = ProvidedProperty(field.Name, typ, GetterCode = fun (Singleton row) -> Expr.TupleGet(row, index))
              Convert = fun rowVarExpr -> conv <@ TextConversions.AsString((%%rowVarExpr:string[]).[index]) @> } )
        
        // The erased row type will be a tuple of all the field types (without the units of measure)
        let rowErasedType = 
            FSharpType.MakeTupleType([| for field in fields -> field.TypeForTuple |])
            |> replacer.ToRuntime
        
        let rowType = ProvidedTypeDefinition("Row", Some rowErasedType, HideObjectMethods = true)
        
        // Each property of the generated row type will simply be a tuple get
        for field in fields do
            rowType.AddMember field.Property
        
        let tableErasedWithRowErasedType = (replacer.ToRuntime typedefof<HtmlTable<_>>).MakeGenericType(rowErasedType)
        let tableErasedTypeWithGeneratedRow = (replacer.ToRuntime typedefof<HtmlTable<_>>).MakeGenericType(rowType)
        
        let rowConverter =
            let rowVar = Var("row", typeof<string[]>)
            let rowVarExpr = Expr.Var rowVar
            let body = 
              Expr.NewTuple [ for field in fields -> field.Convert rowVarExpr ]
              |> replacer.ToRuntime
        
            let delegateType = 
              typedefof<Func<_,_>>.MakeGenericType(typeof<string[]>, rowErasedType)
        
            Expr.NewDelegate(delegateType, [rowVar], body)
        
        let create (htmlDoc:Expr) =
            let rowConverterVar = Var("rowConverter", rowConverter.Type)
            let body = tableErasedWithRowErasedType?Create () (Expr.Var rowConverterVar, htmlDoc, htmlTable.Name)
            Expr.Let(rowConverterVar, rowConverter, body)
        
        let tableName = uniqueNiceName htmlTable.Name
        let propertyName = NameUtils.capitalizeFirstLetter tableName

        let tableType = ProvidedTypeDefinition(tableName, Some tableErasedTypeWithGeneratedRow, HideObjectMethods = true)
        tableType.AddMember rowType
        propertyName, create, tableType

    let private createListType (replacer:AssemblyReplacer) preferOptionals missingValuesStr cultureStr (htmlList : HtmlList) =
        if htmlList.Values.Length > 0 
        then
            let columns = 
                HtmlInference.inferListType 
                    preferOptionals 
                    (TextRuntime.GetMissingValues missingValuesStr) 
                    (TextRuntime.GetCulture cultureStr) htmlList.Values

            let uniqueNiceName = NameUtils.uniqueGenerator <| fun s ->
                    HtmlParser.invalidTypeNameRegex.Value.Replace(s, " ")
                    |> NameUtils.nicePascalName

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
                let body = listTypeWithErasedType?Create () (Expr.Var rowConverterVar, htmlDoc, htmlList.Name)
                Expr.Let(rowConverterVar, rowConverter, body)

            let listNiceName = uniqueNiceName htmlList.Name
            
            let listType = ProvidedTypeDefinition(listNiceName, Some listTypeWithErasedType, HideObjectMethods = true)
            Some (listNiceName, create, listType)
        else None

    let private createDefinitionListType (replacer:AssemblyReplacer) preferOptionals missingValuesStr cultureStr (htmlList : HtmlDefinitionList) =

        let uniqueNiceName = NameUtils.uniqueGenerator <| fun s ->
                HtmlParser.invalidTypeNameRegex.Value.Replace(s, " ")
                |> NameUtils.nicePascalName

        let createListType (replacer:AssemblyReplacer) preferOptionals missingValuesStr cultureStr index (l : HtmlList) =
            if l.Values.Length > 0
            then 
                let columns = 
                    HtmlInference.inferListType 
                        preferOptionals 
                        (TextRuntime.GetMissingValues missingValuesStr) 
                        (TextRuntime.GetCulture cultureStr) l.Values

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
                    let body = listTypeWithErasedType?CreateNested () (Expr.Var rowConverterVar, htmlDoc, htmlList.Name, index)
                    Expr.Let(rowConverterVar, rowConverter, body) 
                           
                let listNiceName = uniqueNiceName l.Name
                
                let listType = ProvidedTypeDefinition(listNiceName, Some listTypeWithErasedType, HideObjectMethods = true)
                Some (listNiceName, create, listType)
            else None

        let listNiceName = uniqueNiceName htmlList.Name

        let definitionListType = ProvidedTypeDefinition(listNiceName, Some (replacer.ToRuntime typeof<TypedHtmlDocument>), HideObjectMethods = true)
        
        let lists = 
            htmlList.Definitions
            |> List.mapi(createListType replacer preferOptionals missingValuesStr cultureStr)
            |> List.choose id

        for (listName, create, listType) in lists do
            definitionListType.AddMember(listType)
            let prop = ProvidedProperty(listName, listType, GetterCode = fun (Singleton doc) -> create doc)
            definitionListType.AddMember(prop)

        listNiceName, definitionListType

    let generateTypes asm ns typeName preferOptionals (missingValuesStr, cultureStr) (replacer:AssemblyReplacer) (objects:HtmlObject list) =

        let htmlType = ProvidedTypeDefinition(asm, ns, typeName, Some (replacer.ToRuntime typeof<TypedHtmlDocument>), HideObjectMethods = true)
        
        let containerTypes = new Dictionary<string, ProvidedTypeDefinition>()

        let getOrCreateContainer name = 
             match containerTypes.TryGetValue(name) with
             | true, t -> t
             | false, _ ->
                let containerType = ProvidedTypeDefinition(name + "Container", Some (replacer.ToRuntime typeof<TypedHtmlDocument>), HideObjectMethods = true)
                htmlType.AddMember <| ProvidedProperty(name, containerType, GetterCode = fun (Singleton doc) -> doc)
                htmlType.AddMember containerType
                containerTypes.Add(name, containerType)
                containerType

        for htmlObj in objects do
            match htmlObj with
            | Table(table) ->
                 let containerType = getOrCreateContainer "Tables"
                 let (tableNiceName, create, tableType) = createTableType replacer preferOptionals missingValuesStr cultureStr table
                 htmlType.AddMember tableType
                 containerType.AddMember <| ProvidedProperty(tableNiceName, tableType, GetterCode = fun (Singleton doc) -> create doc)
            | List(l) ->
                let containerType = getOrCreateContainer "Lists"
                match createListType replacer preferOptionals missingValuesStr cultureStr l with
                | Some(listName, create, tableType) ->
                    htmlType.AddMember tableType
                    containerType.AddMember <| ProvidedProperty(listName, tableType, GetterCode = fun (Singleton doc) -> create doc)
                | None -> ()
            | DefinitionList(dl) ->
                let containerType = getOrCreateContainer "DefinitionLists"       
                let (listName, tableType) = createDefinitionListType replacer preferOptionals missingValuesStr cultureStr dl
                htmlType.AddMember tableType
                containerType.AddMember <| ProvidedProperty(listName, tableType, GetterCode = fun (Singleton doc) -> doc)
        htmlType
