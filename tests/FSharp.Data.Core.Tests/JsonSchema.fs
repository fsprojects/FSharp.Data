module FSharp.Data.Tests.JsonSchema

open System
open NUnit.Framework
open FsUnit
open FSharp.Data
open FSharp.Data.Runtime

[<TestFixture>]
type JsonSchemaTests() =

    // Test data
    let createTestSchema schemaType =
        sprintf """{ "type": "%s" }""" schemaType
        |> JsonValue.Parse

    let createObjectSchema properties required =
        let reqPart = 
            if List.isEmpty required then ""
            else sprintf """, "required": [%s]""" (required |> List.map (sprintf "\"%s\"") |> String.concat ", ")
        let propJson = 
            properties 
            |> List.map (fun (name, typ) -> sprintf "\"%s\": { \"type\": \"%s\" }" name typ)
            |> String.concat ", "
        sprintf """{ "type": "object", "properties": {%s}%s }""" propJson reqPart
        |> JsonValue.Parse

    [<Test>]
    member _.``Empty schema has expected default values``() =
        let empty = JsonSchema.empty
        empty.Type |> should equal JsonSchema.JsonSchemaType.Any
        empty.Description |> should equal None
        empty.Properties |> should equal None
        empty.Required |> should equal None

    [<Test>]
    member _.``parseSchema parses basic string type``() =
        let schema = createTestSchema "string"
        let parsed = JsonSchema.parseSchema schema
        parsed.Type |> should equal JsonSchema.JsonSchemaType.String

    [<Test>]
    member _.``parseSchema parses basic number type``() =
        let schema = createTestSchema "number"
        let parsed = JsonSchema.parseSchema schema
        parsed.Type |> should equal JsonSchema.JsonSchemaType.Number

    [<Test>]
    member _.``parseSchema parses basic integer type``() =
        let schema = createTestSchema "integer"
        let parsed = JsonSchema.parseSchema schema
        parsed.Type |> should equal JsonSchema.JsonSchemaType.Integer

    [<Test>]
    member _.``parseSchema parses basic boolean type``() =
        let schema = createTestSchema "boolean"
        let parsed = JsonSchema.parseSchema schema
        parsed.Type |> should equal JsonSchema.JsonSchemaType.Boolean

    [<Test>]
    member _.``parseSchema parses basic object type``() =
        let schema = createTestSchema "object"
        let parsed = JsonSchema.parseSchema schema
        parsed.Type |> should equal JsonSchema.JsonSchemaType.Object

    [<Test>]
    member _.``parseSchema parses basic array type``() =
        let schema = createTestSchema "array"
        let parsed = JsonSchema.parseSchema schema
        parsed.Type |> should equal JsonSchema.JsonSchemaType.Array

    [<Test>]
    member _.``parseSchema parses basic null type``() =
        let schema = createTestSchema "null"
        let parsed = JsonSchema.parseSchema schema
        parsed.Type |> should equal JsonSchema.JsonSchemaType.Null

    [<Test>]
    member _.``parseSchema handles unknown type as Any``() =
        let schema = JsonValue.Parse """{ "type": "unknown" }"""
        let parsed = JsonSchema.parseSchema schema
        parsed.Type |> should equal JsonSchema.JsonSchemaType.Any

    [<Test>]
    member _.``parseSchema handles missing type as Any``() =
        let schema = JsonValue.Parse """{ "description": "test" }"""
        let parsed = JsonSchema.parseSchema schema
        parsed.Type |> should equal JsonSchema.JsonSchemaType.Any

    [<Test>]
    member _.``parseSchema parses array type from union types``() =
        let schema = JsonValue.Parse """{ "type": ["null", "string"] }"""
        let parsed = JsonSchema.parseSchema schema
        parsed.Type |> should equal JsonSchema.JsonSchemaType.String

    [<Test>]
    member _.``parseSchema parses description``() =
        let schema = JsonValue.Parse """{ "type": "string", "description": "A test string" }"""
        let parsed = JsonSchema.parseSchema schema
        parsed.Description |> should equal (Some "A test string")

    [<Test>]
    member _.``parseSchema parses enum values``() =
        let schema = JsonValue.Parse """{ "type": "string", "enum": ["red", "green", "blue"] }"""
        let parsed = JsonSchema.parseSchema schema
        parsed.Enum |> should not' (equal None)
        match parsed.Enum with
        | Some values -> values.Length |> should equal 3
        | None -> failwith "Expected enum values"

    [<Test>]
    member _.``parseSchema parses minimum and maximum for numbers``() =
        let schema = JsonValue.Parse """{ "type": "number", "minimum": 0, "maximum": 100 }"""
        let parsed = JsonSchema.parseSchema schema
        parsed.Minimum |> should equal (Some 0m)
        parsed.Maximum |> should equal (Some 100m)

    [<Test>]
    member _.``parseSchema parses minLength and maxLength for strings``() =
        let schema = JsonValue.Parse """{ "type": "string", "minLength": 3, "maxLength": 10 }"""
        let parsed = JsonSchema.parseSchema schema
        parsed.MinLength |> should equal (Some 3)
        parsed.MaxLength |> should equal (Some 10)

    [<Test>]
    member _.``parseSchema parses format for strings``() =
        let schema = JsonValue.Parse """{ "type": "string", "format": "date-time" }"""
        let parsed = JsonSchema.parseSchema schema
        parsed.Format |> should equal (Some "date-time")

    [<Test>]
    member _.``parseSchema parses pattern for strings``() =
        let schema = JsonValue.Parse """{ "type": "string", "pattern": "^[a-z]+$" }"""
        let parsed = JsonSchema.parseSchema schema
        parsed.Pattern |> should equal (Some "^[a-z]+$")

    [<Test>]
    member _.``parseSchema parses object properties``() =
        let schema = createObjectSchema [("name", "string"); ("age", "integer")] []
        let parsed = JsonSchema.parseSchema schema
        parsed.Properties |> should not' (equal None)
        match parsed.Properties with
        | Some props -> 
            Map.containsKey "name" props |> should equal true
            Map.containsKey "age" props |> should equal true
        | None -> failwith "Expected properties"

    [<Test>]
    member _.``parseSchema parses required properties``() =
        let schema = createObjectSchema [("name", "string"); ("age", "integer")] ["name"]
        let parsed = JsonSchema.parseSchema schema
        parsed.Required |> should equal (Some ["name"])

    [<Test>]
    member _.``parseSchema parses array items schema``() =
        let schema = JsonValue.Parse """{ "type": "array", "items": { "type": "string" } }"""
        let parsed = JsonSchema.parseSchema schema
        parsed.Items |> should not' (equal None)
        match parsed.Items with
        | Some itemSchema -> itemSchema.Type |> should equal JsonSchema.JsonSchemaType.String
        | None -> failwith "Expected items schema"

    [<Test>]
    member _.``parseSchema parses references``() =
        let schema = JsonValue.Parse """{ "$ref": "#/definitions/Person" }"""
        let parsed = JsonSchema.parseSchema schema
        parsed.Reference |> should equal (Some "#/definitions/Person")

    [<Test>]
    member _.``formatToType converts date-time format to DateTime``() =
        let result = JsonSchema.formatToType "date-time"
        result |> should equal typeof<DateTime>

    [<Test>]
    member _.``formatToType converts guid format to Guid``() =
        let result = JsonSchema.formatToType "guid"
        result |> should equal typeof<Guid>

    [<Test>]
    member _.``formatToType converts email format to string``() =
        let result = JsonSchema.formatToType "email"
        result |> should equal typeof<string>

    [<Test>]
    member _.``formatToType converts int32 format to int``() =
        let result = JsonSchema.formatToType "int32"
        result |> should equal typeof<int>

    [<Test>]
    member _.``formatToType converts unknown format to string``() =
        let result = JsonSchema.formatToType "unknown"
        result |> should equal typeof<string>

    [<Test>]
    member _.``parseSchemaFromString parses valid JSON schema``() =
        let schemaString = """{ "type": "string", "maxLength": 20 }"""
        let parsed = JsonSchema.parseSchemaFromString schemaString
        parsed.Type |> should equal JsonSchema.JsonSchemaType.String
        parsed.MaxLength |> should equal (Some 20)

    [<Test>]
    member _.``validate accepts valid string value``() =
        let schema = JsonSchema.parseSchemaFromString """{ "type": "string" }"""
        let value = JsonValue.String "test"
        let result = JsonSchema.validate schema value
        result |> should equal JsonSchema.ValidationResult.Valid

    [<Test>]
    member _.``validate rejects non-string value for string schema``() =
        let schema = JsonSchema.parseSchemaFromString """{ "type": "string" }"""
        let value = JsonValue.Number 123m
        let result = JsonSchema.validate schema value
        match result with
        | JsonSchema.ValidationResult.Invalid msg -> msg |> should equal "Expected a string value"
        | _ -> failwith "Expected validation failure"

    [<Test>]
    member _.``validate accepts valid number value``() =
        let schema = JsonSchema.parseSchemaFromString """{ "type": "number" }"""
        let value = JsonValue.Number 42.5m
        let result = JsonSchema.validate schema value
        result |> should equal JsonSchema.ValidationResult.Valid

    [<Test>]
    member _.``validate accepts valid integer value``() =
        let schema = JsonSchema.parseSchemaFromString """{ "type": "integer" }"""
        let value = JsonValue.Number 42m
        let result = JsonSchema.validate schema value
        result |> should equal JsonSchema.ValidationResult.Valid

    [<Test>]
    member _.``validate rejects non-integer value for integer schema``() =
        let schema = JsonSchema.parseSchemaFromString """{ "type": "integer" }"""
        let value = JsonValue.Number 42.5m
        let result = JsonSchema.validate schema value
        match result with
        | JsonSchema.ValidationResult.Invalid msg -> msg |> should equal "Expected an integer value"
        | _ -> failwith "Expected validation failure"

    [<Test>]
    member _.``validate accepts valid boolean value``() =
        let schema = JsonSchema.parseSchemaFromString """{ "type": "boolean" }"""
        let value = JsonValue.Boolean true
        let result = JsonSchema.validate schema value
        result |> should equal JsonSchema.ValidationResult.Valid

    [<Test>]
    member _.``validate validates string length constraints``() =
        let schema = JsonSchema.parseSchemaFromString """{ "type": "string", "minLength": 3, "maxLength": 10 }"""
        
        // Valid length
        let validValue = JsonValue.String "hello"
        JsonSchema.validate schema validValue |> should equal JsonSchema.ValidationResult.Valid
        
        // Too short
        let shortValue = JsonValue.String "hi"
        match JsonSchema.validate schema shortValue with
        | JsonSchema.ValidationResult.Invalid _ -> () // Expected
        | _ -> failwith "Expected validation failure for short string"
        
        // Too long
        let longValue = JsonValue.String "this is too long"
        match JsonSchema.validate schema longValue with
        | JsonSchema.ValidationResult.Invalid _ -> () // Expected
        | _ -> failwith "Expected validation failure for long string"

    [<Test>]
    member _.``validate validates number range constraints``() =
        let schema = JsonSchema.parseSchemaFromString """{ "type": "number", "minimum": 0, "maximum": 100 }"""
        
        // Valid range
        let validValue = JsonValue.Number 50m
        JsonSchema.validate schema validValue |> should equal JsonSchema.ValidationResult.Valid
        
        // Below minimum
        let belowMin = JsonValue.Number -10m
        match JsonSchema.validate schema belowMin with
        | JsonSchema.ValidationResult.Invalid _ -> () // Expected
        | _ -> failwith "Expected validation failure for value below minimum"
        
        // Above maximum
        let aboveMax = JsonValue.Number 150m
        match JsonSchema.validate schema aboveMax with
        | JsonSchema.ValidationResult.Invalid _ -> () // Expected
        | _ -> failwith "Expected validation failure for value above maximum"

    [<Test>]
    member _.``validate validates string pattern``() =
        let schema = JsonSchema.parseSchemaFromString """{ "type": "string", "pattern": "^[a-z]+$" }"""
        
        // Valid pattern
        let validValue = JsonValue.String "hello"
        JsonSchema.validate schema validValue |> should equal JsonSchema.ValidationResult.Valid
        
        // Invalid pattern
        let invalidValue = JsonValue.String "Hello123"
        match JsonSchema.validate schema invalidValue with
        | JsonSchema.ValidationResult.Invalid _ -> () // Expected
        | _ -> failwith "Expected validation failure for invalid pattern"

    [<Test>]
    member _.``validate validates object with required properties``() =
        let schema = JsonSchema.parseSchemaFromString """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer" }
            },
            "required": ["name"]
        }"""
        
        // Valid object with required property
        let validObj = JsonValue.Parse """{ "name": "John", "age": 30 }"""
        JsonSchema.validate schema validObj |> should equal JsonSchema.ValidationResult.Valid
        
        // Valid object without optional property
        let validObjMinimal = JsonValue.Parse """{ "name": "John" }"""
        JsonSchema.validate schema validObjMinimal |> should equal JsonSchema.ValidationResult.Valid
        
        // Invalid object missing required property
        let invalidObj = JsonValue.Parse """{ "age": 30 }"""
        match JsonSchema.validate schema invalidObj with
        | JsonSchema.ValidationResult.Invalid msg -> 
            msg |> should contain "Missing required properties: name"
        | _ -> failwith "Expected validation failure for missing required property"

    [<Test>]
    member _.``validate validates array with item schema``() =
        let schema = JsonSchema.parseSchemaFromString """
        {
            "type": "array",
            "items": { "type": "string" }
        }"""
        
        // Valid array
        let validArray = JsonValue.Parse """["hello", "world"]"""
        JsonSchema.validate schema validArray |> should equal JsonSchema.ValidationResult.Valid
        
        // Invalid array with wrong item type
        let invalidArray = JsonValue.Parse """["hello", 123]"""
        match JsonSchema.validate schema invalidArray with
        | JsonSchema.ValidationResult.Invalid _ -> () // Expected
        | _ -> failwith "Expected validation failure for invalid array item"

    [<Test>]
    member _.``validate handles null values correctly``() =
        let stringSchema = JsonSchema.parseSchemaFromString """{ "type": "string" }"""
        let nullSchema = JsonSchema.parseSchemaFromString """{ "type": "null" }"""
        
        let nullValue = JsonValue.Null
        
        // Null should fail for string schema
        match JsonSchema.validate stringSchema nullValue with
        | JsonSchema.ValidationResult.Invalid _ -> () // Expected
        | _ -> failwith "Expected validation failure for null in string schema"
        
        // Null should pass for null schema
        JsonSchema.validate nullSchema nullValue |> should equal JsonSchema.ValidationResult.Valid

    [<Test>]
    member _.``validate always passes for Any type except null``() =
        let anySchema = JsonSchema.parseSchemaFromString """{}""" // No type specified = Any
        
        let stringValue = JsonValue.String "test"
        let numberValue = JsonValue.Number 123m
        let boolValue = JsonValue.Boolean true
        
        JsonSchema.validate anySchema stringValue |> should equal JsonSchema.ValidationResult.Valid
        JsonSchema.validate anySchema numberValue |> should equal JsonSchema.ValidationResult.Valid
        JsonSchema.validate anySchema boolValue |> should equal JsonSchema.ValidationResult.Valid
        
        // Note: null values are handled specially by the validation logic - they only pass for explicit null schema

    [<Test>]
    member _.``createValidator creates working validator function``() =
        let schema = JsonSchema.parseSchemaFromString """{ "type": "string", "minLength": 3 }"""
        let validator = JsonSchema.createValidator schema
        
        let validValue = JsonValue.String "hello"
        let invalidValue = JsonValue.String "hi"
        
        validator validValue |> should equal JsonSchema.ValidationResult.Valid
        match validator invalidValue with
        | JsonSchema.ValidationResult.Invalid _ -> () // Expected
        | _ -> failwith "Expected validation failure"

    [<Test>]
    member _.``resolveReferences handles simple local references``() =
        let rootSchema = JsonValue.Parse """
        {
            "definitions": {
                "Address": {
                    "type": "object",
                    "properties": {
                        "street": { "type": "string" },
                        "city": { "type": "string" }
                    }
                }
            },
            "type": "object",
            "properties": {
                "address": { "$ref": "#/definitions/Address" }
            }
        }"""
        
        let schemaWithRef = JsonSchema.parseSchemaFromString """{ "$ref": "#/definitions/Address" }"""
        let resolved = JsonSchema.resolveReferences schemaWithRef rootSchema
        
        resolved.Type |> should equal JsonSchema.JsonSchemaType.Object
        resolved.Reference |> should equal None

    [<Test>]
    member _.``parseSchemaFromTextReader works with TextReader input``() =
        let schemaString = """{ "type": "string", "format": "email" }"""
        use reader = new System.IO.StringReader(schemaString)
        let parsed = JsonSchema.parseSchemaFromTextReader "" reader
        
        parsed.Type |> should equal JsonSchema.JsonSchemaType.String
        parsed.Format |> should equal (Some "email")