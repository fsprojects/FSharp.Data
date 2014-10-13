// --------------------------------------------------------------------------------------
// HTML type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
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

    let generateTypes asm ns typeName (missingValuesStr, cultureStr) (replacer:AssemblyReplacer) columnsPerTable =
        
        let htmlType = ProvidedTypeDefinition(asm, ns, typeName, Some (replacer.ToRuntime typeof<TypedHtmlDocument>), HideObjectMethods = true)
    
        if not (List.isEmpty columnsPerTable) then

            let tableContainer = ProvidedTypeDefinition("TableContainer", Some (replacer.ToRuntime typeof<TypedHtmlDocument>), HideObjectMethods = true)
            htmlType.AddMember <| ProvidedProperty("Tables", tableContainer, GetterCode = fun (Singleton doc) -> doc)
            htmlType.AddMember tableContainer
            
            let uniqueNiceName = NameUtils.uniqueGenerator <| fun s ->
                HtmlParser.invalidTypeNameRegex.Value.Replace(s, " ")
                |> NameUtils.nicePascalName

            for (tableName : string), columns in columnsPerTable do
            
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
                    let body = tableErasedWithRowErasedType?Create () (Expr.Var rowConverterVar, htmlDoc, tableName)
                    Expr.Let(rowConverterVar, rowConverter, body)
                
                let propertyName = NameUtils.capitalizeFirstLetter tableName
                let typeName = uniqueNiceName tableName

                let tableType = ProvidedTypeDefinition(typeName, Some tableErasedTypeWithGeneratedRow, HideObjectMethods = true)
                tableType.AddMember rowType
                htmlType.AddMember tableType
                tableContainer.AddMember <| ProvidedProperty(propertyName, tableType, GetterCode = fun (Singleton doc) -> create doc)              

        htmlType
