// --------------------------------------------------------------------------------------
// CSV type provider (inference engine and code generator)
// --------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text.RegularExpressions
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes
open FSharp.Data
open FSharp.Data.Csv
open FSharp.Data.StructureInference

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
    Seq.reduce subtypeInfered
     (seq { for row in Seq.takeMax count csv.Data ->
              let fields = 
                [ for (unit, header), value in Seq.zip headers row.Columns ->
                    let typ = inferPrimitiveType value unit
                    { Name = header; Optional = false; Type = typ } ]
              Record(None, fields) })

// --------------------------------------------------------------------------------------
//
// --------------------------------------------------------------------------------------

[<TypeProvider>]
type public CsvProvider(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  // Generate namespace and type 'FSharp.Data.CsvProvider'
  let asm, isPortable, replacer = AssemblyResolver.init cfg
  let ns = "FSharp.Data"
  let csvProvTy = ProvidedTypeDefinition(asm, ns, "CsvProvider", Some(typeof<obj>))

  let buildTypes (typeName:string) (args:obj[]) =
    // Generate the required type with empty constructor
    let resTy = ProvidedTypeDefinition(asm, ns, typeName, Some(replacer.ToRuntime typeof<CsvFile>))

    // A type that is used to hide all generated domain types
    let domainTy = ProvidedTypeDefinition("DomainTypes", Some(typeof<obj>))
    resTy.AddMember(domainTy)

    let sample = args.[0] :?> string
    let separator = args.[1] :?> string
    let culture = args.[2] :?> string
    let inferRows = args.[3] :?> int
    let resolutionFolder = args.[4] :?> string

    // Infer the schema from a specified file or URI sample
    use sample = 
      try
        let reader = ProviderHelpers.readTextAtDesignTime cfg this.Invalidate resolutionFolder sample
        CsvFile.Parse(reader, separator)
      with _ ->
        CsvFile.Parse(new StringReader(sample), separator)
      
    let infered = CsvInference.inferType sample inferRows

    let (|Singleton|) = function Singleton s -> replacer.ToDesignTime s

    let generateCsvType culture (domainType:ProvidedTypeDefinition) = function
        | Record(_, fields) ->
            let objectTy = ProvidedTypeDefinition("Row", Some (replacer.ToRuntime typeof<CsvRow>))
            domainType.AddMember(objectTy)

            for index, field in fields |> Seq.mapi (fun i v -> i, v) do
              let baseTyp, propTyp =
                match field.Type with
                | Primitive(typ, Some unit) -> 
                    typ, ProvidedMeasureBuilder.Default.AnnotateType(typ, [unit])
                | Primitive(typ, None) -> typ, typ
                | _ -> typeof<string>, typeof<string>

              let p = ProvidedProperty(NameUtils.nicePascalName field.Name, propTyp)
              let _, conv = Conversions.convertValue culture field.Name false baseTyp replacer
              p.GetterCode <- fun (Singleton row) -> conv <@@ Some((%%row:CsvRow).Columns.[index]) @@>
              objectTy.AddMember(p)

            objectTy
        | _ -> failwith "generateCsvType: Type inference returned wrong type"

    let ctx = domainTy
    let methResTy = generateCsvType culture ctx infered
    let seqType ty = typedefof<seq<_>>.MakeGenericType[| ty |]

    // 'Data' property has the generated type
    let p = ProvidedProperty("Data", seqType methResTy)
    p.GetterCode <- fun (Singleton self) -> replacer.ToRuntime <@@ (%%self : CsvFile).Data @@>
    resTy.AddMember(p)
    
    // Generate static Parse method
    let args = [ ProvidedParameter("source", typeof<string>) ]
    let m = ProvidedMethod("Parse", args, resTy)
    m.IsStaticMethod <- true
    m.InvokeCode <- fun (Singleton source) -> replacer.ToRuntime <@@ CsvFile.Parse(new StringReader(%%source:string), separator) @@>
    resTy.AddMember(m)

    // Generate static Load stream method
    let args = [ ProvidedParameter("stream", typeof<Stream>) ]
    let m = ProvidedMethod("Load", args, resTy)
    m.IsStaticMethod <- true
    m.InvokeCode <- fun (Singleton stream) -> replacer.ToRuntime <@@ CsvFile.Parse(new StreamReader(%%stream:Stream), separator) @@>
    resTy.AddMember(m)

    // Generate static Load location method
    let args = [ ProvidedParameter("location", typeof<string>) ]
    let m = ProvidedMethod("Load", args, resTy)
    m.IsStaticMethod <- true
    let isHostedExecution = cfg.IsHostedExecution
    let defaultResolutionFolder = cfg.ResolutionFolder
    m.InvokeCode <- fun (Singleton location) -> replacer.ToRuntime <@@ let reader = Importing.readTextAtRunTime isHostedExecution defaultResolutionFolder resolutionFolder %%location
                                                                       CsvFile.Parse(reader, separator) @@>
    resTy.AddMember(m)

    // Return the generated type
    resTy

  // Add static parameter that specifies the API we want to get (compile-time) 
  let parameters = 
    [ ProvidedStaticParameter("Sample", typeof<string>) 
      ProvidedStaticParameter("Separator", typeof<string>, parameterDefaultValue = ",") 
      ProvidedStaticParameter("Culture", typeof<string>, parameterDefaultValue = "")
      ProvidedStaticParameter("InferRows", typeof<int>, parameterDefaultValue = Int32.MaxValue)
      ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "") ]

  let helpText = 
    """<summary>Typed representation of a CSV file</summary>
       <param name='Sample'>CSV sample file location</param>
       <param name='Separator'>Column delimiter</param>                     
       <param name='Culture'>The culture used for parsing numbers and dates.</param>                     
       <param name='InferRows'>Number of rows to use for inference. If this is zero (the default), all rows are used.</param>
       <param name='ResolutionFolder'>A directory that is used when resolving relative file references (at design time and in hosted execution)</param>"""

  do csvProvTy.AddXmlDoc helpText
  do csvProvTy.DefineStaticParameters(parameters, buildTypes)

  // Register the main type with F# compiler
  do this.AddNamespace(ns, [ csvProvTy ])