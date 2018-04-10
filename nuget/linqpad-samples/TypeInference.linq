<Query Kind="FSharpProgram">
  <GACReference>FSharp.Core, Version=4.3.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</GACReference>
  <Reference>&lt;ProgramFilesX86&gt;\Reference Assemblies\Microsoft\FSharp\.NETFramework\v4.0\4.3.0.0\FSharp.Core.dll</Reference>
  <NuGetReference>FSharp.Data</NuGetReference>
</Query>

open FSharp.Data

// The JsonProvider<...> takes one static parameter of type string. The parameter can be either a sample string or a sample file 
// (relative to the current folder or online accessible via http or https). It is not likely that this could lead to ambiguities.
type Simple = JsonProvider<""" { "name":"John", "age":94 } """>
let simple = Simple.Parse(""" { "name":"Tomas", "age":4 } """)
simple.Age |> Dump
simple.Name |> Dump

// A list may mix integers and floats. When the sample is a collection, the type provider generates a type 
// that can be used to store all values in the sample. In this case, the resulting type is decimal, because one of the values is not an integer
type Numbers = JsonProvider<""" [1, 2, 3, 3.14] """>
Numbers.Parse(""" [1.2, 45.1, 98.2, 5] """) |> Dump

// Other primitive types cannot be combined into a single type. For example, if the list contains numbers and strings. 
// In this case, the provider generates two methods that can be used to get values that match one of the types:
type Mixed = JsonProvider<""" [1, 2, "hello", "world"] """>
let mixed = Mixed.Parse(""" [4, 5, "hello", "world" ] """)

mixed.Numbers |> Seq.sum |> Dump
mixed.Strings |> String.concat ", " |> Dump