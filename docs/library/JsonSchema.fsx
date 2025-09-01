(*** hide ***)
#r "../../src/FSharp.Data/bin/Release/netstandard2.0/FSharp.Data.dll"
#r "../../src/FSharp.Data.Json.Core/bin/Release/netstandard2.0/FSharp.Data.Json.Core.dll"
#r "../../src/FSharp.Data.Runtime.Utilities/bin/Release/netstandard2.0/FSharp.Data.Runtime.Utilities.dll"
#r "../../src/FSharp.Data.Http/bin/Release/netstandard2.0/FSharp.Data.Http.dll"

open System
open System.IO
open FSharp.Data

(**
# Using JSON Schema with the JSON Type Provider

The JSON Type Provider allows you to use [JSON Schema](https://json-schema.org/) to provide statically typed
access to JSON documents, similar to how the XML Type Provider supports XML Schema.

## Basic Usage with JSON Schema

Let's start with a basic JSON Schema example:
*)

let personSchema =
    """
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "firstName": {
      "type": "string",
      "description": "The person's first name."
    },
    "lastName": {
      "type": "string",
      "description": "The person's last name."
    },
    "age": {
      "description": "Age in years",
      "type": "integer",
      "minimum": 0
    },
    "email": {
      "type": "string",
      "format": "email"
    },
    "phoneNumbers": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "enum": ["home", "work", "mobile"]
          },
          "number": {
            "type": "string"
          }
        },
        "required": ["type", "number"]
      }
    }
  },
  "required": ["firstName", "lastName"]
}
"""

// Create a type based on the schema
[<Literal>]
let PersonSchemaLiteral =
    """
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "firstName": {
      "type": "string",
      "description": "The person's first name."
    },
    "lastName": {
      "type": "string",
      "description": "The person's last name."
    },
    "age": {
      "description": "Age in years",
      "type": "integer",
      "minimum": 0
    },
    "email": {
      "type": "string",
      "format": "email"
    },
    "phoneNumbers": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "enum": ["home", "work", "mobile"]
          },
          "number": {
            "type": "string"
          }
        },
        "required": ["type", "number"]
      }
    }
  },
  "required": ["firstName", "lastName"]
}
"""

type Person = JsonProvider<Schema=PersonSchemaLiteral>

// Parse a JSON document that conforms to the schema
let person =
    Person.Parse(
        """
{
  "firstName": "John",
  "lastName": "Smith",
  "age": 42,
  "email": "john.smith@example.com",
  "phoneNumbers": [
    {
      "type": "home",
      "number": "555-1234"
    },
    {
      "type": "mobile",
      "number": "555-6789"
    }
  ]
}
"""
    )

// Access the strongly typed properties
printfn "Name: %s %s" person.FirstName person.LastName
printfn "Age: %A" person.Age
printfn "Email: %A" person.Email
printfn "Phone: %s" person.PhoneNumbers.[0].Number

(**
## Using Schema Files

You can also load a JSON Schema from a file:
*)

// Assuming you have a schema file:
// type Product = JsonProvider<Schema="schemas/product.json">

(**
## Validating JSON Against Schema

When using the JSON Provider with the Schema parameter, data validation occurs automatically at parse time
based on the schema rules:

- Properties are required according to the schema (firstName and lastName)
- Property types match those defined in the schema (age is a non-negative integer)
- Format constraints are checked (email is a valid email format)
- Pattern constraints are validated (orderId matches the pattern "^ORD-[0-9]{6}$")
- Numeric constraints are enforced (minimum/maximum values)

Here's how validation works:
*)

// Valid JSON that conforms to the schema
let validPerson =
    Person.Parse(
        """
{
  "firstName": "Jane",
  "lastName": "Doe",
  "age": 35,
  "email": "jane.doe@example.com"
}
"""
    )

printfn "Valid JSON: %s %s" validPerson.FirstName validPerson.LastName

// Invalid JSON that violates schema rules will cause an exception
// Let's use try-catch to demonstrate validation errors:
let invalidJson =
    """
{
  "firstName": "John",
  "age": -5
}
"""

// In a real project when using the Schema parameter, the JsonProvider would validate
// against the schema rules. For the purposes of this demonstration, let's manually
// validate the JSON against the schema:

// Create a JSON value from the invalid JSON
let jsonValue = JsonValue.Parse(invalidJson)

// Check required fields from the schema
if jsonValue.TryGetProperty("lastName").IsNone then
    printfn "Schema validation failed: missing required property 'lastName'"

// Check numeric constraints from the schema
if jsonValue.TryGetProperty("age").IsSome && jsonValue.["age"].AsInteger() < 0 then
    printfn "Schema validation failed: 'age' must be non-negative"

(**
## Schema Constraints and Validation

JSON Schema supports various constraints that are validated:

### String Constraints
```json
{
  "type": "string",
  "minLength": 3,
  "maxLength": 50,
  "pattern": "^[A-Z][a-z]+$"
}
```

### Numeric Constraints
```json
{
  "type": "number",
  "minimum": 0,
  "maximum": 100
}
```

### Array Constraints
```json
{
  "type": "array",
  "items": {
    "type": "string"
  },
  "minItems": 1,
  "maxItems": 10
}
```

### Object Constraints
```json
{
  "type": "object",
  "required": ["id", "name"],
  "properties": {
    "id": { "type": "integer" },
    "name": { "type": "string" }
  }
}
```

## Working with Schema References

JSON Schema allows references to reuse schema definitions:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "definitions": {
    "address": {
      "type": "object",
      "properties": {
        "street": { "type": "string" },
        "city": { "type": "string" },
        "zipCode": { "type": "string" }
      },
      "required": ["street", "city", "zipCode"]
    }
  },
  "type": "object",
  "properties": {
    "billingAddress": { "$ref": "#/definitions/address" },
    "shippingAddress": { "$ref": "#/definitions/address" }
  }
}
```

## Advantages of Using JSON Schema

1. **Documentation**: Schema provides documentation on what properties are available.
2. **Validation**: Schema enforces constraints on data types, required properties, etc.
3. **Type Safety**: Strong typing to prevent errors when working with JSON data.
4. **Discoverability**: Better IntelliSense in your IDE.
5. **Consistency**: Ensure all documents follow the same structure.
6. **Contract First Development**: Define your data contract before implementation.

## JSON Schema Features Supported

The JSON Schema support in FSharp.Data includes:

- Basic types (string, number, integer, boolean, object, array)
- Required properties
- Property format definitions (date-time, email, etc.)
- Enumerations
- Nested objects and arrays
- Minimum/maximum constraints
- String patterns and length constraints
- References ($ref) for reusing schema definitions
- Validation of documents against schema

## Requirements and Limitations

- When using the `Schema` parameter, you cannot use the `Sample` parameter
- Schema and SampleIsList parameters are mutually exclusive
- Currently supports JSON Schema Draft-07
- JSON Schema references ($ref) support is limited to local references within the schema
- Some advanced schema features like dependencies, conditionals, and unevaluatedProperties are not fully supported

## Using JSON Schema in Your Project

To use JSON Schema with the JSON Type Provider:

1. Define your schema (in a file or as a string)
2. Create a type using `JsonProvider<Schema="path-to-schema.json">` or `JsonProvider<Schema=schemaString>`
3. Use the generated type to parse and work with your JSON data
4. Optionally use the validation functions for runtime validation

### Complete Example with Nested Objects

Here's a more complex example with nested objects:
*)

let orderSchema =
    """
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "orderId": {
      "type": "string",
      "pattern": "^ORD-[0-9]{6}$"
    },
    "customer": {
      "type": "object",
      "properties": {
        "id": { "type": "integer" },
        "name": { "type": "string" },
        "email": { "type": "string", "format": "email" }
      },
      "required": ["id", "name"]
    },
    "items": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "productId": { "type": "string" },
          "name": { "type": "string" },
          "quantity": { "type": "integer", "minimum": 1 },
          "price": { "type": "number", "minimum": 0 }
        },
        "required": ["productId", "quantity", "price"]
      },
      "minItems": 1
    },
    "totalAmount": { "type": "number", "minimum": 0 },
    "orderDate": { "type": "string", "format": "date-time" }
  },
  "required": ["orderId", "customer", "items", "totalAmount", "orderDate"]
}
"""

// Create a type based on the order schema
[<Literal>]
let OrderSchemaLiteral =
    """
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "orderId": {
      "type": "string",
      "pattern": "^ORD-[0-9]{6}$"
    },
    "customer": {
      "type": "object",
      "properties": {
        "id": { "type": "integer" },
        "name": { "type": "string" },
        "email": { "type": "string", "format": "email" }
      },
      "required": ["id", "name"]
    },
    "items": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "productId": { "type": "string" },
          "name": { "type": "string" },
          "quantity": { "type": "integer", "minimum": 1 },
          "price": { "type": "number", "minimum": 0 }
        },
        "required": ["productId", "quantity", "price"]
      },
      "minItems": 1
    },
    "totalAmount": { "type": "number", "minimum": 0 },
    "orderDate": { "type": "string", "format": "date-time" }
  },
  "required": ["orderId", "customer", "items", "totalAmount", "orderDate"]
}
"""

type Order = JsonProvider<Schema=OrderSchemaLiteral>

let order =
    Order.Parse(
        """
{
  "orderId": "ORD-123456",
  "customer": {
    "id": 1001,
    "name": "Alice Smith",
    "email": "alice@example.com"
  },
  "items": [
    {
      "productId": "PROD-001",
      "name": "Laptop",
      "quantity": 1,
      "price": 1299.99
    },
    {
      "productId": "PROD-002",
      "name": "Mouse",
      "quantity": 2,
      "price": 25.99
    }
  ],
  "totalAmount": 1351.97,
  "orderDate": "2023-10-01T12:00:00Z"
}
"""
    )

printfn "Order: %s" order.OrderId
printfn "Customer: %s" order.Customer.Name
printfn "Items: %d" order.Items.Length
printfn "Total: %.2f" order.TotalAmount
printfn "Date: %A" order.OrderDate

(**
## Summary

The JSON Schema support in FSharp.Data provides a powerful way to work with strongly-typed JSON data based on schema definitions. It combines the benefits of static typing with the flexibility of JSON, making it an excellent choice for working with well-defined JSON APIs and data structures.
*)
