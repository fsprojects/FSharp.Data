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

    let private getPropertyName = NameUtils.capitalizeFirstLetter
    
    let private typeNameGenerator() =
        NameUtils.uniqueGenerator <| fun s ->
            HtmlParser.invalidTypeNameRegex.Value.Replace(s, " ")
            |> NameUtils.nicePascalName

    type private FieldInfo = 
      { 
        Name : string
        ReturnType : Type
        Property : ProvidedProperty
      }

    type private Field = 
        | Primitive of FieldInfo
        | Record of FieldInfo
        member x.ReturnType = 
            match x with
            | Primitive f -> f.ReturnType
            | Record (f) -> f.ReturnType
        member x.Property = 
            match x with
            | Primitive f -> f.Property
            | Record (f) -> f.Property
        member x.Name = 
            match x with
            | Primitive f -> f.Name
            | Record (f) -> f.Name


    let rec private convertProperty(replacer:AssemblyReplacer, preferOptionals, missingValueStr, cultureStr, index, propIndex, isSingleton, field:InferedProperty) = 
        let getTypeAndWrapper typ optional = 
            if optional then
              if preferOptionals then typ, TypeWrapper.Option
              elif typ = typeof<float> then typ, TypeWrapper.None
              elif typ = typeof<decimal> then typeof<float>, TypeWrapper.None
              elif typ = typeof<Bit0> || typ = typeof<Bit1> || typ = typeof<int> || typ = typeof<int64> then typ, TypeWrapper.Nullable
              else typ, TypeWrapper.Option
            else typ, TypeWrapper.None

        match field.Type with
        | InferedType.Primitive(typ, unit, optional) ->
            let typ, typWrapper = getTypeAndWrapper typ optional    
            let field = PrimitiveInferedProperty.Create(field.Name, typ, typWrapper, unit)
            let (typ, typWithoutMeasure, conv, _convBack) = ConversionsGenerator.convertStringValue replacer missingValueStr cultureStr field
            {
              Name = field.Name 
              ReturnType = typWithoutMeasure
              Property = ProvidedProperty(field.Name, typ, GetterCode = fun (Singleton row) -> <@@ (%%row : HtmlRow).GetColumn(index).GetValue(propIndex) @@> |> replacer.ToRuntime) 
            }
            |> Primitive
        | InferedType.Record(name, props, optional) -> 
            let thisType = ProvidedTypeDefinition(typeNameGenerator() (if name.IsSome then name.Value else sprintf "NestedRecord_%d_%d" index propIndex), Some (replacer.ToRuntime  typeof<HtmlColumn>), HideObjectMethods = true)

            props |> List.iteri (fun i prop ->
                match convertProperty(replacer, preferOptionals, missingValueStr, cultureStr, index, i, isSingleton, prop) with
                | Primitive field -> thisType.AddMember field.Property
                | Record(field) -> thisType.AddMember(field.ReturnType); thisType.AddMember field.Property
                )

            { 
              Name = field.Name
              ReturnType = thisType
              Property = ProvidedProperty(field.Name, thisType, GetterCode = fun (Singleton row) -> <@@ (%%row : HtmlRow).GetColumn(index) @@> |> replacer.ToRuntime) 
            }
            |> Record
        | _ -> failwith "unsupported conversion"

    let private createTableType (replacer:AssemblyReplacer) getTableTypeName (inferenceParameters, missingValuesStr, cultureStr) (table:HtmlTable) = 

        let columns =  
            match table.InferedProperties with
            | Some inferedProperties -> inferedProperties
            | None -> 
                HtmlInference.inferColumns inferenceParameters 
                                           table.HeaderNamesAndUnits.Value 
                                           (if table.HasHeaders then table.Rows.[1..] else table.Rows)
        
        let rowType = ProvidedTypeDefinition("Row", Some (replacer.ToRuntime typeof<HtmlRow>), HideObjectMethods = true)

        let fields = columns |> List.mapi (fun index field ->
             let field = convertProperty(replacer,inferenceParameters.PreferOptionals,missingValuesStr,cultureStr, index, 0, columns.Length = 1, field)
             match field with
             | Primitive p -> rowType.AddMember(p.Property); p
             | Record r -> rowType.AddMember r.ReturnType; rowType.AddMember r.Property; r
            )
        
        let tableName = table.Name
        let hasHeaders = table.HasHeaders

        let tableErasedWithRowErasedType = (typedefof<seq<_>>).MakeGenericType(rowType)
        
        let create (htmlDoc:Expr) : Expr = 
            let rowType = (replacer.ToRuntime typedefof<HtmlRow>) 
            let body : Expr = rowType?Create () (htmlDoc, tableName, hasHeaders)
            body
        
        let tableType = ProvidedTypeDefinition(getTableTypeName table.Name, Some tableErasedWithRowErasedType, HideObjectMethods = true)
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

        let listType = ProvidedTypeDefinition(getListTypeName list.Name, Some listTypeWithErasedType, HideObjectMethods = true)
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

            let listType = ProvidedTypeDefinition(getListTypeName list.Name, Some listTypeWithErasedType, HideObjectMethods = true)
            let prop = ProvidedProperty(getPropertyName list.Name, listType, GetterCode = fun (Singleton doc) -> create doc)

            prop, listType
            
        let definitionListType = ProvidedTypeDefinition(getDefinitionListTypeName definitionList.Name, Some (replacer.ToRuntime typeof<HtmlDocument>), HideObjectMethods = true)
        
        for prop, listType in List.mapi createListType definitionList.Definitions do
            definitionListType.AddMember listType
            definitionListType.AddMember prop

        definitionListType

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
                 let create, tableType = createTableType replacer getTableTypeName parameters table
                 htmlType.AddMember tableType
                 containerType.AddMember <| ProvidedProperty(getPropertyName table.Name, tableType, GetterCode = fun (Singleton doc) -> create doc)
            | List list ->
                let containerType = getOrCreateContainer "Lists"
                let create, tableType = createListType replacer getListTypeName parameters list
                htmlType.AddMember tableType
                containerType.AddMember <| ProvidedProperty(getPropertyName list.Name, tableType, GetterCode = fun (Singleton doc) -> create doc)
            | DefinitionList definitionList ->
                let containerType = getOrCreateContainer "DefinitionLists"
                let tableType = createDefinitionListType replacer getDefinitionListTypeName parameters definitionList
                htmlType.AddMember tableType
                containerType.AddMember <| ProvidedProperty(getPropertyName definitionList.Name, tableType, GetterCode = fun (Singleton doc) -> doc)

        htmlType
