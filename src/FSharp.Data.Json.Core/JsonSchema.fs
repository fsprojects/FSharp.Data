namespace FSharp.Data.Runtime

open System
open System.Globalization
open System.Collections.Generic
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes
open FSharp.Data.Runtime.StructuralInference

/// Module that handles JSON Schema parsing and type inference
module JsonSchema =
    
    /// Represents the result of validating a JSON value against a schema
    type ValidationResult = 
        | Valid
        | Invalid of string

    /// Represents a JSON Schema validator function
    type JsonSchemaValidator = JsonValue -> ValidationResult
    
    /// Represents basic JSON Schema types
    type JsonSchemaType =
        | String
        | Number
        | Integer
        | Boolean
        | Object
        | Array
        | Null
        | Any
    
    /// Represents a parsed JSON Schema
    type JsonSchemaDefinition = {
        Type: JsonSchemaType
        Description: string option
        Properties: Map<string, JsonSchemaDefinition> option
        Required: string list option
        Items: JsonSchemaDefinition option
        Enum: JsonValue list option
        Minimum: decimal option
        Maximum: decimal option
        MinLength: int option
        MaxLength: int option
        Format: string option
        Pattern: string option
        OneOf: JsonSchemaDefinition list option
        AnyOf: JsonSchemaDefinition list option
        AllOf: JsonSchemaDefinition list option
        Reference: string option
    }
    
    /// Default empty schema definition
    let empty = {
        Type = Any
        Description = None
        Properties = None
        Required = None
        Items = None
        Enum = None
        Minimum = None
        Maximum = None
        MinLength = None
        MaxLength = None
        Format = None
        Pattern = None
        OneOf = None
        AnyOf = None
        AllOf = None
        Reference = None
    }
    
    /// Convert JSON Schema format to .NET type
    let formatToType (format: string) =
        match format.ToLowerInvariant() with
        | "date-time" | "date" | "time" -> typeof<DateTime>
        | "email" | "hostname" | "ipv4" | "ipv6" | "uri" -> typeof<string>
        | "uuid" | "guid" -> typeof<Guid>
        | "int32" | "int64" -> typeof<int>
        | "float" | "double" -> typeof<float>
        | _ -> typeof<string>
    
    /// Parse a JSON Schema from a JsonValue
    let rec parseSchema (schemaJson: JsonValue) =
        let getStringProp name =
            if schemaJson.TryGetProperty(name).IsSome then
                match schemaJson.[name] with
                | JsonValue.String s -> Some s
                | _ -> None
            else None
            
        let getNumberProp name =
            if schemaJson.TryGetProperty(name).IsSome then
                match schemaJson.[name] with
                | JsonValue.Number n -> Some n
                | _ -> None
            else None
            
        let getIntProp name =
            getNumberProp name |> Option.map int
            
        let getType () =
            if schemaJson.TryGetProperty("type").IsSome then
                match schemaJson.["type"] with
                | JsonValue.String "string" -> String
                | JsonValue.String "number" -> Number
                | JsonValue.String "integer" -> Integer
                | JsonValue.String "boolean" -> Boolean
                | JsonValue.String "object" -> Object
                | JsonValue.String "array" -> Array
                | JsonValue.String "null" -> Null
                | JsonValue.Array types -> 
                    // If a type is an array, take the first non-null type
                    types 
                    |> Array.tryPick (function 
                        | JsonValue.String "string" -> Some String
                        | JsonValue.String "number" -> Some Number
                        | JsonValue.String "integer" -> Some Integer
                        | JsonValue.String "boolean" -> Some Boolean
                        | JsonValue.String "object" -> Some Object
                        | JsonValue.String "array" -> Some Array
                        | _ -> None)
                    |> Option.defaultValue Any
                | _ -> Any
            else Any
            
        let getEnum () =
            if schemaJson.TryGetProperty("enum").IsSome then
                match schemaJson.["enum"] with
                | JsonValue.Array values -> Some (values |> Array.toList)
                | _ -> None
            else None
            
        let getRequired () =
            if schemaJson.TryGetProperty("required").IsSome then
                match schemaJson.["required"] with
                | JsonValue.Array values -> 
                    values 
                    |> Array.choose (function 
                        | JsonValue.String s -> Some s 
                        | _ -> None) 
                    |> Array.toList
                    |> Some
                | _ -> None
            else None
            
        let getProperties () =
            if schemaJson.TryGetProperty("properties").IsSome then
                match schemaJson.["properties"] with
                | JsonValue.Record properties -> 
                    properties 
                    |> Array.map (fun (name, schema) -> name, parseSchema schema)
                    |> Map.ofArray
                    |> Some
                | _ -> None
            else None
            
        let getItems () =
            if schemaJson.TryGetProperty("items").IsSome then
                match schemaJson.["items"] with
                | JsonValue.Record _ as itemSchema -> Some (parseSchema itemSchema)
                | JsonValue.Array schemas when schemas.Length > 0 ->
                    // For tuple schemas, just use the first schema
                    Some (parseSchema schemas.[0])
                | _ -> None
            else None
            
        let getOneOf () =
            if schemaJson.TryGetProperty("oneOf").IsSome then
                match schemaJson.["oneOf"] with
                | JsonValue.Array schemas -> 
                    schemas
                    |> Array.map parseSchema
                    |> Array.toList
                    |> Some
                | _ -> None
            else None
            
        let getAnyOf () =
            if schemaJson.TryGetProperty("anyOf").IsSome then
                match schemaJson.["anyOf"] with
                | JsonValue.Array schemas -> 
                    schemas
                    |> Array.map parseSchema
                    |> Array.toList
                    |> Some
                | _ -> None
            else None
            
        let getAllOf () =
            if schemaJson.TryGetProperty("allOf").IsSome then
                match schemaJson.["allOf"] with
                | JsonValue.Array schemas -> 
                    schemas
                    |> Array.map parseSchema
                    |> Array.toList
                    |> Some
                | _ -> None
            else None
            
        let getReference () =
            if schemaJson.TryGetProperty("$ref").IsSome then
                match schemaJson.["$ref"] with
                | JsonValue.String ref -> Some ref
                | _ -> None
            else None
            
        {
            Type = getType()
            Description = getStringProp "description"
            Properties = getProperties()
            Required = getRequired()
            Items = getItems()
            Enum = getEnum()
            Minimum = getNumberProp "minimum"
            Maximum = getNumberProp "maximum"
            MinLength = getIntProp "minLength"
            MaxLength = getIntProp "maxLength"
            Format = getStringProp "format"
            Pattern = getStringProp "pattern"
            OneOf = getOneOf()
            AnyOf = getAnyOf()
            AllOf = getAllOf()
            Reference = getReference()
        }
    
    /// Parse a JSON Schema from a string
    let parseSchemaFromString (schemaString: string) =
        JsonValue.Parse(schemaString) |> parseSchema
        
    /// Parse a JSON Schema from a TextReader
    let parseSchemaFromTextReader (resolutionFolder: string) (reader: System.IO.TextReader) =
        let schemaString = reader.ReadToEnd()
        parseSchemaFromString schemaString
        
    // Helper functions to create InferedType values
    let createStringType optional = 
        InferedType.Primitive(typeof<string>, None, optional, false)
        
    let createIntType optional = 
        InferedType.Primitive(typeof<int>, None, optional, false)
        
    let createDecimalType optional = 
        InferedType.Primitive(typeof<decimal>, None, optional, false)
        
    let createBooleanType optional = 
        InferedType.Primitive(typeof<bool>, None, optional, false)
        
    let createDateTimeType optional = 
        InferedType.Primitive(typeof<DateTime>, None, optional, false)
        
    let createGuidType optional = 
        InferedType.Primitive(typeof<Guid>, None, optional, false)
        
    /// Convert a JSON Schema type to an InferedType for the type provider
    let rec schemaToInferedType (umps: IUnitsOfMeasureProvider) (schema: JsonSchemaDefinition) =
        match schema.Type with
        | String ->
            match schema.Format with
            | Some format -> 
                match format.ToLowerInvariant() with
                | "date-time" | "date" -> createDateTimeType false
                | "uuid" | "guid" -> createGuidType false
                | _ -> createStringType false
            | None -> createStringType false
        | Number -> createDecimalType false
        | Integer -> createIntType false
        | Boolean -> createBooleanType false
        | Object ->
            match schema.Properties with
            | Some props ->
                let properties = 
                    props
                    |> Map.toArray
                    |> Array.map (fun (name, propSchema) -> 
                        let isOptional = 
                            match schema.Required with
                            | Some required -> not (List.contains name required)
                            | None -> true
                            
                        let propType = schemaToInferedType umps propSchema
                        
                        // Create property with the appropriate type and optionality
                        { Name = name; Type = if isOptional then propType.EnsuresHandlesMissingValues false else propType }
                    )
                    |> Array.toList
                
                InferedType.Record(None, properties, false)
            | None -> InferedType.Record(None, [], false)
        | Array ->
            match schema.Items with
            | Some itemSchema -> 
                let elementType = schemaToInferedType umps itemSchema
                let tag = typeTag elementType
                let order = [tag]
                let types = Map.ofList [(tag, (InferedMultiplicity.Multiple, elementType))]
                InferedType.Collection(order, types)
            | None -> 
                let order = [InferedTypeTag.Null]
                let types = Map.ofList [(InferedTypeTag.Null, (InferedMultiplicity.Multiple, InferedType.Top))]
                InferedType.Collection(order, types)
        | Null -> InferedType.Null
        | Any -> InferedType.Top
        
    /// Resolve references in a schema (simple implementation)
    let resolveReferences (schema: JsonSchemaDefinition) (rootSchema: JsonValue) =
        // This is a simplified implementation - a complete one would handle JSON pointers properly
        let rec resolveRef (refPath: string) =
            match refPath with
            | path when path.StartsWith("#/") ->
                // Handle local references like "#/definitions/Point"
                let parts = path.Substring(2).Split('/')
                let rec navigate current parts =
                    match parts with
                    | [||] -> current
                    | _ ->
                        match current with
                        | JsonValue.Record fields ->
                            match Array.tryFind (fun (name, _) -> name = parts.[0]) fields with
                            | Some (_, value) -> navigate value parts.[1..]
                            | None -> failwith $"Reference part '{parts.[0]}' not found"
                        | _ -> failwith "Invalid reference path"
                
                let referencedValue = navigate rootSchema parts
                parseSchema referencedValue
            | _ -> failwith $"Only local references are supported: {refPath}"
        
        let rec resolve (schema: JsonSchemaDefinition) =
            match schema.Reference with
            | Some refPath -> resolveRef refPath
            | None -> 
                // Also resolve references in nested schemas
                let resolvedProperties =
                    schema.Properties
                    |> Option.map (Map.map (fun _ v -> resolve v))
                
                let resolvedItems =
                    schema.Items |> Option.map resolve
                
                let resolvedOneOf =
                    schema.OneOf |> Option.map (List.map resolve)
                
                let resolvedAnyOf =
                    schema.AnyOf |> Option.map (List.map resolve)
                
                let resolvedAllOf =
                    schema.AllOf |> Option.map (List.map resolve)
                
                { schema with 
                    Properties = resolvedProperties
                    Items = resolvedItems
                    OneOf = resolvedOneOf
                    AnyOf = resolvedAnyOf
                    AllOf = resolvedAllOf
                    Reference = None }
        
        resolve schema
        
    /// Validate a JSON value against a schema
    let rec validate (schema: JsonSchemaDefinition) (value: JsonValue) : ValidationResult =
        // Check nulls first
        if value = JsonValue.Null then
            match schema.Type with
            | Null -> Valid
            | _ -> Invalid "Expected a non-null value"
        else
            match schema.Type with
            | String ->
                match value with
                | JsonValue.String str -> 
                    // Validate string constraints
                    match schema.MinLength, schema.MaxLength with
                    | Some minLen, Some maxLen when str.Length < minLen || str.Length > maxLen ->
                        Invalid $"String length must be between {minLen} and {maxLen}"
                    | Some minLen, None when str.Length < minLen ->
                        Invalid $"String length must be at least {minLen}"
                    | None, Some maxLen when str.Length > maxLen ->
                        Invalid $"String length must be at most {maxLen}"
                    | _ -> 
                        // Validate pattern
                        match schema.Pattern with
                        | Some pattern ->
                            let regex = System.Text.RegularExpressions.Regex(pattern)
                            if regex.IsMatch(str) then Valid else Invalid $"String does not match pattern: {pattern}"
                        | None -> Valid
                | _ -> Invalid "Expected a string value"
                
            | Number ->
                match value with
                | JsonValue.Number num ->
                    // Validate number constraints
                    match schema.Minimum, schema.Maximum with
                    | Some min, Some max when num < min || num > max ->
                        Invalid $"Number must be between {min} and {max}"
                    | Some min, None when num < min ->
                        Invalid $"Number must be at least {min}"
                    | None, Some max when num > max ->
                        Invalid $"Number must be at most {max}"
                    | _ -> Valid
                | _ -> Invalid "Expected a number value"
                
            | Integer ->
                match value with
                | JsonValue.Number num ->
                    // Check if it's an integer
                    if Math.Round(num) <> num then
                        Invalid "Expected an integer value"
                    else
                        // Validate integer constraints
                        match schema.Minimum, schema.Maximum with
                        | Some min, Some max when num < min || num > max ->
                            Invalid $"Integer must be between {min} and {max}"
                        | Some min, None when num < min ->
                            Invalid $"Integer must be at least {min}"
                        | None, Some max when num > max ->
                            Invalid $"Integer must be at most {max}"
                        | _ -> Valid
                | _ -> Invalid "Expected an integer value"
                
            | Boolean ->
                match value with
                | JsonValue.Boolean _ -> Valid
                | _ -> Invalid "Expected a boolean value"
                
            | Object ->
                match value with
                | JsonValue.Record properties ->
                    // Validate required properties
                    match schema.Required with
                    | Some requiredProps ->
                        let missingProps = 
                            requiredProps 
                            |> List.filter (fun prop -> properties |> Array.exists (fun (name, _) -> name = prop) |> not)
                        
                        if missingProps.Length > 0 then
                            let missingPropsStr = String.concat ", " missingProps
                            Invalid $"Missing required properties: {missingPropsStr}"
                        else
                            // Validate property values
                            match schema.Properties with
                            | Some propSchemas ->
                                let propResults =
                                    properties
                                    |> Array.choose (fun (name, propValue) ->
                                        match Map.tryFind name propSchemas with
                                        | Some propSchema -> 
                                            match validate propSchema propValue with
                                            | Valid -> None
                                            | Invalid msg -> Some ($"Property '{name}': {msg}")
                                        | None -> None // Allow additional properties
                                    )
                                
                                if propResults.Length > 0 then
                                    Invalid (String.concat ", " propResults)
                                else
                                    Valid
                            | None -> Valid
                    | None -> Valid
                | _ -> Invalid "Expected an object value"
                
            | Array ->
                match value with
                | JsonValue.Array items ->
                    // Validate array items
                    match schema.Items with
                    | Some itemSchema ->
                        let itemResults =
                            items
                            |> Array.mapi (fun idx item ->
                                match validate itemSchema item with
                                | Valid -> None
                                | Invalid msg -> Some ($"Item {idx}: {msg}")
                            )
                            |> Array.choose id
                        
                        if itemResults.Length > 0 then
                            Invalid (String.concat ", " itemResults)
                        else
                            Valid
                    | None -> Valid
                | _ -> Invalid "Expected an array value"
                
            | Null -> Invalid "Expected a null value"
            | Any -> Valid
    
    /// Create a validator function from a schema
    let createValidator (schema: JsonSchemaDefinition) : JsonSchemaValidator =
        fun jsonValue -> validate schema jsonValue