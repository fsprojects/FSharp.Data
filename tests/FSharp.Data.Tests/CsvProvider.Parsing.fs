// --------------------------------------------------------------------------------------
// Tests for the CSV parsing code
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Tests

open NUnit.Framework
open System.IO
open FSharp.Data.Csv.CsvReader

module CsvParser =

  [<Test>]
  let ``Line with quotes parsed`` () = 
    let sr = new StringReader("123,\"456\"")
    let actual = readCsvFile sr [| ',' |] |> Array.ofSeq
    let expected = [|[|"123"; "456"|]|]
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Quotes can be escaped with double quote`` () = 
    let sr = new StringReader("123,\"45\"\"6\"")
    let actual = readCsvFile sr [| ',' |] |> Array.ofSeq
    let expected = [|[|"123"; "45\"6"|]|]
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Quoted new line does not start new row`` () = 
    let sr = new StringReader("123,\"45\n6\"")
    let actual = readCsvFile sr [| ',' |] |> Array.ofSeq
    let expected = [|[|"123"; "45\n6"|]|]
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Quoted separator does not start new column`` () = 
    let sr = new StringReader("123,\"45,6\"")
    let actual = readCsvFile sr [| ',' |] |> Array.ofSeq
    let expected = [|[|"123"; "45,6"|]|]
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Multiple separators are supported`` () = 
    let sr = new StringReader("12,34;\"5;6,7\"")
    let actual = readCsvFile sr [| ','; ';' |] |> Array.ofSeq
    let expected = [|[|"12"; "34"; "5;6,7" |]|]
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Blank lines are ignored and parsed correctly`` () = 
    let sr = new StringReader("\n\r12,34\r\n\r\n56,78\n90,10\n")
    let actual = readCsvFile sr [| ',' |] |> Array.ofSeq
    let expected = [| [|"12"; "34" |]; [|"56"; "78"|]; [|"90"; "10" |]|]
    Assert.AreEqual(expected, actual)

  [<Test>]
  let ``Quoted strings parsed correctly`` () = 
    let sr = new StringReader("\n\r12,\"a\n\rb\"\r\n\"123\",\"\"\"hello\"\" world\"\n\r")
    let actual = readCsvFile sr [| ',' |] |> Array.ofSeq
    let expected = [|[|"12"; "a\n\rb"|]; [|"123"; "\"hello\" world"|]|]
    Assert.AreEqual(expected, actual)

