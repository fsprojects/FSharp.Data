module FSharp.Data.JsonSchemaValidator

open System.Text.RegularExpressions

// --------------------------------------------------------
// Json Schema Validation
// The validation is based on the official validation guide
// For more information see this page : 
//  http://json-schema.org/latest/json-schema-validation.html
// ---------------------------------------------------------

module Option = 

    let defaultValue defaultValue opt = defaultArg opt defaultValue

type JsonSchema = {
    MultipleOf : int
    Maximum : int
    //ExclusiveMaximum : int
    //Minimum : int
    //MaxLength : int
    //MinLength : int
    //Pattern : Regex
    //Items : JsonSchema option
    //AdditionalItems : JsonSchema option
    //MaxItems : uint32
    //MinItems : uint32
    //UniqueItems : bool
    //Contains : JsonSchema option
    //MaxProperties : uint32
    //MinProperties : uint32
    //Required : string []
    Properties : JsonSchema []
    //PatternProperties : (Regex * JsonSchema) []
    //AdditionalProperties : JsonSchema
    //Dependencies : JsonValue
    Enum : JsonValue []
    // Const : JsonValue
    Type : JsonValue [] // Meant raw here, don't interpret the values! Only the type please.
    AllOf : JsonSchema []
    AnyOf : JsonSchema []
    //OneOf : JsonSchema []
    //Not : JsonSchema
    Title : string
}

let validateMultipleOf value = 
    match value with 
    | JsonValue.Number v -> 
        v |> int |> Some
    | _ -> None 

let validateMaximum value =
    match value with 
    | JsonValue.Number v -> 
        v |> int |> Some
    | _ -> None 

let validateExclusiveMaximum value =
    match value with 
    | JsonValue.Number v -> 
        v |> int |> Some
    | _ -> None 

let validateMinimum value =
    match value with 
    | JsonValue.Number v -> 
        v |> int |> Some
    | _ -> None 

let validateExclusiveMinimum value =
    match value with 
    | JsonValue.Number v -> 
        v |> int |> Some
    | _ -> None 

let validateMaxLength value =
    match value with 
    | JsonValue.Number v -> 
        v |> int |> Some
    | _ -> None 

let validateMinLength value =
    match value with 
    | JsonValue.Number v -> 
        v |> int |> Some
    | _ -> None 

let validatePattern value : Regex option =
    match value with
    | JsonValue.String pattern ->
        try
            Regex pattern |> Some
        with _ -> None
    | _ -> None

let rec validateSchema (value:JsonValue) =
    let throw () = failwith "invalid value"
        
    //let validationKeywordsWithTypes =
    //    [|  "multipleOf", validateMultipleOf
    //        "maximum", validateMaximum
    //        "exclusiveMaximum", validateExclusiveMaximum
    //        "minimum", validateMinimum
    //        "exclusiveMinimum", validateExclusiveMinimum
    //        "maxLength", validateMaxLength
    //        "minLength", validateMinLength
    //        "pattern", validatePattern
    //        "items", validateItems // should be - jsonschema
    //        "additionalItems", validateAdditionalItems // should be - jsonschema
    //        "maxItems", validateMaxItems
    //        "minItems", validateMinItems
    //        "uniqueItems", validateUniqueItems
    //        "contains", validateContains // should be - jsonschema
    //        "maxProperties", validateMaxProperties
    //        "minProperties", validateMinProperties
    //        "required", validateRequired
    //        "properties", validateProperties // should be - jsonschema array
    //        "patternProperties", validatePatternProperties // should be - ECMA 262 Regex Pattern
    //        "additionalProperties", validateAdditionalProperties // should be - jsonschema
    //        "dependencies", validateDependencies // should be - jsonschema dependency array
    //        "propertyNames", validatePropertyNames // should be - jsonschema
    //        "enum", validateEnum // -
    //        "const", validateConst
    //        "type", validateType
    //        "allOf", validateAllOf // should be - jsonschema array
    //        "anyOf", validateAnyOf // should be - jsonschema array
    //        "oneOf", validateOneOf // should be - jsonschema array
    //        "not", validateNot // should be - jsonschema
    //        |] |> Map
        
    //let validate value =
    //    match value with
    //    | JsonValue.Record properties ->
    //        for property in properties do
    //            let name, value = property
    //            if validationKeywordsWithTypes.ContainsKey name then 
    //            else
    //                failwith "Unallowed name detected"
    //            ()
    //    | _ -> ()
    
    validateRoot value

