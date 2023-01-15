// --------------------------------------------------------------------------------------
// Tests for the CSV parsing code
// --------------------------------------------------------------------------------------

module FSharp.Data.Tests.CsvReader

open NUnit.Framework
open FsUnit
open System.IO
open FSharp.Data.Runtime.CsvReader

[<Test>]
let ``Line with quotes parsed`` () = 
  let sr = new StringReader("123,\"456\"")
  let actual = readCsvFile sr "," '"'|> Seq.map fst |> Array.ofSeq
  let expected = [|[|"123"; "456"|]|]
  actual |> should equal expected

[<Test>]
let ``Quotes can be escaped with double quote`` () = 
  let sr = new StringReader("123,\"45\"\"6\"")
  let actual = readCsvFile sr "," '"'|> Seq.map fst |> Array.ofSeq
  let expected = [|[|"123"; "45\"6"|]|]
  actual |> should equal expected

[<Test>]
let ``Line with custom quotes parsed`` () = 
  let sr = new StringReader("123,'45,6'")
  let actual = readCsvFile sr "," '''|> Seq.map fst |> Array.ofSeq
  let expected = [|[|"123"; "45,6"|]|]
  actual |> should equal expected

[<Test>]
let ``Custom quotes can be escaped with double custom quote`` () = 
  let sr = new StringReader("123,'45''6'")
  let actual = readCsvFile sr "," '''|> Seq.map fst |> Array.ofSeq
  let expected = [|[|"123"; "45'6"|]|]
  actual |> should equal expected

[<Test>]
let ``Quoted new line does not start new row`` () = 
  let sr = new StringReader("123,\"45\n6\"")
  let actual = readCsvFile sr "," '"'|> Seq.map fst |> Array.ofSeq
  let expected = [|[|"123"; "45\n6"|]|]
  actual |> should equal expected

[<Test>]
let ``Quoted separator does not start new column`` () = 
  let sr = new StringReader("123,\"45,6\"")
  let actual = readCsvFile sr "," '"'|> Seq.map fst |> Array.ofSeq
  let expected = [|[|"123"; "45,6"|]|]
  actual |> should equal expected

[<Test>]
let ``Multiple separators are supported`` () = 
  let sr = new StringReader("12,34;\"5;6,7\"")
  let actual = readCsvFile sr ",;" '"'|> Seq.map fst |> Array.ofSeq
  let expected = [|[|"12"; "34"; "5;6,7" |]|]
  actual |> should equal expected

[<Test>]
let ``Blank lines are ignored and parsed correctly`` () = 
  let sr = new StringReader("\n\r12,34\r\n\r\n56,78\n90,10\n")
  let actual = readCsvFile sr "," '"'|> Seq.map fst |> Array.ofSeq
  let expected = [| [|"12"; "34" |]; [|"56"; "78"|]; [|"90"; "10" |]|]
  actual |> should equal expected

[<Test>]
let ``Quoted strings parsed correctly`` () = 
  let sr = new StringReader("\n\r12,\"a\n\rb\"\r\n\"123\",\"\"\"hello\"\" world\"\n\r")
  let actual = readCsvFile sr "," '"'|> Seq.map fst |> Array.ofSeq
  let expected = [|[|"12"; "a\n\rb"|]; [|"123"; "\"hello\" world"|]|]
  actual |> should equal expected


[<Test>]
let ``Read all rows from non seekable slow network stream`` () =

  let data = """ABC;1
DEF;2
GHI;3
"QUOTED";4
;5"""

  let encoding = System.Text.Encoding.UTF8
  let bytes = data |> encoding.GetBytes 
  use memoryStream = new MemoryStream(bytes)

  use fakeNetworkStream =
    { new System.IO.Stream () with
        override _.CanRead: bool = memoryStream.CanRead
        override _.CanSeek: bool = false
        override _.CanWrite: bool = false
        override _.Flush (): unit = memoryStream.Flush( )
        override _.Length: int64 = raise (System.NotSupportedException())
        override _.Position
            with get (): int64 = memoryStream.Position
            and set (v: int64): unit = raise (System.NotSupportedException ())
        override _.Read(buffer: byte[], offset: int, _: int): int = 
          memoryStream.Read (buffer, offset, 1 (* Ignores the count parameter and reads one byte only to simulate a slow network stream *))
        override _.ReadByte(): int = memoryStream.ReadByte ()
        override _.Seek(offset: int64, origin: SeekOrigin): int64 = raise (System.NotSupportedException ())
        override _.SetLength(value: int64): unit = raise (System.NotSupportedException ())
        override _.Write(buffer: byte[], offset: int, count: int): unit = raise (System.NotSupportedException ())
        override _.WriteByte(value: byte): unit = raise (System.NotSupportedException ()) }

  let reader = new StreamReader(fakeNetworkStream, encoding)

  let actual = readCsvFile reader ";" '"' |> Seq.map fst |> Array.ofSeq
  let expected = 
    [| [| "ABC"; "1" |]
       [| "DEF"; "2" |]
       [| "GHI"; "3" |]
       [| "QUOTED"; "4" |]
       [| ""; "5" |] |]

  actual |> should equal expected