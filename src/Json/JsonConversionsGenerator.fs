// ----------------------------------------------------------------------------------------------
// Conversions from string to various primitive types
// ----------------------------------------------------------------------------------------------

module ProviderImplementation.JsonConversionsGenerator

open System
open Microsoft.FSharp.Quotations
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.BaseTypes
open FSharp.Data.Runtime.StructuralTypes
open ProviderImplementation
open ProviderImplementation.QuotationBuilder

#nowarn "10001"

let getConversionQuotation missingValuesStr cultureStr typ (value:Expr<JsonValue option>) =
  if typ = typeof<string> then <@@ JsonRuntime.ConvertString(cultureStr, %value) @@>
  elif typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then <@@ JsonRuntime.ConvertInteger(cultureStr, %value) @@>
  elif typ = typeof<int64> then <@@ JsonRuntime.ConvertInteger64(cultureStr, %value) @@>
  elif typ = typeof<decimal> then <@@ JsonRuntime.ConvertDecimal(cultureStr, %value) @@>
  elif typ = typeof<float> then <@@ JsonRuntime.ConvertFloat(cultureStr, missingValuesStr, %value) @@>
  elif typ = typeof<bool> || typ = typeof<Bit> then <@@ JsonRuntime.ConvertBoolean(%value) @@>
  elif typ = typeof<DateTime> then <@@ JsonRuntime.ConvertDateTime(cultureStr, %value) @@>
  elif typ = typeof<Guid> then  <@@ JsonRuntime.ConvertGuid(%value) @@>
  else failwith "getConversionQuotation: Unsupported primitive type"

type JsonConversionCallingType = 
    JsonDocument | JsonValueOption | JsonValueOptionAndPath

/// Creates a function that takes Expr<JsonValue option> and converts it to 
/// an expression of other type - the type is specified by `field`
let convertJsonValue (replacer:AssemblyReplacer) missingValuesStr cultureStr canPassAllConversionCallingTypes (field:PrimitiveInferedProperty) = 

  assert (field.TypeWithMeasure = field.RuntimeType)
  assert (field.Name = "")

  let returnType = 
    match field.TypeWrapper with
    | TypeWrapper.None -> field.RuntimeType
    | TypeWrapper.Option -> typedefof<option<_>>.MakeGenericType field.RuntimeType
    | TypeWrapper.Nullable -> typedefof<Nullable<_>>.MakeGenericType field.RuntimeType
    |> replacer.ToRuntime

  let wrapInLetIfNeeded (value:Expr) getBody =
    match value with
    | Patterns.Var var ->
        let varExpr = Expr.Cast<'T> (Expr.Var var)
        getBody varExpr
    | _ ->
        let var = Var("value", typeof<'T>)
        let varExpr = Expr.Cast<'T> (Expr.Var var)
        Expr.Let(var, value, getBody varExpr)

  let convert (value:Expr) =
    let convert value = 
      getConversionQuotation missingValuesStr cultureStr field.InferedType value
    match field.TypeWrapper, canPassAllConversionCallingTypes with
    | TypeWrapper.None, true ->
        wrapInLetIfNeeded value <| fun (varExpr:Expr<JsonValueOptionAndPath>) ->
          typeof<JsonRuntime>?GetNonOptionalValue (field.RuntimeType) (<@ (%varExpr).Path @>, convert <@ (%varExpr).JsonOpt @>, <@ (%varExpr).JsonOpt @>)
    | TypeWrapper.None, false ->
        wrapInLetIfNeeded value <| fun (varExpr:Expr<IJsonDocument>) ->
          typeof<JsonRuntime>?GetNonOptionalValue (field.RuntimeType) (<@ (%varExpr).Path() @>, convert <@ Some (%varExpr).JsonValue @>, <@ Some (%varExpr).JsonValue @>)
    | TypeWrapper.Option, true ->
        convert <@ (%%value:JsonValue option) @>
    | TypeWrapper.Option, false ->
        //TODO: not covered in tests
        convert <@ Some (%%value:IJsonDocument).JsonValue @>
    | TypeWrapper.Nullable, true -> 
        //TODO: not covered in tests
        typeof<TextRuntime>?OptionToNullable (field.RuntimeType) (convert <@ (%%value:JsonValue option) @>)
    | TypeWrapper.Nullable, false -> 
        //TODO: not covered in tests
        typeof<TextRuntime>?OptionToNullable (field.RuntimeType) (convert <@ Some (%%value:IJsonDocument).JsonValue @>)
    |> replacer.ToRuntime

  let conversionCallingType =
    if canPassAllConversionCallingTypes then
        match field.TypeWrapper with
        | TypeWrapper.None -> JsonValueOptionAndPath
        | TypeWrapper.Option | TypeWrapper.Nullable -> JsonValueOption
    else JsonDocument

  returnType, convert, conversionCallingType
