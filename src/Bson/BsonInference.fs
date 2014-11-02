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
    | BsonValue.Null -> InferedType.Null
    | BsonValue.Boolean _ -> InferedType.Primitive (typeof<bool>, None, false)
    | BsonValue.Int32 _ -> InferedType.Primitive (typeof<int>, None, false)
    | BsonValue.Int64 _ -> InferedType.Primitive (typeof<int64>, None, false)
    | BsonValue.Double _ -> InferedType.Primitive (typeof<float>, None, false)
    | BsonValue.String _ -> InferedType.Primitive (typeof<string>, None, false)
    | BsonValue.DateTime _ -> InferedType.Primitive (typeof<DateTime>, None, false)

    | BsonValue.Binary _ -> failwith "not implemented yet"
    | BsonValue.ObjectId _ -> failwith "not implemented yet"

    | BsonValue.Array elems ->
        let elemName = NameUtils.singularize parentName
        let allowEmptyValues = false

        elems
        |> Seq.map (inferType elemName)
        |> StructuralInference.inferCollectionType allowEmptyValues

    | BsonValue.Document elems ->
        let recordName =
            if String.IsNullOrEmpty parentName
            then None
            else Some parentName
        let fields =
            [ for elemName, value in elems ->
                let typ = inferType elemName value
                { Name = elemName; Type = typ } ]

        InferedType.Record (recordName, fields, false)
