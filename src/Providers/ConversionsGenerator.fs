// ----------------------------------------------------------------------------------------------
// Conversions from string to various primitive types
// ----------------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open Microsoft.FSharp.Quotations
open FSharp.Data.RuntimeImplementation.StructuralTypes
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference
open FSharp.Data.Json

[<RequireQualifiedAccess>]
type TypeWrapper = None | Option | Nullable

/// Represents type information about primitive property (used mainly in the CSV provider)
/// This type captures the type, unit of measure and handling of missing values (if we
/// infer that the value may be missing, we can generate option<T> or nullable<T>)
type PrimitiveInferedProperty =
  { Name : string
    InferedType : Type
    RuntimeType : Type
    TypeWithMeasure : Type
    TypeWrapper : TypeWrapper }
  static member Create(name, typ, ?typWrapper, ?unit) =
    let runtimeTyp = 
      if typ = typeof<Bit> then typeof<bool>
      elif typ = typeof<Bit0> || typ = typeof<Bit1> then typeof<int>
      else typ
    let typWithMeasure =
      match unit with
      | None -> runtimeTyp
      | Some unit -> 
          if supportsUnitsOfMeasure runtimeTyp
          then ProvidedMeasureBuilder.Default.AnnotateType(runtimeTyp, [unit])
          else failwithf "Units of measure not supported by type %s" runtimeTyp.Name
    { Name = name
      InferedType = typ
      RuntimeType = runtimeTyp
      TypeWithMeasure = typWithMeasure
      TypeWrapper = defaultArg typWrapper TypeWrapper.None }
  static member Create(name, typ, optional) =
    PrimitiveInferedProperty.Create(name, typ, (if optional then TypeWrapper.Option else TypeWrapper.None), ?unit=None)

module ConversionsGenerator = 

  open Microsoft.FSharp.Quotations
  open FSharp.Data.RuntimeImplementation
  open QuotationBuilder

  let getConversionQuotation missingValues culture typ (value:Expr<string option>) =
    if typ = typeof<string> then <@@ CommonRuntime.ConvertString(%value) @@>
    elif typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then <@@ CommonRuntime.ConvertInteger(culture, %value) @@>
    elif typ = typeof<int64> then <@@ CommonRuntime.ConvertInteger64(culture, %value) @@>
    elif typ = typeof<decimal> then <@@ CommonRuntime.ConvertDecimal(culture, %value) @@>
    elif typ = typeof<float> then <@@ CommonRuntime.ConvertFloat(culture, missingValues, %value) @@>
    elif typ = typeof<bool> || typ = typeof<Bit> then <@@ CommonRuntime.ConvertBoolean(culture, %value) @@>
    elif typ = typeof<DateTime> then <@@ CommonRuntime.ConvertDateTime(culture, %value) @@>
    elif typ = typeof<Guid> then  <@@ CommonRuntime.ConvertGuid(%value) @@>
    else failwith "getConversionQuotation: Unsupported primitive type"

  let getConversionQuotationJson missingValues culture typ (value:Expr<JsonValue option>) =
    if typ = typeof<string> then <@@ CommonRuntime.JsonConvertString(culture, %value) @@>
    elif typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then <@@ CommonRuntime.JsonConvertInteger(culture, %value) @@>
    elif typ = typeof<int64> then <@@ CommonRuntime.JsonConvertInteger64(culture, %value) @@>
    elif typ = typeof<decimal> then <@@ CommonRuntime.JsonConvertDecimal(culture, %value) @@>
    elif typ = typeof<float> then <@@ CommonRuntime.JsonConvertFloat(culture, missingValues, %value) @@>
    elif typ = typeof<bool> || typ = typeof<Bit> then <@@ CommonRuntime.JsonConvertBoolean(culture, %value) @@>
    elif typ = typeof<DateTime> then <@@ CommonRuntime.JsonConvertDateTime(culture, %value) @@>
    elif typ = typeof<Guid> then  <@@ CommonRuntime.JsonConvertGuid(%value) @@>
    else failwith "getConversionQuotationJson: Unsupported primitive type"

  let getBackConversionQuotation missingValues culture typ value : Expr<string> =
    if typ = typeof<int> || typ = typeof<Bit0> || typ = typeof<Bit1> then <@ CommonRuntime.ConvertIntegerBack(culture, %%value) @>
    elif typ = typeof<int64> then <@ CommonRuntime.ConvertInteger64Back(culture, %%value) @>
    elif typ = typeof<decimal> then <@ CommonRuntime.ConvertDecimalBack(culture, %%value) @>
    elif typ = typeof<float> then <@ CommonRuntime.ConvertFloatBack(culture, missingValues, %%value) @>
    elif typ = typeof<string> then <@ CommonRuntime.ConvertStringBack(%%value) @>
    elif typ = typeof<bool> then <@ CommonRuntime.ConvertBooleanBack(culture, %%value, false) @>
    elif typ = typeof<Bit> then <@ CommonRuntime.ConvertBooleanBack(culture, %%value, true) @>
    elif typ = typeof<Guid> then <@ CommonRuntime.ConvertGuidBack(%%value) @>
    elif typ = typeof<DateTime> then <@ CommonRuntime.ConvertDateTimeBack(culture, %%value) @>
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
          let body = typeof<CommonRuntime>?GetNonOptionalValue (field.RuntimeType) (field.Name, convert varExpr, varExpr)
          Expr.Let(var, value, body)
      | TypeWrapper.Option -> convert value
      | TypeWrapper.Nullable -> typeof<CommonRuntime>?OptionToNullable (field.RuntimeType) (convert value)
      |> replacer.ToRuntime

    let convertBack value = 
      let value = 
        match field.TypeWrapper with
        | TypeWrapper.None -> typeof<CommonRuntime>?GetOptionalValue (field.RuntimeType) value
        | TypeWrapper.Option -> value
        | TypeWrapper.Nullable -> typeof<CommonRuntime>?NullableToOption (field.RuntimeType) value
        |> replacer.ToDesignTime
      getBackConversionQuotation missingValues culture field.InferedType value |> replacer.ToRuntime

    returnTyp, returnTypWithoutMeasure, convert, convertBack

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
        getConversionQuotationJson missingValues culture field.InferedType value
      match field.TypeWrapper with
      | TypeWrapper.None ->
          //prevent value being calculated twice
          let var = Var("value", typeof<JsonValue option>)
          let varExpr = Expr.Cast<JsonValue option> (Expr.Var var)
          let body = typeof<CommonRuntime>?JsonGetNonOptionalValue (field.RuntimeType) (field.Name, convert varExpr, varExpr)
          Expr.Let(var, value, body)
      | TypeWrapper.Option -> convert value
      | TypeWrapper.Nullable -> typeof<CommonRuntime>?OptionToNullable (field.RuntimeType) (convert value)
      |> replacer.ToRuntime

    returnTyp, convert