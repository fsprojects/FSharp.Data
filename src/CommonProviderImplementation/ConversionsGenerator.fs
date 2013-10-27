// ----------------------------------------------------------------------------------------------
// Conversions from string to various primitive types
// ----------------------------------------------------------------------------------------------

module ProviderImplementation.ConversionsGenerator

open System
open Microsoft.FSharp.Quotations
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.QuotationBuilder

let getConversionQuotation missingValues culture typ (value:Expr<string option>) =
  if typ = typeof<string> then <@@ TextRuntime.ConvertString(%value) @@>
  elif typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then <@@ TextRuntime.ConvertInteger(culture, %value) @@>
  elif typ = typeof<int64> then <@@ TextRuntime.ConvertInteger64(culture, %value) @@>
  elif typ = typeof<decimal> then <@@ TextRuntime.ConvertDecimal(culture, %value) @@>
  elif typ = typeof<float> then <@@ TextRuntime.ConvertFloat(culture, missingValues, %value) @@>
  elif typ = typeof<bool> || typ = typeof<Bit> then <@@ TextRuntime.ConvertBoolean(culture, %value) @@>
  elif typ = typeof<DateTime> then <@@ TextRuntime.ConvertDateTime(culture, %value) @@>
  elif typ = typeof<Guid> then  <@@ TextRuntime.ConvertGuid(%value) @@>
  else failwith "getConversionQuotation: Unsupported primitive type"

let getBackConversionQuotation missingValues culture typ value : Expr<string> =
  if typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then <@ TextRuntime.ConvertIntegerBack(culture, %%value) @>
  elif typ = typeof<int64> then <@ TextRuntime.ConvertInteger64Back(culture, %%value) @>
  elif typ = typeof<decimal> then <@ TextRuntime.ConvertDecimalBack(culture, %%value) @>
  elif typ = typeof<float> then <@ TextRuntime.ConvertFloatBack(culture, missingValues, %%value) @>
  elif typ = typeof<string> then <@ TextRuntime.ConvertStringBack(%%value) @>
  elif typ = typeof<bool> then <@ TextRuntime.ConvertBooleanBack(culture, %%value, false) @>
  elif typ = typeof<Bit> then <@ TextRuntime.ConvertBooleanBack(culture, %%value, true) @>
  elif typ = typeof<Guid> then <@ TextRuntime.ConvertGuidBack(%%value) @>
  elif typ = typeof<DateTime> then <@ TextRuntime.ConvertDateTimeBack(culture, %%value) @>
  else failwith "getBackConversionQuotation: Unsupported primitive type"

/// Creates a function that takes Expr<string option> and converts it to 
/// an expression of other type - the type is specified by `field`
let convertStringValue (replacer:AssemblyReplacer) missingValues culture (field:PrimitiveInferedProperty) = 

  let returnTyp = 
    match field.TypeWrapper with
    | TypeWrapper.None -> field.TypeWithMeasure
    | TypeWrapper.Option -> typedefof<option<_>>.MakeGenericType field.TypeWithMeasure
    | TypeWrapper.Nullable -> typedefof<Nullable<_>>.MakeGenericType field.TypeWithMeasure

  let returnTypWithoutMeasure = 
    match field.TypeWrapper with
    | TypeWrapper.None -> field.RuntimeType
    | TypeWrapper.Option -> typedefof<option<_>>.MakeGenericType field.RuntimeType
    | TypeWrapper.Nullable -> typedefof<Nullable<_>>.MakeGenericType field.RuntimeType

  let convert (value:Expr<string option>) =
    let convert value = 
      getConversionQuotation missingValues culture field.InferedType value
    match field.TypeWrapper with
    | TypeWrapper.None ->
        //prevent value being calculated twice
        let var = Var("value", typeof<string option>)
        let varExpr = Expr.Cast<string option> (Expr.Var var)
        let body = typeof<TextRuntime>?GetNonOptionalValue (field.RuntimeType) (field.Name, convert varExpr, varExpr)
        Expr.Let(var, value, body)
    | TypeWrapper.Option -> convert value
    | TypeWrapper.Nullable -> typeof<TextRuntime>?OptionToNullable (field.RuntimeType) (convert value)
    |> replacer.ToRuntime

  let convertBack value = 
    let value = 
      match field.TypeWrapper with
      | TypeWrapper.None -> typeof<TextRuntime>?GetOptionalValue (field.RuntimeType) value
      | TypeWrapper.Option -> value
      | TypeWrapper.Nullable -> typeof<TextRuntime>?NullableToOption (field.RuntimeType) value
      |> replacer.ToDesignTime
    getBackConversionQuotation missingValues culture field.InferedType value |> replacer.ToRuntime

  returnTyp, returnTypWithoutMeasure, convert, convertBack
