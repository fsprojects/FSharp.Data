namespace global

open System.Runtime.CompilerServices
open FSharp.Core.CompilerServices
open FSharp.Data
open FSharp.Data.Runtime

[<assembly: TypeProviderAssembly("FSharp.Data.DesignTime")>]
[<assembly: InternalsVisibleTo("FSharp.Data.Tests, PublicKey=00240000048000001401000006020000002400005253413100080000010001000de370e30996d51c2da4ba3878423843e8553ff8cf95bd0171fe6785d20e2f73c8a54feb5bf55888115de98bdf0f8c0e26ee79e4c0f535201582628313859078ab3be84442114655340980fa0232281badaa21c1c2849c1925d0cfbc3dfa8d22b00ba9800a3d9a6c00c5daf7344e3286c3ed6c3e62d7705db32e2a35ffef84963b8ae0a3fa8a365b4020007d22127bc24783a65602e858680d88f36d4d3ff7567fcbece85143ea5945330eb74e53596d0ead1209c56eaf2c5adbb80a05d70e59ba06b50af250a3b87239dd88b60ed57263ede090ea195f093aac2216897669634235b638fdd47b78fe55c9e34389c2a7cac21250b79c49e3a6e2f78dd3de9487")>]
do ()

// Expose JsonSchema functionality for users to be able to parse JSON Schema
// and validate JSON documents against schemas
namespace FSharp.Data

open System.IO
open FSharp.Data.Runtime

/// Represents the result of validating a JSON value against a schema
type JsonSchemaValidationResult =
    | Valid
    | Invalid of string

[<AutoOpen>]
module JsonSchemaExtensions =
    /// Parse a JSON Schema from a string
    let ParseJsonSchema (schemaString: string) =
        JsonSchema.parseSchemaFromString schemaString

    /// Parse a JSON Schema from a file
    let LoadJsonSchema (schemaPath: string) =
        use reader = new StreamReader(schemaPath)
        JsonSchema.parseSchemaFromTextReader "" reader

    /// Validate a JSON value against a schema
    let ValidateJsonAgainstSchema (schema: JsonSchema.JsonSchemaDefinition) (json: JsonValue) =
        match JsonSchema.validate schema json with
        | JsonSchema.Valid -> JsonSchemaValidationResult.Valid
        | JsonSchema.Invalid msg -> JsonSchemaValidationResult.Invalid msg
