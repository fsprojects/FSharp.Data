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
        Conv : (Expr<string option> -> Expr)
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


    let rec private convertProperty(replacer:AssemblyReplacer, preferOptionals, missingValueStr, cultureStr, index, value, field:InferedProperty) = 
        
        let flags = 
            Reflection.BindingFlags.Instance
            ||| Reflection.BindingFlags.Public

        let getColumnMethodInfo = 
            (replacer.ToRuntime typeof<HtmlRow>).GetMethod("GetColumn", flags)
//        
//        let getValueMethodInfo = 
//            (replacer.ToRuntime typeof<HtmlCell>).GetMethod("GetValue", flags)

        let runtimeValueType = 
            (replacer.ToRuntime typeof<HtmlInference.HtmlValue>)
        
        let getTypeAndWrapper typ optional = 
            if optional then
              if preferOptionals then typ, TypeWrapper.Option
              elif typ = typeof<float> then typ, TypeWrapper.None
              elif typ = typeof<decimal> then typeof<float>, TypeWrapper.None
              elif typ = typeof<Bit0> || typ = typeof<Bit1> || typ = typeof<int> || typ = typeof<int64> then typ, TypeWrapper.Nullable
              else typ, TypeWrapper.Option
            else typ, TypeWrapper.None
//
//        let rowAccessorExpr index propIndex row = 
//                <@ 
//                    let call = (%%Expr.Call(Expr.Call(row, getColumnMethodInfo, [Expr.Value(index)]), getValueMethodInfo, [Expr.Value(propIndex)]) : string)
//                    TextConversions.AsString(call)
//                @>
//        
        let cellAccessorExpr (index:int) row = 
            Expr.Call(row, getColumnMethodInfo, [Expr.Value(index)])
//        
//        let valueAccessorExpr propIndex col : Expr<string option> = 
//                <@ 
//                    let call = (%%Expr.Call(col, getValueMethodInfo, [Expr.Value(propIndex)]) : string)
//                    TextConversions.AsString(call)
//                @>
        
        let htmlValueReader, unionTagReader = 
            FSharpType.GetUnionCases(runtimeValueType)
            |> Array.map (fun x -> x.Tag, (x.GetFields() |> Array.map (fun f -> (fun (v:Expr) -> Expr.PropertyGet(v,f)))))
            |> Map.ofArray, FSharpValue.PreComputeUnionTagReader(typeof<HtmlInference.HtmlValue>)

        let createProperty name returnType (converterExpr:Expr<string option> -> Expr) value = 
            match value with
            | HtmlInference.HtmlValue.Primitive _ -> 
                let unionTag = unionTagReader value
                let accessor (row : Expr)  = 
                    <@ 
                        TextConversions.AsString (%%(htmlValueReader.[unionTag].[0] (cellAccessorExpr index row)) : string) 
                    @>
                    |> converterExpr 
                    |> replacer.ToRuntime

                ProvidedProperty(name, returnType, GetterCode = (fun (Singleton row) -> accessor row))
            | _ -> failwithf "Only primitives supported at the moment"
            
//
//        let getTypeName = function 
//            | Some n -> typeNameGenerator() n
//            | None -> typeNameGenerator() ""
//
//        let createPrimitiveProperty name accessor typ unit optional = 
//            let typ, typWrapper = getTypeAndWrapper typ optional    
//            let field = PrimitiveInferedProperty.Create(field.Name, typ, typWrapper, unit)
//            let (typ, typWithoutMeasure, conv, _convBack) = ConversionsGenerator.convertStringValue replacer missingValueStr cultureStr field
//            {
//              Name = (getPropertyName field.Name) 
//              ReturnType = typWithoutMeasure
//              Property = ProvidedProperty(getPropertyName name, typ, GetterCode = fun (Singleton row) -> conv (accessor row) |> replacer.ToRuntime)
//              Conv = conv
//            }
//
//        let rec createRecordProperty name (typeName :string option) properties optional index = 
//            let thisType = ProvidedTypeDefinition(getTypeName typeName, Some (replacer.ToRuntime typeof<HtmlInference.HtmlValue>), HideObjectMethods = true)
//            properties |> List.iteri (fun i prop ->
//                match prop.Type with
//                | InferedType.Primitive(typ, unit, optional) ->
//                     let field = createPrimitiveProperty prop.Name (fun col -> valueAccessorExpr i col) typ unit optional
//                     thisType.AddMember field.Property
//                | InferedType.Record(name, props, optional) ->
//                    let field = createRecordProperty prop.Name name props optional i
//                    thisType.AddMember(field.ReturnType); thisType.AddMember field.Property
//                )
//
//            { 
//              Name = (getPropertyName name)
//              ReturnType = thisType
//              Property = ProvidedProperty(getPropertyName name, thisType, GetterCode = fun (Singleton row) -> row) 
//              Conv = (fun x -> x :> Expr)
//            }

        match field.Type with
        | InferedType.Primitive(typ, unit, optional) ->
            let typ, typWrapper = getTypeAndWrapper typ optional    
            let field = PrimitiveInferedProperty.Create(getPropertyName field.Name, typ, typWrapper, unit)
            let (typ, typWithoutMeasure, conv, _convBack) = ConversionsGenerator.convertStringValue replacer missingValueStr cultureStr field
            let property = createProperty field.Name typ conv value
            {
              Name = field.Name 
              ReturnType = typWithoutMeasure
              Property = property
              Conv = conv
            } |> Primitive
//        | InferedType.Record(name, props, optional) -> 
//            (createRecordProperty field.Name name props optional index) |> Field.Record
        | _ -> failwith "unsupported conversion"

    let private createTableType (replacer:AssemblyReplacer) getTableTypeName (inferenceParameters, missingValuesStr, cultureStr) (table:HtmlTable) = 

        let properties =  
            match table.InferedProperties with
            | Some inferedProperties -> Seq.zip table.Rows.[0] inferedProperties |> Seq.toList
            | None -> 
                HtmlInference.inferColumns inferenceParameters 
                                           table.HeaderNamesAndUnits.Value 
                                           (if table.HasHeaders then table.Rows.[1..] else table.Rows)
        
        let rowType = ProvidedTypeDefinition("Row", Some (replacer.ToRuntime typeof<HtmlRow>), HideObjectMethods = true)

        let fields = properties |> List.mapi (fun index (value, prop) ->
             let field = convertProperty(replacer,inferenceParameters.PreferOptionals,missingValuesStr,cultureStr, index, value, prop)
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
