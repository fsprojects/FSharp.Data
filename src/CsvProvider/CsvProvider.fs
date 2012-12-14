// --------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation 2005-2011.
// This sample code is provided "as is" without warranty of any kind. 
// We disclaim all warranties, either express or implied, including the 
// warranties of merchantability and fitness for a particular purpose. 
// --------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.StructureInference

// --------------------------------------------------------------------------------------
// Runtime representation of CSV file
// --------------------------------------------------------------------------------------

/// Simple type that represents a single CSV row
type CsvRow internal (data:string[]) =
  member x.Columns = data

// Simple type wrapping CSV data
type CsvFile private (text:string) =
  // Cache the sequence of all data lines (all lines but the first)
  let lines = text.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
  let lines =  [| for line in lines -> line.Split(',') |]
  let data = lines |> Seq.skip 1 |> Seq.map (fun d -> CsvRow(d)) |> Array.ofSeq
  member x.Data = data
  member x.Headers = lines |> Seq.head
  static member Parse(data) = new CsvFile(data)

// --------------------------------------------------------------------------------------
// Inference
// --------------------------------------------------------------------------------------

module CsvInference = 
  /// Infers the type of a CSV file using the specified number of rows
  /// (This handles units in the same way as the original MiniCSV provider)
  let inferType (csv:CsvFile) count =

    // Infer the units and names from the headers
    let headers = csv.Headers |> Seq.map (fun header ->
      let m = Regex.Match(header, @"(?<field>.+) \((?<unit>.+)\)")
      if m.Success then
        let headerName = m.Groups.["field"].Value
        let unitName = m.Groups.["unit"].Value
        Some(ProvidedMeasureBuilder.Default.SI unitName), headerName
      else None, header)

    // Infer the type of collection using structural inference
    inferCollectionType
      [ for row in Seq.takeMax count csv.Data ->
          let fields = 
            [ for (unit, header), value in Seq.zip headers row.Columns ->
                let typ = inferPrimitiveType value unit
                { Name = header; Optional = false; Type = typ } ]
          Record(None, fields) ]

// --------------------------------------------------------------------------------------
//
// --------------------------------------------------------------------------------------

module internal CsvTypeBuilder = 
  let generateCsvType (domainType:ProvidedTypeDefinition) = function
    | Collection(SingletonMap(_, (_, Record(_, fields)))) ->
        let objectTy = ProvidedTypeDefinition("Row", Some(typeof<CsvRow>))
        domainType.AddMember(objectTy)

        for index, field in fields |> Seq.mapi (fun i v -> i, v) do
          let baseTyp, propTyp =
            match field.Type with
            | Primitive(typ, Some unit) -> 
                typ, ProvidedMeasureBuilder.Default.AnnotateType(typ, [unit])
            | Primitive(typ, None) -> typ, typ
            | _ -> typeof<string>, typeof<string>

          let p = ProvidedProperty(NameUtils.nicePascalName field.Name, propTyp)
          let _, conv = Conversions.convertValue field.Name false baseTyp
          p.GetterCode <- fun (Singleton row) -> conv <@@ Some((%%row:CsvRow).Columns.[index]) @@> 
          objectTy.AddMember(p)

        objectTy
    | _ -> failwith "generateCsvType: Type inference returned wrong type"

// --------------------------------------------------------------------------------------

[<TypeProvider>]
type public CsvProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.JsonProvider'
  let asm = System.Reflection.Assembly.GetExecutingAssembly()
  let ns = "FSharp.Data"
  let xmlProvTy = ProvidedTypeDefinition(asm, ns, "CsvProvider", Some(typeof<obj>))

  let buildTypes (typeName:string) (args:obj[]) =

    // Generate the required type with empty constructor
    let resTy = ProvidedTypeDefinition(asm, ns, typeName, Some(typeof<CsvFile>))

    // A type that is used to hide all generated domain types
    let domainTy = ProvidedTypeDefinition("DomainTypes", Some(typeof<obj>))
    resTy.AddMember(domainTy)

    // Infer the schema from a specified file or URI sample
    let sample = 
      try CsvFile.Parse(ProviderHelpers.readFileInProvider cfg (args.[0] :?> string) )
      with _ -> failwith "Specified argument is not a well-formed CSV file."
    let infered = CsvInference.inferType sample Int32.MaxValue

    let ctx = domainTy
    let methResTy = CsvTypeBuilder.generateCsvType ctx infered

    // 'Data' proeprty has the generated type
    let p = ProvidedProperty("Data", methResTy.MakeArrayType())
    p.GetterCode <- fun (Singleton self) -> <@@ (%%self : CsvFile).Data @@>
    resTy.AddMember(p)
    
    // Generate static Parse method
    let args = [ ProvidedParameter("source", typeof<string>) ]
    let m = ProvidedMethod("Parse", args, resTy)
    m.IsStaticMethod <- true
    m.InvokeCode <- fun (Singleton source) -> <@@ CsvFile.Parse(%%source) @@>
    resTy.AddMember(m)

    // Generate static Load method
    let args =  [ ProvidedParameter("path", typeof<string>) ]
    let m = ProvidedMethod("Load", args, resTy)
    m.IsStaticMethod <- true
    m.InvokeCode <- fun (Singleton source) -> <@@ CsvFile.Parse(File.ReadAllText(%%source)) @@>
    resTy.AddMember(m)

    // Return the generated type
    resTy

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = [ ProvidedStaticParameter("Sample", typeof<string>) ]
  do xmlProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ xmlProvTy ])