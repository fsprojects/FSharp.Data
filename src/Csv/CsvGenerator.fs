// --------------------------------------------------------------------------------------
// CSV type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection
open FSharp.Data
open FSharp.Data.Runtime
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder

module internal CsvTypeBuilder =

  type private FieldInfo = 
    { TypeForTuple : Type
      Property : ProvidedProperty
      Convert: Expr -> Expr
      ConvertBack: Expr -> Expr
      Param : ProvidedParameter }

  let generateTypes asm ns typeName (missingValuesStr, cultureStr) replacer inferredFields =
    
    let fields = inferredFields |> List.mapi (fun index field ->
      let typ, typWithoutMeasure, conv, convBack = ConversionsGenerator.convertStringValue replacer missingValuesStr cultureStr field
      let propertyName = NameUtils.capitalizeFirstLetter field.Name
      { TypeForTuple = typWithoutMeasure
        Property = ProvidedProperty(propertyName, typ, GetterCode = fun (Singleton row) -> Expr.TupleGet(row, index))
        Convert = fun rowVarExpr -> conv <@ TextConversions.AsString((%%rowVarExpr:string[]).[index]) @>
        ConvertBack = fun rowVarExpr -> convBack (Expr.TupleGet(rowVarExpr, index))
        Param = ProvidedParameter(NameUtils.niceCamelName propertyName, typ) } )

    // The erased row type will be a tuple of all the field types (without the units of measure)
    let rowErasedType = 
      FSharpType.MakeTupleType([| for field in fields -> field.TypeForTuple |])
      |> replacer.ToRuntime
    
    let rowType = ProvidedTypeDefinition("Row", Some rowErasedType, HideObjectMethods = true, NonNullable = true)

    let ctor = ProvidedConstructor([ for field in fields -> field.Param ], InvokeCode = fun args -> Expr.NewTuple(args))
    rowType.AddMember ctor
    
    // Each property of the generated row type will simply be a tuple get
    for field in fields do
      rowType.AddMember field.Property

    // The erased csv type will be parameterised by the tuple type
    let csvErasedTypeWithRowErasedType = 
      (replacer.ToRuntime typedefof<CsvFile<_>>).MakeGenericType(rowErasedType) 
    let csvErasedTypeWithGeneratedRowType = 
      (replacer.ToRuntime typedefof<CsvFile<_>>).MakeGenericType(rowType) 

    let csvType = ProvidedTypeDefinition(asm, ns, typeName, Some csvErasedTypeWithGeneratedRowType, HideObjectMethods = true, NonNullable = true)
    csvType.AddMember rowType
    
    // Based on the set of fields, create a function that converts a string[] to the tuple type
    let stringArrayToRow = 
      let parentVar = Var("parent", typeof<obj>)
      let rowVar = Var("row", typeof<string[]>)
      let rowVarExpr = Expr.Var rowVar

      // Convert each element of the row using the appropriate conversion
      let body = 
        Expr.NewTuple [ for field in fields -> field.Convert rowVarExpr ]

      let delegateType = 
        typedefof<Func<_,_,_>>.MakeGenericType(typeof<obj>, typeof<string[]>, rowErasedType)

      Expr.NewDelegate(delegateType, [parentVar; rowVar], body)

    // Create a function that converts the tuple type to a string[]
    let rowToStringArray =
      let rowVar = Var("row", rowErasedType)
      let rowVarExpr = Expr.Var rowVar
      let body = 
        Expr.NewArray(typeof<string>, [ for field in fields -> field.ConvertBack rowVarExpr ])
      let delegateType = 
        typedefof<Func<_,_>>.MakeGenericType(rowErasedType, typeof<string[]>)

      Expr.NewDelegate(delegateType, [rowVar], body)

    csvType, csvErasedTypeWithRowErasedType, rowType, stringArrayToRow, rowToStringArray
