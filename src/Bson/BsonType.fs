// -----------------------------------------------------------------------------
// A simple F# portable parser for BSON data
// -----------------------------------------------------------------------------

namespace FSharp.Data

open System
open System.Text

/// Represents the type of a BSON value.
[<RequireQualifiedAccess>]
type BsonType =
   | Double
   | String
   | Document of elements : (string * BsonType) list
   | Array of elements : BsonType list
   // | Binary of subtype : byte
   | ObjectId
   | Boolean
   | DateTime
   | Null
   // | Regex
   // | JavaScript
   // | JavaScriptWithScope
   | Int32
   // | Timestamp
   | Int64
   // | MinKey
   // | MaxKey

// -----------------------------------------------------------------------------
// BSON element types
// -----------------------------------------------------------------------------

type private BsonElementType =
   | Double              = 0x01
   | String              = 0x02
   | Document            = 0x03
   | Array               = 0x04
   | Binary              = 0x05
   | ObjectId            = 0x07
   | Boolean             = 0x08
   | DateTime            = 0x09
   | Null                = 0x0A
   | Regex               = 0x0B
   | JavaScript          = 0x0D
   | JavaScriptWithScope = 0x0F
   | Int32               = 0x10
   | Timestamp           = 0x11
   | Int64               = 0x12
   | MinKey              = 0xFF
   | MaxKey              = 0x7F

// -----------------------------------------------------------------------------
// BSON parser
// -----------------------------------------------------------------------------

type private BsonParser(bson : byte[]) =

    let mutable i = 0

    let readCString() =
        // Get the length by finding the first null-terminator
        let length = bson |> Seq.ofArray |> Seq.skip i |> Seq.findIndex ((=) 0uy)
        let value = Encoding.UTF8.GetString(bson, i, length)
        i <- i + length + sizeof<byte> // for the null-terminator

        value

    let readInt32() =
        let value = BitConverter.ToInt32(bson, i)
        i <- i + sizeof<int>

        value

    let rec parseDocument() =
        let elems = seq {
            let start = i
            let size = readInt32()

            while i < start + size - sizeof<byte> do // for the null-terminator
                let elemType = int bson.[i]
                i <- i + sizeof<byte>

                let key = readCString()

                let (typ, inc) =
                    match elemType |> enum<BsonElementType> with
                    | BsonElementType.Double -> (BsonType.Double, sizeof<float>)
                    | BsonElementType.String -> (BsonType.String, readInt32())
                    | BsonElementType.Document -> (parseDocument(), 0)

                    | BsonElementType.Array ->
                        match parseDocument() with
                        | BsonType.Document elems -> (List.map snd elems |> BsonType.Array, 0)
                        | _ -> failwith "expected parseDocument() to return a BsonType.Document"

                    // | BsonElementType.Binary ->
                    //     let length = readInt32()
                    //     (BsonType.Binary bson.[i], length + sizeof<byte>)

                    | BsonElementType.ObjectId -> (BsonType.ObjectId, 12 * sizeof<byte>)
                    | BsonElementType.Boolean -> (BsonType.Boolean, sizeof<byte>)
                    | BsonElementType.DateTime -> (BsonType.DateTime, sizeof<int64>)
                    | BsonElementType.Null -> (BsonType.Null, 0)

                    // ...

                    | BsonElementType.Int32 -> (BsonType.Int32, sizeof<int>)
                    | BsonElementType.Int64 -> (BsonType.Int64, sizeof<int64>)

                    // ...

                    | _ -> failwithf "unsupported BSON element type %d" elemType

                i <- i + inc
                yield (key, typ)
        }

        let value = BsonType.Document <| Seq.toList elems
        i <- i + sizeof<byte>

        value

    member __.Parse() =
        parseDocument()

    member __.ParseMultiple() =
        seq {
            while i < bson.Length do
                yield parseDocument()
        }

type BsonType with

    static member Parse (text : string) =
        let bytes = Encoding.UTF8.GetBytes( text)
        BsonParser(bytes).Parse()

    static member ParseMultiple (text : string) =
        let bytes = Encoding.UTF8.GetBytes(text)
        BsonParser(bytes).ParseMultiple()
