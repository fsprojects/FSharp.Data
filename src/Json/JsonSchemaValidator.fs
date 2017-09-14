module FSharp.Data.JsonSchemaValidator

open System.Text.RegularExpressions

type JsonSchema = {
    MultipleOf : int
    Maximum : int
    ExclusiveMaximum : int
    Minimum : int
    MaxLength : int
    MinLength : int
    Pattern : Regex
    Items : JsonSchema option
    AdditionalItems : JsonSchema option
    MaxItems : uint32
    MinItems : uint32
    UniqueItems : bool
    Contains : JsonSchema option
    MaxProperties : uint32
    MinProperties : uint32
    Required : string []
    Properties : (string * JsonSchema) []
    PatternProperties : (Regex * JsonSchema) []
    AdditionalProperties : JsonSchema
    Dependencies : JsonValue
    Enum : JsonValue []
    Const : JsonValue
    Type : JsonValue []
    AllOf : JsonSchema []
    AnyOf : JsonSchema []
    OneOf : JsonSchema []
    Not : JsonSchema
}

let validateSchema (value:JsonValue) =
        

        let validationKeywordsWithTypes =
            [|  "multipleOf", typeof<int>
                "maximum", typeof<int>
                "exclusiveMaximum", typeof<int>
                "minimum", typeof<int>
                "exclusiveMinimum", typeof<int>
                "maxLength", typeof<uint32>
                "minLength", typeof<uint32>
                "pattern", typeof<string>
                "items", typeof<string> // should be - jsonschema
                "additionalItems", typeof<string> // should be - jsonschema
                "maxItems", typeof<uint32>
                "minItems", typeof<uint32>
                "uniqueItems", typeof<bool>
                "contains", typeof<string> // should be - jsonschema
                "maxProperties", typeof<uint32>
                "minProperties", typeof<uint32>
                "required", typeof<string[]>
                "properties", typeof<string[]> // should be - jsonschema array
                "patternProperties", typeof<obj> // should be - ECMA 262 Regex Pattern
                "additionalProperties", typeof<obj> // should be - jsonschema
                "dependencies", typeof<obj[][]> // should be - jsonschema dependency array
                "propertyNames", typeof<string> // should be - jsonschema
                "enum", typeof<obj[]> // -
                "const", typeof<obj> 
                "type", typeof<string[]>
                "allOf", typeof<string[]> // should be - jsonschema array
                "anyOf", typeof<string[]> // should be - jsonschema array
                "oneOf", typeof<string[]> // should be - jsonschema array
                "not", typeof<string> // should be - jsonschema
                |] |> Map
        
        let validate value =
            match value with
            | JsonValue.Record properties ->
                for property in properties do
                    let name, value = property
                    if validationKeywordsWithTypes.ContainsKey name then
                        let requiredType = validationKeywordsWithTypes.[name]

                        ()
                    else
                        failwith "Unallowed name detected"
                    ()
            | _ -> ()
        
        validate value
        
        Some value

