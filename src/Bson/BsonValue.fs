// -----------------------------------------------------------------------------
// A simple F# portable parser for BSON data
// -----------------------------------------------------------------------------

namespace FSharp.Data

open System
open System.Text

/// Represents the type of a BSON value.
[<RequireQualifiedAccess>]
type BsonValue =
   | Double of value : float
   | String of value : string
   | Document of elements : (string * BsonValue) list
   | Array of elements : BsonValue list
   | Binary of subtype : byte * value : byte[]
   | ObjectId of value : byte[]
   | Boolean of value : bool
   | DateTime of value : DateTime
   | Null
   // | Regex
   // | JavaScript
   // | JavaScriptWithScope
   | Int32 of value : int
   // | Timestamp
   | Int64 of value : int64
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

    static let UnixEpoch = DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)

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

    let readInt64() =
        let value = BitConverter.ToInt64(bson, i)
        i <- i + sizeof<int64>
        value

    let readDouble() =
        let value = BitConverter.ToDouble(bson, i)
        i <- i + sizeof<float>
        value

    let readString() =
        let length = readInt32()
        let value = Encoding.UTF8.GetString(bson, i, length - 1)
        i <- i + length
        value

    let rec parseDocument() =
        let elems = seq {
            let start = i
            let size = readInt32()

            while i < start + size - sizeof<byte> do // for the null-terminator
                let elemType = int bson.[i]
                i <- i + sizeof<byte>

                let key = readCString()

                let typ =
                    match elemType |> enum<BsonElementType> with
                    | BsonElementType.Double -> readDouble() |> BsonValue.Double
                    | BsonElementType.String -> readString() |> BsonValue.String

                    | BsonElementType.Document -> parseDocument()

                    | BsonElementType.Array ->
                        match parseDocument() with
                        | BsonValue.Document elems -> List.map snd elems |> BsonValue.Array
                        | _ -> failwith "expected parseDocument() to return a BsonValue.Document"

                    | BsonElementType.Binary ->
                        let length = readInt32()
                        let subtype = bson.[i]
                        let value = Array.sub bson (i + 1) length
                        i <- i + length + sizeof<byte>
                        BsonValue.Binary (subtype, value)

                    | BsonElementType.ObjectId ->
                        let length = 12
                        let value = Array.sub bson i length
                        i <- i + length * sizeof<byte>
                        BsonValue.ObjectId value

                    | BsonElementType.Boolean ->
                        let value =
                            if bson.[i] = 0uy then false
                            else if bson.[i] = 1uy then true
                            else failwith "expected either 0 or 1 for Boolean literal"
                        i <- i + sizeof<byte>
                        BsonValue.Boolean value

                    | BsonElementType.DateTime ->
                        readInt64() * 10000L
                        |> UnixEpoch.AddTicks
                        |> BsonValue.DateTime

                    | BsonElementType.Null -> BsonValue.Null

                    // ...

                    | BsonElementType.Int32 -> readInt32() |> BsonValue.Int32
                    | BsonElementType.Int64 -> readInt64() |> BsonValue.Int64

                    // ...

                    | _ -> failwithf "unsupported BSON element type %d" elemType

                yield (key, typ)
        }

        let value = BsonValue.Document <| Seq.toList elems
        i <- i + sizeof<byte>

        value

    member __.Parse() =
        parseDocument()

    member __.ParseMultiple() =
        seq {
            while i < bson.Length do
                yield parseDocument()
        }

type BsonValue with

    static member Parse (text : string) =
        let bytes = Encoding.UTF8.GetBytes( text)
        BsonParser(bytes).Parse()

    static member ParseMultiple (text : string) =
        let bytes = Encoding.UTF8.GetBytes(text)
        BsonParser(bytes).ParseMultiple()
