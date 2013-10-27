// --------------------------------------------------------------------------------------
// Tests for Http Utilities (mainly HttpUtility.JavaScriptStringEncode now)
// --------------------------------------------------------------------------------------

module FSharp.Data.Tests.HttpUtility

#if INTERACTIVE
#r "../../bin/FSharp.Data.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#endif

open FsUnit
open NUnit.Framework
open System
open ProviderImplementation.HttpUtility

/// Standard System.Web implementation to be used as a baseline
let JavaScriptStringEncodeDotNet s =
  System.Web.HttpUtility.JavaScriptStringEncode(s)

[<Test>]
let ``Basic special characters encoded correctly`` () = 
  let input = " \"quoted\" and \'quoted\' and \r\n and \uABCD "
  let actual = JavaScriptStringEncode input
  let expected = " \\\"quoted\\\" and \\u0027quoted\\u0027 and \\r\\n and \uABCD "
  actual |> should equal expected

[<Test>]
let ``Encoding of simple string is the same as in System.Web`` () = 
  let input = "sample \"json\" with \t\r\n \' quotes etc."
  let actual = JavaScriptStringEncode input
  let expected = JavaScriptStringEncodeDotNet input    
  actual |> should equal expected

[<Test>]
let ``Encoding of characters 0 .. 65535 is the same as in System.Web`` () = 
  let input = new String([| for i in 0 .. 65535 -> char i |])
  let actual = JavaScriptStringEncode input
  let expected = JavaScriptStringEncodeDotNet input    
  actual |> should equal expected