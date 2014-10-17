// --------------------------------------------------------------------------------------
// HTML type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder
open FSharp.Data.Runtime

module internal HtmlGenerator =

    type private FieldInfo = 
      { TypeForTuple : Type
        Property : ProvidedProperty
        Convert: Expr -> Expr }
    
    let private createTableType replacer uniqueNiceName preferOptionals missingValuesStr cultureStr (htmlTable : HtmlTable) = 
        let columns = 
            HtmlInference.inferColumns 
                preferOptionals 
                (TextRuntime.GetMissingValues missingValuesStr) 
                (TextRuntime.GetCulture cultureStr) htmlTable.Headers htmlTable.Rows

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
        
        let tableErasedWithRowErasedType = (replacer.ToRuntime typedefof<HtmlObject<_>>).MakeGenericType(rowErasedType)
        let tableErasedTypeWithGeneratedRow = (replacer.ToRuntime typedefof<HtmlObject<_>>).MakeGenericType(rowType)
        
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
        
        let tableNiceName = uniqueNiceName htmlTable.Name
        
        let tableType = ProvidedTypeDefinition(tableNiceName, Some tableErasedTypeWithGeneratedRow, HideObjectMethods = true)
        tableType.AddMember rowType
        tableNiceName, create, tableType

    let generateTypes asm ns typeName preferOptionals (missingValuesStr, cultureStr) (replacer:AssemblyReplacer) (objects:HtmlObject list) =

        let htmlType = ProvidedTypeDefinition(asm, ns, typeName, Some (replacer.ToRuntime typeof<TypedHtmlDocument>), HideObjectMethods = true)
        
        let createContainer name = 
             let containerType = ProvidedTypeDefinition(name + "Container", Some (replacer.ToRuntime typeof<TypedHtmlDocument>), HideObjectMethods = true)
             htmlType.AddMember <| ProvidedProperty(name, containerType, GetterCode = fun (Singleton doc) -> doc)
             htmlType.AddMember containerType
             containerType

        let containerTypes = 
            Map [ "Tables", (createContainer "Tables")
                  "Lists",  (createContainer "Lists") ]

        for htmlObj in objects do
            match htmlObj with
            | Table(table) -> 
                 let uniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName
                 let (tableNiceName, create, tableType) = createTableType replacer uniqueNiceName preferOptionals missingValuesStr cultureStr table
                 htmlType.AddMember tableType
                 containerTypes.["Tables"].AddMember <| ProvidedProperty(tableNiceName, tableType, GetterCode = fun (Singleton doc) -> create doc)
            | List(l) ->   ()
            | DefinitionList(dl) -> ()           

        htmlType
