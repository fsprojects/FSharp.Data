// Copyright 2011-2015, Tomas Petricek (http://tomasp.net), Gustavo Guerra (http://functionalflow.co.uk), and other contributors
// Licensed under the Apache License, Version 2.0, see LICENSE.md in this project
//
// Conversions from string to various primitive types

module ProviderImplementation.ConversionsGenerator

open System
open FSharp.Quotations
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder

let getConversionQuotation missingValuesStr cultureStr typ (value: Expr<string option>) =
    if typ = typeof<string> then
        <@@ TextRuntime.ConvertString(%value) @@>
    elif typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then
        <@@ TextRuntime.ConvertInteger(cultureStr, %value) @@>
    elif typ = typeof<int64> then
        <@@ TextRuntime.ConvertInteger64(cultureStr, %value) @@>
    elif typ = typeof<decimal> then
        <@@ TextRuntime.ConvertDecimal(cultureStr, %value) @@>
    elif typ = typeof<float> then
        <@@ TextRuntime.ConvertFloat(cultureStr, missingValuesStr, %value) @@>
    elif typ = typeof<bool> || typ = typeof<Bit> then
        <@@ TextRuntime.ConvertBoolean(%value) @@>
    elif typ = typeof<DateTime> then
        <@@ TextRuntime.ConvertDateTime(cultureStr, %value) @@>
    elif typ = typeof<DateTimeOffset> then
        <@@ TextRuntime.ConvertDateTimeOffset(cultureStr, %value) @@>
    elif typ = typeof<TimeSpan> then
        <@@ TextRuntime.ConvertTimeSpan(cultureStr, %value) @@>
#if NET6_0_OR_GREATER
    elif typ = typeof<DateOnly> then
        <@@ TextRuntime.ConvertDateOnly(cultureStr, %value) @@>
    elif typ = typeof<TimeOnly> then
        <@@ TextRuntime.ConvertTimeOnly(cultureStr, %value) @@>
#endif
    elif typ = typeof<Guid> then
        <@@ TextRuntime.ConvertGuid(%value) @@>
    else
        failwith "getConversionQuotation: Unsupported primitive type"

let getBackConversionQuotation missingValuesStr cultureStr typ value : Expr<string> =
    if typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then
        <@ TextRuntime.ConvertIntegerBack(cultureStr, %%value) @>
    elif typ = typeof<int64> then
        <@ TextRuntime.ConvertInteger64Back(cultureStr, %%value) @>
    elif typ = typeof<decimal> then
        <@ TextRuntime.ConvertDecimalBack(cultureStr, %%value) @>
    elif typ = typeof<float> then
        <@ TextRuntime.ConvertFloatBack(cultureStr, missingValuesStr, %%value) @>
    elif typ = typeof<string> then
        <@ TextRuntime.ConvertStringBack(%%value) @>
    elif typ = typeof<bool> || typ = typeof<Bit> then
        <@ TextRuntime.ConvertBooleanBack(%%value, false) @>
    elif typ = typeof<Guid> then
        <@ TextRuntime.ConvertGuidBack(%%value) @>
    elif typ = typeof<DateTime> then
        <@ TextRuntime.ConvertDateTimeBack(cultureStr, %%value) @>
    elif typ = typeof<DateTimeOffset> then
        <@ TextRuntime.ConvertDateTimeOffsetBack(cultureStr, %%value) @>
    elif typ = typeof<TimeSpan> then
        <@ TextRuntime.ConvertTimeSpanBack(cultureStr, %%value) @>
#if NET6_0_OR_GREATER
    elif typ = typeof<DateOnly> then
        <@ TextRuntime.ConvertDateOnlyBack(cultureStr, %%value) @>
    elif typ = typeof<TimeOnly> then
        <@ TextRuntime.ConvertTimeOnlyBack(cultureStr, %%value) @>
#endif
    else
        failwith "getBackConversionQuotation: Unsupported primitive type"

/// Creates a function that takes Expr<string option> and converts it to
/// an expression of other type - the type is specified by `field`
let internal convertStringValue missingValuesStr cultureStr exceptionIfMissing (field: PrimitiveInferedProperty) =
    let fieldName = field.Name
    let field = field.Value

    let returnType =
        match field.TypeWrapper with
        | TypeWrapper.None -> field.TypeWithMeasure
        | TypeWrapper.Option -> typedefof<option<_>>.MakeGenericType field.TypeWithMeasure
        | TypeWrapper.Nullable -> typedefof<Nullable<_>>.MakeGenericType field.TypeWithMeasure

    let returnTypeWithoutMeasure =
        match field.TypeWrapper with
        | TypeWrapper.None -> field.RuntimeType
        | TypeWrapper.Option -> typedefof<option<_>>.MakeGenericType field.RuntimeType
        | TypeWrapper.Nullable -> typedefof<Nullable<_>>.MakeGenericType field.RuntimeType

    let convert (value: Expr<string option>) =
        let convert value =
            getConversionQuotation missingValuesStr cultureStr field.InferedType value

        let getNonOptionalValueMethod =
            if exceptionIfMissing then
                nameof (TextRuntime.GetNonOptionalValueStrict)
            else
                nameof (TextRuntime.GetNonOptionalValue)

        match field.TypeWrapper with
        | TypeWrapper.None ->
            //prevent value being calculated twice
            let var = Var("value", typeof<string option>)
            let varExpr = Expr.Cast<string option>(Expr.Var var)

            let body =
                typeof<TextRuntime>?(getNonOptionalValueMethod) field.RuntimeType (fieldName, convert varExpr, varExpr)

            Expr.Let(var, value, body)
        | TypeWrapper.Option -> convert value
        | TypeWrapper.Nullable ->
            typeof<TextRuntime>?(nameof (TextRuntime.OptionToNullable)) field.RuntimeType (convert value)

    let convertBack value =
        let value =
            match field.TypeWrapper with
            | TypeWrapper.None -> ProviderHelpers.some field.RuntimeType value
            | TypeWrapper.Option -> value
            | TypeWrapper.Nullable ->
                typeof<TextRuntime>?(nameof (TextRuntime.NullableToOption)) field.RuntimeType value

        getBackConversionQuotation missingValuesStr cultureStr field.InferedType value :> Expr

    returnType, returnTypeWithoutMeasure, convert, convertBack
