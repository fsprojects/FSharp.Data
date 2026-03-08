// --------------------------------------------------------------------------------------
// CSV type provider - generate code for accessing inferred elements
// --------------------------------------------------------------------------------------
namespace ProviderImplementation

open System
open System.Reflection
open FSharp.Quotations
open FSharp.Reflection
open FSharp.Data
open FSharp.Data.Runtime
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder

module internal CsvTypeBuilder =

    type private FieldInfo =
        {
            /// The representation type that is part of the tuple we extract the field from
            TypeForTuple: Type
            /// The provided property corresponding to the field
            ProvidedProperty: ProvidedProperty
            Convert: Expr -> Expr
            ConvertBack: Expr -> Expr
            /// The provided parameter corresponding to the field
            ProvidedParameter: ProvidedParameter
        }

    let generateTypes asm ns typeName (missingValuesStr, cultureStr) useOriginalNames inferredFields =

        let fields =
            inferredFields
            |> List.mapi (fun index field ->
                let typ, typWithoutMeasure, conv, convBack =
                    ConversionsGenerator.convertStringValue missingValuesStr cultureStr false field

                let propertyName =
                    if useOriginalNames then
                        field.Name
                    else
                        NameUtils.capitalizeFirstLetter field.Name

                let prop =
                    ProvidedProperty(
                        propertyName,
                        typ,
                        getterCode =
                            fun (Singleton row) ->
                                match inferredFields with
                                | [ _ ] -> row
                                | _ -> Expr.TupleGet(row, index)
                    )

                let convert rowVarExpr =
                    conv <@ TextConversions.AsString((%%rowVarExpr: string[]).[index]) @>

                let convertBack rowVarExpr =
                    convBack (
                        match inferredFields with
                        | [ _ ] -> rowVarExpr
                        | _ -> Expr.TupleGet(rowVarExpr, index)
                    )

                let paramName =
                    if useOriginalNames then
                        field.Name
                    else
                        NameUtils.niceCamelName propertyName

                { TypeForTuple = typWithoutMeasure
                  ProvidedProperty = prop
                  Convert = convert
                  ConvertBack = convertBack
                  ProvidedParameter = ProvidedParameter(paramName, typ) })

        // The erased row type will be a tuple of all the field types (without the units of measure).  If there is a single column then it is just the column type.
        let rowErasedType =
            match fields with
            | [ field ] -> field.TypeForTuple
            | _ -> FSharpType.MakeTupleType([| for field in fields -> field.TypeForTuple |])

        let rowType =
            ProvidedTypeDefinition("Row", Some rowErasedType, hideObjectMethods = true, nonNullable = true)

        let ctor =
            let parameters = [ for field in fields -> field.ProvidedParameter ]

            let invoke args =
                match args with
                | [ arg ] -> arg
                | _ -> Expr.NewTuple(args)

            ProvidedConstructor(parameters, invokeCode = invoke)

        rowType.AddMember ctor

        // Each property of the generated row type will simply be a tuple get
        for field in fields do
            rowType.AddMember field.ProvidedProperty

        // Add With* methods so users can create a modified copy of a row
        // e.g. myRow.WithAmount(42.0) returns a new Row identical to myRow except Amount = 42.0
        for targetIdx, targetField in List.indexed fields do
            let methodName = "With" + targetField.ProvidedProperty.Name

            let withMethod =
                ProvidedMethod(
                    methodName,
                    [ ProvidedParameter(targetField.ProvidedParameter.Name, targetField.ProvidedProperty.PropertyType) ],
                    rowType,
                    invokeCode =
                        fun args ->
                            let row = args.[0]
                            let newVal = args.[1]

                            match fields with
                            | [ _ ] ->
                                // Single-column CSV: Row erases to the value itself
                                newVal
                            | _ ->
                                let tupleArgs =
                                    fields
                                    |> List.mapi (fun i _ -> if i = targetIdx then newVal else Expr.TupleGet(row, i))

                                Expr.NewTuple tupleArgs
                )

            rowType.AddMember withMethod

        // The erased csv type will be parameterised by the tuple type
        let csvErasedTypeWithRowErasedType =
            typedefof<CsvFile<_>>.MakeGenericType(rowErasedType)

        let csvErasedTypeWithGeneratedRowType =
            typedefof<CsvFile<_>>.MakeGenericType(rowType)

        let csvType =
            ProvidedTypeDefinition(
                asm,
                ns,
                typeName,
                Some csvErasedTypeWithGeneratedRowType,
                hideObjectMethods = true,
                nonNullable = true
            )

        csvType.AddMember rowType

        // Based on the set of fields, create a function that converts a string[] to the tuple type
        let stringArrayToRow =
            let parentVar = Var("parent", typeof<obj>)
            let rowVar = Var("row", typeof<string[]>)
            let rowVarExpr = Expr.Var rowVar

            // Convert each element of the row using the appropriate conversion
            let body =
                match [ for field in fields -> field.Convert rowVarExpr ] with
                | [ col ] -> col
                | cols -> Expr.NewTuple cols

            let delegateType =
                typedefof<Func<_, _, _>>.MakeGenericType(typeof<obj>, typeof<string[]>, rowErasedType)

            Expr.NewDelegate(delegateType, [ parentVar; rowVar ], body)

        // Create a function that converts the tuple type to a string[]
        let rowToStringArray =
            let rowVar = Var("row", rowErasedType)
            let rowVarExpr = Expr.Var rowVar

            let body =
                Expr.NewArray(typeof<string>, [ for field in fields -> field.ConvertBack rowVarExpr ])

            let delegateType =
                typedefof<Func<_, _>>.MakeGenericType(rowErasedType, typeof<string[]>)

            Expr.NewDelegate(delegateType, [ rowVar ], body)

        csvType, csvErasedTypeWithRowErasedType, rowType, stringArrayToRow, rowToStringArray
