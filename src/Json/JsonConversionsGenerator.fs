// ----------------------------------------------------------------------------------------------
// Conversions from string to various primitive types
// ----------------------------------------------------------------------------------------------

module ProviderImplementation.JsonConversionsGenerator

open System
open Microsoft.FSharp.Quotations
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open ProviderImplementation.QuotationBuilder

let getConversionQuotation missingValues culture typ (value:Expr<JsonValue option>) =
  if typ = typeof<string> then <@@ JsonRuntime.ConvertString(culture, %value) @@>
  elif typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then <@@ JsonRuntime.ConvertInteger(culture, %value) @@>
  elif typ = typeof<int64> then <@@ JsonRuntime.ConvertInteger64(culture, %value) @@>
  elif typ = typeof<decimal> then <@@ JsonRuntime.ConvertDecimal(culture, %value) @@>
  elif typ = typeof<float> then <@@ JsonRuntime.ConvertFloat(culture, missingValues, %value) @@>
  elif typ = typeof<bool> || typ = typeof<Bit> then <@@ JsonRuntime.ConvertBoolean(culture, %value) @@>
  elif typ = typeof<DateTime> then <@@ JsonRuntime.ConvertDateTime(culture, %value) @@>
  elif typ = typeof<Guid> then  <@@ JsonRuntime.ConvertGuid(%value) @@>
  else failwith "getConversionQuotation: Unsupported primitive type"

/// Creates a function that takes Expr<JsonValue option> and converts it to 
/// an expression of other type - the type is specified by `field`
let convertJsonValue (replacer:AssemblyReplacer) missingValues culture (field:PrimitiveInferedProperty) = 

  assert (field.TypeWithMeasure = field.RuntimeType)

  let returnTyp = 
    match field.TypeWrapper with
    | TypeWrapper.None -> field.RuntimeType
    | TypeWrapper.Option -> typedefof<option<_>>.MakeGenericType field.RuntimeType
    | TypeWrapper.Nullable -> typedefof<Nullable<_>>.MakeGenericType field.RuntimeType

  let convert (value:Expr<JsonValue option>) =
    let convert value = 
      getConversionQuotation missingValues culture field.InferedType value
    match field.TypeWrapper with
    | TypeWrapper.None ->
        //prevent value being calculated twice
        let var = Var("value", typeof<JsonValue option>)
        let varExpr = Expr.Cast<JsonValue option> (Expr.Var var)
        let body = typeof<JsonRuntime>?GetNonOptionalValue (field.RuntimeType) (field.Name, convert varExpr, varExpr)
        Expr.Let(var, value, body)
    | TypeWrapper.Option -> convert value
    | TypeWrapper.Nullable -> typeof<TextRuntime>?OptionToNullable (field.RuntimeType) (convert value)
    |> replacer.ToRuntime

  returnTyp, convert
