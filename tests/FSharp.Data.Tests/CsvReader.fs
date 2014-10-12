// --------------------------------------------------------------------------------------
// Tests for the CSV parsing code
// --------------------------------------------------------------------------------------

module FSharp.Data.Tests.CsvReader

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

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

