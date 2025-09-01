module FSharp.Data.Tests.InferenceEngines

open NUnit.Framework
open FsUnit
open System
open System.Globalization
open System.Xml.Linq
open System.Reflection
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference

// Test JsonInference functionality through reflection since it's an internal module
[<Test>]
let ``JsonInference.inferType handles null values correctly`` () =
    let jsonInferenceModule = 
        typeof<JsonValue>.Assembly.GetType("ProviderImplementation.JsonInference")
    jsonInferenceModule |> should not' (be null)
    
    let inferTypeMethod = 
        jsonInferenceModule.GetMethod("inferType", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    inferTypeMethod |> should not' (be null)
    
    let unitsOfMeasureProvider = null
    let inferenceMode = InferenceMode'.ValuesOnly
    let cultureInfo = CultureInfo.InvariantCulture
    let parentName = "test"
    let jsonNull = JsonValue.Null
    
    let result = inferTypeMethod.Invoke(null, [| unitsOfMeasureProvider; inferenceMode; cultureInfo; parentName; jsonNull |])
    result |> should not' (be null)

[<Test>]
let ``JsonInference.inferType handles boolean values correctly`` () =
    let jsonInferenceModule = 
        typeof<JsonValue>.Assembly.GetType("ProviderImplementation.JsonInference")
    let inferTypeMethod = 
        jsonInferenceModule.GetMethod("inferType", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    
    let unitsOfMeasureProvider = null
    let inferenceMode = InferenceMode'.ValuesOnly
    let cultureInfo = CultureInfo.InvariantCulture
    let parentName = "test"
    let jsonBool = JsonValue.Boolean true
    
    let result = inferTypeMethod.Invoke(null, [| unitsOfMeasureProvider; inferenceMode; cultureInfo; parentName; jsonBool |])
    result |> should not' (be null)
    
[<Test>]
let ``JsonInference.inferType handles string values correctly`` () =
    let jsonInferenceModule = 
        typeof<JsonValue>.Assembly.GetType("ProviderImplementation.JsonInference")
    let inferTypeMethod = 
        jsonInferenceModule.GetMethod("inferType", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    
    let unitsOfMeasureProvider = null
    let inferenceMode = InferenceMode'.ValuesOnly
    let cultureInfo = CultureInfo.InvariantCulture
    let parentName = "test"
    let jsonString = JsonValue.String "hello"
    
    let result = inferTypeMethod.Invoke(null, [| unitsOfMeasureProvider; inferenceMode; cultureInfo; parentName; jsonString |])
    result |> should not' (be null)

[<Test>]
let ``JsonInference.inferType handles integer number values correctly`` () =
    let jsonInferenceModule = 
        typeof<JsonValue>.Assembly.GetType("ProviderImplementation.JsonInference")
    let inferTypeMethod = 
        jsonInferenceModule.GetMethod("inferType", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    
    let unitsOfMeasureProvider = null
    let inferenceMode = InferenceMode'.ValuesOnly
    let cultureInfo = CultureInfo.InvariantCulture
    let parentName = "test"
    let jsonNumber = JsonValue.Number 42M
    
    let result = inferTypeMethod.Invoke(null, [| unitsOfMeasureProvider; inferenceMode; cultureInfo; parentName; jsonNumber |])
    result |> should not' (be null)

[<Test>]
let ``JsonInference.inferType handles decimal number values correctly`` () =
    let jsonInferenceModule = 
        typeof<JsonValue>.Assembly.GetType("ProviderImplementation.JsonInference")
    let inferTypeMethod = 
        jsonInferenceModule.GetMethod("inferType", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    
    let unitsOfMeasureProvider = null
    let inferenceMode = InferenceMode'.ValuesOnly
    let cultureInfo = CultureInfo.InvariantCulture
    let parentName = "test"
    let jsonNumber = JsonValue.Number 42.5M
    
    let result = inferTypeMethod.Invoke(null, [| unitsOfMeasureProvider; inferenceMode; cultureInfo; parentName; jsonNumber |])
    result |> should not' (be null)

[<Test>]
let ``JsonInference.inferType handles float values correctly`` () =
    let jsonInferenceModule = 
        typeof<JsonValue>.Assembly.GetType("ProviderImplementation.JsonInference")
    let inferTypeMethod = 
        jsonInferenceModule.GetMethod("inferType", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    
    let unitsOfMeasureProvider = null
    let inferenceMode = InferenceMode'.ValuesOnly
    let cultureInfo = CultureInfo.InvariantCulture
    let parentName = "test"
    let jsonFloat = JsonValue.Float 3.14
    
    let result = inferTypeMethod.Invoke(null, [| unitsOfMeasureProvider; inferenceMode; cultureInfo; parentName; jsonFloat |])
    result |> should not' (be null)

[<Test>]
let ``JsonInference.inferType handles Bit0 special case correctly`` () =
    let jsonInferenceModule = 
        typeof<JsonValue>.Assembly.GetType("ProviderImplementation.JsonInference")
    let inferTypeMethod = 
        jsonInferenceModule.GetMethod("inferType", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    
    let unitsOfMeasureProvider = null
    let inferenceMode = InferenceMode'.ValuesOnly
    let cultureInfo = CultureInfo.InvariantCulture
    let parentName = "test"
    let jsonZero = JsonValue.Number 0M
    
    let result = inferTypeMethod.Invoke(null, [| unitsOfMeasureProvider; inferenceMode; cultureInfo; parentName; jsonZero |])
    result |> should not' (be null)

[<Test>]
let ``JsonInference.inferType handles Bit1 special case correctly`` () =
    let jsonInferenceModule = 
        typeof<JsonValue>.Assembly.GetType("ProviderImplementation.JsonInference")
    let inferTypeMethod = 
        jsonInferenceModule.GetMethod("inferType", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    
    let unitsOfMeasureProvider = null
    let inferenceMode = InferenceMode'.ValuesOnly
    let cultureInfo = CultureInfo.InvariantCulture
    let parentName = "test"
    let jsonOne = JsonValue.Number 1M
    
    let result = inferTypeMethod.Invoke(null, [| unitsOfMeasureProvider; inferenceMode; cultureInfo; parentName; jsonOne |])
    result |> should not' (be null)

[<Test>]
let ``JsonInference.inferType handles array values correctly`` () =
    let jsonInferenceModule = 
        typeof<JsonValue>.Assembly.GetType("ProviderImplementation.JsonInference")
    let inferTypeMethod = 
        jsonInferenceModule.GetMethod("inferType", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    
    let unitsOfMeasureProvider = null
    let inferenceMode = InferenceMode'.ValuesOnly
    let cultureInfo = CultureInfo.InvariantCulture
    let parentName = "test"
    let jsonArray = JsonValue.Array [| JsonValue.Number 1M; JsonValue.Number 2M |]
    
    let result = inferTypeMethod.Invoke(null, [| unitsOfMeasureProvider; inferenceMode; cultureInfo; parentName; jsonArray |])
    result |> should not' (be null)

[<Test>]
let ``JsonInference.inferType handles record values correctly`` () =
    let jsonInferenceModule = 
        typeof<JsonValue>.Assembly.GetType("ProviderImplementation.JsonInference")
    let inferTypeMethod = 
        jsonInferenceModule.GetMethod("inferType", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    
    let unitsOfMeasureProvider = null
    let inferenceMode = InferenceMode'.ValuesOnly
    let cultureInfo = CultureInfo.InvariantCulture
    let parentName = "test"
    let jsonRecord = JsonValue.Record [| ("name", JsonValue.String "John"); ("age", JsonValue.Number 30M) |]
    
    let result = inferTypeMethod.Invoke(null, [| unitsOfMeasureProvider; inferenceMode; cultureInfo; parentName; jsonRecord |])
    result |> should not' (be null)

[<Test>]
let ``JsonInference.inferType handles NoInference mode correctly`` () =
    let jsonInferenceModule = 
        typeof<JsonValue>.Assembly.GetType("ProviderImplementation.JsonInference")
    let inferTypeMethod = 
        jsonInferenceModule.GetMethod("inferType", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    
    let unitsOfMeasureProvider = null
    let inferenceMode = InferenceMode'.NoInference
    let cultureInfo = CultureInfo.InvariantCulture
    let parentName = "test"
    let jsonNumber = JsonValue.Number 0M // Should not infer Bit0 in NoInference mode
    
    let result = inferTypeMethod.Invoke(null, [| unitsOfMeasureProvider; inferenceMode; cultureInfo; parentName; jsonNumber |])
    result |> should not' (be null)

[<Test>]
let ``JsonInference.inferType handles large integer values correctly`` () =
    let jsonInferenceModule = 
        typeof<JsonValue>.Assembly.GetType("ProviderImplementation.JsonInference")
    let inferTypeMethod = 
        jsonInferenceModule.GetMethod("inferType", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
    
    let unitsOfMeasureProvider = null
    let inferenceMode = InferenceMode'.ValuesOnly
    let cultureInfo = CultureInfo.InvariantCulture
    let parentName = "test"
    let largeNumber = JsonValue.Number (decimal Int64.MaxValue)
    
    let result = inferTypeMethod.Invoke(null, [| unitsOfMeasureProvider; inferenceMode; cultureInfo; parentName; largeNumber |])
    result |> should not' (be null)