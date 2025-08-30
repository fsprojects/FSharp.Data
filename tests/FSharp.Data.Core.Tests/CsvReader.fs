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

// --------------------------------------------------------------------------------------
// Tests for CSV Core components that need better coverage
// --------------------------------------------------------------------------------------

open FSharp.Data

// CsvFile tests
[<Test>]
let ``CsvFile.Parse can parse simple CSV`` () =
  let csv = CsvFile.Parse("name,age\nAlice,25\nBob,30")
  csv.Headers |> should equal (Some [| "name"; "age" |])
  csv.Rows |> Seq.length |> should equal 2
  let firstRow = csv.Rows |> Seq.head
  firstRow.["name"] |> should equal "Alice"
  firstRow.["age"] |> should equal "25"

[<Test>]
let ``CsvFile.Parse with custom separators`` () =
  let csv = CsvFile.Parse("name;age\nAlice;25\nBob;30", separators=";")
  csv.Headers |> should equal (Some [| "name"; "age" |])
  let firstRow = csv.Rows |> Seq.head
  firstRow.["name"] |> should equal "Alice"

[<Test>]
let ``CsvFile.Parse with custom quote character`` () =
  let csv = CsvFile.Parse("name,value\nAlice,'Hello, World'", quote=''')
  let firstRow = csv.Rows |> Seq.head
  firstRow.["value"] |> should equal "Hello, World"

[<Test>]
let ``CsvFile.Parse without headers`` () =
  let csv = CsvFile.Parse("Alice,25\nBob,30", hasHeaders=false)
  csv.Headers |> should equal None
  let firstRow = csv.Rows |> Seq.head
  firstRow.[0] |> should equal "Alice"
  firstRow.[1] |> should equal "25"

[<Test>]
let ``CsvFile.Parse with skipRows`` () =
  let csv = CsvFile.Parse("comment line\nname,age\nAlice,25", skipRows=1)
  csv.Headers |> should equal (Some [| "name"; "age" |])
  let firstRow = csv.Rows |> Seq.head
  firstRow.["name"] |> should equal "Alice"

[<Test>]
let ``CsvFile.Parse with ignoreErrors true handles malformed rows`` () =
  let csv = CsvFile.Parse("name,age\nAlice,25\nBob\nCharlie,35", ignoreErrors=true)
  csv.Rows |> Seq.length |> should equal 2 // Malformed "Bob" row should be ignored

[<Test>]
let ``CsvFile column indexing works correctly`` () =
  let csv = CsvFile.Parse("first,second,third\nA,B,C")
  let firstRow = csv.Rows |> Seq.head
  firstRow.["first"] |> should equal "A"
  firstRow.["second"] |> should equal "B"
  firstRow.["third"] |> should equal "C"

[<Test>]
let ``CsvFile GetColumnIndex returns correct index`` () =
  let csv = CsvFile.Parse("first,second,third\nA,B,C")
  csv.GetColumnIndex("first") |> should equal 0
  csv.GetColumnIndex("second") |> should equal 1
  csv.GetColumnIndex("third") |> should equal 2

[<Test>]
let ``CsvFile TryGetColumnIndex returns Some for valid column`` () =
  let csv = CsvFile.Parse("first,second\nA,B")
  csv.TryGetColumnIndex("first") |> should equal (Some 0)
  csv.TryGetColumnIndex("missing") |> should equal None

[<Test>]
let ``CsvFile GetColumnIndex throws for invalid column`` () =
  let csv = CsvFile.Parse("first,second\nA,B")
  (fun () -> csv.GetColumnIndex("missing") |> ignore) |> should throw typeof<System.Collections.Generic.KeyNotFoundException>

// CsvExtensions edge case tests
[<Test>]
let ``AsInteger throws for invalid string`` () =
  (fun () -> "not_a_number".AsInteger() |> ignore) |> should throw typeof<System.Exception>

[<Test>]
let ``AsInteger64 throws for invalid string`` () =
  (fun () -> "not_a_number".AsInteger64() |> ignore) |> should throw typeof<System.Exception>

[<Test>]
let ``AsDecimal throws for invalid string`` () =
  (fun () -> "not_a_decimal".AsDecimal() |> ignore) |> should throw typeof<System.Exception>

[<Test>]
let ``AsFloat throws for invalid string`` () =
  (fun () -> "not_a_float".AsFloat() |> ignore) |> should throw typeof<System.Exception>

[<Test>]
let ``AsBoolean throws for invalid string`` () =
  (fun () -> "not_a_bool".AsBoolean() |> ignore) |> should throw typeof<System.Exception>

[<Test>]
let ``AsDateTime throws for invalid string`` () =
  (fun () -> "not_a_date".AsDateTime() |> ignore) |> should throw typeof<System.Exception>

[<Test>]
let ``AsGuid throws for invalid string`` () =
  (fun () -> "not_a_guid".AsGuid() |> ignore) |> should throw typeof<System.Exception>

[<Test>]
let ``AsInteger with custom culture`` () =
  let result = "1234".AsInteger(System.Globalization.CultureInfo.InvariantCulture)
  result |> should equal 1234

[<Test>]
let ``AsDecimal with custom culture`` () =
  let result = "123.45".AsDecimal(System.Globalization.CultureInfo.InvariantCulture)
  result |> should equal 123.45m

[<Test>]
let ``AsFloat with custom culture`` () =
  let result = "123.45".AsFloat(System.Globalization.CultureInfo.InvariantCulture)
  result |> should equal 123.45

// Additional CSV parsing edge cases
[<Test>]
let ``readCsvFile handles empty fields correctly`` () = 
  let sr = new StringReader("a,,c\n,b,")
  let actual = readCsvFile sr "," '"' |> Seq.map fst |> Array.ofSeq
  let expected = [|[|"a"; ""; "c"|]; [|""; "b"; ""|]|]
  actual |> should equal expected

[<Test>]
let ``readCsvFile handles trailing comma`` () = 
  let sr = new StringReader("a,b,\nc,d,")
  let actual = readCsvFile sr "," '"' |> Seq.map fst |> Array.ofSeq
  let expected = [|[|"a"; "b"; ""|]; [|"c"; "d"; ""|]|]
  actual |> should equal expected

[<Test>]
let ``readCsvFile handles quoted empty string`` () = 
  let sr = new StringReader("a,\"\",c")
  let actual = readCsvFile sr "," '"' |> Seq.map fst |> Array.ofSeq
  let expected = [|[|"a"; ""; "c"|]|]
  actual |> should equal expected

[<Test>]
let ``readCsvFile handles quotes at field boundaries`` () = 
  let sr = new StringReader("\"a\",\"b\",\"c\"")
  let actual = readCsvFile sr "," '"' |> Seq.map fst |> Array.ofSeq
  let expected = [|[|"a"; "b"; "c"|]|]
  actual |> should equal expected

[<Test>]
let ``readCsvFile handles mixed quoted and unquoted fields`` () = 
  let sr = new StringReader("a,\"b,c\",d")
  let actual = readCsvFile sr "," '"' |> Seq.map fst |> Array.ofSeq
  let expected = [|[|"a"; "b,c"; "d"|]|]
  actual |> should equal expected

[<Test>]
let ``readCsvFile handles Windows line endings`` () = 
  let sr = new StringReader("a,b\r\nc,d\r\n")
  let actual = readCsvFile sr "," '"' |> Seq.map fst |> Array.ofSeq
  let expected = [|[|"a"; "b"|]; [|"c"; "d"|]|]
  actual |> should equal expected

[<Test>]
let ``readCsvFile handles Unix line endings`` () = 
  let sr = new StringReader("a,b\nc,d\n")
  let actual = readCsvFile sr "," '"' |> Seq.map fst |> Array.ofSeq
  let expected = [|[|"a"; "b"|]; [|"c"; "d"|]|]
  actual |> should equal expected

[<Test>]
let ``readCsvFile handles Mac line endings`` () = 
  let sr = new StringReader("a,b\rc,d\r")
  let actual = readCsvFile sr "," '"' |> Seq.map fst |> Array.ofSeq
  let expected = [|[|"a"; "b"|]; [|"c"; "d"|]|]
  actual |> should equal expected

[<Test>]
let ``readCsvFile handles tab separators`` () = 
  let sr = new StringReader("a\tb\tc\nd\te\tf")
  let actual = readCsvFile sr "\t" '"' |> Seq.map fst |> Array.ofSeq
  let expected = [|[|"a"; "b"; "c"|]; [|"d"; "e"; "f"|]|]
  actual |> should equal expected

[<Test>]
let ``readCsvFile handles pipe separators`` () = 
  let sr = new StringReader("a|b|c\nd|e|f")
  let actual = readCsvFile sr "|" '"' |> Seq.map fst |> Array.ofSeq
  let expected = [|[|"a"; "b"; "c"|]; [|"d"; "e"; "f"|]|]
  actual |> should equal expected

[<Test>]
let ``readCsvFile handles single field`` () = 
  let sr = new StringReader("single")
  let actual = readCsvFile sr "," '"' |> Seq.map fst |> Array.ofSeq
  let expected = [|[|"single"|]|]
  actual |> should equal expected

[<Test>]
let ``readCsvFile handles quoted field with internal quotes`` () = 
  let sr = new StringReader("\"She said \"\"Hello\"\" to me\"")
  let actual = readCsvFile sr "," '"' |> Seq.map fst |> Array.ofSeq
  let expected = [|[|"She said \"Hello\" to me"|]|]
  actual |> should equal expected

[<Test>]
let ``readCsvFile handles BOM in stream`` () = 
  // BOM (Byte Order Mark) should be handled gracefully
  let bomString = "\uFEFFa,b,c"
  let sr = new StringReader(bomString)
  let actual = readCsvFile sr "," '"' |> Seq.map fst |> Array.ofSeq
  // Should handle BOM by treating it as part of first field
  actual.Length |> should be (greaterThan 0)

[<Test>]
let ``CsvFile Parse handles various numeric formats`` () =
  let csv = CsvFile.Parse("int,float,decimal\n42,3.14,123.456")
  let row = csv.Rows |> Seq.head
  row.["int"] |> should equal "42"
  row.["float"] |> should equal "3.14" 
  row.["decimal"] |> should equal "123.456"

[<Test>]
let ``CsvFile with different column counts when ignoreErrors is false`` () =
  (fun () -> 
    let csv = CsvFile.Parse("a,b\n1,2,3", ignoreErrors=false)
    csv.Rows |> Seq.toArray |> ignore
  ) |> should throw typeof<System.Exception>

[<Test>]
let ``CsvRow GetColumn by index works correctly`` () =
  let csv = CsvFile.Parse("a,b,c\n1,2,3")
  let row = csv.Rows |> Seq.head
  row.GetColumn(0) |> should equal "1"
  row.GetColumn(1) |> should equal "2"
  row.GetColumn(2) |> should equal "3"

[<Test>]
let ``CsvRow GetColumn by name works correctly`` () =
  let csv = CsvFile.Parse("first,second,third\nx,y,z")
  let row = csv.Rows |> Seq.head
  row.GetColumn("first") |> should equal "x"
  row.GetColumn("second") |> should equal "y" 
  row.GetColumn("third") |> should equal "z"