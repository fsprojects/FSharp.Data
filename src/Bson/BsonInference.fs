// -----------------------------------------------------------------------------
// Implements type inference for BSON
// -----------------------------------------------------------------------------

module ProviderImplementation.BsonInference

open System
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes

/// Infer the type of a BSON value.
let rec inferType parentName bson =
    match bson with
    | BsonType.Null -> InferedType.Null
    | BsonType.Boolean -> InferedType.Primitive (typeof<bool>, None, false)
    | BsonType.Int32 -> InferedType.Primitive (typeof<int>, None, false)
    | BsonType.Int64 -> InferedType.Primitive (typeof<int64>, None, false)
    | BsonType.Double -> InferedType.Primitive (typeof<float>, None, false)
    | BsonType.String -> InferedType.Primitive (typeof<string>, None, false)
    | BsonType.DateTime -> InferedType.Primitive (typeof<DateTime>, None, false)

    | BsonType.ObjectId -> failwith "not implemented yet"

    | BsonType.Array elems ->
        let elemName = NameUtils.singularize parentName
        let allowEmptyValues = false

        elems
        |> Seq.map (inferType elemName)
        |> StructuralInference.inferCollectionType allowEmptyValues

    | BsonType.Document elems ->
        let recordName =
            if String.IsNullOrEmpty parentName
            then None
            else Some parentName
        let fields =
            [ for elemName, value in elems ->
                let typ = inferType elemName value
                { Name = elemName; Type = typ } ]

        InferedType.Record (recordName, fields, false)
