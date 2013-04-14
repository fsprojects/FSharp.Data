// --------------------------------------------------------------------------------------
// CSV type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection
open FSharp.Data.RuntimeImplementation
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder

module internal CsvTypeBuilder =

  let generateTypes asm ns typeName (missingValues, culture) replacer inferredFields =
    
    let fields =
      inferredFields
      |> List.map (fun field -> 
        let typ, typWithoutMeasure, conv, convBack = Conversions.convertValue replacer (missingValues, culture) field
        field.Name, typ, typWithoutMeasure, conv, convBack)

    // The erased row type will be a tuple of all the field types (without the units of measure)
    let rowErasedType = 
      FSharpType.MakeTupleType([| for _, _, typWithoutMeasure, _, _ in fields -> typWithoutMeasure |])
      |> replacer.ToRuntime
    
    let rowType = ProvidedTypeDefinition("Row", Some rowErasedType, HideObjectMethods = true)
    
    // Allow to access the underlying representation
    rowType.AddMember <| ProvidedProperty("AsTuple", rowErasedType, GetterCode = fun (Singleton row) -> row)

    // Each property of the generated row type will simply be a tuple get
    fields 
    |> List.mapi (fun index (name, typ, _, _, _) -> 
      ProvidedProperty(name, typ, GetterCode = fun (Singleton row) -> Expr.TupleGet(row, index)))
    |> rowType.AddMembers 

    // The erased csv type will be parameterised by the tuple type
    let csvErasedTypeWithRowErasedType = 
      (replacer.ToRuntime typedefof<CsvFile<_>>).MakeGenericType(rowErasedType) 

    let csvErasedTypeWithGeneratedRowType = 
      (replacer.ToRuntime typedefof<CsvFile<_>>).MakeGenericType(rowType) 

    let csvType = ProvidedTypeDefinition(asm, ns, typeName, Some csvErasedTypeWithGeneratedRowType, HideObjectMethods = true)    
    csvType.AddMember rowType
    
    // Based on the set of fields, create a function that converts a string[] to the tuple type
    let stringArrayToRow = 

      let parentVar = Var("parent", typeof<obj>)
            
      let rowVar = Var("row", typeof<string[]>)
      let rowVarExpr = Expr.Var rowVar

      // Convert each element of the row using the appropriate conversion
      let convertedItems = fields |> List.mapi (fun index (_, _, _, conv, _) -> 
        conv <@@ Operations.AsOption((%%rowVarExpr:string[]).[index]) @@>)
        
      let body = 
        Expr.NewTuple convertedItems
        |> replacer.ToRuntime

      let delegateType = 
        typedefof<Func<_,_,_>>.MakeGenericType(typeof<obj>, typeof<string[]>, rowErasedType)

      Expr.NewDelegate(delegateType, [parentVar; rowVar], body)

    // Create a function that converts the tuple type to a string[]
    let rowToStringArray =
            
      let rowVar = Var("row", rowErasedType)
      let rowVarExpr = Expr.Var rowVar

      // Convert each element back to string
      let convertedItems = fields |> List.mapi (fun index (_, _, _, _, convBack) -> 
        convBack (Expr.TupleGet(rowVarExpr, index)))

      let body = 
        Expr.NewArray(typeof<string>, convertedItems)

      let delegateType = 
        typedefof<Func<_,_>>.MakeGenericType(rowErasedType, typeof<string[]>)

      Expr.NewDelegate(delegateType, [rowVar], body)

    csvType, csvErasedTypeWithRowErasedType, stringArrayToRow, rowToStringArray