and validateRoot value =
    match value with
    | JsonValue.Record properties ->
        let namedProperties = 
            Map properties
        let tryGet name = if namedProperties.ContainsKey name then namedProperties.[name] |> Some else None
        {
            MultipleOf              = tryGet "multipleOf" |> Option.bind validateMultipleOf |> Option.defaultValue 0
            Maximum                 = tryGet "maximum" |> Option.bind validateMaximum |> Option.defaultValue 0
            // ExclusiveMaximum     = tryGet "exclusiveMaximum" |> Option.map validateExclusiveMaximum |> Option.defaultWith 
            // Minimum              = tryGet "minimum" |> Option.map validateMinimum |> Option.defaultWith 0
            // MaxLength            = tryGet "maxLength" |> Option.map validateMaxLength |> Option.defaultWith 0
            // MinLength            = tryGet "minLength" |> Option.map validateMinLength |> Option.defaultWith 0
            // Pattern              = tryGet "pattern" |> Option.map validateExclusiveMaximum |> Option.defaultWith 0
            Properties              = tryGet "properties" |> Option.bind validateProperties |> Option.defaultValue [||]
            Enum                    = tryGet "enum" |> Option.bind validateEnum |> Option.defaultValue [||]
            // Const                   = tryGet "const" |> Option.bind validateConst |> Option.defaultValue JsonValue.Null
            Type                    = tryGet "type" |> Option.bind validateType |> Option.defaultValue [||]
            AllOf                   = tryGet "allOf" |> Option.bind validateAllOf |> Option.defaultValue [||]
            AnyOf                   = tryGet "anyOf" |> Option.bind validateAnyOf |> Option.defaultValue [||]
            Title                   = tryGet "title" |> Option.bind (function | JsonValue.String name -> Some name | _ -> None) |> Option.defaultValue "INVALID"
        } |> Some
    | _ -> None

and validateItems value =

    None

and validateAdditionalItems value =

    None

and validateMaxItems value =

    None

and validateMinItems value =

    None

and validateUniqueItems value =

    None

and validateContains value =

    None

and validateMaxProperties value =

    None

and validateMinProperties value : decimal option =

    None

and validateRequired value : float option =

    None

and validateProperties value : JsonSchema [] option =
    match value with
    | JsonValue.Record properties ->
        [| for property in properties do
               let name, value = property
               let res = validateRoot value |> Option.get
               yield { res with Title = name } |] |> Some
    | _ -> None

and validatePatternProperties value =

    None

and validateAdditionalProperties value =

    None

and validateDependencies value =

    None

and validatePropertyNames value =

    None

and validateEnum value : JsonValue [] option =
    match value with
    | JsonValue.Array content -> Some content
    | _ -> None

and validateConst value =

    None

and validateType value : JsonValue [] option =
    let getType value =
        match value with
        | "null" -> JsonValue.Null
        | "boolean" -> JsonValue.Boolean false
        | "object" -> JsonValue.Record [||]
        | "array" -> JsonValue.Array [||]
        | "number" -> JsonValue.Number 0m
        | "string" -> JsonValue.String ""
        | _ as v -> failwithf "invalid type value %A specified." v
    match value with
    | JsonValue.String value -> [| getType value |] |> Some
    | JsonValue.Array values -> 
        [| for value in values -> 
            match value with
            | JsonValue.String value -> getType value 
            | _ as value -> failwithf "invalid type value %A specified." value |] |> Some
    | _ -> None

and validateAllOf value : JsonSchema [] option =
    match value with
    | JsonValue.Record properties ->
        [| for property in properties ->
            let name, value = property
            match validateRoot value with
            | Some schema -> { schema with Title = name }
            | _ -> failwith "Schema in AllOf section incorrect." |] |> Some
    | _ -> None

and validateAnyOf value : JsonSchema [] option =
    match value with
    | JsonValue.Record properties ->
        [| for property in properties ->
            let name, value = property
            match validateRoot value with
            | Some schema -> { schema with Title = name }
            | _ -> failwith "Schema in AllOf section incorrect." |] |> Some
    | _ -> None

and validateOneOf value : JsonSchema [] option =
    match value with
    | JsonValue.Record properties ->
        [| for property in properties ->
            let name, value = property
            match validateRoot value with
            | Some schema -> { schema with Title = name }
            | _ -> failwith "Schema in AllOf section incorrect." |] |> Some
    | _ -> None

and validateNot value : JsonSchema option = validateRoot value
